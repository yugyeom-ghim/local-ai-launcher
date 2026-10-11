#!/usr/bin/env python3
"""Fail-closed release packaging, Python standard library, no model execution."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile


def sha256(path):
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(4 * 1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--root", type=Path, required=True)
    p.add_argument("--output", type=Path)
    p.add_argument("--manifest-only", action="store_true")
    args = p.parse_args()
    root = args.root.resolve()
    if not args.manifest_only and args.output is None:
        p.error("--output is required when creating a ZIP")
    if args.output is not None and root in args.output.resolve().parents:
        raise ValueError("Write ZIP outside the release tree")
    required = ["LocalAI.Launcher.exe", "LocalAI.Launcher.exe.config", "ASSET_MANIFEST.json", "VALIDATION.md",
                "licenses/llama-LICENSE.txt", "licenses/Qwen-LICENSE.txt", "licenses/CUDA-12.4-EULA.html",
                "licenses/CUDA-13.4-EULA.html", "prerequisites/vc_redist.x64.exe",
                "offline-assets/ai/models/Qwen3.5-4B-Q4_K_M.gguf"]
    for folder in ("win-cpu-x64", "win-cuda-12.4-x64", "win-cuda-13.4-x64"):
        required.append("offline-assets/ai/bin/" + folder + "/llama-server.exe")
    for major, version in ((12, "12.4"), (13, "13.4")):
        for name in ("cudart64_", "cublas64_", "cublasLt64_"):
            required.append("offline-assets/ai/bin/win-cuda-" + version + "-x64/" + name + str(major) + ".dll")
    for rel in required:
        if not (root / rel).is_file() or (root / rel).stat().st_size == 0:
            raise ValueError("Required release file missing: " + rel)
    for entry in json.loads((root / "ASSET_MANIFEST.json").read_text(encoding="utf-8"))["files"]:
        path = root / entry["path"]
        if root not in path.resolve().parents or not path.is_file():
            raise ValueError("Invalid/missing manifest file: " + entry["path"])
        if path.stat().st_size != entry["size_bytes"] or sha256(path) != entry["sha256"]:
            raise ValueError("Asset manifest mismatch: " + entry["path"])
    files = sorted(f for f in root.rglob("*") if f.is_file() and f.name != "SHA256SUMS.txt"
                   and not any(part.startswith(".") for part in f.relative_to(root).parts)
                   and "__pycache__" not in f.relative_to(root).parts)
    for f in files:
        if f.is_symlink() or f.name.startswith("PLACE_") or f.suffix in (".partial", ".tmp"):
            raise ValueError("Incomplete/unexpected release file: " + str(f))
        if "state" in f.relative_to(root).parts:
            raise ValueError("User state/logs must not be packaged")
    lines = [sha256(f) + "  " + f.relative_to(root).as_posix() for f in files]
    checksum_file = root / "SHA256SUMS.txt"
    checksum_file.write_text("\n".join(lines) + "\n", encoding="utf-8")
    files.append(checksum_file)
    if args.manifest_only:
        print("Payload verified:", len(files), "files; SHA256 manifest written.")
        return
    args.output.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.output.with_name(args.output.name + ".partial")
    with zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=1, allowZip64=True) as z:
        for f in files:
            z.write(f, root.name + "/" + f.relative_to(root).as_posix())
    temporary.replace(args.output)
    checksum = sha256(args.output)
    args.output.with_suffix(args.output.suffix + ".sha256").write_text(checksum + "  " + args.output.name + "\n")
    print(args.output.name, args.output.stat().st_size, checksum)


if __name__ == "__main__":
    main()
