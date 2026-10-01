"""Intent detection: decides which specialised workflow handles the request (design doc §19)."""

from __future__ import annotations

import re
from typing import Literal

from pydantic import BaseModel, Field

from app import llm
from app.tracing import traced

Intent = Literal["erp_query", "order_investigation", "inventory_risk", "purchase_recommendation", "policy", "general"]

ORDER_NUMBER = re.compile(r"\bSO-?\s?(\d{4,})\b", re.IGNORECASE)


class Route(BaseModel):
    intent: Intent = Field(description="Which workflow should handle the request.")
    order_number: str | None = Field(default=None, description="Sales order number like SO-10012 if one is mentioned.")
    product: str | None = Field(default=None, description="Product code or name if the request is about one product, e.g. X200.")


SYSTEM = """You route requests for OpsPilot, an ERP assistant. Pick exactly one intent:
- order_investigation: why a specific sales order is late, stuck, or not delivered (usually mentions an order number).
- inventory_risk: whether we will run out of stock, stock-out risk, low stock, days of cover — analysis only.
- purchase_recommendation: asks to prepare/recommend/draft purchase orders or what to reorder.
- policy: company policies and procedures (approval limits, payment terms, returns, reorder rules) — answered from documents.
- erp_query: any other question answerable from ERP data (customers, orders, revenue, suppliers, invoices, counts, lists, totals).
- general: greetings, help, or anything unrelated to the ERP.
Extract an order number or a single product only if explicitly mentioned."""


def heuristic_route(message: str) -> Route:
    """Deterministic fallback when the model is unavailable or returns something unusable."""
    text = message.lower()
    order = ORDER_NUMBER.search(message)
    order_number = f"SO-{order.group(1)}" if order else None

    if order_number and any(w in text for w in ("why", "late", "delay", "deliver", "stuck", "status", "where")):
        intent: Intent = "order_investigation"
    elif any(w in text for w in ("prepare", "recommend", "draft", "reorder", "purchase order", "what should we order", "buy")):
        intent = "purchase_recommendation"
    elif any(w in text for w in ("run out", "stock-out", "stockout", "low stock", "running low", "days of cover", "risk")):
        intent = "inventory_risk"
    elif any(w in text for w in ("policy", "procedure", "allowed to", "approval limit", "approve purchases", "terms", "returns")):
        intent = "policy"
    elif any(w in text for w in ("hello", "hi ", "help", "what can you do", "thanks")) and len(text) < 60:
        intent = "general"
    else:
        intent = "erp_query"
    return Route(intent=intent, order_number=order_number)


@traced("Intent detection")
def classify(state: dict) -> dict:
    message = state["message"]
    usage: dict = {}
    try:
        route, usage = llm.structured(Route, SYSTEM, message, fast=True, history=state.get("history", [])[-4:])
    except llm.LlmUnavailable:
        raise
    except Exception:
        route = heuristic_route(message)

    # A literal order number in the message always wins over the model's extraction.
    if match := ORDER_NUMBER.search(message):
        route.order_number = f"SO-{match.group(1)}"
        if route.intent == "erp_query":
            route.intent = "order_investigation"

    return {
        "intent": route.intent,
        "order_number": route.order_number,
        "product": route.product,
        "usage": usage,
        "_detail": route.intent + (f" · {route.order_number}" if route.order_number else "") + (f" · {route.product}" if route.product else ""),
    }
