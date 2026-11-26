"""V2 formed coach shell. Keeps genuine window/door apertures and the original interior."""
import bpy,bmesh,math
from mathutils import Vector
from geometry import Batch,curve
import materials


def rounded_points(width,height,radius,steps=8):
    out=[]
    for cx,cy,start in [(width/2-radius,height/2-radius,0),(-width/2+radius,height/2-radius,90),
                         (-width/2+radius,-height/2+radius,180),(width/2-radius,-height/2+radius,270)]:
        for j in range(steps+1):
            a=math.radians(start+j*90/steps);out.append((cx+radius*math.cos(a),cy+radius*math.sin(a)))
    return out


def ring(g,p,width,height,radius,frame,depth,mat,axis='X'):
    outer=rounded_points(width,height,radius);inner=rounded_points(width-2*frame,height-2*frame,max(.018,radius-frame))
    n=len(outer);bm=bmesh.new();vv=[]
    for d in (-depth/2,depth/2):
        for contour in (outer,inner):
            for u,v in contour:vv.append(bm.verts.new((d,u,v) if axis=='X' else (u,d,v)))
    for i in range(n):
        j=(i+1)%n
        bm.faces.new((vv[i],vv[j],vv[n+j],vv[n+i]));bm.faces.new((vv[2*n+i],vv[3*n+i],vv[3*n+j],vv[2*n+j]))
        bm.faces.new((vv[i],vv[2*n+i],vv[2*n+j],vv[j]));bm.faces.new((vv[n+i],vv[n+j],vv[3*n+j],vv[3*n+i]))
    g.append(bm,mat,p)


def panel(g,p,width,height,radius,depth,mat,axis='Y'):
    contour=rounded_points(width,height,radius);n=len(contour);bm=bmesh.new()
    vv=[bm.verts.new((d,u,v) if axis=='X' else (u,d,v)) for d in (-depth/2,depth/2) for u,v in contour]
    bm.faces.new(list(reversed(vv[:n])));bm.faces.new(vv[n:])
    for i in range(n):bm.faces.new((vv[i],vv[(i+1)%n],vv[n+(i+1)%n],vv[n+i]))
    g.append(bm,mat,p)


def roof_z(x):
    return 3.42+math.sqrt(3.15*3.15-x*x)-math.sqrt(3.15*3.15-1.6*1.6)


def roof(g,M):
    bm=bmesh.new();n=40;ys=[-7.025,-6.90,-6.64,6.64,6.90,7.025];vv={}
    for layer in range(2):
        for j,y in enumerate(ys):
            end=abs(y)>6.64;lower=.14*max(0,(abs(y)-6.64)/.385);shrink=1-.025*max(0,(abs(y)-6.64)/.385)
            for i in range(n+1):
                x=(-1.60+3.20*i/n)*shrink;vv[layer,j,i]=bm.verts.new((x,y,roof_z(x)-lower-layer*.045))
    for j in range(len(ys)-1):
        for i in range(n):
            bm.faces.new([vv[0,j,i],vv[0,j,i+1],vv[0,j+1,i+1],vv[0,j+1,i]])
            bm.faces.new([vv[1,j,i],vv[1,j+1,i],vv[1,j+1,i+1],vv[1,j,i+1]])
    for j in (0,len(ys)-1):
        for i in range(n):bm.faces.new([vv[0,j,i],vv[1,j,i],vv[1,j,i+1],vv[0,j,i+1]])
    for i in (0,n):
        for j in range(len(ys)-1):bm.faces.new([vv[0,j,i],vv[0,j+1,i],vv[1,j+1,i],vv[1,j,i]])
    g.append(bm,M['coachroof'],smooth=True)


def add(rig,M):
    M['coachpaint']=materials.weathered('Coach lower enamel - attached microtexture',(.045,.115,.13),.34,0)
    M['ivory']=materials.weathered('Coach upper ivory enamel',(.48,.51,.46),.40,0)
    M['coachroof']=materials.weathered('Coach formed aluminium roof finish',(.13,.15,.15),.49,0)
    for key in ('coachpaint','ivory'):
        b=M[key].node_tree.nodes.get('Principled BSDF');b.inputs['Coat Weight'].default_value=.22;b.inputs['Coat Roughness'].default_value=.27
    g=Batch('V2 coach formed shell curved roof rounded frames real doors and cab details')
    roof(g,M)
    for side in (-1,1):
        x=side*1.525
        # Original side glass remains at +/-1.598, Z2.62, Y+/-3.6,+/-5.6.
        intervals=[(-6.76,-1.24),(1.24,6.76)]
        for lo,hi in intervals:
            g.box((x,(lo+hi)/2,1.725),(.15,hi-lo,1.05),M['coachpaint'],.025,3)
            g.box((x,(lo+hi)/2,3.235),(.15,hi-lo,.45),M['ivory'],.025,3)
            cursor=lo
            for y in (-5.6,-3.6,3.6,5.6):
                a,b=y-.80,y+.80
                if a<lo or b>hi:continue
                if a>cursor:g.box((x,(cursor+a)/2,2.62),(.15,a-cursor,.78),M['ivory'],.015,3)
                cursor=b
                ring(g,(side*1.624,y,2.62),1.78,.94,.105,.065,.055,M['roof'])
                ring(g,(side*1.636,y,2.62),1.65,.81,.075,.065,.024,M['rubber'])
                # A real slim sash divider and small mechanical drain holes.
                g.box((side*1.644,y,2.73),(.022,1.53,.022),M['dark'],.003)
                for dy in (-.54,.54):g.box((side*1.653,y+dy,2.245),(.012,.04,.008),M['dark'],.002)
            if cursor<hi:g.box((x,(cursor+hi)/2,2.62),(.15,hi-cursor,.78),M['ivory'],.015,3)
        # Narrow belt, skirt and formed corner returns create a long, manufactured silhouette.
        for lo,hi in intervals:
            g.box((side*1.615,(lo+hi)/2,2.205),(.018,hi-lo,.055),M['yellow'],.005)
            g.box((side*1.59,(lo+hi)/2,1.24),(.035,hi-lo,.10),M['dark'],.012)
        for y in (-6.78,6.78):
            panel(g,(side*1.48,y,2.33),.38,2.24,.13,.20,M['ivory'],axis='X')
        for y in (-5.0,-2.25,2.25,5.0):
            g.box((side*1.606,y,1.70),(.009,.012,.72),M['dark'],.002)
            for z in (1.36,2.04):g.tube((side*1.612,y,z),(side*1.625,y,z),.009,M['roof'],8)
        for y in (-2.15,2.15):
            panel(g,(side*1.607,y,1.82),.57,.50,.055,.025,M['dark'],axis='X')
            for j in range(8):g.box((side*1.635,y,1.62+j*.055),(.022,.46,.017),M['roof'],.003)
        # West boarding aperture remains genuinely open. East side has framed double plug doors.
        for y in (-1.24,1.24):g.box((side*1.59,y,2.28),(.12,.11,2.15),M['roof'],.018,3)
        g.box((side*1.59,0,3.38),(.12,2.55,.15),M['ivory'],.018,3)
        if side>0:
            for y in (-.60,.60):
                panel(g,(1.535,y,1.72),1.15,1.04,.08,.12,M['coachpaint'],axis='X')
                panel(g,(1.535,y,3.19),1.15,.36,.065,.12,M['ivory'],axis='X')
                for edge in (-.56,.56):g.box((1.585,y+edge,2.28),(.07,.055,2.13),M['roof'],.008)
                panel(g,(1.598,y,2.63),.94,.84,.075,.008,M['glass'],axis='X')
                ring(g,(1.628,y,2.63),1.06,.96,.095,.065,.04,M['rubber'])
                g.tube((1.645,y+.30,1.90),(1.645,y+.30,2.12),.015,M['steel'],12)
            g.box((1.64,0,2.29),(.018,.018,2.10),M['rubber'],.003)
        else:
            for y in (-1.22,1.22):g.tube((-1.66,y,1.43),(-1.66,y,2.75),.020,M['steel'],16)
            g.box((-1.64,0,1.19),(.28,2.40,.06),M['steel'],.008)
    for sign in (-1,1):
        y=sign*6.92
        panel(g,(0,y,1.72),3.17,1.04,.20,.17,M['coachpaint'])
        panel(g,(0,y,3.27),3.08,.48,.18,.17,M['ivory'])
        for x in (-1.435,1.435):panel(g,(x,y,2.62),.31,.79,.075,.17,M['ivory'])
        ring(g,(0,sign*7.026,2.57),2.76,.93,.15,.065,.07,M['roof'],axis='Y')
        ring(g,(0,sign*7.055,2.57),2.62,.79,.105,.065,.03,M['rubber'],axis='Y')
        g.box((0,sign*7.067,2.57),(.045,.025,.67),M['roof'],.006)
        for x in (-1.08,1.08):
            g.tube((x,sign*7.005,1.70),(x,sign*7.075,1.70),.125,M['dark'],48)
            g.tube((x,sign*7.076,1.70),(x,sign*7.087,1.70),.090,M['glass'],48)
            g.tube((x,sign*7.065,1.70),(x,sign*7.073,1.70),.073,M['lamp'],48)
        for x in (-.62,.62):
            g.tube((x,sign*7.075,2.25),(x+.20,sign*7.075,2.65),.012,M['dark'],10)
            g.tube((x+.20,sign*7.08,2.52),(x+.20,sign*7.08,2.88),.018,M['rubber'],10)
        panel(g,(0,sign*7.02,1.29),2.83,.22,.09,.13,M['dark'])
        g.box((0,sign*7.15,1.01),(.30,.46,.25),M['dark'],.045,3)
        panel(g,(0,sign*7.40,1.03),.58,.37,.09,.15,M['dark'])
        g.tube((0,sign*7.49,1.03),(0,sign*7.51,1.03),.105,M['steel'],32)
        for x in (-.40,.40):
            points=[(x,sign*7.05,1.26),(x,sign*7.20,1.06),(x*.7,sign*7.19,.91)]
            curve('Coupled brake hose',points,.025,M['rubber'],rig)
    # Rooftop equipment is seated on proper plinths above the curved shell.
    g.box((0,2.0,3.95),(1.7,2.6,.12),M['dark'],.035,3)
    panel(g,(0,2.0,4.10),1.6,.30,.08,2.4,M['coachroof'])
    for side in (-1,1):
        for yy in [1.02+j*.14 for j in range(15)]:g.box((side*.805,yy,4.10),(.020,.07,.15),M['dark'],.004)
    ob=g.finish(rig)
    for y in (-6.0,-4.2,-.4,4.1,6.0):
        curve('Roof pressed seam',[(x,y,roof_z(x)+.006) for x in [-1.5+i*.10 for i in range(31)]],.005,M['coachroof'],rig)
    return ob


def interior(rig,M):
    g=Batch('Visible coach ceiling strip housings and diffusers')
    for y in (-5.1,-1.7,1.7,5.1):
        g.box((0,y,3.49),(.29,1.55,.075),M['roof'],.012,3)
        g.box((0,y,3.446),(.18,1.40,.018),M['lamp'],.006,3)
        data=bpy.data.lights.new('Coach ceiling 18 W diffuse LED strip','AREA');data.energy=18;data.shape='RECTANGLE';data.size=.17;data.size_y=1.35;data.color=(1,.91,.78)
        ob=bpy.data.objects.new(data.name,data);bpy.context.scene.collection.objects.link(ob);ob.parent=rig;ob.location=(0,y,3.43)
    g.finish(rig)
