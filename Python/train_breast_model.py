import os
import joblib
import numpy as np
import pandas as pd

from sklearn.model_selection import train_test_split
from sklearn.preprocessing import StandardScaler
from sklearn.metrics import accuracy_score, classification_report, roc_auc_score

from sklearn.linear_model import LogisticRegression
from sklearn.ensemble import RandomForestClassifier, GradientBoostingClassifier

# =====================================================
# 1. PATH-URI
# =====================================================

BASE_DIR = os.path.dirname(__file__)

DATA_PATH = r"D:\Facultate\Licenta\Breast Cancer Prediction\Breast Cancer Prediction.csv"

MODELS_DIR = os.path.join(BASE_DIR, "models")
os.makedirs(MODELS_DIR, exist_ok=True)

MODEL_PATH = os.path.join(MODELS_DIR, "breast_cancer_model.pkl")
SCALER_PATH = os.path.join(MODELS_DIR, "breast_cancer_scaler.pkl")

# =====================================================
# 2. ÎNCĂRCARE + CURĂȚARE DATE
# =====================================================

print(f"Loading dataset from: {DATA_PATH}")
df = pd.read_csv(DATA_PATH)

print("Columns in CSV before renaming:")
print(df.columns.tolist())

# scoatem id dacă există
if "id" in df.columns:
    df = df.drop(columns=["id"])

# scoatem coloane tip Unnamed: 32 etc.
for col in list(df.columns):
    if col.startswith("Unnamed"):
        df = df.drop(columns=[col])

# normalizăm numele: spații -> _, lowercase
df.columns = [c.strip().lower().replace(" ", "_") for c in df.columns]

print("\nColumns after cleaning/renaming:")
print(df.columns.tolist())

if "diagnosis" not in df.columns:
    raise RuntimeError("Nu există coloana 'diagnosis' în dataset!")

# B/M -> 0/1
df["diagnosis"] = df["diagnosis"].map({"B": 0, "M": 1})

# info de bază
print("\nDATASET INFO:")
print(f"  Total samples: {len(df)}")
print(f"  Benign  (0): {int((df['diagnosis'] == 0).sum())}")
print(f"  Malignant (1): {int((df['diagnosis'] == 1).sum())}")

# =====================================================
# 3. FEATURES & TARGET – DOAR MEAN FEATURES (10)
# =====================================================

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

print(f"\nUsing {len(feature_cols)} features: {feature_cols}")
print(f"X shape: {X.shape} (samples, features)")
print(f"y shape: {y.shape}")

# =====================================================
# 4. TRAIN / TEST SPLIT
# =====================================================

X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.2, random_state=42, stratify=y
)

print("\nSPLIT INFO:")
print(f"  Train samples: {X_train.shape[0]}")
print(f"  Test samples:  {X_test.shape[0]}")

# =====================================================
# 5. SCALING
# =====================================================

scaler = StandardScaler()
X_train_scaled = scaler.fit_transform(X_train)
X_test_scaled = scaler.transform(X_test)

# =====================================================
# 6. MODELE CANDIDATE
# =====================================================

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

# =====================================================
# 7. TRAIN + EVALUARE FIECARE MODEL (PE TOT TRAIN SET)
# =====================================================

best_model_name = None
best_model = None
best_acc = -1.0
best_auc = -1.0

for name, clf in models.items():
    print(f"\n========== Training model: {name} ==========")
    clf.fit(X_train_scaled, y_train)   # 👈 AICI se antrenează pe TOT X_train

    y_pred = clf.predict(X_test_scaled)
    y_proba = clf.predict_proba(X_test_scaled)[:, 1]  # P(malignant)

    acc = accuracy_score(y_test, y_pred)
    auc = roc_auc_score(y_test, y_proba)

    print(f"Accuracy: {acc:.4f}")
    print(f"ROC AUC:  {auc:.4f}")
    print("Classification report:")
    print(classification_report(y_test, y_pred, target_names=["Benign (0)", "Malignant (1)"]))

    if auc > best_auc or (np.isclose(auc, best_auc) and acc > best_acc):
        best_auc = auc
        best_acc = acc
        best_model_name = name
        best_model = clf

print("\n=======================================")
print(f"Best model:   {best_model_name}")
print(f"Best ACC:     {best_acc:.4f}")
print(f"Best ROC AUC: {best_auc:.4f}")
print("=======================================\n")

if best_model is None:
    raise RuntimeError("Nu am reușit să selectez un model. Something went wrong.")

# =====================================================
# 8. DEBUG: PRIMUL RÂND DIN DATASET (DOAR PENTRU VERIFICARE)
# =====================================================

first_row_features = X[0].reshape(1, -1)
first_row_scaled = scaler.transform(first_row_features)

true_label = y[0]             # 0 = B, 1 = M
pred_label = best_model.predict(first_row_scaled)[0]
pred_proba = best_model.predict_proba(first_row_scaled)[0, 1]  # P(malignant)

print("================ FIRST ROW DEBUG ================\n")

print("FEATURE VALUES (FIRST ROW):")
for col, val in zip(feature_cols, first_row_features.flatten()):
    print(f"  {col:25s} = {val}")

print("\nGROUND TRUTH:")
print(f"  True diagnosis: {true_label}  ({'Benign' if true_label == 0 else 'Malignant'})")

print("\nMODEL PREDICTION:")
print(f"  Predicted label: {pred_label}  ({'Benign' if pred_label == 0 else 'Malignant'})")

prob_benign = 1 - pred_proba
prob_malignant = pred_proba

print("\nPROBABILITIES:")
print(f"  P(Benign)    = {prob_benign * 100:.2f}%")
print(f"  P(Malignant) = {prob_malignant * 100:.2f}%")

print("\nINTERPRETATION:")
if pred_label == 0:
    print(f"  Model says BENIGN with confidence {prob_benign * 100:.2f}%")
else:
    print(f"  Model says MALIGNANT with confidence {prob_malignant * 100:.2f}%")

print("\n=================================================\n")

# =====================================================
# 9. SAVE MODEL + SCALER (MODELUL COMPLET)
# =====================================================

joblib.dump(best_model, MODEL_PATH)
joblib.dump(scaler, SCALER_PATH)

print(f"Best model ({best_model_name}) saved to: {MODEL_PATH}")
print(f"Scaler saved to: {SCALER_PATH}")
