from functools import lru_cache
from pathlib import Path

from pydantic_settings import BaseSettings, SettingsConfigDict

SERVICE_ROOT = Path(__file__).resolve().parent.parent


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=SERVICE_ROOT / ".env", extra="ignore")

    groq_api_key: str = ""
    groq_model: str = "openai/gpt-oss-120b"
    groq_fast_model: str = "openai/gpt-oss-20b"
    internal_key: str = ""
    erp_api_url: str = "https://localhost:7170/api"
    erp_verify_tls: bool = False
    checkpoint_db: Path = SERVICE_ROOT / "data" / "checkpoints.sqlite"
    knowledge_dir: Path = SERVICE_ROOT / "knowledge"


@lru_cache
def get_settings() -> Settings:
    return Settings()
