#!/usr/bin/env python3
"""Prepare verified offline assets, ZIP64 archive, and GitHub-sized parts."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import zipfile

from prepare_assets import digest, verify


def split_archive(archive, out, base_url, part_size=1536 * 1024 * 1024):
    parts = []
    whole = hashlib.sha256()
    with archive.open("rb") as source:
        index = 1
        while True:
            block = source.read(min(4 * 1024 * 1024, part_size))
            if not block:
                break
            name = archive.name + ".part%02d" % index
            path = out / name
            size = 0
            checksum = hashlib.sha256()
            with path.open("xb") as target:
                while block:
                    target.write(block)
                    checksum.update(block)
                    whole.update(block)
                    size += len(block)
                    if size == part_size:
                        break
                    block = source.read(min(4 * 1024 * 1024, part_size - size))
            parts.append({"name": name, "size": size, "sha256": checksum.hexdigest(), "url": base_url + name})
            index += 1
    return {"archive": {"name": archive.name, "size": archive.stat().st_size, "sha256": whole.hexdigest()}, "parts": parts}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--overlay", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--installer-only", action="store_true")
    args = parser.parse_args()
    repo = Path(__file__).resolve().parent.parent
    version = (repo / "release-version.txt").read_text().strip()
    work = Path(os.environ["RUNNER_TEMP"]) / ("si-full-" + version)
    cache = work / "cache"
    cache.mkdir(parents=True)
    root = work / "SI_Local_AI"
    lock = json.loads((repo / "scripts/ASSET_LOCK.json").read_text(encoding="utf-8"))
    # Reuse the exact archived license snapshots; live license pages can change.
    for item in lock["assets"]:
        existing = repo / item["target"]
        if verify(existing, item):
            shutil.copyfile(existing, cache / item["name"])
    subprocess.run([sys.executable, str(repo / "scripts/prepare_assets.py"), "--cache", str(cache), "--destination", str(root)], check=True)
    shutil.rmtree(cache)
    shutil.copytree(args.overlay, root, dirs_exist_ok=True)
    for folder in ("licenses", "provenance", "scripts", "tools", "installer"):
        shutil.copytree(repo / folder, root / folder, dirs_exist_ok=True, ignore=shutil.ignore_patterns("__pycache__"))
    for name in ("ASSET_MANIFEST.json", "Verify-Package.cmd", "Verify-Package.ps1", "README.md", "README_FULL_DOWNLOAD_KO.md", "README_INSTALL_KO.txt"):
        shutil.copyfile(repo / name, root / name)
    manifest_path = root / "ASSET_MANIFEST.json"
    assets = json.loads(manifest_path.read_text(encoding="utf-8"))
    assets["package"] = "SI Local AI Offline " + version
    assets["prepared_date"] = datetime.datetime.now(datetime.timezone.utc).date().isoformat()
    assets["launcher_build"] = "Windows Server 2022, .NET Framework 4.8 x64 Release"
    assets["windows_execution_verified"] = False
    assets["validation_note"] = "Windows build and logic checks passed; GUI/model inference/company MCP not verified. See validation/WINDOWS_CI.md."
    manifest_path.write_text(json.dumps(assets, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (root / "README_API_UPDATE_KO.md").write_text(
        "이 전체 패키지는 모델과 CPU/CUDA 실행기를 포함합니다. 아래 업데이트 적용 단계는 기존 사용자용입니다. 신규 설치는 START_HERE_KO.txt를 확인하세요.\n\n"
        + (root / "README_API_UPDATE_KO.md").read_text(encoding="utf-8-sig"), encoding="utf-8")
    if args.installer_only:
        subprocess.run([sys.executable, str(repo / "scripts/package_release.py"), "--root", str(root), "--manifest-only"], check=True)
        receipt = {
            "version": version, "commit": os.environ["GITHUB_SHA"],
            "asset_files_verified": len(assets["files"]),
            "asset_bytes_verified": sum(x["size_bytes"] for x in assets["files"]),
            "model_sha256": next(x["sha256"] for x in lock["assets"] if x["name"].endswith(".gguf")),
            "model_executed": False,
            "installer_validation": "See INSTALLER_VALIDATION.md for installed-file hash checks.",
        }
        (args.output / "FULL_PACKAGE_VALIDATION.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
        shutil.copyfile(root / "ASSET_MANIFEST.json", args.output / "ASSET_MANIFEST.json")
        shutil.copyfile(repo / "scripts/ASSET_LOCK.json", args.output / "ASSET_LOCK.json")
        print("Complete offline installer payload ready:", root, flush=True)
        return
    archive = work / ("SI_Local_AI_Full_v" + version + "_Windows.zip")
    subprocess.run([sys.executable, str(repo / "scripts/package_release.py"), "--root", str(root), "--output", str(archive)], check=True)
    # Reading every member checks CRC and proves the final ZIP is complete.
    with zipfile.ZipFile(archive) as z:
        bad = z.testzip()
        if bad:
            raise ValueError("ZIP CRC failed: " + bad)
    base_url = "https://github.com/" + os.environ["GITHUB_REPOSITORY"] + "/releases/download/v" + version + "/"
    manifest = split_archive(archive, args.output, base_url)
    if manifest["archive"]["sha256"] != digest(archive):
        raise ValueError("Split archive hash mismatch")
    (args.output / "full-download-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    receipt = {
        "version": version, "commit": os.environ["GITHUB_SHA"],
        "archive": manifest["archive"], "parts": manifest["parts"],
        "asset_files_verified": len(assets["files"]), "zip_crc_verified": True,
        "model_sha256": next(x["sha256"] for x in lock["assets"] if x["name"].endswith(".gguf")),
        "model_executed": False,
    }
    (args.output / "FULL_PACKAGE_VALIDATION.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    shutil.copyfile(root / "ASSET_MANIFEST.json", args.output / "ASSET_MANIFEST.json")
    shutil.copyfile(repo / "scripts/ASSET_LOCK.json", args.output / "ASSET_LOCK.json")
    print("Full package verified and split:", manifest["archive"], flush=True)


if __name__ == "__main__":
    main()
