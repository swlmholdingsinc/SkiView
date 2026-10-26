#!/usr/bin/env python3
"""b10.26: composite T29b Tricks results / landed / denied plates onto live scene shots (Shot has no UI Toolkit)."""
import json, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

def font(size):
    for p in (
        "/workspace/skiview-unity/Assets/SkiView/UI/Fonts/BarlowCondensed-Bold.ttf",
        "/workspace/skiview-unity/Assets/SkiView/UI/Fonts/Barlow-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
    ):
        if Path(p).exists():
            try: return ImageFont.truetype(p, size)
            except Exception: pass
    return ImageFont.load_default()

def paint_results(im, meta):
    W, H = im.size
    sx = W / 1920.0; sy = H / 1080.0
    x0 = int(96 * sx); pw = int(560 * sx); y0 = int(120 * sy); ph = int(700 * sy)
    scrim = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sd = ImageDraw.Draw(scrim)
    for i in range(x0 + pw + 80):
        a = int(140 * max(0, 1 - i / (x0 + pw + 80)))
        sd.line([(i, 0), (i, H)], fill=(7, 12, 18, a))
    im = Image.alpha_composite(im, scrim)
    panel = Image.new("RGBA", (pw, ph), (7, 12, 18, int(0.92 * 255)))
    d = ImageDraw.Draw(panel)
    d.rectangle([0, 0, 6, ph], fill=(31, 95, 214, 255))
    d.text((36, 24), "TRICK RESULT", fill=(174, 185, 198), font=font(max(18, int(22 * sx))))
    score = str(meta.get("total", 0))
    d.text((36, 64), score, fill=(255, 255, 255), font=font(max(72, int(110 * sx))))
    d.text((36 + int(len(score) * 62 * sx) + 8, 110), "PTS", fill=(230, 236, 242), font=font(max(28, int(40 * sx))))
    # b10.26: no gold TBD — pass line under score
    d.text((36, 190), meta.get("passLine", ""), fill=(174, 185, 198), font=font(max(14, int(18 * sx))))
    y = 230
    for line in meta.get("lines", []):
        col = (61, 214, 140) if line.startswith("✓") else ((255, 138, 138) if line.startswith("✕") else (230, 236, 242))
        d.text((36, y), line, fill=col, font=font(max(15, int(20 * sx))))
        y += int(28 * sy)
        if y > ph - 160: break
    y += 8
    d.text((36, y), meta.get("setup", ""), fill=(174, 185, 198), font=font(max(14, int(18 * sx))))
    y += int(36 * sy)
    d.text((36, y), meta.get("status", "BOTH PASSES COMPLETE"), fill=(174, 185, 198), font=font(max(14, int(18 * sx))))
    y += int(44 * sy)
    bw, bh = int(260 * sx), int(64 * sy + 16)
    d.rectangle([36, y, 36 + bw, y + bh], fill=(31, 95, 214, 255), outline=(255, 255, 255, 255), width=4)
    d.text((36 + 28, y + bh // 2 - 12), "TRICK AGAIN", fill=(255, 255, 255), font=font(max(18, int(24 * sx))))
    im.paste(panel, (x0, y0), panel)
    return im

def paint_toast(im, text, ok):
    W, H = im.size
    sx = W / 1920.0; sy = H / 1080.0
    pad_x, pad_y = int(28 * sx), int(18 * sy)
    f = font(max(28, int(40 * sx)))
    tw = int(len(text) * 22 * sx) + pad_x * 2
    th = int(64 * sy)
    x0 = (W - tw) // 2
    y0 = int(160 * sy)
    panel = Image.new("RGBA", (tw, th), (7, 12, 18, int(0.92 * 255)))
    d = ImageDraw.Draw(panel)
    strip = (61, 214, 140, 255) if ok else (255, 138, 138, 255)
    d.rectangle([0, 0, 6, th], fill=strip)
    d.text((pad_x, th // 2 - int(18 * sy)), text, fill=(255, 255, 255), font=f)
    im.paste(panel, (x0, y0), panel)
    return im

def paint_wordmark(im):
    W, H = im.size
    sx = W / 1920.0
    d = ImageDraw.Draw(im)
    d.text((int(48 * sx), int(28 * (H / 1080.0))), "SKI VIEW", fill=(255, 255, 255, 255), font=font(max(28, int(42 * sx))))
    return im

def main():
    if len(sys.argv) < 4:
        print("usage: composite_tricks_results_hud.py mode scene.png out.png [meta.json|toastText]"); sys.exit(2)
    mode, scene, out = sys.argv[1], sys.argv[2], sys.argv[3]
    im = Image.open(scene).convert("RGBA")
    if mode == "results":
        meta = json.loads(Path(sys.argv[4]).read_text())
        im = paint_results(im, meta)
    elif mode == "landed":
        text = sys.argv[4] if len(sys.argv) > 4 else "✓ Landed +80"
        im = paint_toast(im, text, True)
    elif mode == "denied":
        text = sys.argv[4] if len(sys.argv) > 4 else "✕ Denied · Rotation incomplete"
        im = paint_toast(im, text, False)
    elif mode == "wordmark":
        im = paint_wordmark(im)
    else:
        print("unknown mode", mode); sys.exit(2)
    Path(out).parent.mkdir(parents=True, exist_ok=True)
    im.convert("RGB").save(out, "PNG")
    print("wrote", out, mode, f"{im.size[0]}x{im.size[1]}")

if __name__ == "__main__":
    main()
