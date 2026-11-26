"""Small additive V2 -> V3 formed roof end-cap correction. Never renders/overwrites V2.

Default is a stdout-only plan. Root may authorize the prepared CPU source patch after V2 image review:
  nice -n 15 blender -b -t 2 --python-exit-code 1 --python seal_endcaps.py -- --apply
"""
import hashlib,json,sys
from pathlib import Path

HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];OUT=ROOT/'ArtSource/RealisticFinales/Train'
SOURCE=OUT/'Northline_RealisticDeparture_v2.blend';TARGET=OUT/'Northline_RealisticDeparture_v3.blend'
PIN='086fe5ff32719247a4efad339f078190557d1c681d672458373438ca4d343bb1'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    assert sha(SOURCE)==PIN,'Frozen V2 source changed'
    profile={'width_metres':3.12,'bottom_z_metres':3.22,'end_y_metres':[-6.985,6.985],'depth_metres':.08,
             'top':'exact roof_z(x) - .179, overlaps inner roof by 6 mm at Y+/-7.025',
             'glass':'No window/pane/panel removed, hidden or edited. New geometry stays above windscreen.'}
    if '--apply' not in sys.argv:
        print(json.dumps({'status':'PLAN_ONLY','source':str(SOURCE),'source_sha256':PIN,'new_source':str(TARGET),'patch':profile},indent=2));return
    assert not TARGET.exists(),'Never overwrite an existing V3 source'
    import bpy,bmesh
    sys.path.insert(0,str(HERE));from geometry import Batch
    from shell import roof_z
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE),load_ui=False,use_scripts=False)
    scene=bpy.context.scene;scene.cycles.device='CPU';scene.render.threads_mode='FIXED';scene.render.threads=2
    rig=bpy.data.objects['ACTUAL TRAIN RIG - visual only'];mat=bpy.data.materials['Coach upper ivory enamel']
    g=Batch('V3 sealed formed roof end-caps - additive above glazing')
    cross=[(-1.56,3.22),(1.56,3.22)]+[(1.56-3.12*i/40,roof_z(1.56-3.12*i/40)-.179) for i in range(41)]
    for y in (-6.985,6.985):g.extrude(cross,.08,(0,y,0),mat)
    ob=g.finish(rig);ob['purpose']='Seal real arched roof/cab gap. Original panes and occluders unchanged.'
    bm=bmesh.new();bm.from_mesh(ob.data);assert all(len(e.link_faces)==2 for e in bm.edges);assert bm.calc_volume(signed=True)>0;bm.free()
    assert min(v.co.z for v in ob.data.vertices)>3.21,'End cap entered window aperture'
    scene['source_version']=3;scene['derived_v2_sha256']=PIN;scene['additive_patch']='Two formed sealed roof end caps above existing windows; no glass/occluders changed'
    scene.frame_set(1);bpy.context.preferences.filepaths.save_version=0;bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(TARGET),compress=True)
    assert sha(SOURCE)==PIN
    receipt=json.loads((OUT/'train-authoring-v2.json').read_text());receipt.update(version=3,path=str(TARGET.relative_to(ROOT)),sha256=sha(TARGET),bytes=TARGET.stat().st_size,
        derived_from_v2={'path':str(SOURCE.relative_to(ROOT)),'sha256':PIN},roof_end_caps=profile,patch_script_sha256=sha(Path(__file__)),rendered=False)
    (OUT/'train-authoring-v3.json').write_text(json.dumps(receipt,indent=2)+'\n')
    import author
    author.VERSION=3;author.audit()
    print('[realistic-train] V3 additive roof end caps saved and audited; V2 unchanged; no render',flush=True)


if __name__=='__main__':main()
