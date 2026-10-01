"""Service-ticket AI tasks (design doc §13): summarise a ticket's history, and find recurring problems across tickets.

These are one-shot completions over data the ERP has already authorised and sends in; they use no agent tools.
"""

from __future__ import annotations

import json
import time
from datetime import datetime

from pydantic import BaseModel, Field

from app import llm
from app.config import get_settings


class HistoryEntry(BaseModel):
    author: str
    body: str
    isInternal: bool = False
    atUtc: datetime


class TicketSummaryInput(BaseModel):
    ticketNumber: str
    subject: str
    description: str
    customer: str
    category: str
    priority: str
    status: str
    resolution: str | None = None
    history: list[HistoryEntry] = Field(default_factory=list)


class TicketBrief(BaseModel):
    ticketNumber: str
    subject: str
    description: str
    customer: str
    category: str
    priority: str
    status: str
    product: str | None = None
    resolution: str | None = None
    createdAtUtc: datetime


class TicketInsightsInput(BaseModel):
    days: int
    tickets: list[TicketBrief]


SUMMARY_PROMPT = """You summarise a customer service ticket for OpsPilot staff who are picking it up.
Use ONLY the ticket and history provided. Do not invent dates, causes, people or commitments.
Format (markdown, no headings):
- One sentence: what the customer's problem is and its current state.
- Then up to 4 short bullets: key facts found so far, what has been done, what is promised or pending.
- End with a line starting "**Next step:**" giving the single most useful next action, grounded in the history.
Items marked (internal) are staff notes; you may use them, the reader is staff."""

INSIGHTS_PROMPT = """You analyse recent OpsPilot customer service tickets to find RECURRING problems.
Use ONLY the tickets provided. A pattern needs at least 2 tickets; never present a single ticket as a trend.
Format (markdown, no headings):
- Lead with one sentence naming the single most significant recurring problem: the one with the highest
  priority/urgency and the most open tickets. Your first bullet must be that same problem.
- Then one bullet per pattern (most important first, at most 5): **bold short name** — how many tickets, which
  ticket numbers, the common factor (product, category, customer, root cause noted in the tickets; name a product only if
  the ticket rows give it), and impact
  (priority/urgency, open vs resolved).
- End with "**Recommended actions:**" and 1-3 concrete actions that follow from the patterns.
If there are no patterns, say so plainly."""


def _ticket_text(ticket: TicketSummaryInput) -> str:
    lines = [
        f"Ticket {ticket.ticketNumber}: {ticket.subject}",
        f"Customer: {ticket.customer} | Category: {ticket.category} | Priority: {ticket.priority} | Status: {ticket.status}",
        f"Description: {ticket.description}",
    ]
    if ticket.resolution:
        lines.append(f"Resolution: {ticket.resolution}")
    lines.append("History (oldest first):" if ticket.history else "History: no comments yet.")
    for entry in ticket.history:
        tag = " (internal)" if entry.isInternal else ""
        lines.append(f"- {entry.atUtc:%Y-%m-%d %H:%M} {entry.author}{tag}: {entry.body}")
    return "\n".join(lines)


def _briefs_json(tickets: list[TicketBrief]) -> str:
    rows = [
        {
            "ticket": t.ticketNumber, "subject": t.subject, "description": t.description, "customer": t.customer,
            "category": t.category, "priority": t.priority, "status": t.status, "product": t.product,
            "resolution": t.resolution, "opened": t.createdAtUtc.date().isoformat(),
        }
        for t in tickets
    ]
    return json.dumps(rows, ensure_ascii=False)


def _reply(content: str, usage: dict, started: float) -> dict:
    return {
        "content": content,
        "metadata": {
            "model": get_settings().groq_model,
            "usage": usage,
            "durationMs": round((time.perf_counter() - started) * 1000),
        },
    }


def summarize(ticket: TicketSummaryInput) -> dict:
    started = time.perf_counter()
    content, usage = llm.complete(SUMMARY_PROMPT, _ticket_text(ticket))
    return _reply(content, usage, started)


def insights(request: TicketInsightsInput) -> dict:
    started = time.perf_counter()
    if len(request.tickets) < 2:
        return _reply("There are too few tickets in this period to identify recurring problems.", {}, started)
    content, usage = llm.complete(
        INSIGHTS_PROMPT,
        f"Tickets opened in the last {request.days} days ({len(request.tickets)} tickets):\n{_briefs_json(request.tickets)}",
    )
    return _reply(content, usage, started)
