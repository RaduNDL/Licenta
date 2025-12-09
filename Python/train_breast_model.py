import os
import joblib
import numpy as np
import pandas as pd

from sklearn.model_selection import train_test_split
from sklearn.preprocessing import StandardScaler
from sklearn.metrics import accuracy_score, classification_report, roc_auc_score

from sklearn.linear_model import LogisticRegression
from sklearn.ensemble import RandomForestClassifier, GradientBoostingClassifier

BASE_DIR = os.path.dirname(__file__)

DATA_PATH = r"D:\Facultate\Licenta\Breast Cancer Prediction\Breast Cancer Prediction.csv"

MODELS_DIR = os.path.join(BASE_DIR, "models")
os.makedirs(MODELS_DIR, exist_ok=True)

MODEL_PATH = os.path.join(MODELS_DIR, "breast_cancer_model.pkl")
SCALER_PATH = os.path.join(MODELS_DIR, "breast_cancer_scaler.pkl")

df = pd.read_csv(DATA_PATH)

if "id" in df.columns:
    df = df.drop(columns=["id"])

for col in list(df.columns):
    if col.startswith("Unnamed"):
        df = df.drop(columns=[col])

df.columns = [c.strip().lower().replace(" ", "_") for c in df.columns]

if "diagnosis" not in df.columns:
    raise RuntimeError("Nu există coloana 'diagnosis' în dataset!")

df["diagnosis"] = df["diagnosis"].map({"B": 0, "M": 1})

feature_cols = [
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

for col in feature_cols:
    if col not in df.columns:
        raise RuntimeError(f"Lipsește coloana necesară pentru model: {col}")

X = df[feature_cols].values
y = df["diagnosis"].values

X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.2, random_state=42, stratify=y
)

scaler = StandardScaler()
X_train_scaled = scaler.fit_transform(X_train)
X_test_scaled = scaler.transform(X_test)

models = {}

models["logreg"] = LogisticRegression(
    max_iter=5000,
    class_weight="balanced",
    n_jobs=-1,
)

models["rf"] = RandomForestClassifier(
    n_estimators=400,
    max_depth=None,
    min_samples_split=2,
    min_samples_leaf=1,
    class_weight="balanced",
    random_state=42,
    n_jobs=-1,
)

models["gb"] = GradientBoostingClassifier(
    learning_rate=0.05,
    n_estimators=300,
    max_depth=3,
    random_state=42,
)

best_model_name = None
best_model = None
best_acc = -1.0
best_auc = -1.0

for name, clf in models.items():
    clf.fit(X_train_scaled, y_train)

    y_pred = clf.predict(X_test_scaled)
    y_proba = clf.predict_proba(X_test_scaled)[:, 1]

    acc = accuracy_score(y_test, y_pred)
    auc = roc_auc_score(y_test, y_proba)

    if auc > best_auc or (np.isclose(auc, best_auc) and acc > best_acc):
        best_auc = auc
        best_acc = acc
        best_model_name = name
        best_model = clf

if best_model is None:
    raise RuntimeError("Nu am reușit să selectez un model. Something went wrong.")

first_row_features = X[0].reshape(1, -1)
first_row_scaled = scaler.transform(first_row_features)

true_label = y[0]
pred_label = best_model.predict(first_row_scaled)[0]
pred_proba = best_model.predict_proba(first_row_scaled)[0, 1]

prob_benign = 1 - pred_proba
prob_malignant = pred_proba

joblib.dump(best_model, MODEL_PATH)
joblib.dump(scaler, SCALER_PATH)