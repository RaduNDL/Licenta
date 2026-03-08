from __future__ import annotations
import sys
from pathlib import Path
from typing import Optional
from fastapi import FastAPI, UploadFile, File, Form, HTTPException
from fastapi.responses import JSONResponse

PROJECT_ROOT = Path(__file__).resolve().parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

import src.CBISDDSM as mm

app = FastAPI(title="MedFlow ML", version="1.0")


@app.get("/api/status")
def api_status():
    cfg = mm.CbisDdsmConfig()
    tr = mm.get_training_state(cfg)
    return {"ok": bool(tr.get("artifact_ok")), "training": tr}


@app.post("/api/imaging/predict")
async def api_predict(
        file: UploadFile = File(...),
        model_id: str = Form(""),
        image_size: int = Form(224),
        require_quality: int = Form(0),
        require_domain: int = Form(1),
):
    try:
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
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
    finally:
        await file.close()


@app.get("/api/imaging/ground_truth")
def api_ground_truth(filename: Optional[str] = None):
    gt = mm.find_ground_truth(mm.CbisDdsmConfig(), filename or "")
    return {"ground_truth": gt}