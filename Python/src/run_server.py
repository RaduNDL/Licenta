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

    uvicorn.run(
        "src.api:app",
        host=host,
        port=port,
        reload=False,
        log_level=log_level,
    )

if __name__ == "__main__":
    main()