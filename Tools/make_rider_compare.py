"""Builds Screens/rider_compare.png: study-only reference keyframes (top) vs game renders (bottom).
Output is git-ignored; reference frames are never imported into Assets/."""
from PIL import Image, ImageDraw, ImageFont
import os
K='/workspace/waterski-game/reference/motion/keyframes/'; S='/workspace/skiview-unity/Screens/'
refs=[('slalom_01_outbound-glide-handle-low.jpg','ref: outbound glide'),('slalom_07_load-lean-stacked.jpg','ref: load / stacked lean'),
      ('slalom_04_deep-turn-layback-max-spray.jpg','ref: deep turn'),('slalom_09_edge-change-handle-at-hip.jpg','ref: edge change')]
rend=[('rider_pull_side.png','game: pull, side',(560,120,1160,820)),('rider_pull_behind.png','game: pull, from behind',None),
      ('rider_carve_buoy.png','game: carve at buoy',(450,120,1150,820)),('rider_carve_exit.png','game: carve exit',None)]
W,H=420,420; F='/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'
f=ImageFont.truetype(F,18) if os.path.exists(F) else ImageFont.load_default()
out=Image.new('RGB',(W*4,H*2+70),(18,22,30)); d=ImageDraw.Draw(out)
def fit(im):
    im=im.convert('RGB'); r=max(W/im.width,H/im.height); im=im.resize((int(im.width*r)+1,int(im.height*r)+1))
    x=(im.width-W)//2; y=(im.height-H)//2; return im.crop((x,y,x+W,y+H))
for i,(fn,lab) in enumerate(refs):
    out.paste(fit(Image.open(K+fn)),(i*W,30)); d.text((i*W+8,36),lab,fill=(255,230,120),font=f)
for i,(fn,lab,box) in enumerate(rend):
    im=Image.open(S+fn)
    if box: im=im.crop(box)
    else:
        w,h=im.size; im=im.crop((w//2-380,h//2-380,w//2+380,h//2+380))
    out.paste(fit(im),(i*W,30+H+40)); d.text((i*W+8,30+H+46),lab,fill=(140,230,255),font=f)
d.text((8,6),'N1 SKI VIEW rider vs slalom reference — reference frames are STUDY ONLY (not in project / not shipped)',fill=(255,255,255),font=f)
d.text((8,30+H+12),'Game renders, pass 4 (whole-body lean from the edge force balance: one line ski edge to shoulders, head level, spray)',fill=(255,255,255),font=f)
import sys; OUT=sys.argv[1] if len(sys.argv)>1 else "rider_compare.png"
out.save(S+OUT,optimize=True); print(OUT, out.size, os.path.getsize(S+OUT))
