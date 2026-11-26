"""V3 -> V4: only the bogie-shot camera/focus and a shot-local physical soft fill.

No render or runtime write. V3 remains immutable.
  nice -n 15 blender -b -t 2 --python-exit-code 1 --python improve_bogie.py -- --apply
"""
import hashlib,json,math,sys
from array import array
from pathlib import Path

HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];OUT=ROOT/'ArtSource/RealisticFinales/Train'
SOURCE=OUT/'Northline_RealisticDeparture_v3.blend';TARGET=OUT/'Northline_RealisticDeparture_v4.blend'
PIN='d7b2dfaaca96ac8a79698bc6c49b3c201f74eaac38375efb1645674ae80333c9'
CAMERA='02 - 50 mm running gear and real ballast';FOCUS=CAMERA+' focus'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()


def mesh_digest(mesh):
    h=hashlib.sha256()
    for collection,attribute,count,kind in [(mesh.vertices,'co',len(mesh.vertices)*3,'f'),
            (mesh.loops,'vertex_index',len(mesh.loops),'i'),(mesh.polygons,'loop_start',len(mesh.polygons),'i'),
            (mesh.polygons,'loop_total',len(mesh.polygons),'i'),(mesh.polygons,'material_index',len(mesh.polygons),'i')]:
        values=array(kind,[0])*count;collection.foreach_get(attribute,values);h.update(values.tobytes())
    for layer in mesh.uv_layers:
        values=array('f',[0])*(len(layer.data)*2);layer.data.foreach_get('uv',values);h.update(values.tobytes())
    h.update('|'.join(m.name if m else '' for m in mesh.materials).encode())
    return h.hexdigest()


def object_snapshot(scene,excluded):
    result={}
    for frame in (1,84,85,100,144,145,168,256,336,337):
        scene.frame_set(frame)
        result[str(frame)]={o.name:[float(v) for row in o.matrix_world for v in row] for o in scene.objects if o.name not in excluded}
    return result


def main():
    assert sha(SOURCE)==PIN,'Frozen V3 source changed'
    if '--apply' not in sys.argv:
        print(json.dumps({'status':'PLAN_ONLY','source':str(SOURCE),'sha256':PIN,'target':str(TARGET),
            'changes':'Only bogie camera 85-144, measured wheel-surface focus at f/11, 28 W broad soft fill active only during this shot'},indent=2));return
    assert not TARGET.exists(),'Never overwrite an existing V4 source'
    import bpy
    from mathutils import Vector
    from bpy_extras.object_utils import world_to_camera_view
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE),load_ui=False,use_scripts=False)
    scene=bpy.context.scene;scene.cycles.device='CPU';scene.render.threads_mode='FIXED';scene.render.threads=2
    rig=bpy.data.objects['ACTUAL TRAIN RIG - visual only'];cam=bpy.data.objects[CAMERA];focus=bpy.data.objects[FOCUS]
    excluded={CAMERA,FOCUS};before=object_snapshot(scene,excluded)
    meshes={m.name:mesh_digest(m) for m in bpy.data.meshes}
    camera_controls={o.name:{'lens':o.data.lens,'fstop':o.data.dof.aperture_fstop,'focus':o.data.dof.focus_object.name if o.data.dof.focus_object else ''}
                     for o in scene.objects if o.type=='CAMERA' and o!=cam}
    world_pin=sha(SOURCE) # Packed HDRI/material/world source remains byte-pinned by the immutable parent.
    cam.animation_data_clear();focus.animation_data_clear();cam.data.dof.aperture_fstop=11.;cam.data.lens=50
    for frame,p,target in [(85,(4.90,-6.80,1.20),(.30,-4.5,.68)),(144,(5.05,-6.25,1.18),(.30,-4.5,.68))]:
        cam.location=p;cam.rotation_euler=(Vector(target)-Vector(p)).to_track_quat('-Z','Y').to_euler()
        cam.keyframe_insert('location',frame=frame);cam.keyframe_insert('rotation_euler',frame=frame)
    # A real ray intersection on the near leading wheel's lower tread supplies focus,
    # rather than a point inside an opaque bogie crossmember.
    scene.frame_set(100);bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
    local_aim=Vector((.8135,-5.7566,.44));aim=rig.matrix_world@local_aim;origin=cam.matrix_world.translation.copy();direction=(aim-origin).normalized()
    hit,point,normal,face,object_hit,matrix=scene.ray_cast(deps,origin,direction,distance=(aim-origin).length+.10)
    assert hit and object_hit.name.startswith('Rotating wheelset -5.4'),('Focus ray did not reach leading wheel surface',object_hit.name if hit else None)
    focus.location=rig.matrix_world.inverted()@point;focus.keyframe_insert('location',frame=85);focus.keyframe_insert('location',frame=144)
    focus_record={'object':object_hit.name,'face':face,'local_surface_point':list(focus.location),'world_surface_point_frame100':list(point),
                  'distance_metres_frame100':(point-origin).length,'lens_mm':cam.data.lens,'fstop':cam.data.dof.aperture_fstop}
    data=bpy.data.lights.new('Bogie shot only - 28 W broad diffused inspection fill','AREA');data.shape='RECTANGLE';data.size=3.0;data.size_y=1.6;data.color=(.88,.94,1.)
    fill=bpy.data.objects.new(data.name,data);scene.collection.objects.link(fill);fill.parent=rig;fill.location=(3.65,-4.65,1.58)
    fill.rotation_euler=(Vector((.65,-4.5,.60))-fill.location).to_track_quat('-Z','Y').to_euler()
    # Half-frame bounds keep shutter samples of neighbouring shots completely unlit.
    for frame,power in [(1,0),(84.5,0),(84.5001,28),(144.4999,28),(144.5,0),(337,0)]:
        data.energy=power;data.keyframe_insert('energy',frame=frame)
    for fc in data.animation_data.action.fcurves:
        for key in fc.keyframe_points:key.interpolation='CONSTANT'
    excluded.add(fill.name)
    assert object_snapshot(scene,excluded)==before,'Unrelated object transforms changed'
    assert meshes=={m.name:mesh_digest(m) for m in bpy.data.meshes},'Geometry or UV/material assignment changed'
    for name,controls in camera_controls.items():
        o=bpy.data.objects[name];assert controls=={'lens':o.data.lens,'fstop':o.data.dof.aperture_fstop,'focus':o.data.dof.focus_object.name if o.data.dof.focus_object else ''}
    framing=[]
    for frame in (85,100,120,144):
        scene.frame_set(frame);bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get();points=[]
        # Full visible leading bogie envelope: wheel flanges, brakes, springs and rail contact.
        for x in (-1.12,1.12):
            for y in (-5.85,-3.15):
                for z in (.20,1.17):points.append(world_to_camera_view(scene,cam,rig.matrix_world@Vector((x,y,z))))
        minx,miny=min(p.x for p in points),min(p.y for p in points);maxx,maxy=max(p.x for p in points),max(p.y for p in points)
        assert minx>.015 and maxx<.985 and miny>.015 and maxy<.985,(frame,'Bogie envelope cropped',minx,maxx,miny,maxy)
        framing.append({'frame':frame,'screen_min':[minx,miny],'screen_max':[maxx,maxy],'fill_watts':data.energy,
                        'focus_distance_metres':(focus.matrix_world.translation-cam.matrix_world.translation).length})
    for frame in (1,84,145,168,256,336):
        scene.frame_set(frame);assert data.energy==0,(frame,'Fill leaked into accepted shot')
    assert (scene.frame_start,scene.frame_end,scene.render.fps)==(1,336,24)
    scene['source_version']=4;scene['derived_v3_sha256']=PIN;scene['bogie_only_patch']='Clear 3/4 camera, wheel-surface focus at f/11, broad 28 W shot-local physical fill; geometry/other shots unchanged'
    scene.frame_set(1);bpy.context.preferences.filepaths.save_version=0;bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(TARGET),compress=True)
    assert sha(SOURCE)==PIN
    receipt=json.loads((OUT/'train-authoring-v3.json').read_text());receipt.update(version=4,path=str(TARGET.relative_to(ROOT)),sha256=sha(TARGET),bytes=TARGET.stat().st_size,
        derived_from_v3={'path':str(SOURCE.relative_to(ROOT)),'sha256':PIN},bogie_focus=focus_record,bogie_framing=framing,
        bogie_fill={'radiant_watts':28,'size_metres':[3,1.6],'active_shot_frames':[85,144],'zero_in_other_shots':True},
        patch_script_sha256=sha(Path(__file__)),other_transforms_exact=True,geometry_uv_material_assignments_exact=True,other_cameras_exact=True,rendered=False)
    receipt['shots'][1]['fstop']=11.
    (OUT/'train-authoring-v4.json').write_text(json.dumps(receipt,indent=2)+'\n')
    audit={'passed':True,'scope':'CPU source/geometry/framing/focus audit only; no render or visual acceptance',
        'parent_v3_sha256':PIN,'source_v4_sha256':sha(TARGET),'all_mesh_payload_hashes':meshes,'other_sampled_transforms_exact':True,
        'other_camera_controls_exact':True,'no_geometry_change':True,'frame_range':[1,336],'fps':24,'bogie_focus':focus_record,'bogie_framing':framing,
        'fill_zero_at_frames':[1,84,145,168,256,336],'unpacked_images':[i.name for i in bpy.data.images if i.source=='FILE' and not i.packed_file]}
    assert not audit['unpacked_images']
    (OUT/'bogie-camera-audit-v4.json').write_text(json.dumps(audit,indent=2)+'\n')
    sys.path.insert(0,str(HERE));import author;author.VERSION=4;author.audit()
    print('[realistic-train] V4 camera/fill only saved and audited. V3 immutable; no render.',flush=True)


if __name__=='__main__':main()
