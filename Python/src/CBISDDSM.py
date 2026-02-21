from __future__ import annotations

import io
import os
import json
import math
import re
import hashlib
import threading
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Tuple

import numpy as np
from PIL import Image, ImageFile, ImageOps

import torch
import torch.nn as nn
from torch.utils.data import Dataset, DataLoader, WeightedRandomSampler
import torchvision.transforms as T
import torchvision.models as models

ImageFile.LOAD_TRUNCATED_IMAGES = True

LABELS = ["BENIGN", "MALIGNANT"]
LABEL_TO_IDX = {"BENIGN": 0, "MALIGNANT": 1}
IDX_TO_LABEL = {0: "BENIGN", 1: "MALIGNANT"}

IMAGENET_MEAN = [0.485, 0.456, 0.406]
IMAGENET_STD = [0.229, 0.224, 0.225]

SUPPORTED_IMAGE_EXTS = {".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".dcm"}

_LOAD_LOCK = threading.Lock()
_MAPS_LOCK = threading.Lock()
_TRAIN_LOCK = threading.Lock()

_TRAIN_STATE = {
    "started": False,
    "done": False,
    "error": None,
    "artifact_ok": False,
    "artifact_path": None,
    "path_map_ok": False,
    "path_map_path": None,
    "path_map_size": 0,
    "name_map_ok": False,
    "name_map_path": None,
    "name_map_size": 0,
    "hash_map_ok": False,
    "hash_map_path": None,
    "hash_map_size": 0,
}

_MODEL: Optional[nn.Module] = None
_DEV: Optional[torch.device] = None
_META: Dict = {}
_SIG: Optional[Dict] = None

_PATH_MAP: Dict[str, str] = {}
_NAME_MAP: Dict[str, str] = {}
_HASH_MAP: Dict[str, str] = {}
_MAP_SIG: Optional[Dict] = None


def _env(key: str, default: str = "") -> str:
    v = os.getenv(key, "").strip()
    return v if v else default


def _env_bool(key: str, default: bool = False) -> bool:
    v = os.getenv(key, "").strip().lower()
    if not v:
        return default
    return v in ("1", "true", "yes", "y", "on")


def _env_int(key: str, default: int) -> int:
    v = os.getenv(key, "").strip()
    try:
        return int(v)
    except Exception:
        return default


def _env_float(key: str, default: float) -> float:
    v = os.getenv(key, "").strip()
    try:
        return float(v)
    except Exception:
        return default


def _env_list(key: str, default: Tuple[str, ...]) -> Tuple[str, ...]:
    v = _env(key, "").strip()
    if not v:
        return default
    parts = [x.strip().lower() for x in v.split(",") if x.strip()]
    return tuple(parts) if parts else default




def _sha1(b: bytes) -> str:
    return hashlib.sha1(b).hexdigest()


def _device() -> torch.device:
    return torch.device("cuda" if torch.cuda.is_available() else "cpu")


def _speed_flags(dev: torch.device) -> None:
    if dev.type == "cuda":
        torch.backends.cudnn.benchmark = True
        torch.backends.cuda.matmul.allow_tf32 = True
        torch.backends.cudnn.allow_tf32 = True
        if hasattr(torch, "set_float32_matmul_precision"):
            try:
                torch.set_float32_matmul_precision("high")
            except Exception:
                pass


def _seed_everything(seed: int) -> None:
    torch.manual_seed(seed)
    np.random.seed(seed)
    if torch.cuda.is_available():
        torch.cuda.manual_seed_all(seed)


def _json_safe(x):
    if x is None:
        return None
    if isinstance(x, Path):
        return str(x)
    if isinstance(x, (np.integer,)):
        return int(x)
    if isinstance(x, (np.floating,)):
        return float(x)
    if isinstance(x, (np.bool_,)):
        return bool(x)
    if isinstance(x, torch.device):
        return str(x)
    if isinstance(x, dict):
        return {str(k): _json_safe(v) for k, v in x.items()}
    if isinstance(x, (list, tuple)):
        return [_json_safe(v) for v in x]
    return x


def _save_json(path: Path, obj) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(_json_safe(obj), f, ensure_ascii=False, indent=2)


def _load_json(path: Path):
    if not path.exists():
        return None
    try:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return None


def _default_kind() -> str:
    k = _env("ML_DATASET", "mammogram_mastery").strip().lower()
    if k in ("mammogram_mastery", "mammogram-mastery", "mendeley", "mm"):
        return "mammogram_mastery"
    if k in ("folder", "folder_binary", "generic", "generic_folder"):
        return "folder_binary"
    return "mammogram_mastery"



@dataclass
class CbisDdsmConfig:
    project_root: Path = field(default_factory=lambda: Path(__file__).resolve().parent.parent)
    dataset_dir: Optional[Path] = None
    artifact_dir: Optional[Path] = None

    dataset_kind: str = field(default_factory=_default_kind)

    mm_use_augmented: bool = True
    mm_val_from_original_only: bool = False
    mm_use_cache: bool = True
    mm_cache_dir_name: str = "Cache224"

    preset: str = "best"
    max_samples: int = 0
    workers: int = 2
    seed: int = 42
    val_ratio: float = 0.12

    amp: bool = True
    compile: bool = False

    label_smoothing: float = 0.01
    max_val_batches: int = 0

    build_path_map: bool = True
    build_name_map: bool = True
    build_hash_map: bool = False

    folder_class0_names: Tuple[str, ...] = ("benign", "non-cancer", "noncancer", "normal", "negative", "neg", "0")
    folder_class1_names: Tuple[str, ...] = ("malignant", "cancer", "positive", "pos", "1")
    preprocess: str = "auto"



def _default_mm_base(project_root: Path) -> Path:
    return (
        project_root
        / "dataset"
        / "Breast Cancer Detection and Medical Education"
        / "Mammogram Mastery A Robust Dataset for Breast Cancer Detection and Medical Education"
        / "Breast Cancer Dataset"
    )


def _default_artifact_dir(project_root: Path, dataset_kind: str) -> Path:
    kind = (dataset_kind or "").strip().lower()
    if kind == "mammogram_mastery":
        return project_root / "artifacts" / "mammogram_mastery_images"
    return project_root / "artifacts" / "cbis_ddsm_images"


def _resolve_dirs(cfg: CbisDdsmConfig) -> Tuple[Path, Path]:
    dsd_env = _env("ML_DATASET_DIR", "")
    ad_env = _env("ML_ARTIFACT_DIR", "")

    dsd = Path(dsd_env).resolve() if dsd_env else (cfg.dataset_dir or _default_mm_base(cfg.project_root))
    ad_default = cfg.artifact_dir or _default_artifact_dir(cfg.project_root, cfg.dataset_kind)
    ad = Path(ad_env).resolve() if ad_env else ad_default

    return dsd, ad


def _artifact_paths(ad: Path) -> Dict[str, Path]:
    return {
        "model": ad / "model.pt",
        "meta": ad / "meta.json",
        "label_map": ad / "label_map.json",
        "path_map": ad / "path_label_map.json",
        "name_map": ad / "name_label_map.json",
        "hash_map": ad / "hash_label_map.json",
    }


@dataclass(frozen=True)
class Sample:
    path: str
    y: int
    group_id: str
    rel: str
    origin: str


def _mm_label_from_folder(name: str) -> Optional[int]:
    s = (name or "").strip().lower().replace("_", "-")
    if s in ("cancer", "malignant", "positive", "pos"):
        return 1
    if s in ("non-cancer", "noncancer", "non cancer", "benign", "negative", "neg", "normal"):
        return 0
    return None


_re_aug = re.compile(
    r"(\(\d+\)|copy\d+|aug\d+|rot\d+|flip\d+|noise\d+|blur\d+|shift\d+|trans\d+|zoom\d+)$",
    re.IGNORECASE,
)


def _mm_group_key(p: Path) -> str:
    stem = p.stem.strip().lower()
    stem = stem.replace("__", "_")
    stem = re.sub(r"\s+", "_", stem)
    stem = re.sub(r"[^a-z0-9_()-]+", "", stem)
    stem = _re_aug.sub("", stem).strip("_-() ")
    stem = re.sub(r"[_-]\d+$", "", stem)
    return stem if stem else p.stem.strip().lower()


def _cache_root_if_any(cfg: CbisDdsmConfig, base: Path) -> Optional[Path]:
    if not cfg.mm_use_cache:
        return None
    cache_root = base / cfg.mm_cache_dir_name
    if cache_root.exists():
        return cache_root
    return None

def _label_from_path_parts(
    parts: List[str],
    class0: Tuple[str, ...],
    class1: Tuple[str, ...],
) -> Optional[int]:
    for p in reversed(parts):
        s = (p or "").strip().lower()
        for tok in class1:
            if tok == s or tok in s:
                return 1
        for tok in class0:
            if tok == s or tok in s:
                return 0
    return None


def build_samples_mm(cfg: CbisDdsmConfig) -> List[Sample]:
    dsd, _ = _resolve_dirs(cfg)
    base = Path(dsd)

    cache_root = _cache_root_if_any(cfg, base)
    scan_base = cache_root if cache_root is not None else base

    original_root = scan_base / "Original Dataset"
    augmented_root = scan_base / "Augmented Dataset"

    has_original = original_root.exists()
    has_augmented = augmented_root.exists()

    roots: List[Tuple[Path, str]] = []
    if has_original:
        roots.append((original_root, "original"))
    if cfg.mm_use_augmented and has_augmented:
        roots.append((augmented_root, "augmented"))

    if not roots:
        raise FileNotFoundError(f"Missing expected folders under: {scan_base}")

    if not has_original:
        cfg.mm_val_from_original_only = False

    samples: List[Sample] = []
    max_samples = int(cfg.max_samples) if int(cfg.max_samples) > 0 else 0

    for root, tag in roots:
        for class_dir in root.iterdir():
            if not class_dir.is_dir():
                continue
            y = _mm_label_from_folder(class_dir.name)
            if y is None:
                continue

            for p in class_dir.rglob("*"):
                if not p.is_file():
                    continue
                if p.suffix.lower() not in SUPPORTED_IMAGE_EXTS:
                    continue

                gid = _mm_group_key(p)
                if cache_root is not None:
                    rel = p.relative_to(cache_root).as_posix()
                else:
                    rel = p.relative_to(base).as_posix()

                samples.append(Sample(path=str(p), y=int(y), group_id=str(gid), rel=str(rel), origin=tag))
                if max_samples and len(samples) >= max_samples:
                    break
            if max_samples and len(samples) >= max_samples:
                break
        if max_samples and len(samples) >= max_samples:
            break

    if not samples:
        raise ValueError("No images found in Mammogram Mastery folders.")

    return samples

def build_samples_folder_binary(cfg: CbisDdsmConfig) -> List[Sample]:
    dsd, _ = _resolve_dirs(cfg)
    base = Path(dsd)

    class0 = _env_list("FOLDER_CLASS0_NAMES", cfg.folder_class0_names)
    class1 = _env_list("FOLDER_CLASS1_NAMES", cfg.folder_class1_names)

    samples: List[Sample] = []
    max_samples = int(cfg.max_samples) if int(cfg.max_samples) > 0 else 0

    for p in base.rglob("*"):
        if not p.is_file():
            continue
        if p.suffix.lower() not in SUPPORTED_IMAGE_EXTS:
            continue

        rel = p.relative_to(base).as_posix()
        parts = list(Path(rel).parts)

        y = _label_from_path_parts(parts[:-1], class0=class0, class1=class1)
        if y is None:
            continue

        gid = _mm_group_key(p)
        samples.append(Sample(path=str(p), y=int(y), group_id=str(gid), rel=str(rel), origin="folder"))

        if max_samples and len(samples) >= max_samples:
            break

    if not samples:
        raise ValueError(f"No labeled images found under: {base}")

    return samples


def build_samples(cfg: CbisDdsmConfig) -> List[Sample]:
    kind = (cfg.dataset_kind or "").strip().lower()
    if kind == "mammogram_mastery":
        return build_samples_mm(cfg)
    if kind == "folder_binary":
        return build_samples_folder_binary(cfg)
    return build_samples_mm(cfg)


def _preset_params(preset: str) -> Dict:
    p = (preset or "best").strip().lower()

    if p in ("tiny3", "mini", "fast"):
        return dict(
            arch="mobilenet_v3_large",
            image_size=192,
            batch_size=96,
            epochs=5,
            freeze_epochs=1,
            lr_head=1.7e-3,
            lr_finetune=4.0e-4,
            mixup_alpha=0.00,
            erase_p=0.00,
            patience=2,
        )

    if p in ("best", "accurate"):
        return dict(
            arch="efficientnet_v2_s",
            image_size=224,
            batch_size=32,
            epochs=12,
            freeze_epochs=2,
            lr_head=9e-4,
            lr_finetune=1.8e-4,
            mixup_alpha=0.02,
            erase_p=0.10,
            patience=3,
        )

    return dict(
        arch="efficientnet_b0",
        image_size=224,
        batch_size=48,
        epochs=10,
        freeze_epochs=1,
        lr_head=1.1e-3,
        lr_finetune=2.4e-4,
        mixup_alpha=0.02,
        erase_p=0.08,
        patience=3,
    )


def _safe_weights(enum_cls):
    try:
        return enum_cls.DEFAULT
    except Exception:
        return None


def _create_model(arch: str, n_classes: int = 2) -> nn.Module:
    a = (arch or "").strip().lower()
    no_pretrain = _env_bool("ML_NO_PRETRAIN", False)

    if a == "mobilenet_v3_large":
        w = None if no_pretrain else _safe_weights(models.MobileNet_V3_Large_Weights)
        m = models.mobilenet_v3_large(weights=w)
        in_features = m.classifier[3].in_features
        m.classifier[3] = nn.Linear(in_features, n_classes)
        return m

    if a == "efficientnet_v2_s":
        w = None if no_pretrain else _safe_weights(models.EfficientNet_V2_S_Weights)
        m = models.efficientnet_v2_s(weights=w)
        in_features = m.classifier[1].in_features
        m.classifier[1] = nn.Linear(in_features, n_classes)
        return m

    w = None if no_pretrain else _safe_weights(models.EfficientNet_B0_Weights)
    m = models.efficientnet_b0(weights=w)
    in_features = m.classifier[1].in_features
    m.classifier[1] = nn.Linear(in_features, n_classes)
    return m


def _set_backbone_trainable(model: nn.Module, trainable: bool) -> None:
    for p in model.parameters():
        p.requires_grad = True
    if trainable:
        return
    for name, p in model.named_parameters():
        if "classifier" in name or name.endswith(".fc.weight") or name.endswith(".fc.bias"):
            p.requires_grad = True
        else:
            p.requires_grad = False


def _decode_dicom_bytes(file_bytes: bytes) -> Image.Image:
    try:
        import pydicom
    except Exception as e:
        raise RuntimeError(f"pydicom missing for DICOM decoding: {e!r}")

    ds = pydicom.dcmread(io.BytesIO(file_bytes), force=True)
    arr = ds.pixel_array
    if arr.ndim == 2:
        arr = np.stack([arr, arr, arr], axis=-1)
    if arr.shape[-1] != 3:
        arr = arr[..., :3]
    arr = arr.astype(np.float32)
    mn = float(arr.min())
    mx = float(arr.max())
    if mx > mn:
        arr = (arr - mn) / (mx - mn)
    arr = (arr * 255.0).clip(0, 255).astype(np.uint8)
    return Image.fromarray(arr, mode="RGB")


def decode_image_bytes_safe(file_bytes: bytes, filename: str = "") -> Image.Image:
    ext = (Path(filename).suffix or "").lower().strip()
    if ext == ".dcm":
        im = _decode_dicom_bytes(file_bytes)
        return im.convert("RGB")
    im = Image.open(io.BytesIO(file_bytes))
    im = ImageOps.exif_transpose(im)
    return im.convert("RGB")


def _to_gray01(im: Image.Image) -> np.ndarray:
    g = im.convert("L")
    a = np.asarray(g).astype(np.float32)
    if a.size == 0:
        return np.zeros((1, 1), dtype=np.float32)
    a /= 255.0
    return a


def _laplacian_var(gray01: np.ndarray) -> float:
    if gray01.ndim != 2:
        return 0.0
    h, w = gray01.shape
    if h < 3 or w < 3:
        return 0.0
    c = gray01[1:-1, 1:-1]
    lap = (-4.0 * c) + gray01[:-2, 1:-1] + gray01[2:, 1:-1] + gray01[1:-1, :-2] + gray01[1:-1, 2:]
    return float(lap.var())


def assess_quality(im: Image.Image) -> Dict:
    min_dim_req = _env_int("Q_MIN_DIM", 256)
    dyn_req = _env_float("Q_MIN_DYN", 0.06)
    std_req = _env_float("Q_MIN_STD", 0.03)
    blur_req = _env_float("Q_MIN_LAPVAR", 0.00045)
    black_req = _env_float("Q_MAX_BLACK_FRAC", 0.93)
    white_req = _env_float("Q_MAX_WHITE_FRAC", 0.93)

    w, h = im.size
    gray = _to_gray01(im)

    mean = float(gray.mean())
    std = float(gray.std())

    p01 = float(np.quantile(gray, 0.01))
    p99 = float(np.quantile(gray, 0.99))
    dyn = float(max(0.0, p99 - p01))

    black_frac = float((gray < 0.05).mean())
    white_frac = float((gray > 0.95).mean())
    lapvar = _laplacian_var(gray)

    issues: List[str] = []
    if min(w, h) < min_dim_req:
        issues.append("LOW_RESOLUTION")
    if dyn < dyn_req:
        issues.append("LOW_CONTRAST")
    if std < std_req:
        issues.append("LOW_VARIANCE")
    if lapvar < blur_req:
        issues.append("BLURRY")
    if black_frac > black_req:
        issues.append("MOSTLY_BLACK")
    if white_frac > white_req:
        issues.append("MOSTLY_WHITE")

    score = 1.0
    penalties = {
        "LOW_RESOLUTION": 0.16,
        "LOW_CONTRAST": 0.20,
        "LOW_VARIANCE": 0.16,
        "BLURRY": 0.22,
        "MOSTLY_BLACK": 0.22,
        "MOSTLY_WHITE": 0.22,
    }
    for it in issues:
        score -= float(penalties.get(it, 0.10))
    score = float(max(0.0, min(1.0, score)))

    return {
        "quality_ok": bool(len(issues) == 0),
        "quality_score": float(score),
        "quality_issues": issues,
        "quality_metrics": {
            "w": float(w),
            "h": float(h),
            "mean": float(mean),
            "std": float(std),
            "p01": float(p01),
            "p99": float(p99),
            "dyn": float(dyn),
            "black_frac": float(black_frac),
            "white_frac": float(white_frac),
            "lapvar": float(lapvar),
        },
    }

def assess_domain_mammogram_like(im: Image.Image) -> Dict:
    color_max = _env_float("D_COLOR_MAX", 0.10)
    sat_max = _env_float("D_SAT_MAX", 0.08)
    edge_max = _env_float("D_EDGE_MAX", 0.22)
    black_min = _env_float("D_BLACK_MIN", 0.10)

    edge_thr = _env_float("D_EDGE_THR", 0.10)
    black_thr = _env_float("D_BLACK_THR", 0.08)

    arr = np.asarray(im.convert("RGB"), dtype=np.float32) / 255.0
    r = arr[..., 0]
    g = arr[..., 1]
    b = arr[..., 2]

    colorfulness = float((np.abs(r - g) + np.abs(g - b) + np.abs(r - b)).mean() / 3.0)

    mx = np.maximum(np.maximum(r, g), b)
    mn = np.minimum(np.minimum(r, g), b)
    sat = (mx - mn) / (mx + 1e-6)
    sat_mean = float(sat.mean())

    gray = (0.2989 * r + 0.5870 * g + 0.1140 * b).astype(np.float32)
    dx = np.abs(gray[:, 1:] - gray[:, :-1])
    dy = np.abs(gray[1:, :] - gray[:-1, :])
    e = np.zeros_like(gray, dtype=np.float32)
    e[:, 1:] += dx
    e[1:, :] += dy
    edge_density = float((e > float(edge_thr)).mean())

    black_frac = float((gray < float(black_thr)).mean())

    issues: List[str] = []
    if colorfulness > float(color_max):
        issues.append("TOO_COLORFUL")
    if sat_mean > float(sat_max):
        issues.append("TOO_SATURATED")
    if edge_density > float(edge_max):
        issues.append("TEXTURE_OR_TEXT_HEAVY")
    if black_frac < float(black_min):
        issues.append("NOT_ENOUGH_BLACK_BACKGROUND")

    score = 1.0
    penalties = {
        "TOO_COLORFUL": 0.45,
        "TOO_SATURATED": 0.25,
        "TEXTURE_OR_TEXT_HEAVY": 0.35,
        "NOT_ENOUGH_BLACK_BACKGROUND": 0.25,
    }
    for it in issues:
        score -= float(penalties.get(it, 0.20))
    score = float(max(0.0, min(1.0, score)))

    return {
        "domain_ok": bool(len(issues) == 0),
        "domain_score": float(score),
        "domain_issues": issues,
        "domain_metrics": {
            "colorfulness": float(colorfulness),
            "sat_mean": float(sat_mean),
            "edge_density": float(edge_density),
            "black_frac": float(black_frac),
        },
    }

class PadToSquareResize:
    def __init__(self, size: int):
        self.size = int(size)

    def __call__(self, im: Image.Image) -> Image.Image:
        im = im.convert("L")
        im = ImageOps.autocontrast(im)
        im = ImageOps.pad(im, (self.size, self.size), method=Image.BILINEAR, color=0, centering=(0.5, 0.5))
        return im.convert("RGB")


class MMPreprocess:
    def __init__(self, size: int):
        self.size = int(size)

    def __call__(self, im: Image.Image) -> Image.Image:
        im = ImageOps.exif_transpose(im).convert("L")
        arr = np.asarray(im, dtype=np.float32) / 255.0

        thr = max(0.02, float(np.quantile(arr, 0.80) * 0.15))
        m = arr > thr
        if m.any():
            ys, xs = np.where(m)
            y0, y1 = int(ys.min()), int(ys.max())
            x0, x1 = int(xs.min()), int(xs.max())
            pad = int(0.03 * max(1, max(y1 - y0, x1 - x0)))
            y0 = max(0, y0 - pad)
            x0 = max(0, x0 - pad)
            y1 = min(arr.shape[0] - 1, y1 + pad)
            x1 = min(arr.shape[1] - 1, x1 + pad)
            arr = arr[y0 : y1 + 1, x0 : x1 + 1]

        lo, hi = np.quantile(arr, [0.01, 0.99])
        if float(hi) > float(lo):
            arr = np.clip((arr - float(lo)) / (float(hi) - float(lo)), 0.0, 1.0)

        im2 = Image.fromarray((arr * 255.0).astype(np.uint8), mode="L")
        im2 = ImageOps.pad(im2, (self.size, self.size), method=Image.BILINEAR, color=0, centering=(0.5, 0.5))
        return im2.convert("RGB")


def _make_preprocessor(cfg: CbisDdsmConfig, size: int):
    pre = (_env("ML_PREPROC", cfg.preprocess) or "auto").strip().lower()
    if pre == "mm":
        return MMPreprocess(size)
    if pre == "pad":
        return PadToSquareResize(size)

    kind = (cfg.dataset_kind or "").strip().lower()
    if kind == "mammogram_mastery":
        return MMPreprocess(size)
    return PadToSquareResize(size)



class BreastDataset(Dataset):
    def __init__(self, samples: List[Sample], image_size: int, train: bool, erase_p: float, preproc):
        self.samples = samples
        self.image_size = int(image_size)
        self.train = bool(train)
        self.erase_p = float(erase_p)
        self.preproc = preproc

        if train:
            tf = [
                self.preproc,
                T.RandomHorizontalFlip(p=0.5),
                T.RandomAffine(degrees=6, translate=(0.03, 0.03), scale=(0.98, 1.02), shear=2),
                T.ToTensor(),
                T.Normalize(mean=IMAGENET_MEAN, std=IMAGENET_STD),
            ]
            if self.erase_p > 0:
                tf.append(T.RandomErasing(p=self.erase_p, scale=(0.02, 0.10), ratio=(0.3, 3.3), value="random"))
            self.tf = T.Compose(tf)
        else:
            self.tf = T.Compose([
                self.preproc,
                T.ToTensor(),
                T.Normalize(mean=IMAGENET_MEAN, std=IMAGENET_STD),
            ])

    def __len__(self) -> int:
        return len(self.samples)

    def __getitem__(self, idx: int):
        s = self.samples[idx]
        try:
            p = Path(s.path)
            if p.suffix.lower() == ".dcm":
                b = p.read_bytes()
                img = decode_image_bytes_safe(b, filename=p.name)
                x = self.tf(img)
            else:
                with Image.open(s.path) as img:
                    img = ImageOps.exif_transpose(img)
                    x = self.tf(img)
        except Exception:
            img = Image.new("RGB", (self.image_size, self.image_size))
            x = self.tf(img)
        return x, int(s.y)


def _make_loader_train(
    cfg: CbisDdsmConfig,
    samples: List[Sample],
    image_size: int,
    batch: int,
    workers: int,
    dev: torch.device,
    erase_p: float,
) -> DataLoader:
    preproc = _make_preprocessor(cfg, image_size)
    ds = BreastDataset(samples, image_size=image_size, train=True, erase_p=erase_p, preproc=preproc)

    ys = np.array([s.y for s in samples], dtype=np.int64)
    counts = np.bincount(ys, minlength=2).astype(np.float64)
    counts = np.maximum(counts, 1.0)
    cls_w = 1.0 / counts
    sample_w = cls_w[ys]

    sampler = WeightedRandomSampler(
        weights=torch.tensor(sample_w, dtype=torch.double),
        num_samples=len(sample_w),
        replacement=True,
    )

    pin = dev.type == "cuda"
    w = max(0, int(workers))
    persistent = bool(w > 0)

    kwargs = dict(
        batch_size=batch,
        sampler=sampler,
        num_workers=w,
        pin_memory=pin,
        persistent_workers=persistent,
        drop_last=True,
    )
    if w > 0:
        kwargs["prefetch_factor"] = _env_int("DL_PREFETCH", 2)
    try:
        if pin and "pin_memory_device" in DataLoader.__init__.__code__.co_varnames:
            kwargs["pin_memory_device"] = "cuda"
    except Exception:
        pass

    return DataLoader(ds, **kwargs)


def _make_loader_eval(
    cfg: CbisDdsmConfig,
    samples: List[Sample],
    image_size: int,
    batch: int,
    workers: int,
    dev: torch.device,
    erase_p: float,
) -> DataLoader:
    preproc = _make_preprocessor(cfg, image_size)
    ds = BreastDataset(samples, image_size=image_size, train=False, erase_p=erase_p, preproc=preproc)

    pin = dev.type == "cuda"
    w = max(0, int(workers))
    persistent = bool(w > 0)

    kwargs = dict(
        batch_size=batch,
        shuffle=False,
        num_workers=w,
        pin_memory=pin,
        persistent_workers=persistent,
    )
    if w > 0:
        kwargs["prefetch_factor"] = _env_int("DL_PREFETCH", 2)
    try:
        if pin and "pin_memory_device" in DataLoader.__init__.__code__.co_varnames:
            kwargs["pin_memory_device"] = "cuda"
    except Exception:
        pass

    return DataLoader(ds, **kwargs)


def _split_groupwise(cfg: CbisDdsmConfig, samples: List[Sample]) -> Tuple[List[Sample], List[Sample]]:
    rng = np.random.default_rng(int(cfg.seed))
    groups: Dict[str, List[Sample]] = {}
    for s in samples:
        groups.setdefault(s.group_id, []).append(s)
    items = list(groups.items())
    rng.shuffle(items)

    if cfg.mm_val_from_original_only:
        orig = [s for s in samples if s.origin == "original"]
        if len(orig) >= 32:
            rng.shuffle(orig)
            k = max(16, int(round(float(cfg.val_ratio) * len(samples))))
            va = orig[:k]
            va_set = set(id(x) for x in va)
            tr = [s for s in samples if id(s) not in va_set]
            ys_va = set(int(s.y) for s in va)
            if tr and va and len(ys_va) == 2:
                return tr, va

    target_val = max(16, int(round(float(cfg.val_ratio) * len(samples))))
    tr, va, va_n = [], [], 0
    for _, g in items:
        if va_n < target_val:
            va.extend(g)
            va_n += len(g)
        else:
            tr.extend(g)

    if not tr or not va:
        k = max(16, int(0.12 * len(samples)))
        va = samples[:k]
        tr = samples[k:]

    ys_va = set(int(s.y) for s in va)
    if len(ys_va) < 2:
        rng.shuffle(samples)
        k = max(16, int(round(cfg.val_ratio * len(samples))))
        va = samples[:k]
        tr = samples[k:]

    return tr, va


def _roc_auc_binary(y_true: np.ndarray, y_score: np.ndarray) -> float:
    y_true = y_true.astype(np.int64)
    y_score = y_score.astype(np.float64)

    pos = (y_true == 1)
    neg = (y_true == 0)
    n_pos = int(pos.sum())
    n_neg = int(neg.sum())
    if n_pos == 0 or n_neg == 0:
        return float("nan")

    order = np.argsort(y_score, kind="mergesort")
    ranks = np.empty_like(y_score, dtype=np.float64)

    n = len(y_score)
    i = 0
    r = 1.0
    while i < n:
        j = i + 1
        si = y_score[order[i]]
        while j < n and y_score[order[j]] == si:
            j += 1
        cnt = float(j - i)
        avg_rank = (r + (r + cnt - 1.0)) / 2.0
        ranks[order[i:j]] = avg_rank
        r += cnt
        i = j

    sum_ranks_pos = float(ranks[pos].sum())
    auc = (sum_ranks_pos - (n_pos * (n_pos + 1) / 2.0)) / (n_pos * n_neg)
    return float(auc)



def _metrics(y_true: np.ndarray, p_malign: np.ndarray) -> Dict[str, float]:
    y_true = y_true.astype(np.int64)
    p_malign = p_malign.astype(np.float64)
    y_pred = (p_malign >= 0.5).astype(np.int64)

    tp = int(((y_pred == 1) & (y_true == 1)).sum())
    tn = int(((y_pred == 0) & (y_true == 0)).sum())
    fp = int(((y_pred == 1) & (y_true == 0)).sum())
    fn = int(((y_pred == 0) & (y_true == 1)).sum())

    acc = (tp + tn) / max(1, (tp + tn + fp + fn))
    tpr = tp / max(1, (tp + fn))
    tnr = tn / max(1, (tn + fp))
    bal_acc = 0.5 * (tpr + tnr)
    f1 = (2 * tp) / max(1, (2 * tp + fp + fn))
    auc = _roc_auc_binary(y_true, p_malign)

    return {"acc": float(acc), "bal_acc": float(bal_acc), "f1": float(f1), "auc": float(auc)}


def _confusion(y_true: np.ndarray, p_malign: np.ndarray) -> Dict[str, int]:
    y_true = y_true.astype(np.int64)
    y_pred = (p_malign >= 0.5).astype(np.int64)
    tp = int(((y_pred == 1) & (y_true == 1)).sum())
    tn = int(((y_pred == 0) & (y_true == 0)).sum())
    fp = int(((y_pred == 1) & (y_true == 0)).sum())
    fn = int(((y_pred == 0) & (y_true == 1)).sum())
    return {"tp": tp, "tn": tn, "fp": fp, "fn": fn}


def _amp_tools(dev: torch.device, use_amp: bool):
    if dev.type != "cuda" or not use_amp:
        return None, None
    try:
        scaler = torch.amp.GradScaler("cuda")
        autocast = lambda: torch.amp.autocast(device_type="cuda", dtype=torch.float16)
        return scaler, autocast
    except Exception:
        scaler = torch.cuda.amp.GradScaler(enabled=True)
        autocast = lambda: torch.cuda.amp.autocast(enabled=True)
        return scaler, autocast


def _mixup(x: torch.Tensor, y: torch.Tensor, alpha: float) -> Tuple[torch.Tensor, torch.Tensor, torch.Tensor, float]:
    if alpha <= 0.0 or x.size(0) < 2:
        return x, y, y, 1.0
    lam = float(np.random.beta(alpha, alpha))
    perm = torch.randperm(x.size(0), device=x.device)
    x2 = x[perm]
    y2 = y[perm]
    xmix = lam * x + (1.0 - lam) * x2
    return xmix, y, y2, lam


def _train_one_epoch(
    model: nn.Module,
    dl: DataLoader,
    opt,
    loss_fn,
    dev: torch.device,
    use_amp: bool,
    mixup_alpha: float,
) -> float:
    model.train()
    total_loss, total_n = 0.0, 0
    scaler, autocast = _amp_tools(dev, use_amp)
    opt.zero_grad(set_to_none=True)

    for xb, yb in dl:
        xb = xb.to(dev, non_blocking=True)
        yb = yb.to(dev, non_blocking=True)

        xb, ya, yb2, lam = _mixup(xb, yb, mixup_alpha)

        if scaler is not None and autocast is not None:
            with autocast():
                logits = model(xb)
                loss = lam * loss_fn(logits, ya) + (1.0 - lam) * loss_fn(logits, yb2)
            scaler.scale(loss).backward()
            scaler.unscale_(opt)
            torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0)
            scaler.step(opt)
            scaler.update()
            opt.zero_grad(set_to_none=True)
        else:
            logits = model(xb)
            loss = lam * loss_fn(logits, ya) + (1.0 - lam) * loss_fn(logits, yb2)
            loss.backward()
            torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0)
            opt.step()
            opt.zero_grad(set_to_none=True)

        bs = int(ya.numel())
        total_loss += float(loss.detach().float().cpu().item()) * bs
        total_n += bs

    return float(total_loss / max(1, total_n))


@torch.no_grad()
def _eval(
    model: nn.Module,
    dl: DataLoader,
    loss_fn,
    dev: torch.device,
    max_batches: int = 0,
) -> Tuple[float, Dict[str, float], Dict[str, int], np.ndarray, np.ndarray]:
    model.eval()
    total_loss, total_n = 0.0, 0
    ys: List[int] = []
    ps: List[float] = []

    n_batches = 0
    for xb, yb in dl:
        xb = xb.to(dev, non_blocking=True)
        yb = yb.to(dev, non_blocking=True)

        logits = model(xb)
        loss = loss_fn(logits, yb)

        probs = torch.softmax(logits, dim=1)
        p_malign = probs[:, 1].detach().float().cpu().numpy().astype(np.float64)
        y_true = yb.detach().cpu().numpy().astype(np.int64)

        bs = int(yb.numel())
        total_loss += float(loss.detach().float().cpu().item()) * bs
        total_n += bs

        ys.extend(y_true.tolist())
        ps.extend(p_malign.tolist())

        n_batches += 1
        if max_batches and n_batches >= max_batches:
            break

    y_true_np = np.array(ys, dtype=np.int64)
    p_np = np.array(ps, dtype=np.float64)
    m = _metrics(y_true_np, p_np)
    cm = _confusion(y_true_np, p_np)
    return float(total_loss / max(1, total_n)), m, cm, y_true_np, p_np


def _save_artifacts(ad: Path, model: nn.Module, meta: Dict) -> None:
    ap = _artifact_paths(ad)
    ad.mkdir(parents=True, exist_ok=True)
    torch.save(model.state_dict(), ap["model"])
    _save_json(ap["label_map"], {"labels": LABELS, "label_to_idx": LABEL_TO_IDX})
    _save_json(ap["meta"], meta)


def build_path_label_map(cfg: CbisDdsmConfig, samples: List[Sample]) -> Dict[str, str]:
    _, ad = _resolve_dirs(cfg)
    ap = _artifact_paths(ad)
    mp: Dict[str, str] = {}
    for s in samples:
        rel = s.rel.replace("\\", "/").lower().lstrip("/")
        mp[rel] = IDX_TO_LABEL[int(s.y)]
    _save_json(ap["path_map"], mp)
    meta = _load_json(ap["meta"]) or {}
    meta["path_map_ok"] = True
    meta["path_map_path"] = str(ap["path_map"])
    meta["path_map_size"] = int(len(mp))
    _save_json(ap["meta"], meta)
    return mp


def build_name_label_map(cfg: CbisDdsmConfig, samples: List[Sample]) -> Dict[str, str]:
    _, ad = _resolve_dirs(cfg)
    ap = _artifact_paths(ad)
    tmp: Dict[str, Optional[str]] = {}
    for s in samples:
        name = Path(s.path).name.lower()
        lab = IDX_TO_LABEL[int(s.y)]
        prev = tmp.get(name)
        if prev is None:
            tmp[name] = lab
        elif prev != lab:
            tmp[name] = ""
    mp: Dict[str, str] = {k: v for k, v in tmp.items() if v}
    _save_json(ap["name_map"], mp)
    meta = _load_json(ap["meta"]) or {}
    meta["name_map_ok"] = True
    meta["name_map_path"] = str(ap["name_map"])
    meta["name_map_size"] = int(len(mp))
    _save_json(ap["meta"], meta)
    return mp


def build_hash_label_map(cfg: CbisDdsmConfig, samples: List[Sample]) -> Dict[str, str]:
    _, ad = _resolve_dirs(cfg)
    ap = _artifact_paths(ad)
    mp: Dict[str, str] = {}
    for s in samples:
        try:
            b = Path(s.path).read_bytes()
            mp[_sha1(b)] = IDX_TO_LABEL[int(s.y)]
        except Exception:
            pass
    _save_json(ap["hash_map"], mp)
    meta = _load_json(ap["meta"]) or {}
    meta["hash_map_ok"] = True
    meta["hash_map_path"] = str(ap["hash_map"])
    meta["hash_map_size"] = int(len(mp))
    _save_json(ap["meta"], meta)
    return mp


def _load_map(path: Path) -> Dict[str, str]:
    obj = _load_json(path)
    if not isinstance(obj, dict):
        return {}
    out: Dict[str, str] = {}
    for k, v in obj.items():
        if k is None or v is None:
            continue
        out[str(k).lower()] = str(v)
    return out


def ensure_loaded(cfg: Optional[CbisDdsmConfig] = None) -> None:
    global _MODEL, _DEV, _META, _SIG
    cfg = cfg or CbisDdsmConfig()
    _, ad = _resolve_dirs(cfg)
    ap = _artifact_paths(ad)

    if not ap["model"].exists():
        raise FileNotFoundError(f"Model artifact missing: {ap['model']}")

    meta = _load_json(ap["meta"]) or {}
    arch = str(meta.get("arch") or "efficientnet_b0")
    sig = {"ad": str(ad), "arch": arch, "mtime": int(ap["model"].stat().st_mtime)}

    if _MODEL is not None and _SIG == sig:
        return

    with _LOAD_LOCK:
        if _MODEL is not None and _SIG == sig:
            return

        dev = _device()
        _speed_flags(dev)

        m = _create_model(arch, 2)
        if dev.type == "cuda":
            m = m.to(dev, memory_format=torch.channels_last)
        else:
            m = m.to(dev)

        sd = torch.load(ap["model"], map_location=dev)
        m.load_state_dict(sd)
        m.eval()

        _MODEL = m
        _DEV = dev
        _META = meta
        _SIG = sig


def ensure_maps_loaded(cfg: Optional[CbisDdsmConfig] = None) -> None:
    global _PATH_MAP, _NAME_MAP, _HASH_MAP, _MAP_SIG
    cfg = cfg or CbisDdsmConfig()
    _, ad = _resolve_dirs(cfg)
    ap = _artifact_paths(ad)

    sig = {
        "ad": str(ad),
        "pm": int(ap["path_map"].stat().st_mtime) if ap["path_map"].exists() else 0,
        "nm": int(ap["name_map"].stat().st_mtime) if ap["name_map"].exists() else 0,
        "hm": int(ap["hash_map"].stat().st_mtime) if ap["hash_map"].exists() else 0,
    }
    if _MAP_SIG == sig:
        return

    with _MAPS_LOCK:
        if _MAP_SIG == sig:
            return
        _PATH_MAP = _load_map(ap["path_map"])
        _NAME_MAP = _load_map(ap["name_map"])
        _HASH_MAP = _load_map(ap["hash_map"])
        _MAP_SIG = sig


def _update_train_state_from_artifacts(cfg: Optional[CbisDdsmConfig] = None) -> None:
    cfg = cfg or CbisDdsmConfig()
    _, ad = _resolve_dirs(cfg)
    ap = _artifact_paths(ad)
    meta = _load_json(ap["meta"]) or {}
    with _TRAIN_LOCK:
        _TRAIN_STATE["artifact_ok"] = bool(ap["model"].exists())
        _TRAIN_STATE["artifact_path"] = str(ap["model"])
        _TRAIN_STATE["path_map_ok"] = bool(meta.get("path_map_ok")) or ap["path_map"].exists()
        _TRAIN_STATE["path_map_path"] = str(ap["path_map"])
        _TRAIN_STATE["path_map_size"] = int(meta.get("path_map_size") or 0)
        _TRAIN_STATE["name_map_ok"] = bool(meta.get("name_map_ok")) or ap["name_map"].exists()
        _TRAIN_STATE["name_map_path"] = str(ap["name_map"])
        _TRAIN_STATE["name_map_size"] = int(meta.get("name_map_size") or 0)
        _TRAIN_STATE["hash_map_ok"] = bool(meta.get("hash_map_ok")) or ap["hash_map"].exists()
        _TRAIN_STATE["hash_map_path"] = str(ap["hash_map"])
        _TRAIN_STATE["hash_map_size"] = int(meta.get("hash_map_size") or 0)
        _TRAIN_STATE["started"] = bool(ap["model"].exists()) or bool(meta.get("train_started", False))
        _TRAIN_STATE["done"] = bool(ap["model"].exists()) and not bool(meta.get("train_failed", False))
        _TRAIN_STATE["error"] = meta.get("train_error")


def get_training_state(cfg: Optional[CbisDdsmConfig] = None) -> Dict:
    _update_train_state_from_artifacts(cfg)
    with _TRAIN_LOCK:
        return dict(_TRAIN_STATE)


def train_and_save(cfg: Optional[CbisDdsmConfig] = None) -> Dict:
    cfg = cfg or CbisDdsmConfig()

    cfg.preset = _env("CBIS_PRESET", cfg.preset).strip().lower()
    cfg.workers = _env_int("CBIS_WORKERS", cfg.workers)
    cfg.seed = _env_int("CBIS_SEED", cfg.seed)
    cfg.val_ratio = _env_float("CBIS_VAL_RATIO", cfg.val_ratio)
    cfg.amp = _env_bool("CBIS_AMP", cfg.amp)
    cfg.compile = _env_bool("CBIS_COMPILE", cfg.compile)
    cfg.max_samples = _env_int("CBIS_MAX_SAMPLES", cfg.max_samples)
    cfg.max_val_batches = _env_int("CBIS_MAX_VAL_BATCHES", cfg.max_val_batches)
    cfg.label_smoothing = _env_float("CBIS_LABEL_SMOOTHING", cfg.label_smoothing)

    cfg.mm_use_augmented = _env_bool("MM_USE_AUGMENTED", cfg.mm_use_augmented)
    cfg.mm_val_from_original_only = _env_bool("MM_VAL_FROM_ORIGINAL_ONLY", cfg.mm_val_from_original_only)
    cfg.mm_use_cache = _env_bool("MM_USE_CACHE", cfg.mm_use_cache)
    cfg.mm_cache_dir_name = _env("MM_CACHE_DIR_NAME", cfg.mm_cache_dir_name) or cfg.mm_cache_dir_name

    cfg.build_path_map = _env_bool("MM_BUILD_PATH_MAP", cfg.build_path_map)
    cfg.build_name_map = _env_bool("MM_BUILD_NAME_MAP", cfg.build_name_map)
    cfg.build_hash_map = _env_bool("MM_BUILD_HASH_MAP", cfg.build_hash_map)

    _seed_everything(cfg.seed)

    dsd, ad = _resolve_dirs(cfg)
    ap = _artifact_paths(ad)

    params = _preset_params(cfg.preset)
    arch = params["arch"]
    image_size = int(params["image_size"])
    batch_size = int(params["batch_size"])
    epochs = int(params["epochs"])
    freeze_epochs = int(params["freeze_epochs"])
    lr_head = float(params["lr_head"])
    lr_ft = float(params["lr_finetune"])
    mixup_alpha = float(params["mixup_alpha"])
    erase_p = float(params["erase_p"])
    patience = int(params["patience"])

    batch_size = _env_int("CBIS_BATCH_SIZE", batch_size)

    dev = _device()
    _speed_flags(dev)

    epochs_cap = _env_int("CBIS_EPOCHS_CAP", 12 if dev.type == "cuda" else 6)
    if epochs_cap > 0:
        epochs = min(epochs, epochs_cap)
        freeze_epochs = min(freeze_epochs, max(0, epochs - 1))

    if dev.type != "cuda":
        cfg.amp = False
        image_size = min(image_size, 224)
        batch_size = min(batch_size, 24)
        epochs = min(epochs, 6)
        freeze_epochs = min(freeze_epochs, 2)

    if cfg.workers <= 0:
        cfg.workers = max(0, min(4, os.cpu_count() or 0))

    with _TRAIN_LOCK:
        _TRAIN_STATE["started"] = True
        _TRAIN_STATE["done"] = False
        _TRAIN_STATE["error"] = None

    try:
        while True:
            try:
                if dev.type == "cuda":
                    try:
                        torch.cuda.empty_cache()
                    except Exception:
                        pass

                samples = build_samples(cfg)
                tr_s, va_s = _split_groupwise(cfg, samples)

                model = _create_model(arch, 2)
                if dev.type == "cuda":
                    model = model.to(dev, memory_format=torch.channels_last)
                else:
                    model = model.to(dev)

                if cfg.compile and hasattr(torch, "compile") and dev.type == "cuda":
                    try:
                        model = torch.compile(model, mode="reduce-overhead")
                    except Exception:
                        pass

                loss_fn = nn.CrossEntropyLoss(label_smoothing=float(cfg.label_smoothing))

                train_dl = _make_loader_train(cfg, tr_s, image_size, batch_size, cfg.workers, dev, erase_p=erase_p)
                val_dl = _make_loader_eval(cfg, va_s, image_size, max(8, batch_size), cfg.workers, dev, erase_p=erase_p)

                best_score = -1e9
                best_state = None
                best_info: Dict[str, float] = {}
                best_cm: Dict[str, int] = {}
                bad = 0

                def _make_opt(params_iter, lr: float):
                    try:
                        return torch.optim.AdamW(params_iter, lr=lr, weight_decay=1e-4, fused=(dev.type == "cuda"))
                    except Exception:
                        return torch.optim.AdamW(params_iter, lr=lr, weight_decay=1e-4)

                if freeze_epochs > 0:
                    _set_backbone_trainable(model, trainable=False)
                    opt = _make_opt(filter(lambda p: p.requires_grad, model.parameters()), lr_head)
                    sched = torch.optim.lr_scheduler.CosineAnnealingLR(opt, T_max=max(1, freeze_epochs))

                    for ep in range(1, freeze_epochs + 1):
                        tr_loss = _train_one_epoch(model, train_dl, opt, loss_fn, dev, cfg.amp, mixup_alpha)
                        va_loss, va_m, va_cm, _, _ = _eval(model, val_dl, loss_fn, dev, max_batches=int(cfg.max_val_batches))
                        auc = va_m["auc"]
                        auc_term = 0.0 if (isinstance(auc, float) and math.isnan(auc)) else float(auc)
                        score = float(va_m["bal_acc"]) + 0.25 * auc_term

                        if score > best_score:
                            best_score = score
                            best_state = {k: v.detach().cpu().clone() for k, v in model.state_dict().items()}
                            best_info = {
                                "phase": 1.0,
                                "epoch": float(ep),
                                "train_loss": float(tr_loss),
                                "val_loss": float(va_loss),
                                **{f"val_{k}": float(v) for k, v in va_m.items()},
                            }
                            best_cm = dict(va_cm)
                            bad = 0
                        else:
                            bad += 1
                            if bad >= patience:
                                break

                        sched.step()

                _set_backbone_trainable(model, trainable=True)
                opt = _make_opt(model.parameters(), lr_ft)
                finetune_epochs = max(1, epochs - freeze_epochs)
                sched = torch.optim.lr_scheduler.CosineAnnealingLR(opt, T_max=finetune_epochs)

                for ep in range(freeze_epochs + 1, epochs + 1):
                    tr_loss = _train_one_epoch(model, train_dl, opt, loss_fn, dev, cfg.amp, mixup_alpha)
                    va_loss, va_m, va_cm, _, _ = _eval(model, val_dl, loss_fn, dev, max_batches=int(cfg.max_val_batches))
                    auc = va_m["auc"]
                    auc_term = 0.0 if (isinstance(auc, float) and math.isnan(auc)) else float(auc)
                    score = float(va_m["bal_acc"]) + 0.25 * auc_term

                    if score > best_score:
                        best_score = score
                        best_state = {k: v.detach().cpu().clone() for k, v in model.state_dict().items()}
                        best_info = {
                            "phase": 2.0,
                            "epoch": float(ep),
                            "train_loss": float(tr_loss),
                            "val_loss": float(va_loss),
                            **{f"val_{k}": float(v) for k, v in va_m.items()},
                        }
                        best_cm = dict(va_cm)
                        bad = 0
                    else:
                        bad += 1
                        if bad >= patience:
                            break

                    sched.step()

                if best_state is not None:
                    model.load_state_dict(best_state)

                meta = {
                    "train_started": True,
                    "train_failed": False,
                    "train_error": None,
                    "preset": str(cfg.preset),
                    "arch": str(arch),
                    "seed": int(cfg.seed),
                    "image_size": int(image_size),
                    "epochs": int(epochs),
                    "freeze_epochs": int(freeze_epochs),
                    "lr_head": float(lr_head),
                    "lr_finetune": float(lr_ft),
                    "batch_size": int(batch_size),
                    "workers": int(cfg.workers),
                    "val_ratio": float(cfg.val_ratio),
                    "device": str(dev),
                    "use_amp": bool(cfg.amp and dev.type == "cuda"),
                    "compile": bool(cfg.compile),
                    "mm_use_cache": bool(cfg.mm_use_cache),
                    "mm_cache_dir_name": str(cfg.mm_cache_dir_name),
                    "mm_use_augmented": bool(cfg.mm_use_augmented),
                    "mm_val_from_original_only": bool(cfg.mm_val_from_original_only),
                    "n_samples_total": int(len(samples)),
                    "n_samples_train": int(len(tr_s)),
                    "n_samples_val": int(len(va_s)),
                    "best_info": best_info,
                    "best_confusion": best_cm,
                    "dataset_kind": str(cfg.dataset_kind),
                    "dataset_dir": str(dsd),
                    "artifact_path": str(ap["model"]),
                    "path_map_ok": False,
                    "name_map_ok": False,
                    "hash_map_ok": False,
                }

                _save_artifacts(ad, model, meta)

                if cfg.build_path_map:
                    try:
                        build_path_label_map(cfg, samples)
                    except Exception:
                        pass
                if cfg.build_name_map:
                    try:
                        build_name_label_map(cfg, samples)
                    except Exception:
                        pass
                if cfg.build_hash_map:
                    try:
                        build_hash_label_map(cfg, samples)
                    except Exception:
                        pass

                with _TRAIN_LOCK:
                    _TRAIN_STATE["done"] = True
                    _TRAIN_STATE["error"] = None

                _update_train_state_from_artifacts(cfg)
                return meta

            except RuntimeError as e:
                msg = str(e).lower()
                if dev.type == "cuda" and ("out of memory" in msg or "cuda out of memory" in msg):
                    try:
                        torch.cuda.empty_cache()
                    except Exception:
                        pass
                    if batch_size <= 8:
                        raise
                    batch_size = max(8, batch_size // 2)
                    continue
                raise

    except Exception as e:
        meta_fail = {"train_started": True, "train_failed": True, "train_error": repr(e)}
        try:
            _, ad2 = _resolve_dirs(cfg)
            ap2 = _artifact_paths(ad2)
            old = _load_json(ap2["meta"]) or {}
            old.update(meta_fail)
            _save_json(ap2["meta"], old)
        except Exception:
            pass
        with _TRAIN_LOCK:
            _TRAIN_STATE["done"] = True
            _TRAIN_STATE["error"] = repr(e)
        _update_train_state_from_artifacts(cfg)
        raise


def find_ground_truth(cfg: Optional[CbisDdsmConfig], filename: str, file_bytes: Optional[bytes] = None) -> Optional[str]:
    cfg = cfg or CbisDdsmConfig()
    ensure_maps_loaded(cfg)

    f = (filename or "").replace("\\", "/").lower().strip()
    f2 = f.lstrip("/")

    if f2 in _PATH_MAP:
        return _PATH_MAP[f2]

    name = Path(f2).name.lower()
    if name in _NAME_MAP:
        return _NAME_MAP[name]

    if file_bytes is not None and _HASH_MAP:
        h = _sha1(file_bytes)
        if h in _HASH_MAP:
            return _HASH_MAP[h]

    return None


def _normalize_model_id(cfg: CbisDdsmConfig, model_id: str) -> str:
    m = (model_id or "").strip()
    if m:
        return m
    kind = (cfg.dataset_kind or "").strip().lower()
    if kind == "mammogram_mastery":
        return "mammogram_mastery_images:torch_cnn"
    return "cbis_ddsm_images:torch_cnn"



def _make_out_of_domain_result(cfg: CbisDdsmConfig, model_id: str, q: Dict, dom: Dict) -> Dict:
    out = {
        "model_id": _normalize_model_id(cfg, model_id),
        "label": "OUT_OF_DOMAIN",
        "effective_probability": 0.0,
        "best_probability": 0.0,
        "probability": 0.0,
        "proba_malignant": 0.0,
        "probabilities": {"BENIGN": 0.0, "MALIGNANT": 0.0},
        "ground_truth": None,
        "match_with_dataset": None,
    }
    out.update(q or {})
    out.update(dom or {})
    return out


def predict_bytes(
    cfg: Optional[CbisDdsmConfig],
    file_bytes: bytes,
    filename: str = "",
    image_size: int = 224,
    model_id: str = "",
    require_quality: bool = False,
    require_domain: bool = False,
) -> Dict:
    cfg = cfg or CbisDdsmConfig()
    ensure_loaded(cfg)

    dev = _DEV or _device()
    model = _MODEL
    if model is None:
        raise RuntimeError("Model not loaded.")

    image_size = int(image_size)
    im = decode_image_bytes_safe(file_bytes, filename=filename)
    q = assess_quality(im)
    dom = assess_domain_mammogram_like(im)

    req_dom = bool(require_domain or _env_bool("D_REQUIRE", False))

    if require_quality and (q.get("quality_ok") is False):
        out = {
            "model_id": _normalize_model_id(cfg, model_id),
            "label": "UNUSABLE_IMAGE",
            "effective_probability": 0.0,
            "best_probability": 0.0,
            "probability": 0.0,
            "proba_malignant": 0.0,
            "probabilities": {"BENIGN": 0.0, "MALIGNANT": 0.0},
            "ground_truth": None,
            "match_with_dataset": None,
        }
        out.update(q)
        out.update(dom)
        return out

    if req_dom and (dom.get("domain_ok") is False):
        return _make_out_of_domain_result(cfg, model_id, q, dom)

    tf = T.Compose([
        _make_preprocessor(cfg, image_size),
        T.ToTensor(),
        T.Normalize(mean=IMAGENET_MEAN, std=IMAGENET_STD),
    ])

    x = tf(im).unsqueeze(0)
    if dev.type == "cuda":
        x = x.to(dev, non_blocking=True, memory_format=torch.channels_last)
    else:
        x = x.to(dev, non_blocking=True)

    with torch.inference_mode():
        logits = model(x)
        probs = torch.softmax(logits, dim=1).squeeze(0).detach().float().cpu().numpy().astype(np.float64)

    p_benign, p_malign = float(probs[0]), float(probs[1])
    idx = int(np.argmax(probs))
    label = IDX_TO_LABEL[idx]
    probas = {"BENIGN": p_benign, "MALIGNANT": p_malign}

    gt = find_ground_truth(cfg, filename=filename, file_bytes=file_bytes)
    match = (gt == label) if gt else None

    out = {
        "model_id": _normalize_model_id(cfg, model_id),
        "label": label,
        "effective_probability": float(probas[label]),
        "best_probability": float(max(probas.values())),
        "probability": float(probas[label]),
        "proba_malignant": float(probas["MALIGNANT"]),
        "probabilities": {"BENIGN": float(p_benign), "MALIGNANT": float(p_malign)},
        "ground_truth": gt,
        "match_with_dataset": match,
    }

    out.update(q)
    out.update(dom)
    return out


def start_train_background(cfg: Optional[CbisDdsmConfig] = None) -> Dict:
    cfg = cfg or CbisDdsmConfig()

    with _TRAIN_LOCK:
        started = bool(_TRAIN_STATE.get("started"))
        done = bool(_TRAIN_STATE.get("done"))
        if started and not done:
            return {"ok": True, "started": True, "already_running": True}
        _TRAIN_STATE["started"] = True
        _TRAIN_STATE["done"] = False
        _TRAIN_STATE["error"] = None

    def _run():
        try:
            train_and_save(cfg)
        except Exception:
            pass

    t = threading.Thread(target=_run, daemon=True)
    t.start()
    return {"ok": True, "started": True, "already_running": False}
