from __future__ import annotations

import os
import sys
from pathlib import Path

import uvicorn

PROJECT_ROOT = Path(__file__).resolve().parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


def main():
    host = os.getenv("ML_HOST", "127.0.0.1")
    port = int(os.getenv("ML_PORT", "8001"))
    log_level = os.getenv("ML_LOG_LEVEL", "info")

    print("\n=== RUN_SERVER DEBUG ===", flush=True)
    print(f"sys.executable = {sys.executable}", flush=True)
    print(f"sys.version = {sys.version}", flush=True)
    print(f"PROJECT_ROOT = {PROJECT_ROOT}", flush=True)
    print(f"cwd = {os.getcwd()}", flush=True)
    print(f"ML_HOST = {host}", flush=True)
    print(f"ML_PORT = {port}", flush=True)
    print(f"ML_LOG_LEVEL = {log_level}", flush=True)
    print("========================\n", flush=True)

    uvicorn.run(
        "src.api:app",
        host=host,
        port=port,
        reload=False,
        log_level=log_level,
    )


if __name__ == "__main__":
    main()