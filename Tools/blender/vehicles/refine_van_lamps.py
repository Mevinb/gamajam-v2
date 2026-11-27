"""Refine only six Van lamp meshes in an existing complete editable vehicle kit.

Run with Blender --background --threads 1 --python this_file -- --source INPUT
--output FRESH_DIRECTORY. The adjacent author_vehicles.py supplies the portable
procedural geometry. It never invokes that module's whole-kit main or textures.
"""
import argparse, hashlib, importlib.util, json, sys
from pathlib import Path
import bpy
from mathutils import Matrix
sys.dont_write_bytecode = True

def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source',type=Path,required=True)
    p.add_argument('--source-sha256',required=True)
    p.add_argument('--generator-sha256',required=True)
    p.add_argument('--output',type=Path,required=True)
    p.add_argument('--export-van',action='store_true')
    a=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    source=a.source.resolve();out=a.output.resolve();generator=Path(__file__).with_name('author_vehicles.py')
    assert source.is_file() and source.suffix=='.blend' and sha(source)==a.source_sha256
    assert sha(generator)==a.generator_sha256 and not out.exists() and source!=out
    out.mkdir(parents=True)
    r={'status':'INCOMPLETE','scope':'Complete editable kit; Van Body/Glass lamp refinement only. No texture rebake, other vehicle export or render.','sourceSha256':sha(source),'generatorSha256':sha(generator),'recipeSha256':sha(__file__),'refinements':[],'renders':0,'textureRegeneration':False,'vanFbxExports':0}
    def save():(out/'recipe-receipt.json').write_text(json.dumps(r,indent=2)+'\n')
    save()
    try:
        bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False)
        assert all(s.cycles.device=='CPU' for s in bpy.data.scenes)
        spec=importlib.util.spec_from_file_location('portable_vehicle_lamps',generator)
        m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
        objects=sorted((o for o in bpy.data.objects if o.name.startswith('VehicleVan_')),key=lambda o:o.name)
        assert len(objects)==18 and all(o.type=='MESH' and not o.modifiers for o in objects)
        wheels={o.name:m.van_lamp_mesh_signature(o.data) for o in objects if '_Wheel_' in o.name}
        assert len(wheels)==12
        for ob in objects:
            part=ob.name.split('VehicleVan_',1)[1].rsplit('_LOD',1)[0]
            if part in ('Body','Glass'):r['refinements'].append(m.van_headlamp_refine(ob,int(ob.name[-1]),part));save()
        assert wheels=={o.name:m.van_lamp_mesh_signature(o.data) for o in objects if '_Wheel_' in o.name}
        r['originalTwelveWheelBuffersExact']=True
        r['triangles']=[sum(len(o.data.loop_triangles) for o in objects if o.name.endswith('LOD'+str(i))) for i in range(3)]
        # calc_loop_triangles is independent of cached evaluation of a prior source.
        for o in objects:o.data.calc_loop_triangles()
        r['triangles']=[sum(len(o.data.loop_triangles) for o in objects if o.name.endswith('LOD'+str(i))) for i in range(3)]
        assert r['triangles']==[50415,15816,3928]
        assert len(r['refinements'])==6 and len(bpy.data.objects)==97 and len(bpy.data.materials)==10
        if a.export_van:
            selected={o:o.select_get() for o in bpy.context.view_layer.objects};active=bpy.context.view_layer.objects.active;copies=[]
            try:
                bpy.ops.object.select_all(action='DESELECT')
                for original in objects:
                    ob=original.copy();ob.data=original.data.copy();bpy.context.scene.collection.objects.link(ob)
                    ob.name=original.name+'_Export';ob.hide_set(False);ob.hide_render=False
                    ob.data.transform(Matrix.Diagonal((1,-1,1,1)));ob.data.flip_normals();ob.select_set(True);copies.append(ob)
                bpy.context.view_layer.objects.active=copies[0]
                target=out/'VehicleVan.fbx'
                assert bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=True)=={'FINISHED'}
                r.update(vanFbxExports=1,vanFbxSha256=sha(target))
            finally:
                for ob in copies:
                    mesh=ob.data;bpy.data.objects.remove(ob,do_unlink=True)
                    if mesh.users==0:bpy.data.meshes.remove(mesh)
                for ob,was_selected in selected.items():ob.select_set(was_selected)
                bpy.context.view_layer.objects.active=active
        bpy.context.preferences.filepaths.save_version=0
        target=out/'Vehicles.blend'
        assert bpy.ops.wm.save_as_mainfile(filepath=str(target),compress=True,relative_remap=False)=={'FINISHED'}
        assert sha(source)==a.source_sha256 and sha(generator)==a.generator_sha256
        r.update(status='PORTABLE_SIX_MESH_REFINEMENT_SAVED',completeSourceSha256=sha(target),objects=len(bpy.data.objects),materials=len(bpy.data.materials))
    except BaseException as e:r.update(status='FAILED',error=repr(e));raise
    finally:save()
if __name__=='__main__':main()
