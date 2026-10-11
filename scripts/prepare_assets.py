#!/usr/bin/env python3
"""Build the pinned asset tree without executing a model/server/installer.
Python 3.10+, standard library only. Run on an approved download/build machine.
Does not change firewall, execution policy, proxies, or system installations.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import urllib.request
import zipfile


def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda: f.read(4 * 1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def verify(path, item):
    return (path.is_file() and path.stat().st_size == item["size"]
            and digest(path) == item["sha256"])


def safe_name(name):
    p = PurePosixPath(name)
    if (not name or name.startswith("/") or "\\" in name or ":" in name
            or any(ord(c) < 32 for c in name)
            or any(x in (".", "..") or x.endswith((".", " ")) for x in p.parts)
            or any(re.match(r"^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)", x, re.I) for x in p.parts)):
        raise ValueError("Unsafe archive/target path: " + name)
    return p


def download(item, cache, offline):
    path = cache / item["name"]
    if verify(path, item):
        return path
    if path.exists():
        raise ValueError("Existing cache file failed verification: " + str(path))
    if offline:
        raise FileNotFoundError("Missing verified cache asset: " + str(path))
    partial = path.with_name(path.name + ".partial")
    if not item["url"].startswith("https://"):
        raise ValueError("HTTPS required")
    # Interrupted files keep their .partial extension and cannot appear as GGUF models.
    with urllib.request.urlopen(item["url"], timeout=90) as response, partial.open("wb") as out:
        shutil.copyfileobj(response, out, 4 * 1024 * 1024)
    if not verify(partial, item):
        raise ValueError("Downloaded size or SHA256 mismatch: " + item["name"])
    os.replace(partial, path)
    return path


def extract(archive, folder, seen):
    with zipfile.ZipFile(archive) as z:
        # Preflight every entry before writing any entry from this archive.
        entries = []
        archive_seen = set()
        for info in z.infolist():
            rel = safe_name(info.filename.rstrip("/"))
            target = folder.joinpath(*rel.parts)
            key = str(target).casefold()
            if key in seen or key in archive_seen or target.exists():
                raise ValueError("Duplicate output path: " + str(target))
            if stat.S_ISLNK(info.external_attr >> 16) or info.flag_bits & 1:
                raise ValueError("Links and encrypted entries are not accepted")
            archive_seen.add(key)
            entries.append((info, target))
        for info, target in entries:
            if info.is_dir():
                target.mkdir(parents=True, exist_ok=True)
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            with z.open(info) as src, target.open("xb") as out:
                shutil.copyfileobj(src, out, 4 * 1024 * 1024)
            # Reading to EOF checks ZIP CRC. Also check uncompressed length explicitly.
            if target.stat().st_size != info.file_size:
                raise ValueError("Extraction size mismatch")
        seen.update(archive_seen)


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--cache", type=Path, required=True)
    ap.add_argument("--destination", type=Path, required=True)
    ap.add_argument("--lock", type=Path, default=Path(__file__).with_name("ASSET_LOCK.json"))
    ap.add_argument("--offline-cache", action="store_true")
    args = ap.parse_args()
    if args.destination.exists() and any(args.destination.iterdir()):
        raise ValueError("Destination must be new/empty; never overwrite an installation")
    args.cache.mkdir(parents=True, exist_ok=True)
    args.destination.mkdir(parents=True, exist_ok=True)
    lock = json.loads(args.lock.read_text(encoding="utf-8"))
    seen = set()
    for item in lock["assets"]:
        safe_name(item["name"])
        path = download(item, args.cache, args.offline_cache)
        target = args.destination.joinpath(*safe_name(item["target"]).parts)
        if item.get("extract_zip"):
            extract(path, target, seen)
        else:
            if target.exists():
                raise ValueError("Duplicate target: " + str(target))
            target.parent.mkdir(parents=True, exist_ok=True)
            with path.open("rb") as src, target.open("xb") as out:
                shutil.copyfileobj(src, out, 4 * 1024 * 1024)
        print("VERIFIED", item["name"], flush=True)
    (args.destination / "ASSET_LOCK.json").write_text(json.dumps(lock, indent=2), encoding="utf-8")
    print("Asset preparation complete. No model, server or installer was executed.")


if __name__ == "__main__":
    main()
