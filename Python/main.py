from fastapi import FastAPI, File, UploadFile, Form, Body
from pydantic import BaseModel
import random
import joblib
import os
import numpy as np
import pandas as pd  # <- ca să dăm modelului feature names
from typing import Optional, Dict, Any, List

app = FastAPI()


class PredictionResponse(BaseModel):
    Label: str
    Probability: float
    Explanation: Optional[str] = None
    RawModelName: Optional[str] = None


# ============================================================
#  DUMMY LAB ENDPOINT (poți lăsa exact așa, doar exemplu)
# ============================================================
@app.post("/api/lab/analyze", response_model=PredictionResponse)
async def analyze_lab_result(
    lab_result_id: str = Form(...),
    patient_id: str = Form(...),
    file: UploadFile = File(...),
):
    content = await file.read()
    size_kb = len(content) / 1024.0

    label = "High risk" if size_kb > 100 else "Normal"
    prob = float(f"{random.uniform(0.7, 0.99):.2f}")
    explanation = f"Dummy model (lab): file size ≈ {size_kb:.1f} KB, label = {label}"

    return PredictionResponse(
        Label=label,
        Probability=prob,
        Explanation=explanation,
        RawModelName="dummy_lab_v1",
    )


# ============================================================
#  BREAST CANCER MODEL LOADING
# ============================================================
BREAST_MODEL_PATH = "breast_cancer_model.joblib"

BREAST_MODEL = None          # pipeline: StandardScaler + RandomForest
BREAST_FEATURES: List[str] = []
BREAST_THRESHOLD: float = 0.5
BREAST_FEATURE_STATS: Dict[str, Dict[str, float]] = {}
BREAST_CLASS_STATS: Dict[str, Any] = {}

if not os.path.exists(BREAST_MODEL_PATH):
    print(f"[WARN] Breast model not found at {os.path.abspath(BREAST_MODEL_PATH)}")
else:
    bundle = joblib.load(BREAST_MODEL_PATH)
    BREAST_MODEL = bundle["model"]
    BREAST_FEATURES = bundle["features"]
    BREAST_THRESHOLD = float(bundle.get("threshold", 0.5))
    BREAST_FEATURE_STATS = bundle.get("feature_stats", {})
    BREAST_CLASS_STATS = bundle.get("class_stats", {})

    print(f"[INFO] Breast cancer model loaded from {os.path.abspath(BREAST_MODEL_PATH)}")
    print(f"[INFO] Features: {BREAST_FEATURES}")
    print(f"[INFO] Threshold: {BREAST_THRESHOLD}")


def _clip_and_warn(values: List[float]) -> (List[float], List[str]):
    """
    Clipping la [min, max] din dataset si generare de warnings
    pentru valori in afara intervalului.
    """
    if not BREAST_FEATURE_STATS:
        return values, []

    min_vals = BREAST_FEATURE_STATS.get("min", {})
    max_vals = BREAST_FEATURE_STATS.get("max", {})

    clipped = []
    warnings = []

    for name, val in zip(BREAST_FEATURES, values):
        min_v = float(min_vals.get(name, val))
        max_v = float(max_vals.get(name, val))

        original_val = val

        if val < min_v:
            val = min_v
            warnings.append(
                f"{name}={original_val:.3f} is below training range; "
                f"clipped to {min_v:.3f}."
            )
        elif val > max_v:
            val = max_v
            warnings.append(
                f"{name}={original_val:.3f} is above training range; "
                f"clipped to {max_v:.3f}."
            )

        clipped.append(val)

    return clipped, warnings


# ============================================================
#  BREAST CANCER ENDPOINT
# ============================================================
@app.post("/api/breast/analyze", response_model=PredictionResponse)
async def analyze_breast_cancer(payload: dict = Body(...)):
    """
    JSON expected:
    Radius_mean, Texture_mean, perimeter_mean, area_mean, smoothness_mean,
    compactness_mean, concavity_mean, concavepoints_mean / concave points_mean,
    symmetry_mean, fractal_dimension_mean
    """

    if BREAST_MODEL is None:
        return PredictionResponse(
            Label="Error",
            Probability=0.0,
            Explanation="Breast cancer model not loaded on server.",
            RawModelName="none",
        )

    # -------------------------
    # 1) Extragem valorile
    # -------------------------
    try:
        radius = float(payload.get("Radius_mean") or payload.get("radius_mean") or 0.0)
        texture = float(payload.get("Texture_mean") or payload.get("texture_mean") or 0.0)
        perimeter = float(payload.get("perimeter_mean") or 0.0)
        area = float(payload.get("area_mean") or 0.0)
        smoothness = float(payload.get("smoothness_mean") or 0.0)
        compactness = float(payload.get("compactness_mean") or 0.0)
        concavity = float(payload.get("concavity_mean") or 0.0)
        concave_points = float(
            payload.get("concavepoints_mean")
            or payload.get("concave points_mean")
            or 0.0
        )
        symmetry = float(payload.get("symmetry_mean") or 0.0)
        fractal = float(payload.get("fractal_dimension_mean") or 0.0)

        raw_values = [
            radius,
            texture,
            perimeter,
            area,
            smoothness,
            compactness,
            concavity,
            concave_points,
            symmetry,
            fractal,
        ]
    except Exception as e:
        return PredictionResponse(
            Label="Error",
            Probability=0.0,
            Explanation=f"Invalid input format: {e}",
            RawModelName="none",
        )

    # -------------------------
    # 2) Clipping + warnings
    # -------------------------
    clipped_values, warnings = _clip_and_warn(raw_values)

    # -------------------------
    # 3) Pregătim inputul pentru pipeline
    #    (dăm DataFrame cu feature names, ca la training)
    # -------------------------
    x = pd.DataFrame([dict(zip(BREAST_FEATURES, clipped_values))])

    # -------------------------
    # 4) Predict probability of Malignant
    # -------------------------
    prob_malignant = float(BREAST_MODEL.predict_proba(x)[0, 1])
    is_malignant = prob_malignant >= BREAST_THRESHOLD

    label = "Malignant" if is_malignant else "Benign"

    # -------------------------
    # 5) Explicații
    # -------------------------
    explanation_parts = [
        f"Estimated malignant probability = {prob_malignant:.2%}.",
        f"Decision threshold = {BREAST_THRESHOLD:.2f} (tuned to be sensitive to malignant cases).",
    ]

    if prob_malignant < 0.2:
        explanation_parts.append("Model evaluation: strongly in the benign zone.")
    elif prob_malignant > 0.8:
        explanation_parts.append("Model evaluation: strongly in the malignant zone.")
    elif abs(prob_malignant - BREAST_THRESHOLD) <= 0.1:
        explanation_parts.append(
            "Model evaluation: borderline case, values are close to the decision boundary."
        )
    else:
        explanation_parts.append("Model evaluation: intermediate risk region.")

    if warnings:
        explanation_parts.append(
            "Some input values were outside the training range and were clipped; "
            "prediction may be less reliable for those dimensions."
        )
        explanation_parts.extend(warnings)

    explanation = " ".join(explanation_parts)

    # -------------------------
    # 6) Raw model name (RandomForest din pipeline)
    # -------------------------
    raw_model_name = type(BREAST_MODEL).__name__
    try:
        clf = BREAST_MODEL.named_steps.get("clf")
        if clf is not None:
            raw_model_name = type(clf).__name__ + "_rf_v1"
    except Exception:
        pass

    return PredictionResponse(
        Label=label,
        Probability=prob_malignant,
        Explanation=explanation,
        RawModelName=raw_model_name,
    )
