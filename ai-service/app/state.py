"""LangGraph state shared by every node (design doc §18)."""

from __future__ import annotations

import operator
from typing import Annotated, Any, TypedDict


def merge_usage(left: dict | None, right: dict | None) -> dict:
    left, right = left or {}, right or {}
    return {k: left.get(k, 0) + right.get(k, 0) for k in {"input_tokens", "output_tokens"}}


class AgentState(TypedDict, total=False):
    # Request
    message: str
    history: list[dict]
    user: dict

    # Routing
    intent: str
    order_number: str | None
    product: str | None

    # Results
    answer: str
    data: dict | None
    sql: str | None
    citations: list[dict]
    findings: dict | None
    proposal: dict | None
    decision: dict | None

    # Observability: one entry per node and tool call, and summed token usage.
    trace: Annotated[list[dict[str, Any]], operator.add]
    usage: Annotated[dict, merge_usage]
