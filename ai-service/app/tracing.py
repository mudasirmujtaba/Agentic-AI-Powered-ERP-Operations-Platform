"""Node timing for AI observability (design doc §43): every node reports its name, duration and a short detail."""

from __future__ import annotations

import functools
import time
from typing import Any, Callable


def traced(name: str) -> Callable:
    def decorator(node: Callable[[dict], dict]) -> Callable[[dict], dict]:
        @functools.wraps(node)
        def wrapper(state: dict) -> dict:
            started = time.perf_counter()
            status = "ok"
            try:
                result = node(state) or {}
            except Exception:
                status = "error"
                raise
            finally:
                elapsed = round((time.perf_counter() - started) * 1000)
            entry: dict[str, Any] = {"node": name, "ms": elapsed, "status": status}
            if detail := result.pop("_detail", None):
                entry["detail"] = detail
            # Tool timings recorded inside the node come first, then the node itself.
            tool_steps = result.pop("_tools", [])
            result["trace"] = [*tool_steps, entry]
            return result

        return wrapper

    return decorator


class ToolTimer:
    """Collects per-tool timings inside a node: `with timer.step("Inventory API"): ...`."""

    def __init__(self) -> None:
        self.steps: list[dict[str, Any]] = []

    def step(self, name: str):
        timer = self

        class _Step:
            def __enter__(self):
                self.started = time.perf_counter()
                return self

            def __exit__(self, exc_type, exc, tb):
                timer.steps.append({
                    "node": name,
                    "kind": "tool",
                    "ms": round((time.perf_counter() - self.started) * 1000),
                    "status": "error" if exc_type else "ok",
                })
                return False

        return _Step()
