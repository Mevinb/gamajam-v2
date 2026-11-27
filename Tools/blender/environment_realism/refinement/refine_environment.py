"""Portable complete environment refinement. Explicit inputs; no project-path dependency."""
from pathlib import Path
import argparse, hashlib, json, os, shutil, sys
import bpy

sys.dont_write_bytecode=True
PACKAGE=Path(__file__).resolve().parent
sys.path.insert(0,str(PACKAGE))
import environment_state as state
import uv_projection
import curb_refinement
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()

def budget():
    rel=next(v.split(':',2)[2] for v in Path('/proc/self/cgroup').read_text().splitlines() if v.startswith('0::'))
    group=Path('/sys/fs/cgroup')/rel.lstrip('/')
    value={k:(group/k).read_text().strip() for k in ('cpu.max','memory.max','memory.current','memory.peak')};value['nice']=os.getpriority(os.PRIO_PROCESS,0)
    q,p=value['cpu.max'].split();assert q!='max' and int(q)/int(p)<=.200001 and int(value['memory.max'])<=1536*1024**2 and value['nice']>=19
    return value

def simple_curb(ob):
    m=ob.data
    return {'positions':[list(v.co) for v in m.vertices],'faces':[(list(f.vertices),f.material_index,f.use_smooth) for f in m.polygons],'uvs':[[list(v.uv) for v in layer.data] for layer in m.uv_layers],'matrix':[list(v) for v in ob.matrix_world],'materials':[v.name for v in m.materials]}

def normalized(value):return json.loads(json.dumps(value))
def write(path,value):path.write_text(json.dumps(value,indent=2)+'\n')

def main():
    p=argparse.ArgumentParser();p.add_argument('--base-source',type=Path,required=True)
    for key in 'anm':p.add_argument('--atlas-'+key,type=Path,required=True)
    p.add_argument('--output',type=Path,required=True);a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);source=a.base_source.resolve();out=a.output.resolve();maps={key:getattr(a,'atlas_'+key.lower()).resolve() for key in 'ANM'}
    m=json.loads((PACKAGE/'refinement-manifest.json').read_text());assert sha(source)==m['baseSourceSha256'] and all(sha(path)==m['atlasInputs'][key]['acceptedSha256'] for key,path in maps.items());assert sha(PACKAGE/'accepted-curb-reference.json')==m['acceptedCurbReferenceSha256'];assert not out.exists() and out!=source.parent;out.mkdir();(out/'Textures').mkdir();(out/'ReferenceAtlas').mkdir();(out/'Exports').mkdir()
    input_pins={str(source):sha(source),**{str(v):sha(v) for v in maps.values()},**{str(v):sha(v) for v in PACKAGE.iterdir() if v.is_file()}}
    r={'status':'INCOMPLETE','scope':'Distinct complete UV+accepted-curb+accepted-atlas candidate; no source overwrite, shader creation, geometry generation outside accepted curb, rendering, Unity or GPU.','beforeBudget':budget(),'explicitInputPins':input_pins,'renders':0,'canonicalSaves':0}
    try:
        bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False);bpy.context.preferences.filepaths.save_version=0;bpy.context.preferences.addons['cycles'].preferences.compute_device_type='NONE';assert all(s.cycles.device=='CPU' for s in bpy.data.scenes)
        assert set(o.name for o in bpy.data.objects)==set(m['allModules']);before=state.snapshot();write(out/'before-state.json',before);selected=[o.name for o in bpy.context.selected_objects];active=bpy.context.view_layer.objects.active.name if bpy.context.view_layer.objects.active else None
        r['components']={name:uv_projection.correct(bpy.data.objects[name].data) for name in m['uvTargets']}
        curb=bpy.data.objects['CurbStone'];r['curbRecipe']=curb_refinement.refine_curb(curb.data);assert normalized(simple_curb(curb))==json.loads((PACKAGE/'accepted-curb-reference.json').read_text());r['acceptedCurbReferenceExact']=True
        images=list(bpy.data.images);assert {im.name for im in images}=={'EnvironmentRealism_'+key for key in 'ANM'};r['packedAtlas']=[]
        for old in images:
            key=old.name[-1];name=old.name;assert old.packed_file;original_bytes=bytes(old.packed_file.data);assert hashlib.sha256(original_bytes).hexdigest()==m['atlasInputs'][key]['originalPackedSha256'];(out/'ReferenceAtlas'/(name+'.png')).write_bytes(original_bytes)
            target=out/'Textures'/(name+'.png');shutil.copy2(maps[key],target);image=bpy.data.images.load(str(target),check_existing=False);image.colorspace_settings.name=old.colorspace_settings.name;image.alpha_mode=old.alpha_mode;image.use_fake_user=old.use_fake_user;old.user_remap(image);bpy.data.images.remove(old);image.name=name;image.pack();image.filepath='//Textures/'+name+'.png'
            assert image.packed_file and hashlib.sha256(bytes(image.packed_file.data)).hexdigest()==sha(target)
            r['packedAtlas'].append({'name':name,'acceptedPackedSha256':sha(target),'relativePath':image.filepath,'originalPackedSha256':hashlib.sha256(original_bytes).hexdigest()})
        after=state.snapshot();write(out/'after-state.json',after);r['preservation']={}
        for category in before:
            if category not in ('meshes','images'):assert before[category]==after[category],category
        for name,old in before['meshes'].items():
            new=after['meshes'][name]
            if name=='CurbStone':r['preservation'][name]={'exactAcceptedCurbReference':True};continue
            if name not in m['uvTargets']:assert old==new;r['preservation'][name]={'entireMeshIncludingUVExact':True};continue
            assert {k:v for k,v in old.items() if k!='uv'}=={k:v for k,v in new.items() if k!='uv'}
            mesh=bpy.data.meshes[name];uvname=mesh.uv_layers.active.name;stone={i for f in mesh.polygons if all(0<=old['uv'][uvname][i][0]<.5 and 0<=old['uv'][uvname][i][1]<.5 for i in f.loop_indices) for i in f.loop_indices}
            for layer,values in old['uv'].items():
                assert all(value==new['uv'][layer][i] for i,value in enumerate(values) if i not in stone)
            r['preservation'][name]={'positionsTopologyNormalsMaterialsAttributesExact':True,'nonLimestoneUVExact':True}
        assert set(before['images'])==set(after['images'])
        for name,old in before['images'].items():
            new=after['images'][name];assert {k:v for k,v in old.items() if k not in ('packedSha256','source')}=={k:v for k,v in new.items() if k not in ('packedSha256','source')};assert new['packedSha256']==m['atlasInputs'][name[-1]]['acceptedSha256']
        for name in m['exportTargets']:
            bpy.ops.object.select_all(action='DESELECT');ob=bpy.data.objects[name];ob.select_set(True);bpy.context.view_layer.objects.active=ob
            assert bpy.ops.export_scene.fbx(filepath=str(out/'Exports'/(name+'.fbx')),use_selection=True,object_types={'MESH'},use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',path_mode='STRIP',use_custom_props=False,mesh_smooth_type='FACE',use_triangles=True)=={'FINISHED'}
        bpy.ops.object.select_all(action='DESELECT')
        for name in selected:bpy.data.objects[name].select_set(True)
        bpy.context.view_layer.objects.active=bpy.data.objects[active] if active else None
        assert state.snapshot()==after
        blend=out/'EnvironmentRealism.blend';assert bpy.ops.wm.save_as_mainfile(filepath=str(blend),compress=True,relative_remap=False)=={'FINISHED'}
        r.update(status='COMBINED_ENVIRONMENT_REFINEMENT_SOURCE_AND_FOUR_EXPORTS_CPU_PASS',sourceSha256=sha(blend),expectedStateDigest=state.digest(after),exportSha256={name:sha(out/'Exports'/(name+'.fbx')) for name in m['exportTargets']},otherModulesExact=[n for n in m['allModules'] if n not in m['exportTargets']])
    except BaseException as e:r.update(status='FAILED',error=repr(e));raise
    finally:r['afterBudget']=budget();r['explicitInputsUnchanged']=all(sha(Path(p))==h for p,h in input_pins.items());write(out/'receipt.json',r)

if __name__=='__main__':main()
