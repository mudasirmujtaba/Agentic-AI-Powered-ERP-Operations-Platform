from datetime import datetime, timezone

from app import tickets
from app.tickets import HistoryEntry, TicketBrief, TicketInsightsInput, TicketSummaryInput

AT = datetime(2026, 9, 20, 9, 30, tzinfo=timezone.utc)


def test_ticket_text_marks_internal_notes_and_keeps_order():
    ticket = TicketSummaryInput(
        ticketNumber="TCK-50001", subject="X200 leaking", description="Seal drips.", customer="Apex",
        category="ProductDefect", priority="High", status="InProgress",
        history=[
            HistoryEntry(author="Ivy Chen", body="Asked for photos", atUtc=AT),
            HistoryEntry(author="Ivy Chen", body="Batch B-0412", isInternal=True, atUtc=AT.replace(hour=11)),
        ],
    )
    text = tickets._ticket_text(ticket)
    assert "Ticket TCK-50001: X200 leaking" in text
    assert text.index("Asked for photos") < text.index("Batch B-0412")
    assert "Ivy Chen (internal): Batch B-0412" in text


def test_insights_need_at_least_two_tickets_without_calling_the_model(monkeypatch):
    def fail(*_args, **_kwargs):
        raise AssertionError("model should not be called")

    monkeypatch.setattr(tickets.llm, "complete", fail)
    one = TicketBrief(ticketNumber="TCK-1", subject="s", description="d", customer="c", category="General",
                      priority="Low", status="Open", createdAtUtc=AT)
    reply = tickets.insights(TicketInsightsInput(days=90, tickets=[one]))
    assert "too few tickets" in reply["content"]


def test_insights_send_compact_ticket_rows(monkeypatch):
    seen = {}

    def fake(system, user, **_kwargs):
        seen["user"] = user
        return "pattern", {"input_tokens": 1, "output_tokens": 1}

    monkeypatch.setattr(tickets.llm, "complete", fake)
    briefs = [TicketBrief(ticketNumber=f"TCK-{i}", subject="X200 seal leak", description="leak", customer="Apex",
                          category="ProductDefect", priority="High", status="Open", product="Pump X200",
                          createdAtUtc=AT) for i in range(3)]
    reply = tickets.insights(TicketInsightsInput(days=60, tickets=briefs))
    assert reply["content"] == "pattern"
    assert "last 60 days (3 tickets)" in seen["user"]
    assert '"product": "Pump X200"' in seen["user"]
