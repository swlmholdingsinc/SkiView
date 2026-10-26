#!/usr/bin/env python3
"""Upload Builds/WebGL (+ StreamingAssets) into Cloudflare KV as versioned chunks @VER#n, then
rewrite the Worker MAN to point at that version. Uses Cloudflare API token from env CF_API_TOKEN
or the MCP-equivalent path is separate — this script expects curl+token.

Actually: invoked with pre-exported token OR we print the MAN + chunk plan for the Cloudflare execute tool.
Default: write chunks to /tmp/sww-b10.7-chunks and emit MAN JSON + upload via cloudflare API if CF_API_TOKEN set.
"""
import os, sys, json, math, pathlib, hashlib, urllib.request

ROOT = pathlib.Path('/workspace/skiview-unity/Builds/WebGL')
VER = os.environ.get('SWW_VER', 'b10.7')
ACCOUNT = '7d748af0357d43eedb92dde5713e8dfb'
NS = '73693fecbd424207b99702c87ead93e9'
CHUNK = 3 * 1024 * 1024
WORKER = 'sww-8f839bf2fa5365caf948'

def files():
    out = []
    for p in sorted(ROOT.rglob('*')):
        if not p.is_file(): continue
        rel = '/' + str(p.relative_to(ROOT)).replace('\\', '/')
        out.append((rel, p))
    return out

def main():
    plan = {}
    chunks_dir = pathlib.Path(f'/tmp/sww-{VER}-chunks')
    chunks_dir.mkdir(parents=True, exist_ok=True)
    manifest_entries = {}
    total_chunks = 0
    for rel, path in files():
        data = path.read_bytes()
        n = max(1, math.ceil(len(data) / CHUNK))
        base = f'{rel}@{VER}'
        for i in range(n):
            part = data[i*CHUNK:(i+1)*CHUNK]
            (chunks_dir / f'{hashlib.sha1(f\"{base}#{i}\".encode()).hexdigest()}.bin').write_bytes(part)
            # also write a map file
            total_chunks += 1
        # write chunk files with readable names for the uploader
        for i in range(n):
            part = data[i*CHUNK:(i+1)*CHUNK]
            safe = base.strip('/').replace('/', '__') + f'__{i}.bin'
            (chunks_dir / safe).write_bytes(part)
        manifest_entries[rel] = [len(data), n, base]
        print(f'{rel}: {len(data)} bytes → {n} chunks @ {base}')
    man_path = chunks_dir / 'MAN.json'
    man_path.write_text(json.dumps(manifest_entries, indent=2))
    print(f'Wrote {man_path} ({len(manifest_entries)} files, {total_chunks} chunks)')
    print(f'CHUNK_DIR={chunks_dir}')
    return 0

if __name__ == '__main__':
    sys.exit(main())
