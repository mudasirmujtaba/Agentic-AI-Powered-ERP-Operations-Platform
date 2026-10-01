"""LLM access (Groq) plus token accounting for the node trace."""

from __future__ import annotations

import re
from typing import Any, TypeVar

from langchain_core.messages import BaseMessage, HumanMessage, SystemMessage
from langchain_groq import ChatGroq
from pydantic import BaseModel

from app.config import get_settings

T = TypeVar("T", bound=BaseModel)


class LlmUnavailable(Exception):
    pass


def _model(fast: bool) -> ChatGroq:
    settings = get_settings()
    if not settings.groq_api_key:
        raise LlmUnavailable("GROQ_API_KEY is not configured in ai-service/.env.")
    return ChatGroq(
        model=settings.groq_fast_model if fast else settings.groq_model,
        api_key=settings.groq_api_key,
        temperature=0,
        max_retries=2,
        timeout=60,
    )


_UNICODE_SPACES = re.compile(r"[    ]")
_UNICODE_HYPHENS = re.compile(r"[‐‑]")
_NATIVE_CITATION = re.compile(r"【(\d+)†[^】]*】")


def clean(text: str) -> str:
    """Normalises model output: non-breaking spaces/hyphens to ASCII, and native 【1†L3】 citations to [1]."""
    text = _UNICODE_SPACES.sub(" ", text)
    text = _UNICODE_HYPHENS.sub("-", text)
    return _NATIVE_CITATION.sub(r"[\1]", text)


def _usage(message: Any) -> dict:
    meta = getattr(message, "usage_metadata", None) or {}
    return {"input_tokens": meta.get("input_tokens", 0), "output_tokens": meta.get("output_tokens", 0)}


def complete(system: str, user: str, *, fast: bool = False, history: list[dict] | None = None) -> tuple[str, dict]:
    """Plain text completion. Returns (text, usage)."""
    messages: list[BaseMessage] = [SystemMessage(system)]
    for turn in history or []:
        messages.append(HumanMessage(turn["content"]) if turn["role"] == "user" else _assistant(turn["content"]))
    messages.append(HumanMessage(user))
    response = _model(fast).invoke(messages)
    return clean(str(response.content).strip()), _usage(response)


def structured(schema: type[T], system: str, user: str, *, fast: bool = False, history: list[dict] | None = None) -> tuple[T, dict]:
    """Structured output via tool calling. Returns (parsed model, usage)."""
    messages: list[BaseMessage] = [SystemMessage(system)]
    for turn in history or []:
        messages.append(HumanMessage(turn["content"]) if turn["role"] == "user" else _assistant(turn["content"]))
    messages.append(HumanMessage(user))
    result = _model(fast).with_structured_output(schema, include_raw=True).invoke(messages)
    parsed = result.get("parsed")
    if parsed is None:
        raise ValueError(f"Model did not return valid {schema.__name__}: {result.get('parsing_error')}")
    return parsed, _usage(result.get("raw"))


def _assistant(content: str) -> BaseMessage:
    from langchain_core.messages import AIMessage

    return AIMessage(content)
