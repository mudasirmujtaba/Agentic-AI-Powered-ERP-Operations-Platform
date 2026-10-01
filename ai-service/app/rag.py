"""Policy knowledge base (design doc §27): Document → Chunking → Metadata → Embedding → Vector storage → Retrieval.

Embeddings come from a small local ONNX model (fastembed), so retrieval works offline and costs nothing per query.
If the model can't be loaded, retrieval falls back to keyword overlap scoring rather than failing.
"""

from __future__ import annotations

import logging
import math
import re
from collections import Counter
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path

import numpy as np

from app.config import get_settings

log = logging.getLogger(__name__)

EMBEDDING_MODEL = "BAAI/bge-small-en-v1.5"
WORD = re.compile(r"[a-z0-9$]+")


@dataclass(frozen=True)
class Chunk:
    document: str
    section: str
    text: str
    source: str


def chunk_markdown(path: Path) -> list[Chunk]:
    """One chunk per `##` section, prefixed with its document title so each chunk stands alone."""
    content = path.read_text(encoding="utf-8")
    title_match = re.search(r"^# (.+)$", content, re.MULTILINE)
    title = title_match.group(1).strip() if title_match else path.stem
    chunks = []
    for block in re.split(r"^## ", content, flags=re.MULTILINE)[1:]:
        heading, _, body = block.partition("\n")
        text = " ".join(body.split())
        if text:
            chunks.append(Chunk(document=title, section=heading.strip(), text=text, source=path.name))
    return chunks


def _tokens(text: str) -> list[str]:
    return WORD.findall(text.lower())


class KnowledgeBase:
    def __init__(self, chunks: list[Chunk]):
        self.chunks = chunks
        self._embedder = None
        self._vectors: np.ndarray | None = None
        try:
            from fastembed import TextEmbedding

            self._embedder = TextEmbedding(EMBEDDING_MODEL)
            vectors = np.array(list(self._embedder.embed([f"{c.document} — {c.section}: {c.text}" for c in chunks])))
            self._vectors = vectors / np.linalg.norm(vectors, axis=1, keepdims=True)
        except Exception as error:  # Offline or model download blocked: degrade to keyword retrieval.
            log.warning("Embedding model unavailable (%s); using keyword retrieval", error)
            self._embedder = None
        self._doc_freq = Counter(t for c in chunks for t in set(_tokens(c.text + " " + c.section)))

    @property
    def mode(self) -> str:
        return "embeddings" if self._vectors is not None else "keywords"

    def search(self, query: str, k: int = 3) -> list[tuple[Chunk, float]]:
        if self._vectors is not None and self._embedder is not None:
            q = np.array(list(self._embedder.query_embed([query]))[0])
            q = q / np.linalg.norm(q)
            scores = self._vectors @ q
        else:
            scores = np.array([self._keyword_score(query, c) for c in self.chunks])
        order = np.argsort(-scores)[:k]
        return [(self.chunks[i], float(scores[i])) for i in order]

    def _keyword_score(self, query: str, chunk: Chunk) -> float:
        n = len(self.chunks)
        terms = Counter(_tokens(chunk.text + " " + chunk.section + " " + chunk.document))
        return sum(terms[t] * math.log(1 + n / (1 + self._doc_freq[t])) for t in set(_tokens(query)))


@lru_cache(maxsize=1)
def knowledge_base() -> KnowledgeBase:
    directory = get_settings().knowledge_dir
    chunks = [chunk for path in sorted(directory.glob("*.md")) for chunk in chunk_markdown(path)]
    log.info("Indexed %d knowledge chunks from %s", len(chunks), directory)
    return KnowledgeBase(chunks)
