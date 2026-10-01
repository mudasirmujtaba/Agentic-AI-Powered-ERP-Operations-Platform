from pathlib import Path

from app.agents.router import heuristic_route
from app.rag import Chunk, KnowledgeBase, chunk_markdown

KNOWLEDGE = Path(__file__).resolve().parent.parent / "knowledge"


def test_heuristic_routes_cover_each_workflow():
    assert heuristic_route("Why hasn't order SO-10044 been delivered?").intent == "order_investigation"
    assert heuristic_route("Why hasn't order SO-10044 been delivered?").order_number == "SO-10044"
    assert heuristic_route("Prepare a purchase recommendation for low stock").intent == "purchase_recommendation"
    assert heuristic_route("Are we going to run out of X200?").intent == "inventory_risk"
    assert heuristic_route("What is the approval limit policy for purchases?").intent == "policy"
    assert heuristic_route("Which customers spent the most this year?").intent == "erp_query"


def test_markdown_is_chunked_per_section_with_document_title():
    chunks = chunk_markdown(KNOWLEDGE / "procurement-policy.md")
    sections = [c.section for c in chunks]
    assert "Approval thresholds" in sections
    assert all(c.document == "Procurement Policy" for c in chunks)


def test_keyword_retrieval_finds_the_approval_threshold_section():
    chunks = [c for path in sorted(KNOWLEDGE.glob("*.md")) for c in chunk_markdown(path)]
    kb = KnowledgeBase.__new__(KnowledgeBase)  # keyword mode, without loading the embedding model
    kb.chunks = chunks
    kb._embedder = None
    kb._vectors = None
    from collections import Counter

    from app.rag import _tokens

    kb._doc_freq = Counter(t for c in chunks for t in set(_tokens(c.text + " " + c.section)))

    top, _ = kb.search("Who must approve purchases above $10,000?", k=1)[0]
    assert top.section == "Approval thresholds"
    assert isinstance(top, Chunk)


def test_model_output_is_normalised():
    from app.llm import clean

    assert clean("more than 60\u202fdays\u00a0overdue") == "more than 60 days overdue"
    assert clean("non\u2011breaking") == "non-breaking"
    assert clean("approved by a Manager【1†L3-L5】【2†L2】.") == "approved by a Manager[1][2]."
