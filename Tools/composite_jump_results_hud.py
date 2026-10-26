#!/usr/bin/env python3
"""b10.19: composite T32-style Jump results HUD onto a live scene shot using live meta JSON."""
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

def main(scene_png, meta_json, out_png):
    meta = json.loads(Path(meta_json).read_text())
    im = Image.open(scene_png).convert("RGBA")
    W, H = im.size
    # T32 desktop: left 96, width 560 on ~1920; scale to capture W
    sx = W / 1920.0
    x0 = int(96 * sx); pw = int(560 * sx); y0 = int(120 * (H / 1080.0)); ph = int(640 * (H / 1080.0))
    # left scrim
    scrim = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sd = ImageDraw.Draw(scrim)
    for i in range(x0 + pw + 80):
        a = int(140 * max(0, 1 - i / (x0 + pw + 80)))
        sd.line([(i, 0), (i, H)], fill=(7, 12, 18, a))
    im = Image.alpha_composite(im, scrim)
    # panel
    panel = Image.new("RGBA", (pw, ph), (7, 12, 18, int(0.90 * 255)))
    d = ImageDraw.Draw(panel)
    d.rectangle([0, 0, 6, ph], fill=(31, 95, 214, 255))  # strip
    best_ft = meta.get("best_ft")
    best_m = meta.get("best_m")
    unit = meta.get("unit", "ft")
    score = str(best_ft if unit == "ft" else f"{best_m:.1f}")
    suffix = "ft" if unit == "ft" else "m"
    d.text((36, 28), "JUMP RESULT", fill=(174, 185, 198), font=font(max(18, int(22 * sx))))
    d.text((36, 70), score, fill=(255, 255, 255), font=font(max(72, int(110 * sx))))
    # suffix near score
    d.text((36 + int(len(score) * 62 * sx) + 8, 120), suffix, fill=(230, 236, 242), font=font(max(28, int(40 * sx))))
    d.text((36, 210), "longest of 3", fill=(174, 185, 198), font=font(max(16, int(20 * sx))))
    y = 250
    for line in meta.get("passes", []):
        d.text((36, y), line, fill=(230, 236, 242), font=font(max(16, int(22 * sx))))
        y += int(34 * (H / 1080.0) + 8)
    y += 8
    d.text((36, y), meta.get("setup", ""), fill=(174, 185, 198), font=font(max(14, int(18 * sx))))
    y += 48
    # JUMP AGAIN button
    bw, bh = int(240 * sx), int(64 * (H / 1080.0) + 20)
    d.rectangle([36, y, 36 + bw, y + bh], fill=(31, 95, 214, 255), outline=(255, 255, 255, 255), width=4)
    d.text((36 + 28, y + bh // 2 - 12), "JUMP AGAIN", fill=(255, 255, 255), font=font(max(18, int(24 * sx))))
    im.paste(panel, (x0, y0), panel)
    Path(out_png).parent.mkdir(parents=True, exist_ok=True)
    im.convert("RGB").save(out_png, "PNG")
    print("wrote", out_png, f"{W}x{H}", "best", score, suffix)

if __name__ == "__main__":
    if len(sys.argv) != 4:
        print("usage: composite_jump_results_hud.py scene.png meta.json out.png"); sys.exit(2)
    main(sys.argv[1], sys.argv[2], sys.argv[3])
