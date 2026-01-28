import io
import os
import torch
import torch.nn as nn
import torchvision.transforms as T
from torchvision.datasets import ImageFolder
from torch.utils.data import DataLoader, Subset
from PIL import Image
from pathlib import Path
import numpy as np
from torchvision.models import mobilenet_v3_small, MobileNet_V3_Small_Weights, efficientnet_b0, EfficientNet_B0_Weights
from .model_factory import create_model



def get_project_root():
    return Path(__file__).parent.parent


DATA_DIR = get_project_root() / "datasets" / "ISIC 2019 Skin Lesion" / "Images"
if not DATA_DIR.exists():
    DATA_DIR = get_project_root() / "datasets" / "Images"

ARTIFACT_DIR = get_project_root() / "artifacts"
ARTIFACT_DIR.mkdir(parents=True, exist_ok=True)
MODEL_PATH = ARTIFACT_DIR / "ISIC2019.pt"

BATCH_SIZE = 32
IMAGE_SIZE = 224
WARMUP_EPOCHS = 3
FINETUNE_EPOCHS = 5
TOTAL_EPOCHS = WARMUP_EPOCHS + FINETUNE_EPOCHS
LR = 0.001

# Praguri
GENERAL_OBJECT_THRESHOLD = 0.60
MEDICAL_CONFIDENCE_THRESHOLD = 0.50


DISEASE_INFO = {
    "AK": {
        "full_name": "Actinic Keratosis",
        "type": "Malignant (Pre-cancerous)",
        "risk": "Medium",
        "desc": "A rough, scaly patch on the skin caused by years of sun exposure."
    },
    "BCC": {
        "full_name": "Basal Cell Carcinoma",
        "type": "Malignant",
        "risk": "High",
        "desc": "A type of skin cancer that begins in the basal cells."
    },
    "BKL": {
        "full_name": "Benign Keratosis",
        "type": "Benign",
        "risk": "Low",
        "desc": "A non-cancerous skin condition that appears as a waxy brown, black, or tan growth."
    },
    "DF": {
        "full_name": "Dermatofibroma",
        "type": "Benign",
        "risk": "Low",
        "desc": "A common overgrowth of fibrous tissue situated in the dermis."
    },
    "Non-Medical": {
        "full_name": "Non-Medical / Unknown",
        "type": "Non-Medical Object",
        "risk": "None",
        "desc": "The image does not appear to be a biological skin lesion."
    }
}

_medical_model = None
_medical_labels = None
_general_model = None
_general_transforms = None
_general_categories = None


def artifact_path():
    return MODEL_PATH



def calculate_weights(dataset, device):

    targets = np.array(dataset.targets)
    classes, counts = np.unique(targets, return_counts=True)
    print(f"Distributie Clase Detectate: {dict(zip(dataset.classes, counts))}")

    weights = 1.0 / counts
    weights = weights / weights.sum() * len(classes)
    return torch.FloatTensor(weights).to(device)


def load_general_model():
    weights = MobileNet_V3_Small_Weights.DEFAULT
    model = mobilenet_v3_small(weights=weights).eval()
    return model, weights.transforms(), weights.meta["categories"]


def get_models():
    global _medical_model, _medical_labels, _general_model, _general_transforms, _general_categories

    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

    if _medical_model is None:
        if not MODEL_PATH.exists():
            raise FileNotFoundError("Model medical neantrenat!")
        ckpt = torch.load(MODEL_PATH, map_location=device)
        _medical_model = create_model(len(ckpt["classes"]), freeze_backbone=False)
        _medical_model.load_state_dict(ckpt["state_dict"])
        _medical_model.to(device).eval()
        _medical_labels = ckpt["classes"]

    if _general_model is None:
        _general_model, _general_transforms, _general_categories = load_general_model()
        _general_model.to(device)

    return (_medical_model, _medical_labels), (_general_model, _general_transforms, _general_categories)


def train_and_save():
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    print(f"Starting Training on: {device}")

    if MODEL_PATH.exists():
        try:
            os.remove(MODEL_PATH)
        except:
            pass


    transform = T.Compose([
        T.Resize((IMAGE_SIZE, IMAGE_SIZE)),
        T.RandomHorizontalFlip(),
        T.RandomVerticalFlip(),
        T.RandomAffine(degrees=15, translate=(0.1, 0.1), scale=(0.9, 1.1)),
        T.ColorJitter(brightness=0.1, contrast=0.1),
        T.ToTensor(),
        T.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225])
    ])

    if not DATA_DIR.exists():
        print(f"ERROR: Dataset not found at {DATA_DIR}")
        return

    full_dataset = ImageFolder(str(DATA_DIR), transform=transform)
    classes = full_dataset.classes
    print(f"Medical Classes Found: {classes}")

    if len(classes) != 4:
        print(f"WARNING: Expected 4 classes (AK, BCC, BKL, DF), found {len(classes)}.")

    class_weights = calculate_weights(full_dataset, device)


    train_size = min(len(full_dataset), 8000)
    indices = list(range(len(full_dataset)))
    np.random.shuffle(indices)
    subset = Subset(full_dataset, indices[:train_size])

    loader = DataLoader(subset, batch_size=BATCH_SIZE, shuffle=True, num_workers=0, pin_memory=True)

    # --- FAZA 1: WARMUP (Invata doar clasificatorul) ---
    print(f"\n[PHASE 1] Warmup Classifier ({WARMUP_EPOCHS} epochs)...")
    model = create_model(len(classes), freeze_backbone=True).to(device)
    criterion = nn.CrossEntropyLoss(weight=class_weights, label_smoothing=0.1)
    optimizer = torch.optim.Adam(model.classifier.parameters(), lr=1e-3)

    model.train()
    for epoch in range(WARMUP_EPOCHS):
        run_epoch(model, loader, criterion, optimizer, device, epoch, WARMUP_EPOCHS, "Warmup")

    print(f"\n[PHASE 2] Fine-Tuning Full Network ({FINETUNE_EPOCHS} epochs)...")
    for param in model.parameters():
        param.requires_grad = True

    optimizer = torch.optim.AdamW(model.parameters(), lr=5e-5, weight_decay=1e-4)

    for epoch in range(FINETUNE_EPOCHS):
        run_epoch(model, loader, criterion, optimizer, device, epoch, FINETUNE_EPOCHS, "FineTune")

    torch.save({"state_dict": model.state_dict(), "classes": classes}, MODEL_PATH)
    print("\nModel saved successfully. Ready for presentation.")

    global _medical_model
    _medical_model = None


def run_epoch(model, loader, criterion, optimizer, device, epoch, total_epochs, phase):
    running_loss = 0.0
    correct = 0
    total = 0

    for i, (images, labels) in enumerate(loader):
        images, labels = images.to(device), labels.to(device)
        optimizer.zero_grad()
        outputs = model(images)
        loss = criterion(outputs, labels)
        loss.backward()
        optimizer.step()

        running_loss += loss.item()
        _, predicted = torch.max(outputs.data, 1)
        total += labels.size(0)
        correct += (predicted == labels).sum().item()

        if (i + 1) % 50 == 0:
            print(
                f"  [{phase} Ep {epoch + 1}/{total_epochs}] Batch {i + 1} | Loss: {loss.item():.4f} | Acc: {100 * correct / total:.1f}%")


def predict_bytes(image_bytes: bytes, image_size: int):
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

    try:
        img = Image.open(io.BytesIO(image_bytes)).convert("RGB")
    except:
        return "Error", {}, {"type": "Error", "risk": "None", "medical_note": "Invalid Image"}

    (med_model, med_labels), (gen_model, gen_transforms, gen_cats) = get_models()

    with torch.no_grad():
        gen_out = gen_model(gen_transforms(img).unsqueeze(0).to(device))
        gen_probs = torch.softmax(gen_out, dim=1)[0]

    gen_idx = int(torch.argmax(gen_probs))
    gen_conf = float(gen_probs[gen_idx])
    gen_label = gen_cats[gen_idx]

    safe_list = ["band aid", "nematode", "flatworm", "spotlight", "velvet", "tick", "slug", "sea cucumber",
                 "Petri dish"]

    print(f"[DEBUG] General AI sees: {gen_label} ({gen_conf:.2f})")

    if gen_conf > GENERAL_OBJECT_THRESHOLD and gen_label not in safe_list:
        return "Non-Medical", {}, {
            "medical_type": "Detected Object",
            "risk_level": "None",
            "medical_note": f"The AI detected a '{gen_label}' ({gen_conf * 100:.1f}%). Not a skin lesion."
        }

    weights = EfficientNet_B0_Weights.DEFAULT
    med_transforms = weights.transforms()

    with torch.no_grad():
        med_out = med_model(med_transforms(img).unsqueeze(0).to(device))
        med_probs = torch.softmax(med_out, dim=1)[0].cpu().numpy()

    prob_dict = {med_labels[i]: float(med_probs[i]) for i in range(len(med_labels))}
    best_idx = int(np.argmax(med_probs))
    final_label_code = med_labels[best_idx]
    best_prob = float(med_probs[best_idx])

    if best_prob < MEDICAL_CONFIDENCE_THRESHOLD:
        return "Inconclusive", prob_dict, {
            "medical_type": "Unknown",
            "risk_level": "Low",
            "medical_note": "Confidence too low for a definitive diagnosis."
        }

    info = DISEASE_INFO.get(final_label_code, DISEASE_INFO["Non-Medical"])
    result_display_name = info.get("full_name", final_label_code)

    return result_display_name, prob_dict, {
        "medical_type": info["type"],
        "risk_level": info["risk"],
        "medical_note": info["desc"]
    }