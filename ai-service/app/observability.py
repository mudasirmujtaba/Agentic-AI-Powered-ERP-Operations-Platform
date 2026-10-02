"""Trace correlation (design doc §42).

The ERP's HttpClient instrumentation sends a W3C `traceparent` header. We keep it for the request, so that:
- every log line carries the same trace id as the API's logs, and
- calls back into the ERP forward it, putting those requests in the same distributed trace.
"""

from __future__ import annotations

import logging
import re
from contextvars import ContextVar

_TRACEPARENT = re.compile(r"^[0-9a-f]{2}-([0-9a-f]{32})-([0-9a-f]{16})-[0-9a-f]{2}$")

traceparent: ContextVar[str | None] = ContextVar("traceparent", default=None)


def trace_id() -> str | None:
    value = traceparent.get()
    match = _TRACEPARENT.match(value) if value else None
    return match.group(1) if match else None


def set_traceparent(value: str | None) -> None:
    traceparent.set(value if value and _TRACEPARENT.match(value) else None)


class TraceIdFilter(logging.Filter):
    """Adds `trace_id` to every record ("-" outside a traced request)."""

    def filter(self, record: logging.LogRecord) -> bool:
        record.trace_id = trace_id() or "-"
        return True


def configure_logging() -> None:
    handler = logging.StreamHandler()
    handler.addFilter(TraceIdFilter())
    handler.setFormatter(logging.Formatter("%(asctime)s %(levelname)s %(name)s [trace %(trace_id)s]: %(message)s"))
    root = logging.getLogger()
    root.handlers[:] = [handler]
    root.setLevel(logging.INFO)
