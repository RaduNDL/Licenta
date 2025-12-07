# app.py

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import Dict, Any
import joblib
import numpy as np
import os

# ---------------------------
# PATH-URI
# ---------------------------

BASE_DIR = os.path.dirname(__file__)
MODELS_DIR = os.path.join(BASE_DIR, "models")

MODEL_PATH = os.path.join(MODELS_DIR, "breast_cancer_model.pkl")
SCALER_PATH = os.path.join(MODELS_DIR, "breast_cancer_scaler.pkl")

if not (os.path.exists(MODEL_PATH) and os.path.exists(SCALER_PATH)):
    raise RuntimeError(
        f"Model or scaler not found.\n"
        f"Expected:\n  {MODEL_PATH}\n  {SCALER_PATH}\n"
        f"Run train_breast_model.py first."
    )

model = joblib.load(MODEL_PATH)
scaler = joblib.load(SCALER_PATH)

# ---------------------------
# FEATURES (ORDINEA EXACTĂ)
# ---------------------------

FEATURE_COLS = [
    "radius_mean",
    "texture_mean",
    "perimeter_mean",
    "area_mean",
    "smoothness_mean",
    "compactness_mean",
    "concavity_mean",
    "concave_points_mean",
    "symmetry_mean",
    "fractal_dimension_mean",
]

# ---------------------------
# MODEL PENTRU TEST / SWAGGER
# ---------------------------

class BreastCancerFeatures(BaseModel):
    radius_mean: float
    texture_mean: float
    perimeter_mean: float
    area_mean: float
    smoothness_mean: float
    compactness_mean: float
    concavity_mean: float
    concave_points_mean: float
    symmetry_mean: float
    fractal_dimension_mean: float

    radius_se: float
    texture_se: float
    perimeter_se: float
    area_se: float
    smoothness_se: float
    compactness_se: float
    concavity_se: float
    concave_points_se: float
    symmetry_se: float
    fractal_dimension_se: float

    radius_worst: float
    texture_worst: float
    perimeter_worst: float
    area_worst: float
    smoothness_worst: float
    compactness_worst: float
    concavity_worst: float
    concave_points_worst: float
    symmetry_worst: float
    fractal_dimension_worst: float


app = FastAPI(title="Breast Cancer Prediction API")


@app.get("/")
def read_root():
    return {"status": "ok", "message": "Breast Cancer Prediction API running"}


def _normalize_key(key: str) -> str:
    """
    Transformă:
    - 'Radius_mean'
    - 'radiusMean'
    - 'radius mean'
    - 'RADIUS_MEAN'
    în 'radiusmean'
    """
    return key.replace(" ", "").replace("_", "").lower()


def _predict_from_dict(payload: Dict[str, Any]):
    """
    Primește un dict cu orice fel de chei și le mapează la FEATURE_COLS.
    Lipsurile le pune 0.0 ca să NU mai pice request-ul.
    """
    if not isinstance(payload, dict):
        raise HTTPException(status_code=400, detail="Invalid JSON payload")

    print("=== Incoming payload keys ===")
    print(list(payload.keys()))

    normalized_payload = {_normalize_key(k): v for k, v in payload.items()}

    values = []

    missing_cols = []

    for col in FEATURE_COLS:
        norm_col = _normalize_key(col)
        if norm_col in normalized_payload:
            val = normalized_payload[norm_col]
        else:
            # dacă lipsește feature-ul, punem 0.0 ca default
            val = 0.0
            missing_cols.append(col)

        try:
            values.append(float(val))
        except Exception:
            raise HTTPException(
                status_code=400,
                detail=f"Feature {col} has non-numeric value: {val}"
            )

    if missing_cols:
        print("WARNING: Missing features from payload. Using 0.0 for:", missing_cols)

    x = np.array(values, dtype=float).reshape(1, -1)
    x_scaled = scaler.transform(x)

    y_pred = model.predict(x_scaled)[0]
    y_proba = model.predict_proba(x_scaled)[0]

    label = "M" if y_pred == 1 else "B"
    prob_benign = float(y_proba[0])
    prob_malignant = float(y_proba[1])

    return {
        "label": label,
        "probability": prob_malignant,  # P(M)
        "probability_benign": prob_benign,
        "probability_malignant": prob_malignant,
        "explanation": f"Model logistic regression, P(M)={prob_malignant:.2f}"
    }


# Endpoint pentru testare din /docs
@app.post("/predict")
def predict(features: BreastCancerFeatures):
    return _predict_from_dict(features.dict())


# Endpointul chemat de Razor: /api/breast/analyze
@app.post("/api/breast/analyze")
def analyze_breast(payload: Dict[str, Any]):
    return _predict_from_dict(payload)
