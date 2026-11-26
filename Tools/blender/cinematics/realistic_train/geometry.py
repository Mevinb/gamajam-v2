"""Metric Blender Z-up manufacturing primitives, accumulated into material batches."""
import bpy, bmesh, math
from mathutils import Matrix, Vector


class Batch:
    def __init__(self, name):
        self.name=name;self.vertices=[];self.faces=[];self.uv=[];self.indices=[];self.materials=[];self.smooth=[]

    def append(self,bm,material,centre=(0,0,0),rotation=None,smooth=False):
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update()
        if bm.calc_volume(signed=True)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
        bm.verts.ensure_lookup_table();bm.faces.ensure_lookup_table();bm.normal_update()
        rot=rotation or Matrix.Identity(3);pos=Vector(centre)
        vv={v:rot@v.co+pos for v in bm.verts};base=len(self.vertices);index={v:base+i for i,v in enumerate(bm.verts)}
        self.vertices.extend(tuple(vv[v]) for v in bm.verts)
        if material not in self.materials:self.materials.append(material)
        mi=self.materials.index(material)
        for f in bm.faces:
            self.faces.append(tuple(index[v] for v in f.verts));self.indices.append(mi);self.smooth.append(smooth and len(f.verts)==4)
            n=rot@f.normal;axis=max(range(3),key=lambda i:abs(n[i]));a,b=[i for i in range(3) if i!=axis]
            self.uv.append([(vv[v][a],vv[v][b]) for v in f.verts])
        bm.free()

    def box(self,p,size,mat,bevel=0,segments=2,rotation=None):
        bm=bmesh.new();bmesh.ops.create_cube(bm,size=1)
        for v in bm.verts:v.co=Vector([v.co[i]*size[i] for i in range(3)])
        if bevel:bmesh.ops.bevel(bm,geom=list(bm.edges),offset=min(bevel,min(size)*.25),segments=segments,affect='EDGES')
        self.append(bm,mat,p,rotation)

    def tube(self,a,b,r,mat,sides=16):
        bm=bmesh.new();d=Vector(b)-Vector(a)
        bmesh.ops.create_cone(bm,cap_ends=True,cap_tris=False,segments=sides,radius1=r,radius2=r,depth=d.length)
        q=Vector((0,0,1)).rotation_difference(d.normalized())
        self.append(bm,mat,(Vector(a)+Vector(b))/2,q.to_matrix(),True)

    def sphere(self,p,size,mat,subdivisions=2):
        bm=bmesh.new();bmesh.ops.create_icosphere(bm,subdivisions=subdivisions,radius=1)
        for v in bm.verts:v.co=Vector([v.co[i]*size[i] for i in range(3)])
        self.append(bm,mat,p,smooth=True)

    def extrude(self,profile,length,centre,mat):
        # Profile in local X/Z, track extrusion along local Y.
        bm=bmesh.new();n=len(profile)
        vv=[bm.verts.new((x,y,z)) for y in (-length/2,length/2) for x,z in profile]
        bm.faces.new([vv[i] for i in reversed(range(n))]);bm.faces.new([vv[n+i] for i in range(n)])
        for i in range(n):bm.faces.new((vv[i],vv[(i+1)%n],vv[n+(i+1)%n],vv[n+i]))
        self.append(bm,mat,centre)

    def spring(self,p,r,height,wire,mat,turns=4.5):
        bm=bmesh.new();steps=72;sides=7;vv=[]
        for i in range(steps+1):
            a=math.tau*turns*i/steps
            for j in range(sides):
                t=math.tau*j/sides;radius=r+wire*math.cos(t)
                vv.append(bm.verts.new((radius*math.cos(a),radius*math.sin(a),height*(i/steps-.5)+wire*math.sin(t))))
        for i in range(steps):
            for j in range(sides):bm.faces.new((vv[i*sides+j],vv[(i+1)*sides+j],vv[(i+1)*sides+(j+1)%sides],vv[i*sides+(j+1)%sides]))
        bm.faces.new(vv[:sides]);bm.faces.new(list(reversed(vv[-sides:])))
        self.append(bm,mat,p,smooth=True)

    def finish(self,parent=None):
        me=bpy.data.meshes.new(self.name);me.from_pydata(self.vertices,[],self.faces);me.update()
        for m in self.materials:me.materials.append(m)
        uv=me.uv_layers.new(name='MetricUV')
        for f,coords,mi,sm in zip(me.polygons,self.uv,self.indices,self.smooth):
            f.material_index=mi;f.use_smooth=sm
            for li,co in zip(f.loop_indices,coords):uv.data[li].uv=co
        ob=bpy.data.objects.new(self.name,me);bpy.context.scene.collection.objects.link(ob)
        if parent:ob.parent=parent
        return ob


def empty(name,parent=None,location=(0,0,0)):
    ob=bpy.data.objects.new(name,None);bpy.context.scene.collection.objects.link(ob);ob.location=location
    if parent:ob.parent=parent
    return ob


def curve(name,points,r,material,parent=None):
    data=bpy.data.curves.new(name,'CURVE');data.dimensions='3D';data.bevel_depth=r;data.bevel_resolution=2
    spline=data.splines.new('POLY');spline.points.add(len(points)-1)
    for p,co in zip(spline.points,points):p.co=(*co,1)
    ob=bpy.data.objects.new(name,data);bpy.context.scene.collection.objects.link(ob);data.materials.append(material)
    if parent:ob.parent=parent
    return ob
