"""The human-in-the-loop contract: a proposal pauses the graph; resuming with the ERP's outcome finishes it."""

from langgraph.checkpoint.memory import MemorySaver
from langgraph.types import Command

import app.graph as graph_module
from app.tracing import traced


@traced("Intent detection")
def fake_classify(state):
    return {"intent": "purchase_recommendation", "order_number": None, "product": None}


@traced("Procurement agent")
def fake_procurement(state):
    return {
        "answer": "Two products need reordering.",
        "proposal": {"actionType": "CreatePurchaseOrders", "agent": "Procurement agent", "summary": "Create 1 PO",
                     "payload": {"orders": []}},
    }


def build(monkeypatch):
    monkeypatch.setattr(graph_module, "classify", fake_classify)
    monkeypatch.setattr(graph_module, "purchase_recommendation", fake_procurement)
    return graph_module.build_graph(MemorySaver())


def test_proposal_interrupts_and_approval_resumes_with_the_outcome(monkeypatch):
    graph = build(monkeypatch)
    config = {"configurable": {"thread_id": "t1"}}

    graph.invoke({"message": "Prepare purchase orders", "history": [], "user": {}, "trace": [], "usage": {}}, config)
    snapshot = graph.get_state(config)
    assert snapshot.next == ("await_approval",)
    assert snapshot.values["proposal"]["actionType"] == "CreatePurchaseOrders"

    final = graph.invoke(Command(resume={
        "decision": "approved", "comments": "ok",
        "outcome": {"purchaseOrders": [{"poNumber": "PO-20013", "supplierName": "ABC", "totalAmount": 8750.0, "requiresApproval": False}]},
    }), config)

    assert graph.get_state(config).next == ()
    assert "PO-20013" in final["answer"]
    assert [step["node"] for step in final["trace"]][-1] == "Approval outcome"


def test_rejection_reports_nothing_was_created(monkeypatch):
    graph = build(monkeypatch)
    config = {"configurable": {"thread_id": "t2"}}
    graph.invoke({"message": "Prepare purchase orders", "history": [], "user": {}, "trace": [], "usage": {}}, config)

    final = graph.invoke(Command(resume={"decision": "rejected", "comments": "Budget frozen", "outcome": {}}), config)

    assert "rejected" in final["answer"]
    assert "Budget frozen" in final["answer"]


def test_business_rule_failure_is_reported_not_hidden(monkeypatch):
    graph = build(monkeypatch)
    config = {"configurable": {"thread_id": "t3"}}
    graph.invoke({"message": "Prepare purchase orders", "history": [], "user": {}, "trace": [], "usage": {}}, config)

    final = graph.invoke(Command(resume={"decision": "approved", "outcome": {"error": "Supplier ABC is inactive."}}), config)

    assert "Supplier ABC is inactive." in final["answer"]
