from app import jobs


def _assessment(code: str, risk: str, cover: float | None) -> dict:
    return {
        "product_id": "id-" + code, "product": code, "name": code + " name", "available": 10, "safety_stock": 35,
        "reorder_point": 100, "daily_demand": 2.5, "days_of_cover": cover, "stockout_date": None, "incoming": 0,
        "next_delivery": None, "lead_time_days": 14, "projected_at_lead_time": -25, "risk": risk, "recommended_quantity": 130,
    }


def test_scan_orders_risks_and_falls_back_to_a_digest_when_the_model_fails(monkeypatch):
    monkeypatch.setattr(jobs, "analyse", lambda product, timer: [
        _assessment("OK-1", "ok", 90), _assessment("MED", "medium", 30), _assessment("CRIT", "critical", 4)])

    def broken(*_args, **_kwargs):
        raise RuntimeError("model down")

    monkeypatch.setattr(jobs.llm, "complete", broken)
    result = jobs.inventory_scan()

    assert [r["product"] for r in result["risks"]] == ["CRIT", "MED"]
    assert result["productsAnalysed"] == 3
    assert result["aiGenerated"] is False
    assert result["content"].startswith("2 of 3 products are at stock risk.")
    assert "product_id" not in result["risks"][0] and "productId" not in result["risks"][0]


def test_scan_with_no_risk_skips_the_model(monkeypatch):
    monkeypatch.setattr(jobs, "analyse", lambda product, timer: [_assessment("OK-1", "ok", 90)])

    def fail(*_args, **_kwargs):
        raise AssertionError("model should not be called")

    monkeypatch.setattr(jobs.llm, "complete", fail)
    result = jobs.inventory_scan()
    assert result["risks"] == []
    assert "All 1 products" in result["content"]


def test_traceparent_is_validated_and_forwarded():
    from app.erp import _headers
    from app.observability import set_traceparent, trace_id

    set_traceparent("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01")
    assert trace_id() == "0af7651916cd43dd8448eb211c80319c"
    assert _headers("t")["traceparent"].startswith("00-0af7651916cd43dd")

    set_traceparent("garbage")
    assert trace_id() is None
    assert "traceparent" not in _headers("t")
