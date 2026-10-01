"""Per-request context.

The caller's JWT and identity live here rather than in graph state, so they are never written to the LangGraph
checkpoint store. Graph nodes run synchronously in the request's thread and read them through `current()`.
"""

from __future__ import annotations

from contextvars import ContextVar
from dataclasses import dataclass, field

from app.erp import ErpClient


@dataclass
class RequestContext:
    erp: ErpClient
    user: dict
    roles: list[str] = field(default_factory=list)


_current: ContextVar[RequestContext | None] = ContextVar("opspilot_request", default=None)


def set_current(context: RequestContext) -> None:
    _current.set(context)


def current() -> RequestContext:
    context = _current.get()
    if context is None:
        raise RuntimeError("No request context; graph nodes must run inside an API request.")
    return context
