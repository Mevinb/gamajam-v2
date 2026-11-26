"""Read-only tooth arch/normal/visibility audit of a saved oral pilot."""
import argparse
import json
import sys
from pathlib import Path
from collections import Counter

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def main():
    p=argparse.ArgumentParser();p.add_argument('--output',required=True);args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    hero=bpy.data.objects['LakeGirl_LOD0'];rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');uv=hero.data.uv_layers.active.data
    compact=bool(bpy.context.scene.get('compact_oral_strip',False));original_count=int(bpy.context.scene.get('oral_original_lod0_vertices',11769))
    # V1 teeth are in the first right-hand atlas quadrant; compact V2 uses the
    # first3/4 of the guarded top strip, with a dedicated point attribute.
    kind=hero.data.attributes.get('oral_kind')
    if kind:tooth_faces={f.index for f in hero.data.polygons if kind.data[f.index].value==1}
    else:tooth_faces={f.index for f in hero.data.polygons if f.material_index==3 and .50<sum(uv[i].uv.x for i in f.loop_indices)/len(f.loop_indices)<.75}
    verts={i for f in hero.data.polygons if f.index in tooth_faces for i in f.vertices};adj={i:set()for i in verts}
    for face in hero.data.polygons:
        if face.index not in tooth_faces:continue
        for a,b in zip(face.vertices,list(face.vertices)[1:]+[face.vertices[0]]):adj[a].add(b);adj[b].add(a)
    components=[];unseen=set(verts);jaw=hero.vertex_groups.get('Jaw')
    while unseen:
        seed=unseen.pop();stack=[seed];ids=[seed]
        while stack:
            a=stack.pop()
            for b in adj[a]:
                if b in unseen:unseen.remove(b);stack.append(b);ids.append(b)
        coords=[hero.data.vertices[i].co for i in ids];weights=[sum(g.weight for g in hero.data.vertices[i].groups if g.group==jaw.index)for i in ids]
        components.append({'vertices':len(ids),'meanXYZ':[sum(p[a]for p in coords)/len(coords)for a in range(3)],'boundsMin':[min(p[a]for p in coords)for a in range(3)],'boundsMax':[max(p[a]for p in coords)for a in range(3)],'meanJawWeight':sum(weights)/len(weights),'maxJawWeight':max(weights)})
    camera=bpy.data.objects['Oral frontal review'];samples=[]
    for frame in [1,25,49]:
        bpy.context.scene.frame_set(frame);bpy.context.view_layer.update();ev=hero.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();mesh.calc_loop_triangles();triangles=list(mesh.loop_triangles);world=[ev.matrix_world@v.co for v in mesh.vertices];tree=BVHTree.FromPolygons(world,[list(t.vertices)for t in triangles],all_triangles=True)
        try:
            counters=Counter();bounds={}
            for arch in ['upper','lower']:
                ids=[i for i in verts if (sum(g.weight for g in hero.data.vertices[i].groups if g.group==jaw.index)>.25)==(arch=='lower')]
                bounds[arch]={'vertices':len(ids),'min':[min(world[i][a]for i in ids)for a in range(3)]if ids else None,'max':[max(world[i][a]for i in ids)for a in range(3)]if ids else None}
            for tri in triangles:
                if tri.polygon_index not in tooth_faces:continue
                centre=sum((world[i]for i in tri.vertices),Vector())/3
                if abs(centre.x)>.018:continue
                arch='lower'if sum(sum(g.weight for g in hero.data.vertices[i].groups if g.group==jaw.index)for i in tri.vertices)/3>.25 else'upper'
                hit,_,index,distance=tree.ray_cast(camera.location,(centre-camera.location).normalized(),2)
                if hit is None:continue
                first=triangles[index]
                if first.polygon_index in tooth_faces:occluder='tooth'
                elif all(i>=original_count for i in first.vertices):occluder='new cavity/tongue'
                else:occluder='original lip/skin/hair'
                counters[arch+' first-hit '+occluder]+=1
            samples.append({'frame':frame,'jawRotationXYZ':list(rig.pose.bones['Jaw'].rotation_euler),'archBounds':bounds,'centralDentalRayFirstHits':dict(counters)})
        finally:ev.to_mesh_clear()
    report={'source':bpy.data.filepath,'toothFaces':len(tooth_faces),'toothSmoothFaceFraction':sum(hero.data.polygons[i].use_smooth for i in tooth_faces)/len(tooth_faces),'toothComponents':sorted(components,key=lambda x:x['meanXYZ'][2]),'samples':samples,'rendered':False}
    Path(args.output).write_text(json.dumps(report,indent=2)+'\n');print('[oral-occlusion] AUDIT',json.dumps({'faces':len(tooth_faces),'smoothFraction':report['toothSmoothFaceFraction'],'components':len(components),'samples':samples}),flush=True)


if __name__=='__main__':main()
