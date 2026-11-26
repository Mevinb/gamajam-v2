"""CPU ray/atlas diagnostic for visible iris versus sclera; never renders/saves a blend."""
import json
import sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];sys.path.insert(0,str(HERE))
from portrait import eye_position,refine_eye_uv,focus_pin
from author import cameras

# Always start from the immutable uncorrected source, then reconstruct the
# 60 mm review camera. A rerun must never apply the correction twice to v2 UVs.
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/FullCinematics/Lake/Lake_Arrival.blend'))
s=bpy.context.scene;s.frame_set(1);hero=bpy.data.objects['LakeGirl_LOD0'];rig=bpy.data.objects['Armature'];subdiv=hero.modifiers.new('Diagnostic viewport subdivision1','SUBSURF');subdiv.levels=1;s.timeline_markers.clear();pin=focus_pin(s,rig,360);cameras(s,pin,'arrival',360,eye_position(rig));s.frame_set(165);camera=next(m.camera for m in s.timeline_markers if m.frame==121)
eye=eye_position(rig);right=camera.matrix_world.to_3x3()@Vector((1,0,0));up=camera.matrix_world.to_3x3()@Vector((0,1,0))
image=next(i for i in bpy.data.images if i.name.startswith('brown.png'));pixels=np.empty(image.size[0]*image.size[1]*4,np.float32);image.pixels.foreach_get(pixels);pixels=pixels.reshape(image.size[1],image.size[0],4)
result={}
for passname in ('source','iris_uv_1.42'):
    if passname!='source':refine_eye_uv(hero);bpy.context.view_layer.update()
    evaluated=hero.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh();mesh.calc_loop_triangles();world=[evaluated.matrix_world@v.co for v in mesh.vertices];triangles=list(mesh.loop_triangles);tree=BVHTree.FromPolygons(world,[list(t.vertices) for t in triangles],all_triangles=True);uv=mesh.uv_layers.active.data
    slots={i for i,m in enumerate(hero.data.materials) if m.name.startswith('Eyes')};count=0;white=0;dark=0
    for y in np.linspace(-.020,.020,64):
        for x in np.linspace(-.060,.060,180):
            target=eye+right*float(x)+up*float(y);direction=(target-camera.location).normalized();point,normal,index,distance=tree.ray_cast(camera.location,direction,5)
            if point is None:continue
            tri=triangles[index]
            if tri.material_index not in slots:continue
            coords=barycentric_transform(point,*[world[i] for i in tri.vertices],*[Vector((*uv[i].uv,0)) for i in tri.loops]);px=min(image.size[0]-1,max(0,int(coords.x*image.size[0])));py=min(image.size[1]-1,max(0,int(coords.y*image.size[1])));colour=pixels[py,px,:3];count+=1
            if float(colour.mean())>.30:white+=1
            else:dark+=1
    result[passname]={'visible_eye_rays':count,'sclera_rays':white,'iris_pupil_rays':dark,'sclera_fraction':white/max(1,count)};evaluated.to_mesh_clear()
assert result['iris_uv_1.42']['sclera_fraction']>result['source']['sclera_fraction']+.1,result
out=ROOT/'Logs/realistic-lake-author/eye-uv-audit.json';out.write_text(json.dumps(result,indent=2)+'\n');print('[realistic-lake] EYE UV AUDIT PASS',json.dumps(result),flush=True)
