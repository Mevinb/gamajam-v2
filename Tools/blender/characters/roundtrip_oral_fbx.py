"""Independent CPU FBX comparison; reads live/pilot and writes Logs only."""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
from mathutils.kdtree import KDTree
from io_scene_fbx import parse_fbx

ROOT=Path(__file__).resolve().parents[3]


def raw_units(path):
    tree,_=parse_fbx.parse(str(path));find=lambda node,key:next((e for e in node.elems if e.id==key),None);props=find(find(tree,b'GlobalSettings'),b'Properties70')
    units={e.props[0].decode():e.props[4:]for e in props.elems if e.id==b'P'and e.props[0]in [b'UnitScaleFactor',b'OriginalUnitScaleFactor']};scales={}
    for e in find(tree,b'Objects').elems:
        if e.id!=b'Model':continue
        name=e.props[1].decode(errors='replace').split('\x00')[0];p=find(e,b'Properties70');values={q.props[0].decode():q.props[4:]for q in p.elems if q.id==b'P'}if p else{};scales[name]=values.get('Lcl Scaling',[1.,1.,1.])
    return {'units':units,'modelLocalScales':scales}


def read(path):
    for ob in list(bpy.data.objects):bpy.data.objects.remove(ob,do_unlink=True)
    bpy.data.orphans_purge(do_local_ids=True,do_linked_ids=False,do_recursive=True)
    bpy.ops.import_scene.fbx(filepath=str(path),use_image_search=False)
    rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');bones={b.name:{'parent':b.parent.name if b.parent else None,'worldRest':[float(v)for row in rig.matrix_world@b.matrix_local for v in row]}for b in rig.data.bones};lods={}
    for index in range(3):
        ob=next(o for o in bpy.data.objects if o.type=='MESH'and o.name.endswith('_LOD'+str(index)));points=[list(ob.matrix_world@v.co)for v in ob.data.vertices];invalid=[]
        for vertex in ob.data.vertices:
            groups=[(ob.vertex_groups[g.group].name,g.weight)for g in vertex.groups if g.weight>1e-6]
            if len(groups)>4 or any(name not in bones for name,_ in groups):invalid.append(vertex.index)
        lods[str(index)]={'name':ob.name,'vertices':len(points),'triangles':sum(len(p.vertices)-2 for p in ob.data.polygons),'boundsMin':[min(p[a]for p in points)for a in range(3)],'boundsMax':[max(p[a]for p in points)for a in range(3)],'materials':[m.name.split('.')[0]for m in ob.data.materials],'invalidWeightedVertices':len(invalid),'points':points}
    return {'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'bones':bones,'lods':lods}


def main():
    p=argparse.ArgumentParser();p.add_argument('--output',required=True);p.add_argument('--baseline',default=str(ROOT/'Assets/Parasite/Art/Characters/Models/LakeGirl.fbx'));p.add_argument('--pilot',default=str(ROOT/'ArtSource/Characters/OralPilotV2/LakeGirl_OralPilotV2.fbx'));args=p.parse_args(sys.argv[sys.argv.index('--')+1:]);baseline_path=Path(args.baseline);pilot_path=Path(args.pilot)
    raw_a=raw_units(baseline_path);raw_b=raw_units(pilot_path);assert raw_a['units']==raw_b['units'],('Raw FBX units mismatch hidden by normalized import',raw_a['units'],raw_b['units'])
    raw_delta=max(abs(a-b)for name in set(raw_a['modelLocalScales'])&set(raw_b['modelLocalScales'])for a,b in zip(raw_a['modelLocalScales'][name],raw_b['modelLocalScales'][name]));assert raw_delta<1e-5,('Raw local scale mismatch',raw_delta)
    live=read(baseline_path);pilot=read(pilot_path);assert set(live['bones'])==set(pilot['bones'])
    delta=max(abs(a-b)for name in live['bones']for a,b in zip(live['bones'][name]['worldRest'],pilot['bones'][name]['worldRest']));assert delta<1e-5,('FBX rest mismatch',delta)
    assert all(live['bones'][n]['parent']==pilot['bones'][n]['parent']for n in live['bones'])
    result={}
    for index in range(3):
        a=live['lods'][str(index)];b=pilot['lods'][str(index)];assert a['materials']==b['materials']==['Skin','Eyes','Hair','Cloth'];assert b['invalidWeightedVertices']==0
        bounds_delta=max(abs(x-y)for key in ['boundsMin','boundsMax']for x,y in zip(a[key],b[key]));assert bounds_delta<1e-5,(index,'bounds',bounds_delta)
        tree=KDTree(len(b['points']))
        for n,p in enumerate(b['points']):tree.insert(p,n)
        tree.balance();max_missing=max(tree.find(p)[2]for p in a['points']);assert max_missing<1e-5,(index,'outside FBX surface changed',max_missing)
        result[str(index)]={'liveVertices':a['vertices'],'pilotVertices':b['vertices'],'liveTriangles':a['triangles'],'pilotTriangles':b['triangles'],'originalSurfaceNearestMaxDeltaM':max_missing,'boundsMaxDeltaM':bounds_delta,'materials':b['materials'],'invalidWeightedVertices':0}
    report={'baselinePath':str(baseline_path),'pilotPath':str(pilot_path),'live_fbx_sha256':live['sha256'],'pilot_fbx_sha256':pilot['sha256'],'rawUnits':raw_b['units'],'rawLocalScaleMaxDelta':raw_delta,'boneCount':len(live['bones']),'sameBoneNamesParents':True,'worldRestMaxDelta':delta,'lods':result,'scope':'raw unit/local-scale parity plus independent normalized Blender comparison; actual Unity remains a separate required gate'};Path(args.output).write_text(json.dumps(report,indent=2)+'\n');print('[oral-fbx] ROUNDTRIP PASS',json.dumps(report),flush=True)


if __name__=='__main__':main()
