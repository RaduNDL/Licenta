# train_breast_model.py

import os
import joblib
import pandas as pd

from sklearn.ensemble import RandomForestClassifier
from sklearn.model_selection import train_test_split
from sklearn.metrics import (
    classification_report,
    accuracy_score,
    roc_auc_score,
    roc_curve,
)
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler

# === 1. Config ===
CSV_PATH = r"D:\Facultate\Licenta\Breast Cancer Prediction\Breast Cancer Prediction.csv"
MODEL_PATH = "breast_cancer_model.joblib"

FEATURES = [
    "Radius_mean",
    "Texture_mean",
    "perimeter_mean",
    "area_mean",
    "smoothness_mean",
    "compactness_mean",
    "concavity_mean",
    "concave points_mean",
    "symmetry_mean",
    "fractal_dimension_mean",
]

# === 2. Load data ===
df = pd.read_csv(CSV_PATH)

missing = [f for f in FEATURES if f not in df.columns]
if missing:
    raise ValueError(f"Missing features in CSV: {missing}")

X = df[FEATURES].copy()
y = (df["diagnosis"] == "M").astype(int)

# handle NaN dacă există
X = X.fillna(X.median(numeric_only=True))

# === 3. Train/test split ===
X_train, X_test, y_train, y_test = train_test_split(
    X,
    y,
    test_size=0.2,
    random_state=42,
    stratify=y,
)

# === 4. Pipeline: Scaling + RandomForest ===
clf = RandomForestClassifier(
    n_estimators=400,
    max_depth=None,
    min_samples_split=4,
    min_samples_leaf=2,
    random_state=42,
    class_weight="balanced_subsample",
    n_jobs=-1,
)

pipe = Pipeline(
    steps=[
        ("scaler", StandardScaler()),
        ("clf", clf),
    ]
)

pipe.fit(X_train, y_train)

# === 5. Evaluation ===
y_pred = pipe.predict(X_test)
y_prob = pipe.predict_proba(X_test)[:, 1]

acc = accuracy_score(y_test, y_pred)
roc_auc = roc_auc_score(y_test, y_prob)

print("Accuracy:", acc)
print("ROC AUC:", roc_auc)
print("Classification report:")
print(classification_report(y_test, y_pred, digits=3))

# === 6. Alegem threshold orientat pe Malignant ===
fpr, tpr, thresholds = roc_curve(y_test, y_prob)
youden_j = tpr - fpr
best_idx = youden_j.argmax()
best_threshold = float(thresholds[best_idx])

# dacă vrei să fii mai agresiv spre Malignant:
best_threshold = min(best_threshold, 0.45)

print(f"Chosen decision threshold (favoring malignant): {best_threshold:.4f}")

# === 7. Stats pe features (în spațiul RAW, nu scalat) ===
feature_stats = {
    "min": X.min().to_dict(),
    "max": X.max().to_dict(),
    "mean": X.mean().to_dict(),
    "std": X.std().to_dict(),
}

class_stats = {}
for cls in [0, 1]:
    X_cls = X[y == cls]
    class_stats[int(cls)] = {
        "mean": X_cls.mean().to_dict(),
        "std": X_cls.std().to_dict(),
    }

bundle = {
    "model": pipe,           # pipeline: scaler + RF
    "features": FEATURES,
    "threshold": best_threshold,
    "feature_stats": feature_stats,
    "class_stats": class_stats,
}

joblib.dump(bundle, MODEL_PATH)

print("Model saved to:", os.path.abspath(MODEL_PATH))
