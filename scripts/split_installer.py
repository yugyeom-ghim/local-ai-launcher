"""Split a complete offline EXE for GitHub transport, preserving its whole-file hash."""
import argparse
import json
from pathlib import Path
from build_full_release import split_archive


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--installer', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--base-url', required=True)
    parser.add_argument('--version', required=True)
    args = parser.parse_args()
    size = args.installer.stat().st_size
    if not 0 < size < 4294967295:
        raise ValueError('Standalone EXE must be below the Windows 4 GiB limit')
    split = split_archive(args.installer, args.output, args.base_url)
    manifest = dict(version=args.version, installer=split['archive'], parts=split['parts'])
    (args.output / 'installer-download-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    (args.output / 'INSTALLER_SHA256.txt').write_text(split['archive']['sha256'] + '  ' + args.installer.name + '\n', encoding='ascii')
    print('Single installer:', json.dumps(manifest['installer']), flush=True)


if __name__ == '__main__':
    main()
