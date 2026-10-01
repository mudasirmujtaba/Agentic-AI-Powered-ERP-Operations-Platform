"""ERP Query agent (design doc §23): natural language → validated read-only SQL → grounded answer.

Natural Language → Intent → Query Plan → SQL Generation → SQL Validation (by the ERP) → Read-only Execution → Analysis.
"""

from __future__ import annotations

import json
from datetime import datetime, timezone
from functools import lru_cache

from pydantic import BaseModel, Field

from app import llm
from app.context import current
from app.erp import ErpError
from app.tracing import ToolTimer, traced

MAX_ROWS_TO_MODEL = 40
MAX_ROWS_TO_UI = 50


class SqlPlan(BaseModel):
    reasoning: str = Field(description="One sentence: which views and filters answer the question.")
    sql: str = Field(description="A single T-SQL SELECT statement.")


SQL_RULES = """You write one Microsoft SQL Server (T-SQL) SELECT statement that answers the user's question.
Rules:
- Use ONLY the views listed below, always with the ai. prefix (e.g. ai.sales_orders). Never use other tables.
- SELECT only. One statement. No INTO, no comments, no semicolons.
- Use TOP 50 for row listings; aggregate queries may omit TOP.
- Dates: GETUTCDATE(), DATEADD(day, -60, GETUTCDATE()), YEAR(order_date) = YEAR(GETUTCDATE()).
- bit columns are compared with = 1 / = 0. Status values are exact strings as documented.
- Prefer human-readable columns (codes, names) over ids. Give aggregates clear aliases that say what they measure.
- Compute durations in SQL, e.g. DATEDIFF(day, required_date, GETUTCDATE()) AS days_late; never leave them to be inferred.
- If the question cannot be answered from these views, select a single column named note explaining why.
"""


@lru_cache(maxsize=16)
def _schema_text(roles_key: str) -> str:
    """Schema of the views this user may read, cached per role set."""
    views = current().erp.schema()
    lines = []
    for view in views:
        columns = ", ".join(f"{c['name']} {c['type']}" for c in view["columns"])
        lines.append(f"{view['view']} — {view['description']}\n  columns: {columns}")
    return "\n".join(lines)


def _rows_as_records(result: dict, limit: int) -> list[dict]:
    columns = result["columns"]
    return [dict(zip(columns, row)) for row in result["rows"][:limit]]


@traced("ERP query agent")
def erp_query(state: dict) -> dict:
    context = current()
    timer = ToolTimer()
    question = state["message"]

    with timer.step("Schema lookup"):
        schema = _schema_text(",".join(sorted(context.roles)))

    today = datetime.now(timezone.utc).date().isoformat()
    system = SQL_RULES + f"\nToday (UTC) is {today}.\nAvailable views:\n" + schema
    usage_total = {"input_tokens": 0, "output_tokens": 0}

    def add_usage(u: dict) -> None:
        for k in usage_total:
            usage_total[k] += u.get(k, 0)

    with timer.step("SQL generation"):
        plan, usage = llm.structured(SqlPlan, system, question, history=state.get("history", [])[-4:])
    add_usage(usage)

    sql = plan.sql.strip().rstrip(";")
    result = None
    error = None
    for attempt in range(2):
        try:
            with timer.step("SQL validation + read-only execution" + (" (retry)" if attempt else "")):
                result = context.erp.sql(sql)
            break
        except ErpError as failure:
            error = failure.detail
            if attempt == 1 or failure.status not in (400, 422):
                break
            with timer.step("SQL correction"):
                plan, usage = llm.structured(
                    SqlPlan,
                    system,
                    f"Question: {question}\nYour previous SQL:\n{sql}\nwas rejected with: {error}\nWrite a corrected query.",
                )
            add_usage(usage)
            sql = plan.sql.strip().rstrip(";")

    if result is None:
        return {
            "answer": "I couldn't run a valid query for that question, so I won't guess. "
                      f"The last error was: {error}. Try rephrasing, or ask about a specific record.",
            "sql": sql,
            "usage": usage_total,
            "_tools": timer.steps,
            "_detail": "query failed",
        }

    records = _rows_as_records(result, MAX_ROWS_TO_MODEL)
    with timer.step("Result analysis"):
        answer, usage = llm.complete(
            "You are OpsPilot's ERP analyst. Answer the question using ONLY the query result provided. "
            "Be concise: lead with the direct answer, then at most 5 short bullet points of supporting detail. "
            f"Today (UTC) is {today}. Format money as $12,345.67. Do not mention SQL or views. "
            "Only state numbers that appear in the result; never reinterpret a column as something its name does not say. "
            "Do not pad the answer with generic advice. If the result is empty, say no records match. "
            + ("Note: the result was truncated, so totals over the listed rows may be incomplete. " if result.get("truncated") else ""),
            f"Question: {question}\nRows returned: {len(result['rows'])}\nResult (JSON): {json.dumps(records, default=str)}",
        )
    add_usage(usage)

    return {
        "answer": answer,
        "sql": sql,
        "data": {
            "columns": result["columns"],
            "rows": result["rows"][:MAX_ROWS_TO_UI],
            "truncated": bool(result.get("truncated")) or len(result["rows"]) > MAX_ROWS_TO_UI,
        },
        "usage": usage_total,
        "_tools": timer.steps,
        "_detail": f"{len(result['rows'])} rows",
    }
