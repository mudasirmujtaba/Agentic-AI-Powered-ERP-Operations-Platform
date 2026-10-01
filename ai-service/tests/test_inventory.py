from datetime import date

from app.agents.inventory import assess, build_proposal, round_up

TODAY = date(2026, 10, 1)


def item(available, safety=35, reorder=100, code="X200"):
    return {"productId": "p1", "productCode": code, "productName": "Pump", "quantityAvailable": available,
            "safetyStock": safety, "reorderPoint": reorder}


def test_round_up_to_order_multiples():
    assert round_up(1) == 10
    assert round_up(10) == 10
    assert round_up(10.1) == 20
    assert round_up(0) == 0
    assert round_up(-5) == 0


def test_healthy_product_needs_nothing():
    result = assess(item(400), sold_in_window=90, incoming=0, lead_time_days=7, today=TODAY)
    assert result["risk"] == "ok"
    assert result["recommended_quantity"] == 0


def test_stock_below_reorder_point_but_above_safety_is_medium_risk():
    # 1 unit/day, 80 available, 7-day lead time: projected 73 stays above safety 35; reorder to 100 + 7 - 80 = 27 -> 30.
    result = assess(item(80), sold_in_window=90, incoming=0, lead_time_days=7, today=TODAY)
    assert result["risk"] == "medium"
    assert result["recommended_quantity"] == 30


def test_dipping_below_safety_stock_during_lead_time_is_high_risk():
    # 37 available - 7 days x 1/day = 30 projected, under safety stock 35. Reorder to 100 + 7 - 37 = 70.
    result = assess(item(37), sold_in_window=90, incoming=0, lead_time_days=7, today=TODAY)
    assert result["risk"] == "high"
    assert result["days_of_cover"] == 37.0
    assert result["recommended_quantity"] == 70


def test_incoming_purchase_orders_reduce_the_recommendation():
    result = assess(item(37), sold_in_window=90, incoming=100, lead_time_days=7, today=TODAY)
    assert result["recommended_quantity"] == 0


def test_running_out_before_the_supplier_can_deliver_is_critical():
    # 2/day, 10 available -> 5 days of cover, 14-day lead time, nothing on order.
    result = assess(item(10, safety=5, reorder=40), sold_in_window=180, incoming=0, lead_time_days=14, today=TODAY)
    assert result["risk"] == "critical"
    assert result["stockout_date"] == "2026-10-06"
    assert result["recommended_quantity"] == 60  # 40 + 28 - 10 = 58 -> 60


def test_out_of_stock_without_demand_history_is_still_critical():
    result = assess(item(0, safety=10, reorder=30), sold_in_window=0, incoming=0, lead_time_days=8, today=TODAY)
    assert result["risk"] == "critical"
    assert result["days_of_cover"] is None
    assert result["recommended_quantity"] == 30


def test_proposal_groups_lines_by_primary_supplier_and_skips_inactive_suppliers():
    recs = [
        {"product": "X200", "recommended_quantity": 70, "available": 37, "daily_demand": 1, "incoming": 0, "lead_time_days": 7, "risk": "medium"},
        {"product": "X100", "recommended_quantity": 20, "available": 15, "daily_demand": 0.5, "incoming": 0, "lead_time_days": 7, "risk": "high"},
        {"product": "TW-100", "recommended_quantity": 30, "available": 0, "daily_demand": 0, "incoming": 0, "lead_time_days": 8, "risk": "critical"},
    ]
    products = {
        "X200": {"id": "a", "code": "X200", "name": "Pump X200", "cost": 125, "primarySupplierName": "ABC"},
        "X100": {"id": "b", "code": "X100", "name": "Pump X100", "cost": 95, "primarySupplierName": "ABC"},
        "TW-100": {"id": "c", "code": "TW-100", "name": "Torque wrench", "cost": 55, "primarySupplierName": "Old Co"},
    }
    suppliers = {"ABC": {"id": "s1", "name": "ABC", "isActive": True}, "Old Co": {"id": "s2", "name": "Old Co", "isActive": False}}

    proposal = build_proposal(recs, products, suppliers, {"id": "w1", "code": "WH-MAIN"})

    assert len(proposal["orders"]) == 1
    order = proposal["orders"][0]
    assert order["supplierId"] == "s1"
    assert [line["productCode"] for line in order["lines"]] == ["X200", "X100"]
    assert order["lines"][0]["unitCost"] == 125


def test_no_proposal_when_no_supplier_can_take_the_order():
    recs = [{"product": "X200", "recommended_quantity": 10, "available": 0, "daily_demand": 0, "incoming": 0, "lead_time_days": 7, "risk": "critical"}]
    assert build_proposal(recs, {}, {}, {"id": "w1", "code": "WH-MAIN"}) is None
