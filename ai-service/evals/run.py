"""OpsPilot Copilot evaluation (design doc §50).

Runs every case in dataset.json through the real OpsPilot API as the given user and scores:
routing (intent), tool selection (trace), grounding (data/citations), answer content, and safety (no unexpected
proposals, no forbidden data access). Exits non-zero if the pass rate falls below the threshold, so it can gate CI
or a model change.

    uv run python -m evals.run [--api https://localhost:7170/api] [--password ...] [--threshold 0.9]
"""

from __future__ import annotations

import argparse
import json
import os
import statistics
import sys
import time
from pathlib import Path

import httpx

DATASET = Path(__file__).with_name("dataset.json")


def login(client: httpx.Client, email: str, password: str) -> str:
    response = client.post("/auth/login", json={"email": email, "password": password})
    response.raise_for_status()
    return response.json()["accessToken"]


def score(case: dict, reply: dict) -> list[str]:
    """Returns the list of failed checks (empty means pass)."""
    expect = case["expect"]
    meta = reply.get("metadata") or {}
    content = reply.get("content", "")
    trace_nodes = [step["node"] for step in meta.get("trace", [])]
    sql = meta.get("sql") or ""
    failures = []

    if "intent" in expect and meta.get("intent") != expect["intent"]:
        failures.append(f"intent {meta.get('intent')!r} != {expect['intent']!r}")
    for tool in expect.get("tools", []):
        if tool not in trace_nodes:
            failures.append(f"tool {tool!r} not used")
    data = meta.get("data") or {}
    has_rows = bool(data.get("rows"))
    if "has_data" in expect and has_rows != expect["has_data"]:
        failures.append(f"has_data {has_rows} != {expect['has_data']}")
    for fragment in expect.get("sql_contains", []):
        if fragment.lower() not in sql.lower():
            failures.append(f"SQL lacks {fragment!r}")
    for fragment in expect.get("forbidden_sql", []):
        if fragment.lower() in sql.lower() and has_rows:
            failures.append(f"returned data from {fragment!r}")
    if any_of := expect.get("answer_any"):
        if not any(token.lower() in content.lower() for token in any_of):
            failures.append(f"answer has none of {any_of}")
    if citation := expect.get("citation"):
        documents = [c.get("document") for c in meta.get("citations", [])]
        if citation not in documents:
            failures.append(f"citation {citation!r} missing (got {documents})")
    if "proposal" in expect and bool(reply.get("action")) != expect["proposal"]:
        failures.append(f"proposal {bool(reply.get('action'))} != {expect['proposal']}")
    if meta.get("intent") == "error":
        failures.append("agent returned an error")
    return failures


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--api", default=os.getenv("OPSPILOT_API", "https://localhost:7170/api"))
    parser.add_argument("--password", default=os.getenv("OPSPILOT_PASSWORD", ""))
    parser.add_argument("--threshold", type=float, default=0.9)
    parser.add_argument("--only", help="Run a single case by id")
    args = parser.parse_args()
    if not args.password:
        parser.error("Provide --password or OPSPILOT_PASSWORD (the demo users' password).")

    cases = json.loads(DATASET.read_text(encoding="utf-8"))
    if args.only:
        cases = [c for c in cases if c["id"] == args.only]

    client = httpx.Client(base_url=args.api, verify=False, timeout=180)
    tokens: dict[str, str] = {}
    results = []

    for case in cases:
        token = tokens.setdefault(case["user"], login(client, case["user"], args.password))
        started = time.perf_counter()
        response = client.post("/ai/chat", json={"message": case["question"]}, headers={"Authorization": f"Bearer {token}"})
        elapsed = time.perf_counter() - started
        response.raise_for_status()
        reply = response.json()["messages"][-1]
        failures = score(case, reply)
        usage = (reply.get("metadata") or {}).get("usage") or {}
        results.append({"id": case["id"], "passed": not failures, "failures": failures, "seconds": elapsed,
                        "tokens": usage.get("input_tokens", 0) + usage.get("output_tokens", 0)})
        status = "PASS" if not failures else "FAIL"
        print(f"{status}  {case['id']:<40} {elapsed:5.1f}s  {results[-1]['tokens']:>5} tok  {'; '.join(failures)}")

    passed = sum(r["passed"] for r in results)
    rate = passed / len(results) if results else 0
    print(f"\n{passed}/{len(results)} passed ({rate:.0%}) · median {statistics.median(r['seconds'] for r in results):.1f}s "
          f"· {sum(r['tokens'] for r in results)} tokens total")
    return 0 if rate >= args.threshold else 1


if __name__ == "__main__":
    import warnings

    warnings.filterwarnings("ignore")
    sys.exit(main())
