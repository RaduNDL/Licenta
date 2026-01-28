from contextlib import asynccontextmanager
from fastapi import FastAPI, File, UploadFile, HTTPException
from fastapi.responses import JSONResponse
from . import ISIC2019 as isic

_state = {"started": False, "done": False, "error": None}

@asynccontextmanager
async def lifespan(app: FastAPI):
    try:
        _state["started"] = True
        if not isic.artifact_path().exists():
            print("Model artifact not found. Starting initial training...")
            isic.train_and_save()
        _state["done"] = True
    except Exception as e:
        _state["error"] = str(e)
        print(f"Startup error: {e}")
    yield

app = FastAPI(lifespan=lifespan)

@app.get("/api/status")
async def status():
    return {
        "ok": True,
        "training": {
            "started": _state["started"],
            "done": _state["done"],
            "error": _state["error"],
            "artifact_ok": isic.artifact_path().exists(),
            "artifact_path": str(isic.artifact_path())
        }
    }

@app.post("/api/imaging/predict")
async def imaging_predict(file: UploadFile = File(...), image_size: int = 224):
    try:
        if not _state["done"]:
            msg = "Model not ready"
            if _state["error"]:
                msg = f"Model training failed: {_state['error']}"
            raise HTTPException(status_code=503, detail=msg)

        file_bytes = await file.read()
        label, probas, extras = isic.predict_bytes(file_bytes, int(image_size))

        return {
            "label": label,
            "best_probability": float(max(probas.values())) if probas else 0.0,
            "probabilities": probas,
            "extras": extras
        }

    except HTTPException:
        raise
    except Exception as e:
        return JSONResponse(status_code=500, content={"detail": str(e)})