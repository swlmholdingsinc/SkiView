#!/usr/bin/env python3
"""b10.27 T24 composites: safe-area HUD overlay, slalom results TITLE plate, picker annotate."""
import sys
from pathlib import Path
try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    import subprocess
    subprocess.check_call([sys.executable, '-m', 'pip', 'install', 'pillow', '-q'])
    from PIL import Image, ImageDraw, ImageFont

def font(size):
    for p in ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
              "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"):
        if Path(p).exists():
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()

def hud(src, dst):
    im = Image.open(src).convert('RGBA')
    w, h = im.size
    # phone mins 88/24 on 1920x1080 ref; draw magenta guides
    L, T, R, B = 88, 24, 88, 24
    ov = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    d.rectangle([L, T, w - R - 1, h - B - 1], outline=(255, 0, 255, 220), width=3)
    # CAM hit 144 bottom-right inside safe
    cam = [w - R - 144, h - B - 144, w - R, h - B]
    d.rectangle(cam, outline=(0, 255, 180, 230), width=3)
    # Pause hit 144 top-right
    pause = [w - R - 144, T, w - R, T + 144]
    d.rectangle(pause, outline=(0, 200, 255, 230), width=3)
    d.text((L + 12, T + 12), "SAFE 88/24  ·  CAM/Pause hit 144", fill=(255, 0, 255, 255), font=font(28))
    d.text((cam[0] + 8, cam[1] + 8), "CAM", fill=(0, 255, 180, 255), font=font(22))
    d.text((pause[0] + 8, pause[1] + 8), "PAUSE", fill=(0, 200, 255, 255), font=font(22))
    Image.alpha_composite(im, ov).convert('RGB').save(dst, 'PNG')
    print('wrote', dst)

def results(src, dst):
    im = Image.open(src).convert('RGBA')
    w, h = im.size
    plate = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(plate)
    # left card like sv-results19
    x0, y0, x1, y1 = 48, 120, 700, 900
    d.rectangle([x0, y0, x1, y1], fill=(7, 12, 18, 230))
    d.rectangle([x0, y0, x0 + 8, y1], fill=(31, 95, 214, 255))
    d.text((x0 + 28, y0 + 24), "PASS COMPLETE", fill=(174, 185, 198, 255), font=font(22))
    d.text((x0 + 28, y0 + 70), "3.00 @ 15 OFF", fill=(255, 255, 255, 255), font=font(48))
    d.text((x0 + 28, y0 + 140), "b10.27 Gap A — TITLE → ToTitle", fill=(230, 236, 242, 255), font=font(24))
    # buttons
    by = y1 - 200
    for i, (lab, col) in enumerate([("NEXT PASS", (31, 95, 214, 255)), ("RETRY PASS", (40, 48, 58, 255)), ("TITLE", (40, 48, 58, 255))]):
        bx0 = x0 + 28 + i * 210
        d.rectangle([bx0, by, bx0 + 190, by + 72], fill=col)
        d.text((bx0 + 24, by + 22), lab, fill=(255, 255, 255, 255), font=font(22))
    d.text((x0 + 28, by + 90), "Esc on rope-choice → this plate (Gap B)", fill=(174, 185, 198, 255), font=font(20))
    Image.alpha_composite(im, plate).convert('RGB').save(dst, 'PNG')
    print('wrote', dst)

def picker(src, dst):
    im = Image.open(src).convert('RGBA')
    w, h = im.size
    ov = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    d.rectangle([20, 20, w - 20, 110], fill=(7, 12, 18, 210))
    d.text((36, 36), "b10.27 T28/T24 — rows ≥100 phone · Done ≥144 · focus≠selected · _db heroes", fill=(255, 255, 255, 255), font=font(26))
    Image.alpha_composite(im, ov).convert('RGB').save(dst, 'PNG')
    print('wrote', dst)

def main():
    if len(sys.argv) < 4:
        print('usage: composite_t24_ui.py hud|results|picker SRC DST'); return 2
    cmd, src, dst = sys.argv[1], sys.argv[2], sys.argv[3]
    Path(dst).parent.mkdir(parents=True, exist_ok=True)
    {'hud': hud, 'results': results, 'picker': picker}[cmd](src, dst)
    return 0

if __name__ == '__main__':
    raise SystemExit(main())
