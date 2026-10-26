"""SWLM + 'Ski View' hull livery, matching cutline-app 1.2.0 boat.tsx nameTexture() (2048x256 on the hull blue band).
Barlow Condensed (SIL OFL). Output: Assets/SkiView/Generated/livery_swlm_skiview.png"""
from PIL import Image, ImageDraw, ImageFont
import os
FD = "/usr/share/fonts/truetype/sand-box/google/Barlow Condensed/"
BLUE = (13, 56, 158)       # = HullBlue material (0.05, 0.22, 0.62) in sRGB
WHITE = (244, 247, 250)
SS = 2                     # supersample
W, H = 2048 * SS, 256 * SS
img = Image.new("RGB", (W, H), BLUE)
d = ImageDraw.Draw(img)
def spaced(draw, x, y, text, font, spacing, fill):
    for ch in text:
        draw.text((x, y), ch, font=font, fill=fill, anchor="lm")
        x += draw.textlength(ch, font=font) + spacing
    return x
f800 = ImageFont.truetype(FD + "BarlowCondensed-ExtraBold.ttf", 140 * SS)
spaced(d, 80 * SS, 118 * SS, "SWLM", f800, 18 * SS, WHITE)
# slanted italic script, extra -0.28 skew like the canvas transform
lay = Image.new("L", (W, H), 0); ld = ImageDraw.Draw(lay)
fi = ImageFont.truetype(FD + "BarlowCondensed-BoldItalic.ttf", 112 * SS)
spaced(ld, 560 * SS, 122 * SS, "Ski View", fi, 6 * SS, 255)
k = 0.28   # x' = x - k*(y - 128): canvas transform(1,0,-0.28,1) about y=128
lay = lay.transform(lay.size, Image.AFFINE, (1, k, -k * 128 * SS, 0, 1, 0), resample=Image.BICUBIC)
img.paste(Image.new("RGB", (W, H), WHITE), (0, 0), lay)
# swoosh underline (two cubic beziers), pale teal
def bez(p0, p1, p2, p3, n=60):
    return [tuple(((1-t)**3)*a + 3*((1-t)**2)*t*b + 3*(1-t)*t*t*c + t**3*e for a, b, c, e in zip(p0, p1, p2, p3)) for t in (i / n for i in range(n + 1))]
pts = bez((560, 196), (760, 168), (900, 214), (1120, 186)) + bez((1120, 186), (1260, 168), (1360, 176), (1460, 170))
pts = [(x * SS, y * SS) for x, y in pts]
d.line(pts, fill=(197, 216, 212), width=9 * SS, joint="curve")
for p in (pts[0], pts[-1]):
    r = 4.5 * SS; d.ellipse((p[0]-r, p[1]-r, p[0]+r, p[1]+r), fill=(197, 216, 212))
f600 = ImageFont.truetype(FD + "BarlowCondensed-SemiBold.ttf" if os.path.exists(FD + "BarlowCondensed-SemiBold.ttf") else FD + "BarlowCondensed-Bold.ttf", 32 * SS)
c = tuple(int(0.75 * w + 0.25 * b) for w, b in zip(WHITE, BLUE))
spaced(d, 1680 * SS, 200 * SS, "WN 272", f600, 8 * SS, c)
img = img.resize((2048, 256), Image.LANCZOS)
out = os.path.join(os.path.dirname(__file__), "..", "Assets/SkiView/Generated/livery_swlm_skiview.png")
img.save(out); print("wrote", os.path.abspath(out))
