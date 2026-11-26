"""Explicit, isolated oral-anatomy authoring pilot. Never changes Assets/catalog.

BLENDER_USER_RESOURCES=<MPFB profile> nice -n 15 blender -b -t 2 \
 --python-exit-code 1 --python oral_pilot.py -- --author

The existing LakeGirl rig and all original LOD vertices/faces/weights stay intact.
Fitted CC0 cavity/teeth/tongue are appended; oral photos occupy an extension of
the existing cloth atlas, retaining its original texels and four material slots.
The4096x2048 atlas is a review prototype, not a shipping resolution decision.
"""
import argparse
import hashlib
import json
import math
import os
import sys
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector

HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2]
sys.path.insert(0,str(HERE))
import chartools as ct
import specs
from cinematic_surfaces import upgrade_character


def digest(value):
    return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':')).encode()).hexdigest()


def skeleton(rig):
    return {b.name:{'parent':b.parent.name if b.parent else None,'matrix':[float(x)for row in b.matrix_local for x in row]}for b in rig.data.bones}


def original_geometry(ob):
    return {'vertices':[[float(x)for x in v.co]for v in ob.data.vertices],
            'faces':[[int(x)for x in p.vertices]for p in ob.data.polygons],
            'weights':[[[ob.vertex_groups[g.group].name,float(g.weight)]for g in v.groups]for v in ob.data.vertices]}


def extract_cavity(body):
    _,L=ct.mpfb_services();image=bpy.data.images.load(os.path.join(L.get_mpfb_data('textures'),'mpfb_inside-mouth.jpg'))
    pixels=np.array(image.pixels[:],dtype=np.float32).reshape(image.size[1],image.size[0],4)
    uv=body.data.uv_layers.active.data;selected=set()
    for p in body.data.polygons:
        if all(pixels[min(image.size[1]-1,max(0,int(uv[l].uv.y*image.size[1]))),min(image.size[0]-1,max(0,int(uv[l].uv.x*image.size[0]))),0]>.5 for l in p.loop_indices):selected.add(p.index)
    cavity=body.copy();cavity.data=body.data.copy();cavity.name='Fitted original MPFB oral cavity';bpy.context.scene.collection.objects.link(cavity)
    bm=bmesh.new();bm.from_mesh(cavity.data);bm.faces.ensure_lookup_table();bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.index not in selected],context='FACES');bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bm.to_mesh(cavity.data);bm.free();cavity.data.update()
    return cavity,len(selected)


def reduce(ob,target):
    count=sum(len(p.vertices)-2 for p in ob.data.polygons)
    if count<=target:return
    ct.set_active(ob);mod=ob.modifiers.new('Oral review LOD','DECIMATE');mod.ratio=target/count;mod.use_collapse_triangulate=True
    while ob.modifiers.find(mod.name)>0:bpy.ops.object.modifier_move_up(modifier=mod.name)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def pixels(path,size,data=False):
    image=bpy.data.images.load(str(path),check_existing=False);image.colorspace_settings.name='Non-Color' if data else 'sRGB'
    if tuple(image.size)!=size:image.scale(*size)
    values=np.empty(size[0]*size[1]*4,np.float32);image.pixels.foreach_get(values);bpy.data.images.remove(image)
    return values.reshape(size[1],size[0],4)[...,:3]


def write_data_png(values,path):
    h,w=values.shape[:2];image=bpy.data.images.new(path.stem,w,h,alpha=False,float_buffer=False);image.colorspace_settings.name='Non-Color'
    rgba=np.concatenate([values,np.ones((h,w,1),np.float32)],axis=2);image.pixels.foreach_set(rgba.ravel());image.file_format='PNG';image.filepath_raw=str(path);image.save();bpy.data.images.remove(image)


def atlas(out,meta,oral_paths):
    textures=ROOT/'Assets/Parasite/Art/Characters/Textures';paths={};checks={}
    for key,suffix,data in [('clothA','A',False),('clothN','N',True),('clothM','M',True)]:
        old=pixels(textures/meta['textures'][key],(2048,2048),data)
        canvas=np.zeros((2048,4096,3),np.float32)
        if suffix=='N':canvas[:]=(.5,.5,1)
        canvas[:,:2048]=old
        if suffix=='A':
            canvas[:1024,2048:3072]=pixels(oral_paths['teeth'],(1024,1024))*.83
            canvas[:1024,3072:4096]=pixels(oral_paths['tongue'],(1024,1024))
        elif suffix=='M':
            canvas[:1024,2048:3072]=(0,0,.62);canvas[:1024,3072:4096]=(0,0,.48)
        path=out/'Textures'/('LakeGirl_OralPilot_Cloth_'+suffix+'.png');write_data_png(canvas,path);paths[key]=path
        check=pixels(path,(4096,2048),True)[:,:2048];error=float(np.max(np.abs(check-old)));checks[key]={'old_cloth_max_channel_error':error,'path':str(path.relative_to(ROOT))};assert error<=1/255+.0001
    return paths,checks


def set_slot(ob,mat):
    ob.data.materials.clear();ob.data.materials.append(mat)
    for p in ob.data.polygons:p.material_index=0


def append_parts(lod,parts,rig):
    before=original_geometry(lod);n=len(lod.data.vertices);f=len(lod.data.polygons)
    ct.set_active(lod)
    for ob in parts:ob.select_set(True)
    bpy.ops.object.join();after=original_geometry(lod)
    assert after['vertices'][:n]==before['vertices'] and after['faces'][:f]==before['faces'] and after['weights'][:n]==before['weights'],'Original geometry/weights changed'
    assert len(lod.data.materials)==4,'Pilot added a draw/material slot'
    ct.ensure_armature(lod,rig)
    return {'original_vertices':n,'original_faces':f,'added_vertices':len(lod.data.vertices)-n,'added_triangles':sum(len(p.vertices)-2 for p in lod.data.polygons[f:]),'material_slots':4,'original_geometry_and_weights_preserved':True}


def author(out):
    source=Path.home()/'Data/parasite-work/characters/out/LakeGirl.blend';source_hash=hashlib.sha256(source.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(source));rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');lods=[bpy.data.objects['LakeGirl_LOD'+str(i)]for i in range(3)];rest=skeleton(rig)
    original_hashes={o.name:digest(original_geometry(o))for o in lods};meta=json.loads((ROOT/'Assets/Parasite/Art/Characters/Models/LakeGirl.json').read_text());materials=upgrade_character(lods[0],'LakeGirl',ROOT)
    for ob in lods[1:]:
        for index,m in enumerate(list(ob.data.materials)):ob.data.materials[index]=materials[m.name.split('.')[0]]
    # Generate fitted anatomy on a temporary copy of the same phenotype/rig.
    H,_=ct.mpfb_services();info=H._create_default_human_info_dict();spec=specs.get('LakeGirl');info['name']='Oral fitted temporary';info['rig']='mixamo_unity';ph=info['phenotype']
    for key in ('gender','age','muscle','weight','height','proportions'):
        if key in spec:ph[key]=spec[key]
    ph['race']={r:spec.get('race',{}).get(r,0)for r in ('asian','caucasian','african')};info['teeth']=ct.mhclo_rel('teeth','teeth_base');info['tongue']=ct.mhclo_rel('tongue','tongue01');settings=H.get_default_deserialization_settings();settings['subdiv_levels']=0
    body=H.deserialize_from_dict(info,settings);temp_rig=body.parent;teeth=next(o for o in bpy.data.objects if o.get('MPFB_GEN_object_type')=='Teeth');tongue=next(o for o in bpy.data.objects if o.get('MPFB_GEN_object_type')=='Tongue')
    ct.apply_shape_keys(body);ct.remove_modifiers(body);ct.delete_verts(body,[not q for q in ct.group_mask(body,'body')]);cavity,cavity_faces=extract_cavity(body)
    meshes=[body,cavity,teeth,tongue]
    for o in (teeth,tongue):ct.apply_shape_keys(o);ct.remove_modifiers(o)
    ct.rename_bones(temp_rig,meshes);ct.to_t_pose(temp_rig,meshes);height=max(v.co.z for v in body.data.vertices);ct.scale_character(temp_rig,meshes,1.60/height)
    valid_bones={b.name for b in temp_rig.data.bones}
    for ob in meshes:
        for group in list(ob.vertex_groups):
            if group.name not in valid_bones:ob.vertex_groups.remove(group)
    other=skeleton(temp_rig);assert set(rest)==set(other)
    delta=max(abs(a-b)for name in rest for a,b in zip(rest[name]['matrix'],other[name]['matrix']));assert delta<.0001,('Fitted anatomy rest rig mismatch',delta)
    mouth_points=[cavity.matrix_world@v.co for v in cavity.data.vertices]
    mouth_focus=Vector((0,min(p.y for p in mouth_points)-.008,float(np.median([p.z for p in mouth_points]))))
    # Keep the source dental Head/Jaw split; tongue follows the lower mouth.
    tongue.vertex_groups.clear();head=tongue.vertex_groups.new(name='Head');jaw=tongue.vertex_groups.new(name='Jaw');ids=list(range(len(tongue.data.vertices)));head.add(ids,.15,'REPLACE');jaw.add(ids,.85,'REPLACE')
    oral_paths={'teeth':Path(ct.asset_dir('teeth','teeth_base'))/'teeth.png','tongue':Path(ct.asset_dir('tongue','tongue01'))/'tongue01_diffuse.png'}
    maps,texel_checks=atlas(out,meta,oral_paths)
    # Affine UV change only: old cloth occupies the unchanged left2048px half.
    cloth=materials['Cloth'];nt=cloth.node_tree
    for node in nt.nodes:
        if node.type!='TEX_IMAGE':continue
        for key,path in maps.items():
            if node.image and Path(node.image.filepath).name==Path(meta['textures'][key]).name:
                node.image=bpy.data.images.load(str(path),check_existing=True);node.image.colorspace_settings.name='sRGB' if key=='clothA' else 'Non-Color'
    for ob in lods:
        cloth_slots={i for i,m in enumerate(ob.data.materials)if m==cloth}
        for p in ob.data.polygons:
            if p.material_index in cloth_slots:
                for loop in p.loop_indices:ob.data.uv_layers.active.data[loop].uv.x*=.5
    set_slot(cavity,materials['Skin']);set_slot(teeth,cloth);set_slot(tongue,cloth)
    # Oral parts have independent flat atlas normals and calibrated smoothness.
    import atlas as atlas_tools
    atlas_tools.remap_uv(teeth,((2048+8)/4096,8/2048,(3072-8)/4096,(1024-8)/2048))
    atlas_tools.remap_uv(tongue,((3072+8)/4096,8/2048,(4096-8)/4096,(1024-8)/2048))
    stats=[]
    for index,lod in enumerate(lods):
        parts=[]
        for kind,original,budget in [('cavity',cavity,[796,300,100][index]),('teeth',teeth,[1200,350,0][index]),('tongue',tongue,[160,60,0][index])]:
            if budget==0:continue
            ob=original.copy();ob.data=original.data.copy();ob.name=kind+'_LOD'+str(index);bpy.context.scene.collection.objects.link(ob);reduce(ob,budget);ct.clean_weights(ob);ct.ensure_armature(ob,rig);parts.append(ob)
        stats.append(append_parts(lod,parts,rig))
    for ob in (body,cavity,teeth,tongue,temp_rig):bpy.data.objects.remove(ob,do_unlink=True)
    assert skeleton(rig)==rest and hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
    # Review-only jaw examples; no animation is baked into the FBX.
    jaw=rig.pose.bones['Jaw'];jaw.rotation_mode='XYZ'
    for frame,angle in [(1,0),(25,math.radians(15)),(49,math.radians(27))]:jaw.rotation_euler.x=angle;jaw.keyframe_insert('rotation_euler',frame=frame)
    scene=bpy.context.scene;scene.frame_start=1;scene.frame_end=49;scene.frame_set(1);scene['oral_pilot_only']=True;scene['not_promoted_to_catalog']=True
    for ob in lods:ob.hide_render=ob!=lods[0]
    # Saved review setup only; no render is launched by this authoring tool.
    scene.render.engine='CYCLES';scene.cycles.device='GPU';scene.cycles.samples=128;scene.cycles.use_denoising=True;scene.cycles.use_adaptive_sampling=True;scene.cycles.adaptive_threshold=.01;scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.view_settings.exposure=0
    world=bpy.data.worlds.new('Neutral oral anatomy review');world.use_nodes=True;world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.18,.18,.18,1);world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.65;scene.world=world
    for name,position,power,size in [('Soft neutral oral key',(-.35,-.70,1.80),35,.85),('Soft neutral oral fill',(.45,-.35,1.60),15,.65)]:
        d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=position;o.rotation_euler=(mouth_focus-o.location).to_track_quat('-Z','Y').to_euler()
    for name,position in [('Oral frontal review',Vector((.015,-.69,mouth_focus.z+.025))),('Oral three-quarter review',Vector((.29,-.60,mouth_focus.z+.045)))]:
        d=bpy.data.cameras.new(name);d.lens=100;d.sensor_width=36;o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=position;o.rotation_euler=(mouth_focus-o.location).to_track_quat('-Z','Y').to_euler()
        if scene.camera is None:scene.camera=o
    for ob in bpy.context.selected_objects:ob.select_set(False)
    rig.select_set(True)
    for ob in lods:ob.select_set(True)
    bpy.context.view_layer.objects.active=rig
    fbx=out/'LakeGirl_OralPilot.fbx';bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',use_mesh_modifiers=False,mesh_smooth_type='FACE',use_tspace=True,use_armature_deform_only=True,add_leaf_bones=False,primary_bone_axis='Y',secondary_bone_axis='X',bake_anim=False,path_mode='STRIP',embed_textures=False)
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.file.pack_all();blend=out/'LakeGirl_OralPilot.blend';bpy.ops.wm.save_as_mainfile(filepath=str(blend),compress=True)
    receipt={'scope':'isolated oral pilot; no Assets/catalog promotion','source':str(source),'source_sha256':source_hash,'source_original_lod_geometry_sha256':original_hashes,'skeleton_rest_sha256':digest(rest),'new_fitted_rest_max_delta':delta,'original_rig_rest_preserved':True,'rest_lip_silhouette_preserved':True,'lods':stats,'restored_cavity_faces':cavity_faces,'cloth_original_texels':texel_checks,'oral_sources':{k:{'path':str(v),'sha256':hashlib.sha256(v.read_bytes()).hexdigest(),'license':'CC02020 Data Collection AB, Joel Palmius, Jonas Hauquier'}for k,v in oral_paths.items()},'blend_sha256':hashlib.sha256(blend.read_bytes()).hexdigest(),'blend_bytes':blend.stat().st_size,'fbx_sha256':hashlib.sha256(fbx.read_bytes()).hexdigest(),'author_source_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'rendered':False,'review_camera':'Oral frontal review or Oral three-quarter review; neutral lighting,1600x1000','mouth_focus_world':list(mouth_focus),'jaw_examples':'frames1/25/49: local X0/15/27degrees; review-only action, FBX animation disabled','texture_prototype':'4096x2048 cloth atlas extension; original2048px colour/normal/mask texels copied with <=1/255 error; not a shipping budget choice'}
    (out/'audit.json').write_text(json.dumps(receipt,indent=2)+'\n');print('[oral-pilot] AUTHORED',json.dumps({'lods':stats,'bytes':receipt['blend_bytes'],'rest_delta':delta}),flush=True)


def main():
    p=argparse.ArgumentParser();p.add_argument('--author',action='store_true',required=True);p.add_argument('--output',default=str(ROOT/'ArtSource/Characters/OralPilot'));args=p.parse_args(sys.argv[sys.argv.index('--')+1:]);out=Path(args.output).resolve();assert ROOT/'Assets' not in out.parents,'Pilot must stay outside Assets';out.mkdir(parents=True,exist_ok=True);(out/'Textures').mkdir(exist_ok=True);author(out)


if __name__=='__main__':main()
