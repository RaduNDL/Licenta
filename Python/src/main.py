import sys
import os
import uvicorn

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

def main():
    host = os.getenv("ML_HOST", "0.0.0.0")
    port = int(os.getenv("ML_PORT", "8001"))
    uvicorn.run("src.api:app", host=host, port=port, log_level="info", reload=False)

if __name__ == "__main__":
    main()