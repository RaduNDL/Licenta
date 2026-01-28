import torch
import torch.nn as nn
import torchvision.transforms as T
from torchvision.datasets import ImageFolder
from torchvision.models import mobilenet_v3_small, MobileNet_V3_Small_Weights
from torch.utils.data import DataLoader, random_split
from pathlib import Path

DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

DATASET_PATH = Path(r"D:\Facultate\Licenta\Licenta\Python\datasets\ISIC 2019 Skin Lesion\Images")
MODEL_OUT = Path("isic2019_mobilenet.pt")

BATCH_SIZE = 32
EPOCHS = 5
LR = 1e-3

def main():
    weights = MobileNet_V3_Small_Weights.DEFAULT
    transforms = weights.transforms()

    dataset = ImageFolder(DATASET_PATH, transform=transforms)

    train_len = int(0.8 * len(dataset))
    val_len = len(dataset) - train_len
    train_ds, val_ds = random_split(dataset, [train_len, val_len])

    train_loader = DataLoader(train_ds, batch_size=BATCH_SIZE, shuffle=True)
    val_loader = DataLoader(val_ds, batch_size=BATCH_SIZE)

    model = mobilenet_v3_small(weights=weights)
    model.classifier[3] = nn.Linear(model.classifier[3].in_features, len(dataset.classes))
    model.to(DEVICE)

    optimizer = torch.optim.Adam(model.parameters(), lr=LR)
    loss_fn = nn.CrossEntropyLoss()

    for epoch in range(EPOCHS):
        model.train()
        for x, y in train_loader:
            x, y = x.to(DEVICE), y.to(DEVICE)
            optimizer.zero_grad()
            out = model(x)
            loss = loss_fn(out, y)
            loss.backward()
            optimizer.step()

        model.eval()
        correct = 0
        total = 0
        with torch.no_grad():
            for x, y in val_loader:
                x, y = x.to(DEVICE), y.to(DEVICE)
                out = model(x)
                pred = out.argmax(1)
                correct += (pred == y).sum().item()
                total += y.size(0)

        acc = correct / total
        print(f"Epoch {epoch+1} - Val Accuracy: {acc:.4f}")

    torch.save({
        "model_state": model.state_dict(),
        "class_names": dataset.classes
    }, MODEL_OUT)

    print("Saved:", MODEL_OUT)

if __name__ == "__main__":
    main()
