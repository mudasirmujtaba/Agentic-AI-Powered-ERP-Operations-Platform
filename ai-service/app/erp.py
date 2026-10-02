"""Typed access to the OpsPilot API, always as the signed-in user.

Every call carries the user's own bearer token, so the ERP's role-based authorization applies to the agents exactly
as it does to the UI: a user who cannot see invoices in OpsPilot cannot see them through the Copilot either.
"""

from __future__ import annotations

import time
from typing import Any

import httpx

from app.config import get_settings
from app.observability import traceparent


class ErpError(Exception):
    """An ERP call failed. `detail` is the API's ProblemDetails message when there is one."""

    def __init__(self, message: str, status: int | None = None, detail: str | None = None):
        super().__init__(message)
        self.status = status
        self.detail = detail or message


def _headers(access_token: str) -> dict[str, str]:
    headers = {"Authorization": f"Bearer {access_token}"}
    # Forward the caller's trace so the ERP's handling of these calls joins the same distributed trace.
    if parent := traceparent.get():
        headers["traceparent"] = parent
    return headers


class ErpClient:
    def __init__(self, access_token: str, base_url: str | None = None, transport: httpx.BaseTransport | None = None):
        settings = get_settings()
        self._client = httpx.Client(
            base_url=(base_url or settings.erp_api_url).rstrip("/") + "/",
            headers=_headers(access_token),
            verify=settings.erp_verify_tls,
            timeout=15.0,
            transport=transport,
        )

    def close(self) -> None:
        self._client.close()

    def get(self, path: str, **params: Any) -> Any:
        clean = {k: v for k, v in params.items() if v is not None}
        return self._send("GET", path, params=clean)

    def post(self, path: str, body: dict) -> Any:
        return self._send("POST", path, json=body)

    def _send(self, method: str, path: str, **kwargs: Any) -> Any:
        last_error: Exception | None = None
        # One retry for transient network failures; HTTP errors are returned to the caller immediately.
        for attempt in range(2):
            try:
                response = self._client.request(method, path.lstrip("/"), **kwargs)
            except httpx.TransportError as error:
                last_error = error
                time.sleep(0.3 * (attempt + 1))
                continue

            if response.is_success:
                return response.json() if response.content else None

            detail = None
            try:
                body = response.json()
                detail = body.get("detail") or body.get("title")
                if body.get("errors"):
                    detail = "; ".join(f"{k}: {' '.join(v)}" for k, v in body["errors"].items())
            except ValueError:
                pass
            raise ErpError(f"{method} {path} failed with {response.status_code}", response.status_code, detail)

        raise ErpError(f"{method} {path} failed: {last_error}")

    # Convenience wrappers ------------------------------------------------------------------------------------

    def sql(self, query: str) -> dict:
        return self.post("ai/sql", {"sql": query})

    def schema(self) -> list[dict]:
        return self.get("ai/schema")
