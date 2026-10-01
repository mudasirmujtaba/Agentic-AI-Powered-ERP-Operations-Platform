"""Order Investigation agent (design doc §21).

Order → Order items → Inventory → Warehouse → Purchase orders → Supplier → Payment → Analysis.
Facts are gathered deterministically through the ERP API; the model only explains them, so it cannot invent a cause.
"""

from __future__ import annotations

import json
from datetime import datetime, timezone

from app import llm
from app.context import current
from app.erp import ErpError
from app.tracing import ToolTimer, traced


def _parse_date(value: str | None) -> datetime | None:
    if not value:
        return None
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def _sql_list(values: list[str]) -> str:
    return ", ".join("'" + v.replace("'", "''") + "'" for v in values)


def build_findings(order: dict, stock: dict[str, dict], incoming: list[dict], customer: dict | None, now: datetime) -> dict:
    """Pure function: turns gathered records into ranked, explainable findings."""
    status = order["status"]
    required = _parse_date(order.get("requiredDateUtc"))
    open_statuses = {"Draft", "Confirmed", "Processing"}
    is_late = status in open_statuses and required is not None and required.date() < now.date()
    days_late = (now.date() - required.date()).days if is_late and required else 0

    causes: list[dict] = []
    lines = []
    for line in order["lines"]:
        product_stock = stock.get(line["productId"], {})
        warehouse = next((w for w in product_stock.get("warehouses", []) if w["warehouseName"] == order["warehouseName"]), None)
        available_here = warehouse["quantityAvailable"] if warehouse else 0
        reserved_here = warehouse["quantityReserved"] if warehouse else 0
        product_incoming = [i for i in incoming if i["product_code"] == line["productCode"]]
        lines.append({
            "product": line["productCode"],
            "ordered": line["quantity"],
            "available_in_warehouse": available_here,
            "reserved_in_warehouse": reserved_here,
            "available_all_warehouses": product_stock.get("quantityAvailable"),
            "incoming": [
                {"po": i["po_number"], "status": i["status"], "outstanding": i["quantity_outstanding"],
                 "expected": i["expected_delivery_date"], "supplier": i["supplier_name"]}
                for i in product_incoming
            ],
        })
        # Unreserved stock only matters before confirmation; confirmed orders already hold their reservation.
        if status == "Draft" and line["quantity"] > available_here:
            causes.append({
                "type": "stock_shortage",
                "detail": f"{line['productCode']}: needs {line['quantity']}, only {available_here} available in {order['warehouseName']}",
                "incoming": [f"{i['po_number']} ({i['status']}, {i['quantity_outstanding']} due {str(i['expected_delivery_date'])[:10]})" for i in product_incoming],
            })

    if customer and customer.get("status") != "Active" and status in open_statuses:
        causes.append({"type": "customer_status", "detail": f"Customer {customer['name']} is {customer['status']}"})

    if status == "Draft":
        causes.append({"type": "not_confirmed", "detail": "The order is still a draft; it has not been confirmed, so no stock is reserved."})
    elif status == "Confirmed":
        causes.append({"type": "awaiting_fulfilment", "detail": "Confirmed with stock reserved, but the warehouse has not started processing it."})
    elif status == "Processing":
        causes.append({"type": "awaiting_shipment", "detail": "Picking/packing is under way but the order has not shipped."})
    elif status == "Cancelled":
        causes.append({"type": "cancelled", "detail": "The order was cancelled."})

    return {
        "order_number": order["orderNumber"],
        "customer": order["customerName"],
        "status": status,
        "warehouse": order["warehouseName"],
        "order_date": order["orderDateUtc"],
        "required_date": order.get("requiredDateUtc"),
        "is_late": is_late,
        "days_late": days_late,
        "shipped_at": order.get("shippedAtUtc"),
        "delivered_at": order.get("deliveredAtUtc"),
        "carrier": order.get("carrier"),
        "tracking": order.get("trackingNumber"),
        "invoice": order.get("invoiceNumber"),
        "total": order["totalAmount"],
        "lines": lines,
        "likely_causes": causes,
    }


@traced("Order investigation agent")
def investigate_order(state: dict) -> dict:
    erp = current().erp
    timer = ToolTimer()
    number = state.get("order_number")
    if not number:
        return {"answer": "Which order should I look into? Give me the order number, for example SO-10044.", "_detail": "no order number"}

    with timer.step("Sales order lookup"):
        matches = erp.get("sales-orders", search=number, pageSize=5)["items"]
    match = next((o for o in matches if o["orderNumber"].upper() == number.upper()), None)
    if match is None:
        return {"answer": f"I couldn't find order {number}. Check the number, or that you have access to sales orders.",
                "_tools": timer.steps, "_detail": "not found"}

    with timer.step("Order details"):
        order = erp.get(f"sales-orders/{match['id']}")

    stock: dict[str, dict] = {}
    with timer.step("Inventory by warehouse"):
        for line in order["lines"]:
            stock[line["productId"]] = erp.get(f"inventory/stock/{line['productId']}")

    incoming: list[dict] = []
    codes = [line["productCode"] for line in order["lines"]]
    with timer.step("Open purchase orders"):
        try:
            result = erp.sql(
                "SELECT l.po_number, p.status, p.expected_delivery_date, p.supplier_name, l.product_code, l.quantity_outstanding "
                "FROM ai.purchase_order_lines l JOIN ai.purchase_orders p ON p.po_number = l.po_number "
                f"WHERE l.product_code IN ({_sql_list(codes)}) AND l.quantity_outstanding > 0 "
                "AND p.status IN ('Draft', 'PendingApproval', 'Approved', 'Ordered', 'PartiallyReceived')"
            )
            incoming = [dict(zip(result["columns"], row)) for row in result["rows"]]
        except ErpError:
            incoming = []  # Purchasing data is supplementary; the investigation continues without it.

    customer = None
    with timer.step("Customer account"):
        try:
            customer = erp.get(f"customers/{order['customerId']}")
        except ErpError:
            pass

    findings = build_findings(order, stock, incoming, customer, datetime.now(timezone.utc))

    with timer.step("Analysis"):
        answer, usage = llm.complete(
            "You are OpsPilot's operations analyst. Explain the status of a sales order using ONLY the findings JSON. "
            "Start with a one-sentence verdict (on track / late by N days / delivered / cancelled) and the most likely cause. "
            "Then up to 4 bullets: evidence (stock, incoming purchase orders with dates, customer status, shipment). "
            "End with one concrete next step for the user. Use plain business language and never mention JSON field names "
            "(say \"not shipped yet\", not \"shipped_at is null\"). Never invent facts that are not in the findings.",
            f"Question: {state['message']}\nFindings: {json.dumps(findings, default=str)}",
        )

    return {
        "answer": answer,
        "findings": findings,
        "usage": usage,
        "_tools": timer.steps,
        "_detail": f"{findings['status']}{' · late ' + str(findings['days_late']) + 'd' if findings['is_late'] else ''}",
    }
