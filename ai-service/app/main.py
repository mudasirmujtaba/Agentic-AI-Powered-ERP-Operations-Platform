"""FastAPI surface of the AI service. Only the OpsPilot API calls it (shared internal key)."""

from __future__ import annotations

import hmac
import logging
import threading
import time
from contextlib import asynccontextmanager
from typing import Any

from fastapi import Depends, FastAPI, Header, HTTPException
from langgraph.types import Command
from pydantic import BaseModel, Field

from app import llm, tickets
from app.config import get_settings
from app.context import RequestContext, set_current
from app.erp import ErpClient, ErpError
from app.graph import create_persistent_graph
from app.rag import knowledge_base

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
log = logging.getLogger("opspilot.ai")

graph = create_persistent_graph()


@asynccontextmanager
async def lifespan(_: FastAPI):
    # Build the policy index in the background so the first policy question isn't slow.
    threading.Thread(target=knowledge_base, daemon=True).start()
    yield


app = FastAPI(title="OpsPilot AI service", version="0.1.0", lifespan=lifespan)


class User(BaseModel):
    id: str
    email: str = ""
    name: str = ""
    roles: list[str] = Field(default_factory=list)


class HistoryMessage(BaseModel):
    role: str
    content: str


class ChatRequest(BaseModel):
    threadId: str
    message: str
    history: list[HistoryMessage] = Field(default_factory=list)
    user: User
    accessToken: str


class ResumeRequest(BaseModel):
    threadId: str
    decision: str
    comments: str | None = None
    outcome: dict[str, Any] = Field(default_factory=dict)
    user: User
    accessToken: str


def require_internal_key(x_internal_key: str = Header(default="")) -> None:
    expected = get_settings().internal_key
    if not expected or not hmac.compare_digest(x_internal_key, expected):
        raise HTTPException(status_code=401, detail="Invalid internal key")


@app.get("/health")
def health() -> dict:
    settings = get_settings()
    return {
        "status": "ok",
        "llmConfigured": bool(settings.groq_api_key),
        "model": settings.groq_model,
        "fastModel": settings.groq_fast_model,
    }


@app.post("/agent/chat", dependencies=[Depends(require_internal_key)])
def chat(request: ChatRequest) -> dict:
    config = {"configurable": {"thread_id": request.threadId}}
    initial = {
        "message": request.message,
        "history": [m.model_dump() for m in request.history],
        "user": request.user.model_dump(),
        "trace": [],
        "usage": {},
    }
    return _run(request.accessToken, request.user, lambda: graph.invoke(initial, config), config, trace_offset=0)


@app.post("/agent/resume", dependencies=[Depends(require_internal_key)])
def resume(request: ResumeRequest) -> dict:
    config = {"configurable": {"thread_id": request.threadId}}
    snapshot = graph.get_state(config)
    if not snapshot.next:
        raise HTTPException(status_code=409, detail="This thread is not waiting for a decision.")
    offset = len(snapshot.values.get("trace", []))
    value = {"decision": request.decision, "comments": request.comments, "outcome": request.outcome}
    return _run(request.accessToken, request.user, lambda: graph.invoke(Command(resume=value), config), config, trace_offset=offset)


@app.post("/tickets/summarize", dependencies=[Depends(require_internal_key)])
def summarize_ticket(request: tickets.TicketSummaryInput) -> dict:
    return _one_shot(lambda: tickets.summarize(request))


@app.post("/tickets/insights", dependencies=[Depends(require_internal_key)])
def ticket_insights(request: tickets.TicketInsightsInput) -> dict:
    return _one_shot(lambda: tickets.insights(request))


def _one_shot(task) -> dict:
    try:
        return task()
    except llm.LlmUnavailable as error:
        raise HTTPException(status_code=503, detail=str(error)) from error
    except Exception as error:
        log.exception("Ticket AI task failed")
        raise HTTPException(status_code=502, detail="The AI model call failed.") from error


def _run(access_token: str, user: User, invoke, config: dict, trace_offset: int) -> dict:
    erp = ErpClient(access_token)
    set_current(RequestContext(erp=erp, user=user.model_dump(), roles=user.roles))
    started = time.perf_counter()
    try:
        state = invoke()
    except llm.LlmUnavailable as error:
        return _reply(f"The AI model isn't configured yet: {error}", "error", started)
    except ErpError as error:
        log.warning("ERP call failed: %s (%s)", error, error.detail)
        return _reply("I couldn't retrieve the data I needed from OpsPilot "
                      f"({error.detail}), so I won't guess. Please try again.", "error", started)
    except Exception:
        log.exception("Agent run failed")
        return _reply("Something went wrong while I was working on that, and I'd rather not guess. Please try again.", "error", started)
    finally:
        erp.close()

    snapshot = graph.get_state(config)
    awaiting_approval = bool(snapshot.next)
    trace = (state.get("trace") or [])[trace_offset:]
    return _reply(
        state.get("answer") or "I don't have an answer for that.",
        state.get("intent"),
        started,
        proposal=state.get("proposal") if awaiting_approval else None,
        metadata={
            "data": state.get("data") if trace_offset == 0 else None,
            "sql": state.get("sql") if trace_offset == 0 else None,
            "citations": (state.get("citations") or []) if trace_offset == 0 else [],
            "findings": state.get("findings") if trace_offset == 0 else None,
            "trace": trace,
            "usage": state.get("usage") or {},
            "awaitingApproval": awaiting_approval,
        },
    )


def _reply(content: str, intent: str | None, started: float, proposal: dict | None = None, metadata: dict | None = None) -> dict:
    settings = get_settings()
    meta = metadata or {}
    meta["model"] = settings.groq_model
    meta["durationMs"] = round((time.perf_counter() - started) * 1000)
    return {"content": content, "intent": intent, "proposal": proposal, "metadata": meta}
