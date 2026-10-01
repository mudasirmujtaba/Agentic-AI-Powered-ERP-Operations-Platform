from datetime import datetime, timezone

from app.agents.order_investigation import build_findings

NOW = datetime(2026, 10, 1, 12, tzinfo=timezone.utc)


def order(status, required="2026-09-25T00:00:00Z", quantity=40):
    return {
        "orderNumber": "SO-10044", "customerId": "c1", "customerName": "Apex", "status": status,
        "warehouseName": "Main Distribution Center", "orderDateUtc": "2026-09-18T10:00:00Z", "requiredDateUtc": required,
        "totalAmount": 7560, "lines": [{"productId": "p1", "productCode": "X200", "quantity": quantity}],
    }


STOCK = {"p1": {"quantityAvailable": 37, "warehouses": [
    {"warehouseName": "Main Distribution Center", "quantityAvailable": 24, "quantityReserved": 6}]}}
INCOMING = [{"po_number": "PO-20008", "status": "Ordered", "quantity_outstanding": 100,
             "expected_delivery_date": "2026-10-13T00:00:00", "supplier_name": "ABC", "product_code": "X200"}]


def test_draft_order_short_on_stock_reports_shortage_and_incoming_po():
    findings = build_findings(order("Draft"), STOCK, INCOMING, {"name": "Apex", "status": "Active"}, NOW)

    assert findings["is_late"] is True
    assert findings["days_late"] == 6
    types = [c["type"] for c in findings["likely_causes"]]
    assert types[0] == "stock_shortage"
    assert "not_confirmed" in types
    assert "PO-20008" in findings["likely_causes"][0]["incoming"][0]


def test_confirmed_order_is_waiting_on_the_warehouse_not_on_stock():
    findings = build_findings(order("Confirmed"), STOCK, INCOMING, {"name": "Apex", "status": "Active"}, NOW)

    types = [c["type"] for c in findings["likely_causes"]]
    assert "stock_shortage" not in types  # confirmed orders already hold their reservation
    assert types == ["awaiting_fulfilment"]


def test_customer_on_hold_is_flagged():
    findings = build_findings(order("Draft", quantity=1), STOCK, [], {"name": "Lakeside", "status": "OnHold"}, NOW)
    assert any(c["type"] == "customer_status" for c in findings["likely_causes"])


def test_delivered_order_is_not_late():
    findings = build_findings(order("Delivered"), STOCK, [], None, NOW)
    assert findings["is_late"] is False
    assert findings["likely_causes"] == []
