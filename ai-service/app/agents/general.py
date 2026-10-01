from __future__ import annotations

from app import llm
from app.tracing import traced

CAPABILITIES = """I'm the OpsPilot Copilot. I can:
- Answer questions about your ERP data — customers, orders, revenue, suppliers, stock, invoices.
- Investigate why a sales order is late (for example: "Why is SO-10044 delayed?").
- Assess stock-out risk ("Are we going to run out of X200?").
- Prepare purchase recommendations for at-risk products, which an approver must sign off before any purchase order is created.
- Answer policy questions with citations ("What's the approval limit for purchases?")."""


@traced("General assistant")
def general(state: dict) -> dict:
    answer, usage = llm.complete(
        "You are the OpsPilot Copilot inside an ERP. Reply briefly and helpfully. Never state ERP facts or numbers; "
        "for data questions, suggest how the user could ask. Capabilities:\n" + CAPABILITIES,
        state["message"],
        fast=True,
        history=state.get("history", [])[-4:],
    )
    return {"answer": answer, "usage": usage}
