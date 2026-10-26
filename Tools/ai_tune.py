# Python mirror of SkierSim.cs (port of cutline sim.ts slalom) to tune the autopilot.
import math
LEAN=23; MAXO=2.45; GATEH=2.5; SLACK=1.9; V=34*0.44704
PYZ=0.55; PYY=1.46; HY=0.92; ARM=0.9
def run(L=18.25, kp=0.55, kd=0.30, lead=0.55, tgt=13.6, verbose=False):
    th,om,lean=0.48,-0.5,0.0; boatZ=78.0; dt=1/60
    nb=0; score=0; gates=False; t=0; prevZ=None; prevTh=th; maxw=0
    def pose():
        horiz=math.sqrt(max(4,L*L-(PYY-HY)**2)); R=horiz+ARM
        x=-R*math.sin(th); z=boatZ+PYZ+R*math.cos(th); vx=-R*om*math.cos(th)
        return x,z,vx
    x,z,vx=pose(); prevZ=z
    while t<40:
        t+=dt
        # autopilot: target lateral outside next buoy, or centre for gates/exit
        if not gates: target=0.0
        elif nb<6:
            side=1 if nb%2==0 else -1
            bz=-(27+41*nb)
            target=side*tgt
        else: target=0.0
        xp=x+vx*lead
        steer=max(-1,min(1,-(kp*(target-xp)) - kd*0))
        steer=max(-1,min(1,steer))
        k=4.2 if steer==0 else 22
        lean+=(steer-lean)*(1-math.exp(-k*dt))
        torque=lean*LEAN/max(8,L)
        cin=abs(th)>0.22 and lean!=0 and math.copysign(1,lean)!=math.copysign(1,th)
        gout=abs(th)>0.15 and lean!=0 and math.copysign(1,lean)==math.copysign(1,th)
        if cin: torque*=1.55
        if gout: torque*=1.22
        if abs(th)<0.18 and abs(om)>0.6: torque*=1.12
        drag=1.02+abs(lean)*0.22
        om+=(torque-drag*om)*dt; om=max(-MAXO,min(MAXO,om))
        th+=om*dt; th=max(-1.62,min(1.62,th))
        boatZ-=V*dt
        prevZ=z; x,z,vx=pose()
        if prevTh*th<0: om*=1.06
        prevTh=th
        if not gates and prevZ>=0 and z<0:
            if abs(x)>GATEH: return ('gate',x,score)
            gates=True
        if gates and nb<6:
            bz=-(27+41*nb); side=1 if nb%2==0 else -1
            if prevZ>=bz and z<bz:
                w=abs(x)+abs(lean)*1.85
                if x*side>=11.5-SLACK or (w+0.55>=11.5-SLACK*0.35 and x*side>0):
                    score+=1; nb+=1
                    if verbose: print('buoy',nb,'x=%.2f'%x,'t=%.2f'%t)
                else: return ('miss',nb+1,score,round(x,2))
        if nb==6 and prevZ>=-259 and z<-259:
            return ('exit',abs(x)<=GATEH+0.8,score,round(x,2))
    return ('timeout',score)
best=None
for L in (18.25,16.0,14.25):
  for kp in (0.25,0.35,0.5,0.7,1.0):
    for lead in (0.3,0.5,0.7,0.9):
      for tgt in (12.5,13.5,14.5):
        r=run(L,kp,0,lead,tgt)
        if r[0]=='exit' and r[1]: print(L,kp,lead,tgt,r)
