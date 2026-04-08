from __future__ import annotations

import os
import sys
import traceback
from pathlib import Path
from typing import Optional

import torch
from fastapi import FastAPI, UploadFile, File, Form, HTTPException
from fastapi.responses import JSONResponse

PROJECT_ROOT = Path(__file__).resolve().parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

import src.CBISDDSM as mm

app = FastAPI(title="MedFlow ML", version="1.0")


def _print_ml_env_debug() -> None:
    print("\n=== ML SERVER ENV DEBUG ===", flush=True)
    print(f"sys.executable = {sys.executable}", flush=True)
    print(f"sys.version = {sys.version}", flush=True)
    print(f"PROJECT_ROOT = {PROJECT_ROOT}", flush=True)
    print(f"cwd = {os.getcwd()}", flush=True)
    print(f"torch.__version__ = {torch.__version__}", flush=True)
    print(f"torch.version.cuda = {torch.version.cuda}", flush=True)
    print(f"torch.cuda.is_available() = {torch.cuda.is_available()}", flush=True)
    print(f"torch.cuda.device_count() = {torch.cuda.device_count()}", flush=True)
    print(f"CUDA_VISIBLE_DEVICES = {os.environ.get('CUDA_VISIBLE_DEVICES')}", flush=True)
    print(f"ML_HOST = {os.environ.get('ML_HOST')}", flush=True)
    print(f"ML_PORT = {os.environ.get('ML_PORT')}", flush=True)
    print("=================================\n", flush=True)


_print_ml_env_debug()


@app.get("/api/status")
def api_status():
    cfg = mm.CbisDdsmConfig()
    tr = mm.get_training_state(cfg)

    return {
        "ok": bool(tr.get("artifact_ok")),
        "training": tr,
        "runtime": {
            "python": sys.executable,
            "torch_version": torch.__version__,
            "torch_cuda_version": torch.version.cuda,
            "cuda_available": torch.cuda.is_available(),
            "cuda_device_count": torch.cuda.device_count(),
        },
    }


@app.post("/api/imaging/predict")
async def api_predict(
    file: UploadFile = File(...),
    model_id: str = Form(""),
    image_size: int = Form(224),
    require_quality: int = Form(0),
    require_domain: int = Form(1),
):
    try:
        print("\n=== BEFORE PREDICT ===", flush=True)
        print(f"sys.executable = {sys.executable}", flush=True)
        print(f"filename = {file.filename}", flush=True)
        print(f"model_id = {model_id}", flush=True)
        print(f"image_size = {image_size}", flush=True)
        print(f"require_quality = {require_quality}", flush=True)
        print(f"require_domain = {require_domain}", flush=True)
        print(f"torch.cuda.is_available() = {torch.cuda.is_available()}", flush=True)
        print(f"torch.version.cuda = {torch.version.cuda}", flush=True)
        if torch.cuda.is_available():
            try:
                print(f"GPU = {torch.cuda.get_device_name(0)}", flush=True)
            except Exception:
                traceback.print_exc()
        print("======================\n", flush=True)

        b = await file.read()
        if not b:
            raise HTTPException(status_code=400, detail="File is empty")

        out = mm.predict_bytes(
            mm.CbisDdsmConfig(),
            b,
            filename=file.filename or "",
            image_size=int(image_size),
            model_id=model_id,
            require_quality=bool(int(require_quality)),
            require_domain=bool(int(require_domain)),
        )

        status = 422 if out.get("label") in ("OUT_OF_DOMAIN", "UNUSABLE_IMAGE") else 200
        return JSONResponse(status_code=status, content=out)

    except HTTPException:
        raise
    except Exception as e:
        print("\n=== ML PREDICT ERROR ===", flush=True)
        print(f"filename = {file.filename}", flush=True)
        print(f"model_id = {model_id}", flush=True)
        print(f"image_size = {image_size}", flush=True)
        print(f"require_quality = {require_quality}", flush=True)
        print(f"require_domain = {require_domain}", flush=True)
        print(f"sys.executable = {sys.executable}", flush=True)
        print(f"torch.__version__ = {torch.__version__}", flush=True)
        print(f"torch.version.cuda = {torch.version.cuda}", flush=True)
        print(f"torch.cuda.is_available() = {torch.cuda.is_available()}", flush=True)
        traceback.print_exc()
        print("=================================\n", flush=True)
        raise HTTPException(status_code=500, detail=f"{type(e).__name__}: {e}")
    finally:
        await file.close()


@app.get("/api/imaging/ground_truth")
def api_ground_truth(filename: Optional[str] = None):
    gt = mm.find_ground_truth(mm.CbisDdsmConfig(), filename or "")
    return {"ground_truth": gt}