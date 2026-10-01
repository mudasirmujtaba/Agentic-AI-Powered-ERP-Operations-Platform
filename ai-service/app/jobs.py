"""Scheduled agent work (design doc §40). Called by the ERP's background jobs as its automation account.

The nightly inventory scan reuses the Inventory Intelligence agent's deterministic analysis, then asks the model for
a short briefing. If the model is unavailable the job still succeeds with a plain digest, so a scheduled run never
fails just because the LLM did.
"""

from __future__ import annotations

import logging
import time

from app import llm
from app.agents.inventory import RISK_ORDER, analyse, for_model
from app.config import get_settings
from app.tracing import ToolTimer

log = logging.getLogger("opspilot.jobs")

BRIEFING_PROMPT = """You write OpsPilot's nightly inventory briefing for the inventory manager.
Use ONLY the assessment JSON. Lead with one sentence: how many products are at risk and the most urgent one.
Then one bullet per at-risk product, most urgent first (at most 8): **code** — available vs safety stock, days of
cover and stockout date if given, incoming quantity with next_delivery (or "nothing on order"), lead time, and the
recommended order quantity if above 0. End with one line of recommended next steps. Never invent numbers or dates."""


def _risk_item(a: dict) -> dict:
    return {
        "product": a["product"], "name": a["name"], "risk": a["risk"], "available": a["available"],
        "safetyStock": a["safety_stock"], "reorderPoint": a["reorder_point"], "daysOfCover": a["days_of_cover"],
        "stockoutDate": a["stockout_date"], "incoming": a["incoming"], "nextDelivery": a["next_delivery"],
        "leadTimeDays": a["lead_time_days"], "recommendedQuantity": a["recommended_quantity"],
    }


def digest(at_risk: list[dict], total: int) -> str:
    """Deterministic fallback briefing."""
    if not at_risk:
        return f"All {total} products have enough stock and incoming supply to cover demand through their lead times."
    lines = [f"{len(at_risk)} of {total} products are at stock risk."]
    for a in at_risk[:8]:
        cover = f"{a['days_of_cover']} days of cover" if a["days_of_cover"] is not None else "no recent sales"
        incoming = f"{a['incoming']} incoming ({a['next_delivery']})" if a["incoming"] else "nothing on order"
        lines.append(f"- **{a['product']}** ({a['risk']}): {a['available']} available vs safety stock {a['safety_stock']}, "
                     f"{cover}, {incoming}, recommend ordering {a['recommended_quantity']}.")
    return "\n".join(lines)


def inventory_scan() -> dict:
    started = time.perf_counter()
    timer = ToolTimer()
    assessments = analyse(None, timer)
    at_risk = sorted((a for a in assessments if a["risk"] != "ok"),
                     key=lambda a: (RISK_ORDER[a["risk"]], a["days_of_cover"] if a["days_of_cover"] is not None else 9999))

    content, usage, ai_generated = digest(at_risk, len(assessments)), {}, False
    if at_risk:
        try:
            with timer.step("Briefing"):
                content, usage = llm.complete(BRIEFING_PROMPT, f"Assessment of {len(assessments)} products, at-risk only: {for_model(at_risk)}")
            ai_generated = True
        except Exception:  # noqa: BLE001 - any model failure falls back to the digest
            log.exception("Inventory briefing failed; using the deterministic digest")

    return {
        "content": content,
        "aiGenerated": ai_generated,
        "productsAnalysed": len(assessments),
        "risks": [_risk_item(a) for a in at_risk],
        "metadata": {
            "model": get_settings().groq_model if ai_generated else None,
            "usage": usage,
            "trace": timer.steps,
            "durationMs": round((time.perf_counter() - started) * 1000),
        },
    }
