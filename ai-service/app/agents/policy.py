"""Policy answers grounded in the knowledge base, with citations (design doc §27–28)."""

from __future__ import annotations

from app import llm
from app.rag import knowledge_base
from app.tracing import ToolTimer, traced


@traced("Policy (RAG) agent")
def answer_policy(state: dict) -> dict:
    timer = ToolTimer()
    with timer.step("Retrieval"):
        kb = knowledge_base()
        hits = kb.search(state["message"], k=3)

    sources = "\n\n".join(f"[{i + 1}] {chunk.document} — {chunk.section}\n{chunk.text}" for i, (chunk, _) in enumerate(hits))
    with timer.step("Grounded answer"):
        answer, usage = llm.complete(
            "You answer questions about company policy using ONLY the numbered sources. Cite sources inline like [1]. "
            "If the sources don't contain the answer, say the policy documents don't cover it — never invent a policy. "
            "Be concise: two to five sentences.",
            f"Question: {state['message']}\n\nSources:\n{sources}",
            history=state.get("history", [])[-2:],
        )

    citations = [
        {"index": i + 1, "document": chunk.document, "section": chunk.section, "source": chunk.source,
         "snippet": chunk.text[:240] + ("…" if len(chunk.text) > 240 else ""), "score": round(score, 3)}
        for i, (chunk, score) in enumerate(hits)
    ]
    return {"answer": answer, "citations": citations, "usage": usage, "_tools": timer.steps, "_detail": f"{kb.mode} · {len(hits)} sources"}
