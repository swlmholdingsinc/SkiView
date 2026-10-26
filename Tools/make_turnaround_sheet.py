"""Pass 18: turnaround_v18 sheet - post-pass sequence at Ski View (home) and Saltwhite: sink holding the handle, DWS spray,
skiing round the turn island behind the boat, set-up into the gates (high chase), plus boat cam at the island and first-person DWS."""
import sys
from PIL import Image, ImageDraw, ImageFont
S = 'Screens/'; W, H = 480, 270
cols = [('1_sink', 'Sink, holding the handle (close chase)'), ('2_dws', 'Deep-water start spray (close chase)'), ('3_island', 'Skiing round the island'),
        ('4_setup', 'Set-up into the gates'), ('3b_island_boat', 'Boat cam, island'), ('2b_dws_first', 'First person, DWS')]
lakes = [('skiview', 'Ski View (home) - CCW round the south island'), ('saltwhite', 'Saltwhite - turn per Holly layout')]
try:
    F = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 15); FB = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 20)
except OSError:
    F = FB = ImageFont.load_default()
out = Image.new('RGB', (W * len(cols), 40 + len(lakes) * (H + 50)), (24, 26, 30)); d = ImageDraw.Draw(out)
d.text((10, 8), 'turnaround_v18 - end-of-pass sequence (sink -> DWS -> island turn -> set-up); tap skips to the set-up', font=FB, fill=(240, 240, 240))
for r, (lid, lt) in enumerate(lakes):
    y = 40 + r * (H + 50); d.text((10, y + 4), lt, font=FB, fill=(255, 210, 120))
    for c, (k, cap) in enumerate(cols):
        try:
            im = Image.open(f'{S}turn18_{lid}_{k}.png').convert('RGB').resize((W, H), Image.LANCZOS); out.paste(im, (c * W, y + 30))
        except FileNotFoundError:
            pass
        d.rectangle([c * W, y + 30 + H - 24, c * W + W, y + 30 + H], fill=(0, 0, 0)); d.text((c * W + 6, y + 30 + H - 21), cap, font=F, fill=(255, 255, 255))
out.save(S + 'turnaround_v18.png'); out.convert('RGB').save('/tmp/turn18_preview.jpg', quality=80); print('ok', out.size)
