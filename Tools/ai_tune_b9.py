import math, itertools, sys
LEAN=23; MAXO=2.45; V=15.2; PYY=1.285; HY=0.92; ARM=0.9; ST=1.0; CUT=1.06; HOLD=1.06
ROPES=[18.25,16.0,14.25,13.0,12.0,11.25,10.75,10.25]
def sgn(v): return (v>0)-(v<0)
def run(L, P, dbg=False):
    gcut,pre,kp,lead,tgt,sw = P
    th,om,lean=0.05,0.0,0.0; boatZ=70.0; dt=1/120
    nb=0; score=0.0; gates=False; pend=0; t=0
    horiz=math.sqrt(max(4,L*L-(PYY-HY)**2))
    def R():
        s01 = min(1,max(0,(abs(th)-0.6)/0.6))*min(1,abs(lean)) if abs(th)>0.6 and sgn(lean)==sgn(th) else 0
        return horiz+ARM+ST*s01
    def pose():
        r=R(); return -r*math.sin(th), boatZ+r*math.cos(th), -r*om*math.cos(th)
    x,z,vx=pose(); ballx=[]
    while t<30:
        t+=dt
        if not gates:
            target = -tgt*0.8 if z>gcut else tgt   # pre-swing left, cut at gcut metres before the gate toward ball 1 (right)
            target*=sw
        elif nb<6: target=(1 if nb%2==0 else -1)*tgt
        else: target=0
        xp=x+vx*lead
        steer=max(-1,min(1,-kp*(target-xp)))
        k=4.2 if steer==0 else 22
        lean+=(steer-lean)*(1-math.exp(-k*dt))
        torque=lean*LEAN*CUT/max(8,L)
        if abs(th)>0.22 and sgn(lean)!=0 and sgn(lean)!=sgn(th): torque*=1.55
        if abs(th)>0.15 and sgn(lean)==sgn(th) and lean!=0: torque*=1.22
        if abs(th)<0.18 and abs(om)>0.6: torque*=1.12
        drag=1.02+abs(lean)*0.22
        om+=(torque-drag*om)*dt; mo=MAXO*HOLD; om=max(-mo,min(mo,om))
        pth=th; th+=om*dt; th=max(-1.62,min(1.62,th))
        if pth*th<0: om*=1.06
        boatZ-=V*dt; pz=z; px=x; x,z,vx=pose()
        if not gates:
            if pz>=0 and z<0:
                a=math.degrees(math.atan(abs(x-px)/max(1e-4,abs(z-pz))))
                tol=1.75 if a>=15 else 1.25
                if abs(x)<=tol: gates=True
                else: return score,'gate %.2f'%x,ballx
            continue
        if pend and x*pend<0: score+=0.5; pend=0
        if nb<6:
            bz=-(27+41*nb); side=1 if nb%2==0 else -1
            if pz>=bz and z<bz:
                ballx.append(x*side)
                if x*side>=11.5: score+=0.5; pend=side; nb+=1
                else: return score,'miss %d'%(nb+1),ballx
        elif pz>=-259 and z<-259:
            if abs(x)<=1.75: return score+(0.5 if pend else 0),'FULL',ballx
            return score,'exit',ballx
    return score,'time',ballx
best=None
for P in itertools.product([6,10,14,18,22],[0],[0.3,0.5,0.8],[0.3,0.45,0.6,0.8],[12.5,13.5,15,17],[0.4,0.7,1.0]):
    sc=[run(L,P)[0] for L in ROPES]
    key=(sum(sc[:4]),sum(sc))
    if best is None or key>best[0]: best=(key,P,sc)
print(best)
for L in ROPES: print(L, run(L,best[1]))
