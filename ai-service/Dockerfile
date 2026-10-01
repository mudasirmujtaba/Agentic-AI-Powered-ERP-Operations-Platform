# OpsPilot AI service — FastAPI + LangGraph, dependencies installed from the uv lockfile.
FROM python:3.12-slim
COPY --from=ghcr.io/astral-sh/uv:0.8 /uv /uvx /bin/

WORKDIR /app
ENV UV_COMPILE_BYTECODE=1 UV_LINK_MODE=copy PYTHONUNBUFFERED=1

COPY pyproject.toml uv.lock ./
RUN uv sync --frozen --no-dev --no-install-project

COPY app ./app
COPY knowledge ./knowledge

# Checkpoints (paused approval runs) and the embedding model cache live on volumes.
ENV PATH="/app/.venv/bin:$PATH" FASTEMBED_CACHE_PATH=/app/models CHECKPOINT_DB=/app/data/checkpoints.sqlite
RUN useradd --create-home --uid 10001 opspilot && mkdir -p /app/data /app/models && chown -R opspilot /app/data /app/models
USER opspilot

EXPOSE 8001
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8001"]
