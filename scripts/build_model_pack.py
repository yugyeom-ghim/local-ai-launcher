#!/usr/bin/env python3
"""Stream one revision-pinned GGUF into GitHub-sized offline parts, then verify it."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import time
import urllib.request

CHUNK = 1536 * 1024 * 1024

def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for b in iter(lambda: f.read(4 * 1024 * 1024), b''): h.update(b)
    return h.hexdigest()

def build(model, out, base):
    out.mkdir(parents=True, exist_ok=True)
    if shutil.disk_usage(out).free < model['size'] * 2 + 1024**3:
        raise RuntimeError('Insufficient disk for parts and Windows import validation')
    # Only complete, verified prior parts are resumed. Full SHA is checked again below.
    parts = []
    offset = 0
    while offset < model['size']:
        name = model['file'] + '.part%02d' % (len(parts) + 1)
        path = out / name
        size = min(CHUNK, model['size'] - offset)
        record = path.with_suffix(path.suffix + '.verified.json')
        if path.exists() and record.exists():
            saved = json.loads(record.read_text())
            if saved.get('revision') == model['revision'] and path.stat().st_size == size and sha(path) == saved['sha256']:
                parts.append(dict(name=name, size=size, sha256=saved['sha256'], url=base+name)); offset += size; continue
        break
    while offset < model['size']:
        start = offset
        try:
            req = urllib.request.Request(model['url'], headers={'User-Agent': 'Local-AI-Launcher-Pack-Builder', 'Range': 'bytes=%d-' % offset})
            with urllib.request.urlopen(req, timeout=120) as response:
                if response.status == 206:
                    if not response.headers.get('Content-Range', '').startswith('bytes %d-' % offset): raise ValueError('Wrong HTTP range')
                elif offset != 0 or response.status != 200: raise ValueError('Server ignored resume range')
                while offset < model['size']:
                    name = model['file'] + '.part%02d' % (len(parts) + 1)
                    path = out / name; temp = path.with_suffix(path.suffix + '.partial')
                    size = min(CHUNK, model['size'] - offset); h = hashlib.sha256(); written = 0
                    with temp.open('wb') as target:
                        while written < size:
                            data = response.read(min(4 * 1024 * 1024, size - written))
                            if not data: raise EOFError('Model download interrupted')
                            target.write(data); h.update(data); written += len(data)
                    temp.replace(path)
                    part = dict(name=name, size=size, sha256=h.hexdigest(), url=base+name)
                    path.with_suffix(path.suffix + '.verified.json').write_text(json.dumps(dict(revision=model['revision'], sha256=h.hexdigest())))
                    parts.append(part); offset += size
                    print('%s %d/%d bytes' % (model['id'], offset, model['size']), flush=True)
            break
        except (OSError, EOFError) as exc:
            if offset == start: raise
            print('Resuming at verified part boundary:', exc, flush=True)
            time.sleep(2)
    whole = hashlib.sha256()
    for part in parts:
        with (out / part['name']).open('rb') as f:
            for block in iter(lambda: f.read(4 * 1024 * 1024), b''): whole.update(block)
    if whole.hexdigest() != model['sha256']: raise ValueError('Publisher GGUF SHA256 mismatch')
    # Exact upstream license; immutable source URL is recorded in the manifest.
    with urllib.request.urlopen(model['license_url'], timeout=90) as r: license_data = r.read()
    if len(license_data) != model['license_size'] or hashlib.sha256(license_data).hexdigest() != model['license_sha256']: raise ValueError('License mismatch')
    (out / (model['id'] + '-LICENSE.txt')).write_bytes(license_data)
    manifest = out / (model['id'] + '.modelpack.json')
    manifest.write_text(json.dumps(dict(schema=1, model=model, parts=parts), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    for record in out.glob('*.verified.json'): record.unlink()
    for partial in out.glob('*.partial'): partial.unlink()
    print('Publisher size/SHA256 verified:', model['file'], flush=True)

def main():
    p = argparse.ArgumentParser(); p.add_argument('--model', required=True); p.add_argument('--output', type=Path, required=True)
    args = p.parse_args(); repo = Path(__file__).resolve().parent.parent
    catalog = json.loads((repo / 'src/Resources/ModelCatalog.json').read_text(encoding='utf-8'))
    model = next(m for m in catalog['models'] if m['id'] == args.model and not m['bundled'])
    version = (repo / 'release-version.txt').read_text().strip()
    base = 'https://github.com/' + os.environ['GITHUB_REPOSITORY'] + '/releases/download/v' + version + '/'
    build(model, args.output, base)

if __name__ == '__main__': main()
