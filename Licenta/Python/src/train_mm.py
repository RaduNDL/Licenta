from __future__ import annotations

import argparse
import os
import sys
import time
import traceback
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

import src.CBISDDSM as mm


def _b(v: str) -> bool:
    s = (v or "").strip().lower()
    return s in ("1", "true", "yes", "y", "on")


def _setenv(k: str, v: str | int | float | bool | Path) -> None:
    os.environ[str(k)] = str(v)


def _gpu_info():
    try:
        ok = mm.torch.cuda.is_available()
        dev = str(mm._device())
        print("torch_cuda_available:", ok, "device:", dev, flush=True)
        if ok:
            p = mm.torch.cuda.get_device_properties(0)
            print("gpu:", p.name, "vram_gb:", round(p.total_memory / (1024**3), 2), flush=True)
    except Exception:
        pass


def parse_args():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dataset-dir", type=str, default=r"D:\Facultate\Licenta\Python\dataset\Breast Cancer Detection and Medical Education\Mammogram Mastery A Robust Dataset for Breast Cancer Detection and Medical Education\Breast Cancer Dataset")
    ap.add_argument("--artifact-dir", type=str, default=str(PROJECT_ROOT / "artifacts" / "mammogram_mastery_images"))
    ap.add_argument("--preset", type=str, default=os.getenv("CBIS_PRESET", "best"))
    ap.add_argument("--epochs-cap", type=int, default=int(os.getenv("CBIS_EPOCHS_CAP", "12")))
    ap.add_argument("--batch-size", type=int, default=int(os.getenv("CBIS_BATCH_SIZE", "32")))
    ap.add_argument("--workers", type=int, default=int(os.getenv("CBIS_WORKERS", "4")))
    ap.add_argument("--seed", type=int, default=int(os.getenv("CBIS_SEED", "42")))
    ap.add_argument("--val-ratio", type=float, default=float(os.getenv("CBIS_VAL_RATIO", "0.12")))
    ap.add_argument("--max-samples", type=int, default=int(os.getenv("CBIS_MAX_SAMPLES", "0")))
    ap.add_argument("--amp", type=str, default=os.getenv("CBIS_AMP", "1"))
    ap.add_argument("--compile", type=str, default=os.getenv("CBIS_COMPILE", "0"))
    ap.add_argument("--max-val-batches", type=int, default=int(os.getenv("CBIS_MAX_VAL_BATCHES", "0")))
    ap.add_argument("--label-smoothing", type=float, default=float(os.getenv("CBIS_LABEL_SMOOTHING", "0.01")))
    ap.add_argument("--dl-prefetch", type=int, default=int(os.getenv("DL_PREFETCH", "2")))
    ap.add_argument("--mm-use-cache", type=str, default=os.getenv("MM_USE_CACHE", "0"))
    ap.add_argument("--mm-cache-dir-name", type=str, default=os.getenv("MM_CACHE_DIR_NAME", "Cache224"))
    ap.add_argument("--mm-use-augmented", type=str, default=os.getenv("MM_USE_AUGMENTED", "1"))
    ap.add_argument("--mm-val-from-original-only", type=str, default=os.getenv("MM_VAL_FROM_ORIGINAL_ONLY", "0"))
    ap.add_argument("--build-path-map", type=str, default=os.getenv("MM_BUILD_PATH_MAP", "1"))
    ap.add_argument("--build-name-map", type=str, default=os.getenv("MM_BUILD_NAME_MAP", "1"))
    ap.add_argument("--build-hash-map", type=str, default=os.getenv("MM_BUILD_HASH_MAP", "0"))
    ap.add_argument("--no-pretrain", type=str, default=os.getenv("ML_NO_PRETRAIN", "0"))
    ap.add_argument("--cuda-alloc", type=str, default=os.getenv("PYTORCH_CUDA_ALLOC_CONF", "max_split_size_mb:256"))
    return ap.parse_args()


def main():
    args = parse_args()

    dataset_dir = Path(args.dataset_dir)
    artifact_dir = Path(args.artifact_dir)

    _setenv("ML_DATASET", "mammogram_mastery")
    _setenv("ML_DATASET_DIR", dataset_dir)
    _setenv("ML_MODEL_ID", "mammogram_mastery_images:torch_cnn")
    _setenv("ML_ARTIFACT_DIR", artifact_dir)

    _setenv("PYTORCH_CUDA_ALLOC_CONF", args.cuda_alloc)

    _setenv("CBIS_PRESET", args.preset)
    _setenv("CBIS_EPOCHS_CAP", args.epochs_cap)
    _setenv("CBIS_BATCH_SIZE", args.batch_size)
    _setenv("CBIS_WORKERS", args.workers)
    _setenv("CBIS_SEED", args.seed)
    _setenv("CBIS_VAL_RATIO", args.val_ratio)
    _setenv("CBIS_MAX_SAMPLES", args.max_samples)
    _setenv("CBIS_AMP", "1" if _b(args.amp) else "0")
    _setenv("CBIS_COMPILE", "1" if _b(args.compile) else "0")
    _setenv("CBIS_MAX_VAL_BATCHES", args.max_val_batches)
    _setenv("DL_PREFETCH", args.dl_prefetch)
    _setenv("CBIS_LABEL_SMOOTHING", args.label_smoothing)

    _setenv("MM_USE_CACHE", "1" if _b(args.mm_use_cache) else "0")
    _setenv("MM_CACHE_DIR_NAME", args.mm_cache_dir_name)
    _setenv("MM_USE_AUGMENTED", "1" if _b(args.mm_use_augmented) else "0")
    _setenv("MM_VAL_FROM_ORIGINAL_ONLY", "1" if _b(args.mm_val_from_original_only) else "0")

    _setenv("MM_BUILD_PATH_MAP", "1" if _b(args.build_path_map) else "0")
    _setenv("MM_BUILD_NAME_MAP", "1" if _b(args.build_name_map) else "0")
    _setenv("MM_BUILD_HASH_MAP", "1" if _b(args.build_hash_map) else "0")

    _setenv("ML_NO_PRETRAIN", "1" if _b(args.no_pretrain) else "0")

    t0 = time.time()
    print("train_mm: start", flush=True)
    _gpu_info()

    cfg = mm.CbisDdsmConfig(
        preset=os.getenv("CBIS_PRESET", "best"),
        workers=int(os.getenv("CBIS_WORKERS", "4")),
        seed=int(os.getenv("CBIS_SEED", "42")),
        val_ratio=float(os.getenv("CBIS_VAL_RATIO", "0.12")),
        amp=_b(os.getenv("CBIS_AMP", "1")),
        compile=_b(os.getenv("CBIS_COMPILE", "0")),
        label_smoothing=float(os.getenv("CBIS_LABEL_SMOOTHING", "0.01")),
        max_val_batches=int(os.getenv("CBIS_MAX_VAL_BATCHES", "0")),
        max_samples=int(os.getenv("CBIS_MAX_SAMPLES", "0")),
        mm_use_augmented=_b(os.getenv("MM_USE_AUGMENTED", "1")),
        mm_val_from_original_only=_b(os.getenv("MM_VAL_FROM_ORIGINAL_ONLY", "0")),
        mm_use_cache=_b(os.getenv("MM_USE_CACHE", "0")),
        mm_cache_dir_name=os.getenv("MM_CACHE_DIR_NAME", "Cache224"),
        build_path_map=_b(os.getenv("MM_BUILD_PATH_MAP", "1")),
        build_name_map=_b(os.getenv("MM_BUILD_NAME_MAP", "1")),
        build_hash_map=_b(os.getenv("MM_BUILD_HASH_MAP", "0")),
        dataset_kind=os.getenv("ML_DATASET", "mammogram_mastery"),
        dataset_dir=Path(os.environ["ML_DATASET_DIR"]),
        artifact_dir=Path(os.environ["ML_ARTIFACT_DIR"]),
    )

    try:
        meta = mm.train_and_save(cfg)
    except Exception:
        print("train_mm: ERROR", flush=True)
        print(traceback.format_exc(), flush=True)
        raise

    dt = time.time() - t0
    print("train_mm: done", flush=True)
    print("seconds:", round(dt, 2), flush=True)

    if isinstance(meta, dict):
        print("artifact:", meta.get("artifact_path"), flush=True)
        print("arch:", meta.get("arch"), "image_size:", meta.get("image_size"), "batch_size:", meta.get("batch_size"), flush=True)
        bi = meta.get("best_info") or {}
        if isinstance(bi, dict) and bi:
            print("best_info:", bi, flush=True)
        bc = meta.get("best_confusion") or {}
        if isinstance(bc, dict) and bc:
            print("best_confusion:", bc, flush=True)


if __name__ == "__main__":
    main()
