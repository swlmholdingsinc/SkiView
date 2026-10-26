"""Pass 17: Saltwhite water edge = Holly shore_build widened ~15 m on the SW bank, plus the two end bulbs round the islands
measured off the study aerial (path read from the staging refs file outside the repo). Writes Tools/lake_data/saltwhite_shore.json."""
import json,numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
d=json.load(open('/workspace/waterski-game/roadmap/venue/lakes/saltwhite_alt_layout_m.json'))
w=np.array(json.load(open('/workspace/skiview-unity/Docs/lakes/saltwhite_shore_build_widened15.json'))['shore_build_widened'])
du=np.array(d['course']['dir_unit']); pu=np.array([-du[1],du[0]])
H=400; S=2  # 0.5 m px
def mask(poly):
  im=Image.new('L',(2*H*S,2*H*S),0); ImageDraw.Draw(im).polygon([((x+H)*S,(y+H)*S) for x,y in poly],fill=1); return np.array(im,bool)
mw=mask(w)
refs=dict(l.strip().split('=',1) for l in open('/workspace/skiview-assets-staging/lake_refs_saltwhite.txt') if '=' in l)   # study aerial path lives outside the repo
im=np.array(Image.open(refs['aerial']).convert('RGB')).astype(float)
cx,cy=im.shape[1]/2+float(refs['aerial_origin_east']),im.shape[0]/2-float(refs['aerial_origin_north'])
r,g,b=im[...,0],im[...,1],im[...,2]
wat=(g>r+8)&(b>r)&(r<70)&(g<110)
lab,n=ndimage.label(wat); ma=lab==lab[int(cy),int(cx)]
ma=ndimage.binary_fill_holes(ndimage.binary_closing(ma,iterations=3)); ma=ndimage.binary_opening(ma,iterations=3)
# resample aerial mask into the 0.5 m grid
yy,xx=np.mgrid[0:2*H*S,0:2*H*S]; X=xx/S-H; Y=yy/S-H
ma2=ma[np.clip((Y+cy).astype(int),0,1599),np.clip((X+cx).astype(int),0,1599)]
A=X*du[0]+Y*du[1]; P=X*pu[0]+Y*pu[1]
st=np.arange(-340,300,2.0); lo=[];hi=[];keep=[]
for a in st:
  s=np.abs(A-a)<0.5
  pw=P[s&mw]; pa=P[s&ma2]
  bl=np.clip((abs(a)-172)/20,0,1)
  cand=[]
  if len(pw): l0,h0=pw.min(),pw.max()
  if len(pa): l1,h1=pa.min(),pa.max()
  if len(pw) and len(pa): l=min(l0,l0+(l1-l0)*bl) if bl>0 else l0; h=max(h0,h0+(h1-h0)*bl) if bl>0 else h0
  elif len(pw): l,h=l0,h0
  elif len(pa) and bl>0.99: l,h=l1,h1
  else: continue
  if h-l<2: continue
  lo.append(l);hi.append(h);keep.append(a)
keep=np.array(keep);lo=ndimage.gaussian_filter1d(np.array(lo),6.0,mode='nearest');hi=ndimage.gaussian_filter1d(np.array(hi),6.0,mode='nearest')
ring=[(a,l) for a,l in zip(keep,lo)]+[(a,h) for a,h in zip(keep[::-1],hi[::-1])]
ring=np.array([a*du+p*pu for a,p in ring])
print(len(ring),'along',keep.min(),keep.max())
for a in range(-300,300,30):
  i=np.argmin(abs(keep-a)); print(a,'p %.0f..%.0f'%(lo[i],hi[i]))
json.dump({'layout':'saltwhite_alt_layout_m.json','note':'Saltwhite water edge, layout frame (+x east, +y south, metres, origin = course centre). Holly saltwhite_alt shore_build + ~15 m SW bank widening (Docs/lakes/saltwhite_shore_build_widened15.json); the two end bulbs round the islands (|along| > 172 m, blended over 20 m) are read off the study aerial because the shore_build trace cut the NW island bulb out. Pass 17.',
 'source':'saltwhite','shore_build':[[round(float(x),2),round(float(y),2)] for x,y in ring]},open('/workspace/skiview-unity/Tools/lake_data/saltwhite_shore.json','w'))
out=Image.new('RGB',(2*H*S,2*H*S),(20,20,20)); dr=ImageDraw.Draw(out)
dr.polygon([((x+H)*S,(y+H)*S) for x,y in ring],fill=(40,90,120))
for ip in d['island_polys']: dr.polygon([((x+H)*S,(y+H)*S) for x,y in ip],fill=(220,210,180))
out.resize((800,800)).save('/tmp/p17/saltwhite_shore_preview.png')
