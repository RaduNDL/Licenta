from __future__ import annotations

import logging
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

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
)
logger = logging.getLogger("ml_server")

DEBUG_ENV = os.environ.get("ML_DEBUG", "0").strip() == "1"

app = FastAPI(title="MedFlow ML", version="1.0")


def _log_ml_env() -> None:
    if not DEBUG_ENV:
        return
    logger.debug("=== ML SERVER ENV ===")
    logger.debug("sys.executable = %s", sys.executable)
    logger.debug("sys.version = %s", sys.version)
    logger.debug("PROJECT_ROOT = %s", PROJECT_ROOT)
    logger.debug("cwd = %s", os.getcwd())
    logger.debug("torch = %s", torch.__version__)
    logger.debug("torch.cuda = %s", torch.version.cuda)
    logger.debug("cuda_available = %s", torch.cuda.is_available())
    logger.debug("cuda_device_count = %s", torch.cuda.device_count())
    logger.debug("CUDA_VISIBLE_DEVICES = %s", os.environ.get("CUDA_VISIBLE_DEVICES"))
    logger.debug("ML_HOST = %s", os.environ.get("ML_HOST"))
    logger.debug("ML_PORT = %s", os.environ.get("ML_PORT"))
    logger.debug("=====================")


_log_ml_env()


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
        logger.info(
            "Predict request: filename=%s model_id=%s image_size=%s "
            "require_quality=%s require_domain=%s",
            file.filename, model_id, image_size, require_quality, require_domain,
        )

        if DEBUG_ENV:
            logger.debug("cuda_available=%s cuda_version=%s", torch.cuda.is_available(), torch.version.cuda)
            if torch.cuda.is_available():
                try:
                    logger.debug("GPU = %s", torch.cuda.get_device_name(0))
                except Exception:
                    logger.debug("Could not get GPU name", exc_info=True)

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

        logger.info("Predict result: label=%s status=%s", out.get("label"), status)

        return JSONResponse(status_code=status, content=out)

    except HTTPException:
        raise
    except Exception as e:
        logger.error(
            "Predict failed: filename=%s model_id=%s error=%s: %s",
            file.filename, model_id, type(e).__name__, e,
            exc_info=True,
        )
        raise HTTPException(status_code=500, detail=f"{type(e).__name__}: {e}")
    finally:
        await file.close()


@app.get("/api/imaging/ground_truth")
def api_ground_truth(filename: Optional[str] = None):
    gt = mm.find_ground_truth(mm.CbisDdsmConfig(), filename or "")
    return {"ground_truth": gt}