"""Inventory Intelligence (design doc §20) and Procurement (§22) agents.

Both share one deterministic analysis: current stock, 90-day sales velocity, open purchase orders and supplier lead
times. Inventory Intelligence explains the risk; Procurement turns it into per-supplier purchase order proposals that
must be approved by a person before anything is created.
"""

from __future__ import annotations

import json
import math
from datetime import date, timedelta

from app import llm
from app.context import current
from app.erp import ErpError
from app.tracing import ToolTimer, traced

DEMAND_WINDOW_DAYS = 90
DEFAULT_LEAD_TIME_DAYS = 14
ORDER_MULTIPLE = 10
PIPELINE_STATUSES = ("Draft", "PendingApproval", "Approved", "Ordered", "PartiallyReceived")


def round_up(quantity: float, multiple: int = ORDER_MULTIPLE) -> int:
    return int(math.ceil(quantity / multiple) * multiple) if quantity > 0 else 0


def assess(item: dict, sold_in_window: int, incoming: int, lead_time_days: int, today: date, next_delivery: str | None = None) -> dict:
    """Pure risk assessment for one product.

    projected = available + incoming - demand during the supplier lead time. Reordering brings stock back to the
    reorder point plus lead-time demand, net of what is already on order; quantities round up to order multiples.
    """
    available = item["quantityAvailable"]
    safety = item["safetyStock"]
    reorder = item["reorderPoint"]
    daily = sold_in_window / DEMAND_WINDOW_DAYS
    cover_days = round(available / daily, 1) if daily > 0 else None
    projected = available + incoming - daily * lead_time_days

    if available <= 0 or (cover_days is not None and cover_days < lead_time_days and incoming == 0):
        risk = "critical"
    elif projected < safety:
        risk = "high"
    elif available <= reorder:
        risk = "medium"
    else:
        risk = "ok"

    needed = reorder + daily * lead_time_days - (available + incoming)
    recommended = round_up(needed) if risk != "ok" else 0

    return {
        "product_id": item["productId"],
        "product": item["productCode"],
        "name": item["productName"],
        "available": available,
        "safety_stock": safety,
        "reorder_point": reorder,
        "daily_demand": round(daily, 2),
        "days_of_cover": cover_days,
        "stockout_date": (today + timedelta(days=int(cover_days))).isoformat() if cover_days is not None else None,
        "incoming": incoming,
        "next_delivery": next_delivery,
        "lead_time_days": lead_time_days,
        "projected_at_lead_time": round(projected),
        "risk": risk,
        "recommended_quantity": recommended,
    }


def _rows(result: dict) -> list[dict]:
    return [dict(zip(result["columns"], row)) for row in result["rows"]]


def analyse(product: str | None, timer: ToolTimer) -> list[dict]:
    """Gathers ERP data (as the user) and assesses every active product, or the one asked about."""
    erp = current().erp

    with timer.step("Inventory levels"):
        stock = erp.get("inventory/stock", search=product, pageSize=100)["items"]
    if not stock:
        return []

    with timer.step("Sales velocity (90 days)"):
        demand = {r["product_code"]: -int(r["sold"] or 0) for r in _rows(erp.sql(
            "SELECT product_code, SUM(quantity) AS sold FROM ai.inventory_transactions "
            f"WHERE type = 'Sale' AND occurred_at >= DATEADD(day, -{DEMAND_WINDOW_DAYS}, GETUTCDATE()) GROUP BY product_code"))}

    with timer.step("Open purchase orders"):
        statuses = ", ".join(f"'{s}'" for s in PIPELINE_STATUSES)
        pipeline = {r["product_code"]: r for r in _rows(erp.sql(
            "SELECT l.product_code, SUM(l.quantity_outstanding) AS outstanding, MIN(p.expected_delivery_date) AS next_delivery "
            "FROM ai.purchase_order_lines l JOIN ai.purchase_orders p ON p.po_number = l.po_number "
            f"WHERE p.status IN ({statuses}) AND l.quantity_outstanding > 0 GROUP BY l.product_code"))}

    with timer.step("Supplier lead times"):
        lead_times = {r["product"]: int(r["lead_time"]) for r in _rows(erp.sql(
            "SELECT p.code AS product, s.average_lead_time_days AS lead_time FROM ai.products p "
            "JOIN ai.suppliers s ON s.name = p.primary_supplier"))}

    today = date.today()
    return [
        assess(item, demand.get(item["productCode"], 0),
               int((pipeline.get(item["productCode"]) or {}).get("outstanding") or 0),
               lead_times.get(item["productCode"], DEFAULT_LEAD_TIME_DAYS), today,
               str((pipeline.get(item["productCode"]) or {}).get("next_delivery") or "")[:10] or None)
        for item in stock
    ]


RISK_ORDER = {"critical": 0, "high": 1, "medium": 2, "ok": 3}


def for_model(assessments: list[dict]) -> str:
    """What the LLM sees: business fields only, no internal ids."""
    return json.dumps([{k: v for k, v in a.items() if k != "product_id"} for a in assessments], default=str)

TABLE_COLUMNS = ["product", "available", "safety_stock", "reorder_point", "daily_demand", "days_of_cover",
                 "incoming", "next_delivery", "lead_time_days", "risk", "recommended_quantity"]


def _table(assessments: list[dict]) -> dict:
    return {"columns": TABLE_COLUMNS, "rows": [[a[c] for c in TABLE_COLUMNS] for a in assessments], "truncated": False}


@traced("Inventory intelligence agent")
def inventory_risk(state: dict) -> dict:
    timer = ToolTimer()
    product = state.get("product")
    assessments = sorted(analyse(product, timer), key=lambda a: (RISK_ORDER[a["risk"]], a["days_of_cover"] or 9999))
    if not assessments:
        return {"answer": f"I couldn't find an active product matching '{product}'." if product else "No active products found.",
                "_tools": timer.steps, "_detail": "no products"}

    focus = assessments if product else [a for a in assessments if a["risk"] != "ok"] or assessments[:5]
    with timer.step("Risk analysis"):
        answer, usage = llm.complete(
            "You are OpsPilot's inventory analyst. Using ONLY the assessment JSON, answer the question. "
            "Lead with a direct verdict. For each at-risk product give: available vs safety stock, days of cover, "
            "incoming quantity with its next_delivery date (say none is on order if 0), supplier lead time, and the projected stock "
            "at lead time. Never state a date or number that is not in the JSON. Keep it to at most 6 bullets. "
            "If a recommended_quantity is above 0, mention it and say you can draft the purchase orders on request.",
            f"Question: {state['message']}\nAssessment: {for_model(focus)}",
        )

    return {
        "answer": answer,
        "data": _table(focus),
        "findings": {"assessments": focus},
        "usage": usage,
        "_tools": timer.steps,
        "_detail": f"{sum(a['risk'] != 'ok' for a in assessments)} at risk of {len(assessments)}",
    }


def build_proposal(recommendations: list[dict], products: dict[str, dict], suppliers: dict[str, dict], warehouse: dict) -> dict | None:
    """Groups recommended lines into one draft purchase order per primary supplier. Pure, for testing."""
    orders: dict[str, dict] = {}
    for rec in recommendations:
        product = products.get(rec["product"])
        supplier = suppliers.get((product or {}).get("primarySupplierName") or "")
        if not product or not supplier or not supplier["isActive"]:
            continue
        order = orders.setdefault(supplier["id"], {
            "supplierId": supplier["id"],
            "supplierName": supplier["name"],
            "warehouseId": warehouse["id"],
            "warehouseCode": warehouse["code"],
            "lines": [],
        })
        order["lines"].append({
            "productId": product["id"],
            "productCode": product["code"],
            "productName": product["name"],
            "quantity": rec["recommended_quantity"],
            "unitCost": product["cost"],
            "reason": f"{rec['available']} available, ~{rec['daily_demand']}/day, {rec['incoming']} on order, "
                      f"{rec['lead_time_days']}-day lead time ({rec['risk']} risk)",
        })
    if not orders:
        return None
    return {"orders": list(orders.values())}


@traced("Procurement agent")
def purchase_recommendation(state: dict) -> dict:
    erp = current().erp
    timer = ToolTimer()
    assessments = analyse(state.get("product"), timer)
    recommendations = sorted((a for a in assessments if a["recommended_quantity"] > 0), key=lambda a: RISK_ORDER[a["risk"]])

    if not recommendations:
        return {
            "answer": "No purchase orders are needed right now: every product's available stock plus what is already on order "
                      "covers demand through its supplier lead time and stays above safety stock.",
            "data": _table(sorted(assessments, key=lambda a: RISK_ORDER[a["risk"]])[:10]),
            "_tools": timer.steps,
            "_detail": "nothing to order",
        }

    with timer.step("Supplier & cost lookup"):
        products = {p["code"]: p for p in erp.get("products", pageSize=100)["items"]}
        suppliers = {s["name"]: s for s in erp.get("suppliers", pageSize=100)["items"]}
        warehouses = erp.get("warehouses", pageSize=100)["items"]
    warehouse = next((w for w in warehouses if w["code"] == "WH-MAIN" and w["isActive"]), None) or next(w for w in warehouses if w["isActive"])

    proposal = build_proposal(recommendations, products, suppliers, warehouse)
    if proposal is None:
        return {"answer": "Some products need reordering but have no active primary supplier, so I can't draft purchase orders for them.",
                "data": _table(recommendations), "_tools": timer.steps, "_detail": "no supplier"}

    total = sum(line["quantity"] * line["unitCost"] for order in proposal["orders"] for line in order["lines"])
    line_count = sum(len(o["lines"]) for o in proposal["orders"])
    summary = (f"Create {len(proposal['orders'])} purchase order draft(s) for {line_count} product(s), "
               f"estimated ${total:,.2f}, receiving at {warehouse['code']}")

    with timer.step("Recommendation rationale"):
        rationale, usage = llm.complete(
            "You are OpsPilot's procurement analyst. In 2-4 sentences, justify these purchase recommendations using ONLY the data: "
            "which products are at risk, why (days of cover vs lead time, safety stock), and the total estimated cost. "
            "Do not claim anything has been ordered; it still needs approval.",
            f"Recommendations: {for_model(recommendations)}\nProposal total: ${total:,.2f}",
        )
    proposal["rationale"] = rationale

    return {
        "answer": rationale + "\n\nI've prepared the purchase order drafts below. **Nothing is created until an approver reviews it.**",
        "data": _table(recommendations),
        "findings": {"assessments": recommendations, "estimated_total": round(total, 2)},
        "proposal": {
            "actionType": "CreatePurchaseOrders",
            "agent": "Procurement agent",
            "summary": summary,
            "payload": proposal,
        },
        "usage": usage,
        "_tools": timer.steps,
        "_detail": f"{line_count} lines · ${total:,.0f}",
    }


def safe_erp_error(error: ErpError) -> str:
    return error.detail or str(error)
