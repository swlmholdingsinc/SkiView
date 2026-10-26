"""Generate SKI VIEW N1 venue data from roadmap/venue/skiview_layout_m.json.
Outputs (Assets/SkiView/Generated): heightmap.r16, splat.png, venue.json, textures.
World frame (Unity): +X east, +Z north, Y up. Venue frame: +x east, +y south -> unity z = -y.
Origin = slalom course centre, water surface at y=0."""
import json, math, numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
SRC='/workspace/waterski-game/roadmap/venue/skiview_layout_m.json'
OUT='/workspace/skiview-unity/Assets/SkiView/Generated/'
rng=np.random.default_rng(8017)
d=json.load(open(SRC))
shore=np.array(d['shore']); shore[:,1]*=-1            # -> unity (x,z)
isl=[np.array(p)*[1,-1] for p in d['island_polys']]
# ---- Scott, Oct 17 2026: main ski water is 2,200 ft x 350 ft (670.6 m x 106.7 m). Rescale the aerial trace
# anisotropically in the course frame: along-axis so the lake's tip-to-tip length = 670.6 m, across-axis so the
# mean width of the straight channel = 106.7 m. Then centre the slalom course between the two islands.
_dvx,_dvy=d['course']['dir_unit']; _f=np.array([_dvx,-_dvy]); _r=np.array([-_f[1],_f[0]])
def _loc(p): return np.stack([p@_f, p@_r],1)
def _inpoly(pt,poly):
    x,y=pt; xs,ys=poly[:,0],poly[:,1]; j=np.roll(np.arange(len(xs)),1)
    c=((ys>y)!=(ys[j]>y))&(x<(xs[j]-xs)*(y-ys)/(ys[j]-ys+1e-12)+xs); return c.sum()%2==1
sl0=_loc(shore); il0=[_loc(p) for p in isl]
amin,amax=sl0[:,0].min(),sl0[:,0].max()
widths=[]
for a_ in np.linspace(amin+0.3*(amax-amin),amax-0.3*(amax-amin),15):
    ls=np.arange(-160,160,0.5); w=0
    for l_ in ls:
        q=np.array([a_,l_]); w+=0.5 if (_inpoly(q,sl0) and not any(_inpoly(q,ii) for ii in il0)) else 0
    widths.append(w)
W0=float(np.mean(widths)); L0=float(amax-amin)
SA=670.6/L0; SC=106.7/W0
ic=[ii.mean(0)*[SA,SC] for ii in il0]
A_SHIFT=float((ic[0][0]+ic[1][0])/2)       # course centre midway between the island centres (along axis)
def _xf(p):
    q=_loc(p); a_=q[:,0]*SA-A_SHIFT; l_=q[:,1]*SC
    return np.outer(a_,_f)+np.outer(l_,_r)
shore=_xf(shore); isl=[_xf(p) for p in isl]
# Narrow spot at real scale: the north island's far-side (tip) gap is only ~16 m. Widen it to ~20 m by shifting the
# north island 4 m toward the course (its near edge then sits 256 m from centre, the same as the south island).
NORTH_ISLAND_SHIFT_M=4.0
isl=[p+_f*NORTH_ISLAND_SHIFT_M if (p@_f).mean()<0 else p for p in isl]
SCALE_INFO={'north_island_shift_m':4.0,'aerial_length_m':round(L0,1),'aerial_mean_channel_width_m':round(W0,1),'along_scale':round(SA,4),'across_scale':round(SC,4),'course_shift_m':round(A_SHIFT,1)}
print('scale',SCALE_INFO)
TS=1600.0; RES=1025; HALF=TS/2; PX=TS/(RES-1)       # terrain covers x,z in [-800,800]
def to_px(p): return [((x+HALF)/PX, (HALF-z)/PX) for x,z in p]   # image row 0 = north (z=+800)
def mask_of(poly):
    im=Image.new('L',(RES,RES),0); ImageDraw.Draw(im).polygon(to_px(poly),fill=1); return np.array(im,bool)
lake=mask_of(shore); islands=np.zeros_like(lake)
for p in isl: islands|=mask_of(p)
water=lake&~islands
dist_in=ndimage.distance_transform_edt(water)*PX      # metres from shore, inside water
dist_out=ndimage.distance_transform_edt(~water)*PX    # metres from shore, on land
# coordinates per pixel
cols=np.arange(RES)*PX-HALF; rows=HALF-np.arange(RES)*PX
X,Z=np.meshgrid(cols,rows)
def fbm(shape,octaves,base,seed):
    r=np.random.default_rng(seed); acc=np.zeros(shape); amp=1; tot=0
    for o in range(octaves):
        n=r.standard_normal((max(2,int(shape[0]/base*2**o)),)*2)
        n=ndimage.zoom(n,shape[0]/n.shape[0],order=3)[:shape[0],:shape[1]]
        acc+=n*amp; tot+=amp; amp*=0.5
    return acc/tot
dvx,dvy=d['course']['dir_unit']; fS=np.array([dvx,-dvy])
hills=fbm((RES,RES),5,8,1)
# east side (house bank) rises toward Ski View Ln; west levee then forest; far hills (fir ridge)
east=(X>fS[0]*Z/fS[1]+12) if 'fS' in dir() else None
h=np.zeros((RES,RES))
land=dist_out
base=np.where(east, 0.4+np.clip(land,0,120)*0.045, 0.4+np.clip(land,0,10)*0.16+np.clip(land-10,0,200)*0.01)
far=np.clip((land-120)/400,0,1)
h=base+far*(14+hills*10)+np.clip(land-15,0,60)/60*hills*1.2
h=np.where(water, -np.clip(dist_in,0,14)/14*3.6-0.25, h)
h=np.where(islands, 0.5+np.clip(ndimage.distance_transform_edt(islands)*PX,0,12)*0.12, h)
h=ndimage.gaussian_filter(h,0.8)
YMIN=-6.0; HRANGE=48.0
r16=np.clip((h-YMIN)/HRANGE,0,1)
# Unity heightmap raw: row 0 = south (z=-800) when flipped vertically; we write bottom-to-top
(np.flipud(r16)*65535).astype('<u2').tofile(OUT+'heightmap.r16')
# splat: R lawn, G dry grass/levee, B forest floor, A mud/rock shoreline
ZMIN,ZMAX=shore[:,1].min(),shore[:,1].max()
lawn=east&(land<95)&(land>1.5)&(Z>ZMIN-30)&(Z<ZMAX+10)
levee=(~east)&(land>1.0)&(land<26)
mud=(land<=2.2)&~water
forest=~(lawn|levee|mud)
spl=np.zeros((RES,RES,4))
spl[...,0]=lawn; spl[...,1]=levee; spl[...,2]=forest; spl[...,3]=mud
spl[islands&~mud]=[0,0,1,0]
spl=ndimage.gaussian_filter(spl,(1.2,1.2,0)); spl/=spl.sum(-1,keepdims=True)+1e-6
Image.fromarray((spl*255).astype(np.uint8),'RGBA').save(OUT+'splat.png')

def sample(arr,x,z):
    c=int(round((x+HALF)/PX)); r=int(round((HALF-z)/PX))
    return arr[min(max(r,0),RES-1),min(max(c,0),RES-1)]
def hgt(x,z): return float(sample(h,x,z))

# ---------- slalom course ----------
dvx,dvy=d['course']['dir_unit']; fS=np.array([dvx,-dvy])  # southbound forward (unity x,z)
def P(s): return fS*s          # point on course line, s measured southbound from centre
# ---------- boat loop: tree-side (west) gap first at both islands (Scott, Oct 17 2026) ----------
# north island: clockwise (heading north turn LEFT, up the west gap, round the tip, back south through the east gap)
# south island: counter-clockwise (heading south turn RIGHT, down the west gap, round the tip, back north through the east gap)
R_=np.array([-fS[1],fS[0]])
def W(a_,l_): return fS*a_+R_*l_
def L_(p): return np.array([p@fS, p@R_])
def island_geo(poly):
    q=np.array([L_(p) for p in poly]); ac,lc=q.mean(0)
    amn,amx=q[:,0].min(),q[:,0].max(); lmn,lmx=q[:,1].min(),q[:,1].max()
    def scan(a_,l_,da,dl):
        k=0
        while sample(water,*W(a_+da*k,l_+dl*k)) and k<400: k+=1
        return k*0.5
    gw=scan(ac,lmn-1.5,0,-0.5)+1.0; ge=scan(ac,lmx+1.5,0,0.5)+1.0
    tip_sign=1 if ac>0 else -1
    tipA=amx if ac>0 else amn
    gt=scan(tipA+tip_sign*1.5,lc,tip_sign*0.5,0)+1.0   # start 1.5 m out (raster edge pixels)
    return dict(ac=ac,lc=lc,ha=(amx-amn)/2,amn=amn,amx=amx,lmn=lmn,lmx=lmx,gw=gw,ge=ge,gt=gt,
                lw=lmn-gw/2, le=lmx+ge/2, tip=tipA+tip_sign*gt/2)
GI=[island_geo(p) for p in isl]; Nn=[g for g in GI if g['ac']<0][0]; Ss=[g for g in GI if g['ac']>0][0]
for nm,g in (('north',Nn),('south',Ss)):
    print('%s island: centre a=%.0f l=%.0f, len %.0f, west gap %.1f m, east gap %.1f m, tip gap %.1f m, near edge %.0f m from course centre'%(
        nm,g['ac'],g['lc'],2*g['ha'],g['gw'],g['ge'],g['gt'], g['amn'] if g['ac']>0 else -g['amx']))
def turn(g,sgn):
    near = g['amn'] if sgn>0 else g['amx']        # island edge facing the course
    far  = g['amx'] if sgn>0 else g['amn']
    mid  = (g['lc'])
    return [W(near-sgn*20, g['lw']*0.45), W(near+sgn*g['ha']*0.25, g['lw']), W(far-sgn*g['ha']*0.25, g['lw']),
            W(g['tip']-sgn*g['gt']*0.15, (g['lw']+mid)/2), W(g['tip'], mid), W(g['tip']-sgn*g['gt']*0.15, (g['le']+mid)/2),
            W(far-sgn*g['ha']*0.25, g['le']), W(near+sgn*g['ha']*0.25, g['le']), W(near-sgn*14, g['le']*0.35)]
COURSE_END=214
wp=[W(a_,0) for a_ in np.linspace(-COURSE_END,COURSE_END,17)]   # southbound
wp+=turn(Ss,+1)
wp+=[W(a_,0) for a_ in np.linspace(COURSE_END,-COURSE_END,17)] # northbound
wp+=turn(Nn,-1)
wp=np.array(wp)
def catmull(pts,n=8):
    out=[]; N=len(pts)
    for i in range(N):
        p0,p1,p2,p3=pts[i-1],pts[i],pts[(i+1)%N],pts[(i+2)%N]
        for t in np.linspace(0,1,n,endpoint=False):
            t2,t3=t*t,t*t*t
            out.append(0.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t2+(-p0+3*p1-3*p2+p3)*t3))
    return np.array(out)
for _ in range(2):    # light smoothing of the control polygon
    wp=0.25*np.roll(wp,1,0)+0.5*wp+0.25*np.roll(wp,-1,0) if _==1 else wp
pth=catmull(wp,10)
# resample at 1 m
seg=np.linalg.norm(np.diff(np.vstack([pth,pth[:1]]),axis=0),axis=1); cum=np.concatenate([[0],np.cumsum(seg)])
L=cum[-1]; ss=np.arange(0,L,1.0)
px=np.interp(ss,cum,np.append(pth[:,0],pth[0,0])); pz=np.interp(ss,cum,np.append(pth[:,1],pth[0,1]))
path=np.stack([px,pz],1)
# clearance from shore and islands
clear=np.array([sample(dist_in,x,z) for x,z in path])
gy,gx=np.gradient(dist_in)   # d/drow, d/dcol
def relax(path):
    for it in range(60):
        al=path@fS; la=path@np.array([-fS[1],fS[0]])
        free=~((np.abs(al)<=COURSE_END)&(np.abs(la)<5))
        for i in np.where(free)[0]:
            x,z=path[i]; c=sample(dist_in,x,z)
            if c<11:
                g=np.array([sample(gx,x,z), -sample(gy,x,z)]); n=np.linalg.norm(g)
                if n>1e-6: path[i]+=g/n*min(1.0,(11-c)*0.4)
        sm=0.25*np.roll(path,1,0)+0.5*path+0.25*np.roll(path,-1,0)
        path[free]=sm[free]
    return path
path=relax(path)
px,pz=path[:,0].copy(),path[:,1].copy()
clear=np.array([sample(dist_in,x,z) for x,z in path])
# along-course coordinate for each sample (for the speed profile and pass segments)
along=path@fS; lat=path@np.array([-fS[1],fS[0]])
hd0=np.arctan2(np.gradient(px),np.gradient(pz))
sgn=np.sign(np.gradient(px)*fS[0]+np.gradient(pz)*fS[1])
prog=along*sgn
oncourse=(prog>=-COURSE_END)&(prog<=184.5)&(np.abs(lat)<6)
V_PASS=34*0.44704; V_TURN=23*0.44704; V_GAP=25/3.6
# heading & curvature
hd=np.arctan2(np.gradient(px),np.gradient(pz)); dh=np.gradient(np.unwrap(hd)); curv=np.abs(dh)/1.0
vt=np.where(oncourse,V_PASS,np.minimum(V_TURN, np.sqrt(3.6/np.maximum(curv,1e-4))))
vt=np.maximum(vt,V_GAP*0.9)
v=vt.copy(); n=len(v)
for _ in range(3):
    for i in range(1,2*n): v[i%n]=min(v[i%n], math.sqrt(v[(i-1)%n]**2+2*3.0*1.0))     # accel limit (tournament inboard ~3 m/s2)
    for i in range(2*n,0,-1):
        k=(i-1)%n
        if not oncourse[k]: v[k]=min(v[k], math.sqrt(v[i%n]**2+2*2.5*1.0)) # decel limit (never inside the pass)
for i in range(1,2*n):
    k=i%n
    if not oncourse[k]: v[k]=max(v[k], math.sqrt(max(0,v[(i-1)%n]**2-2*2.0*1.0)))   # ease off over ~60 m after the exit pre-gates
# the speed must be at V_PASS from 50 m before the pre-gates (|along|<=235): verify
passIdx=np.where(oncourse&(prog>=-184.5)&(prog<=184.5))[0]
print('loop length %.0f m, min clearance %.1f m at %s, pass speed min on course %.2f m/s'%(L,clear.min(),path[clear.argmin()].round(1),v[passIdx].min()))
print('lap time %.1f s'%np.sum(1.0/v))
inpass=oncourse&(np.abs(prog)<=184.5)
tS=np.sum(1.0/v[(~inpass)&(along>0)]); tN=np.sum(1.0/v[(~inpass)&(along<0)])
print('turn time (exit pre-gate -> entry pre-gate): south %.1f s, north %.1f s; pass %.1f s each'%(tS,tN,np.sum(1.0/v[inpass])/2))
# full speed reached how far before the entry pre-gate?
fullAt=[]
for sg in (1,-1):
    idx=np.where((sgn==sg)&(np.abs(lat)<6)&(prog<=-184.5)&(prog>=-COURSE_END-60))[0]
    ok=[prog[i] for i in idx if v[i]>=V_PASS-0.05]
    fullAt.append(round(float(-184.5-min(ok)),1) if ok else None)
print('full 34 mph reached this many m before the entry pre-gate (southbound, northbound):',fullAt)
TURN_INFO={'south_turn_s':round(float(tS),1),'north_turn_s':round(float(tN),1),'full_speed_before_pregate_m':fullAt}
bad=np.where(clear<8)[0]
print('low clearance samples',len(bad), [ (int(i),path[i].round(0).tolist(),round(float(clear[i]),1)) for i in bad[::15]])
lowv=np.where(oncourse&(prog>=-184.5)&(prog<=184.5)&(v<V_PASS-0.05))[0]
dbg=[] and [(int(i),round(float(prog[i])),round(float(lat[i]),1),round(float(v[i]),1)) for i in range(len(v)) if 170<abs(prog[i])<230 and abs(lat[i])<10][::6]
print('ends',dbg)
print('slow on course', [(int(i),round(float(along[i])),round(float(v[i]),1)) for i in lowv[::10]])
# ---------- jump ramp (JUMP-TRICKS-SPEC §1.1-1.2; Scott: south end, house/east side) ----------
# 0.255 ratio, 6.85 m surface -> 1.75 m lip, width 4.22 m, 45 deg navy aprons; centreline 15 m east of the course
# line; skied southbound (lip at the south end); opened +1.75 deg (low end turned toward the course).
RAMP_LIP_A=45.3   # pass 19 fit test: Holly's candidate ramp centre (8.2, 41.9) -> lip 3.4 m further south (was 158 m: no run-out before the south island)
lip=W(RAMP_LIP_A,15.0)
RAMP={'lip':[float(lip[0]),float(lip[1])],'fwd':[float(fS[0]),float(fS[1])],'open_deg':1.75,'ratio':0.255,'length':6.85,'height':1.75,'width':4.22,
      'lip_along_m':RAMP_LIP_A,'offset_m':15.0,'note':'placeholder, not jumpable yet'}
print('ramp lip at a=%.0f m, %.0f m before the south island near edge; water to east shore at ramp %.1f m'%(RAMP_LIP_A,Ss['amn']-RAMP_LIP_A,
      sample(dist_in,*W(RAMP_LIP_A,15.0))))
# ---------- houses on the EAST bank, north->south ----------
order=['8017','8035','8049','8107','8121','8143','8201','8215','8221','8229','8235','8311','8317','8405','8415','8419','8505']
arche={'8017':'HERO','8049':'A','8221':'B','8229':'C','8311':'D','8405':'E','8415':'F','8419':'G'}
fill=['A','D','E','D','A','E','D','A','E']
# east shoreline points: from the shore polygon, x east of the course line, between z=+330 and z=-430
def east_of(x,z,m=18): return x>fS[0]*z/fS[1]+m
es=[p for p in shore if east_of(p[0],p[1]) and ZMIN+5<p[1]<ZMAX-5]
es=np.array(sorted(es,key=lambda p:-p[1]))
sl=np.concatenate([[0],np.cumsum(np.linalg.norm(np.diff(es,axis=0),axis=1))])
houses=[]; fi=0
cols_pal=[[0.36,0.38,0.40],[0.42,0.50,0.58],[0.13,0.20,0.36],[0.86,0.84,0.78],[0.20,0.21,0.22],[0.55,0.47,0.38],[0.70,0.72,0.70]]
for i,a in enumerate(order):
    t=(i+0.5)/len(order)*sl[-1]*0.96+sl[-1]*0.02
    x=np.interp(t,sl,es[:,0]); z=np.interp(t,sl,es[:,1])
    j=np.searchsorted(sl,t); j=min(max(j,1),len(es)-1)
    tang=es[j]-es[j-1]; tang/=np.linalg.norm(tang)
    nrm=np.array([tang[1],-tang[0]])        # points to land (east)?
    if nrm[0]<0: nrm=-nrm
    set_back=34 if a!='8017' else 30
    hx,hz=x+nrm[0]*set_back, z+nrm[1]*set_back
    yaw=math.degrees(math.atan2(-nrm[0],-nrm[1]))   # house front faces the water
    ar=arche.get(a) or fill[fi%len(fill)]
    if a not in arche: fi+=1
    houses.append({'lot':a,'arch':ar,'x':round(float(hx),2),'z':round(float(hz),2),'y':round(hgt(hx,hz),2),'yaw':round(yaw,1),
      'shore':[round(float(x),2),round(float(z),2)],'normal':[round(float(nrm[0]),3),round(float(nrm[1]),3)],
      'color':cols_pal[(i*3)%len(cols_pal)],'dock':a not in('8035',)})
# ---------- trees ----------
trees=[]
cand=rng.uniform(-780,780,(60000,2))
cand=np.vstack([cand, np.array([p.mean(0) for p in isl]).repeat(400,0)+rng.normal(0,14,(800,2))])
for x,z in cand:
    lo=sample(dist_out,x,z)
    if sample(water,x,z) or lo<6: continue
    isl_=sample(islands,x,z)
    e=east_of(x,z,20)
    # density: islands dense, west forest dense, east lots sparse, far ridge dense
    if isl_: p=1.0
    elif e and lo<110 and ZMIN-30<z<ZMAX+10:
        # keep lawns open; firs only along lot lines / behind houses
        near=min(math.hypot(x-hh['x'],z-hh['z']) for hh in houses)
        p=0.0 if near<24 else (0.08 if lo<60 else 0.30)
    elif e and lo<135: p=0.03   # road corridor
    else: p=0.42 if lo<260 else (0.16 if lo<560 else 0.08)
    if rng.random()<p:
        trees.append([round(float(x),1),round(hgt(x,z),2),round(float(z),1),round(float(rng.uniform(0.7,1.35)),2)])
print('trees',len(trees))
venue={'note':'Generated by Tools/gen_venue.py from roadmap/venue/skiview_layout_m.json (aerial scale 0.38 m/px +-15%). Unity frame: +X east, +Z north.',
 'terrain':{'size':TS,'res':RES,'ymin':YMIN,'hrange':HRANGE},
 'course':{'fS':[float(fS[0]),float(fS[1])],'length':259,'firstBuoy':27,'spacing':41,'buoyOffset':11.5,'guideOffset':1.15,'gateOffset':1.25,'preGate':55},
 'path':{'step':1.0,'x':[round(float(a),2) for a in px],'z':[round(float(a),2) for a in pz],'v':[round(float(a),2) for a in v],
         'along':[round(float(a),2) for a in along],'dir':[int(a) for a in sgn]},
 'scale':SCALE_INFO,'turns':TURN_INFO,'islandGeo':[{k:round(float(v_),2) for k,v_ in g.items()} for g in (Nn,Ss)],
 'ramp':RAMP,'lakeBounds':[float(shore[:,0].min()-15),float(shore[:,0].max()+15),float(ZMIN-15),float(ZMAX+15)],
 'islandCentres':[float(v) for p in isl for v in p.mean(0)],
 'houses':houses,'treesFlat':[v for t in trees for v in t]}
json.dump(venue,open(OUT+'venue.json','w'))
# preview diagram
im=Image.new('RGB',(RES,RES)); a=np.zeros((RES,RES,3),np.uint8)
a[...]= (spl[...,:3]*[[[90,150,60]]]+0).astype(np.uint8) if False else 0
a[lawn]=[80,150,60]; a[levee]=[170,150,100]; a[forest&~water]=[30,70,35]; a[water]=[20,60,110]; a[islands]=[40,90,40]
im=Image.fromarray(a); dr=ImageDraw.Draw(im)
dr.line(to_px(path)+to_px(path[:1]),fill=(255,255,255),width=1)
for hh in houses:
    q=to_px([[hh['x'],hh['z']]])[0]; dr.rectangle([q[0]-4,q[1]-4,q[0]+4,q[1]+4],fill=(255,80,60))
for t in trees[::5]:
    q=to_px([[t[0],t[2]]])[0]; dr.point(q,fill=(10,40,15))
im.crop((300,200,700,860)).resize((800,1320)).save('/workspace/skiview-unity/Tools/venue_preview.png')
# ---------- tileable procedural textures ----------
def tile_noise(n,base,seed,oct=5):
    r=np.random.default_rng(seed); acc=np.zeros((n,n)); amp=1
    for o in range(oct):
        k=base*2**o; g=r.standard_normal((k,k))
        g=np.tile(g,(3,3)); z=ndimage.zoom(g,n*3/(k*3),order=3,mode='grid-wrap')[n:2*n,n:2*n]
        acc+=z*amp; amp*=0.55
    acc=(acc-acc.min())/(np.ptp(acc)+1e-6); return acc
def save_tex(name,c0,c1,base,seed,grain=0.12):
    n=512; t=tile_noise(n,base,seed); g=np.random.default_rng(seed+9).random((n,n))
    t=np.clip(t+(g-0.5)*grain,0,1)[...,None]
    img=np.array(c0)*(1-t)+np.array(c1)*t
    # alpha = smoothness for URP Terrain Lit (no mask map): keep ground matte (~0.08-0.15)
    alpha=(20+t[...,0]*18).astype(np.uint8)[...,None]
    Image.fromarray(np.concatenate([np.clip(img,0,255).astype(np.uint8),alpha],-1),'RGBA').save(OUT+name)
save_tex('tex_lawn.png',[52,92,34],[96,140,52],4,11,0.25)
save_tex('tex_drygrass.png',[128,112,72],[178,160,110],4,12,0.3)
save_tex('tex_forest.png',[22,38,20],[58,66,38],3,13,0.35)
save_tex('tex_mud.png',[70,62,50],[120,110,92],6,14,0.4)
print('ok')
