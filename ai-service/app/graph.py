"""The OpsPilot agent graph (design doc §47).

START → Intent detection → one specialised workflow → (Procurement only) Human approval ⏸ → Outcome → END

The approval step uses LangGraph's `interrupt`: the run is checkpointed to SQLite and stops. When an approver decides
in OpsPilot, the ERP executes (or discards) the action through its own business rules and then resumes this thread
with the outcome, so the agent can report what actually happened.
"""

from __future__ import annotations

import sqlite3

from langgraph.checkpoint.sqlite import SqliteSaver
from langgraph.graph import END, START, StateGraph
from langgraph.types import interrupt

from app.agents.erp_query import erp_query
from app.agents.general import general
from app.agents.inventory import inventory_risk, purchase_recommendation
from app.agents.order_investigation import investigate_order
from app.agents.policy import answer_policy
from app.agents.router import classify
from app.config import get_settings
from app.state import AgentState
from app.tracing import traced

WORKFLOWS = {
    "erp_query": "erp_query",
    "order_investigation": "investigate_order",
    "inventory_risk": "inventory_risk",
    "purchase_recommendation": "purchase_recommendation",
    "policy": "answer_policy",
    "general": "general",
}


def await_approval(state: AgentState) -> dict:
    """Pauses the run until a person approves or rejects the proposal in OpsPilot."""
    decision = interrupt({"proposal": state["proposal"]})
    return {"decision": decision}


@traced("Approval outcome")
def finalize_approval(state: AgentState) -> dict:
    decision = state.get("decision") or {}
    outcome = decision.get("outcome") or {}
    comments = decision.get("comments")
    note = f' Approver\'s note: "{comments}".' if comments else ""

    if decision.get("decision") != "approved":
        return {"answer": f"The recommendation was rejected, so nothing was created.{note}", "_detail": "rejected"}

    if error := outcome.get("error"):
        return {"answer": f"The recommendation was approved, but OpsPilot's business rules blocked it: {error}{note}",
                "_detail": "approved · failed"}

    orders = outcome.get("purchaseOrders") or []
    lines = []
    for po in orders:
        follow_up = " — above $10,000, so a manager must approve it after it's submitted" if po.get("requiresApproval") else ""
        lines.append(f"- **{po['poNumber']}** for {po['supplierName']}, ${po['totalAmount']:,.2f}{follow_up}")
    return {
        "answer": f"Approved.{note} I created {len(orders)} purchase order draft(s):\n" + "\n".join(lines)
                  + "\n\nNext step: open each draft in Purchasing, review it and **Submit** it.",
        "_detail": f"approved · {len(orders)} PO draft(s)",
    }


def _route(state: AgentState) -> str:
    return WORKFLOWS.get(state.get("intent", ""), "erp_query")


def _after_procurement(state: AgentState) -> str:
    return "await_approval" if state.get("proposal") else END


def build_graph(checkpointer=None):
    builder = StateGraph(AgentState)
    builder.add_node("classify", classify)
    builder.add_node("erp_query", erp_query)
    builder.add_node("investigate_order", investigate_order)
    builder.add_node("inventory_risk", inventory_risk)
    builder.add_node("purchase_recommendation", purchase_recommendation)
    builder.add_node("answer_policy", answer_policy)
    builder.add_node("general", general)
    builder.add_node("await_approval", await_approval)
    builder.add_node("finalize_approval", finalize_approval)

    builder.add_edge(START, "classify")
    builder.add_conditional_edges("classify", _route, list(WORKFLOWS.values()))
    builder.add_conditional_edges("purchase_recommendation", _after_procurement, ["await_approval", END])
    builder.add_edge("await_approval", "finalize_approval")
    builder.add_edge("finalize_approval", END)
    for node in ("erp_query", "investigate_order", "inventory_risk", "answer_policy", "general"):
        builder.add_edge(node, END)

    return builder.compile(checkpointer=checkpointer)


def create_persistent_graph():
    path = get_settings().checkpoint_db
    path.parent.mkdir(parents=True, exist_ok=True)
    connection = sqlite3.connect(path, check_same_thread=False)
    return build_graph(SqliteSaver(connection))
