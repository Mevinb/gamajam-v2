"""Root-run still recipe for the frozen oral pilot; dry unless --execute.

blender -b ArtSource/Characters/OralPilot/LakeGirl_OralPilot.blend -t 2 \
 --python-exit-code 1 --python Tools/blender/characters/render_oral_pilot.py \
 -- --execute --camera front --frames 1 25 49 --samples 128 --output <fresh dir>

No source, Assets, animation or catalog file is saved/modified.
"""
import argparse
import hashlib
import json
import sys
import time
from pathlib import Path

import bpy

ROOT=Path(__file__).resolve().parents[3]
SOURCE=ROOT/'ArtSource/Characters/OralPilot/LakeGirl_OralPilot.blend'
CAMERAS={'front':'Oral frontal review','threequarter':'Oral three-quarter review'}


def main():
    p=argparse.ArgumentParser();p.add_argument('--execute',action='store_true');p.add_argument('--camera',choices=list(CAMERAS),default='front');p.add_argument('--frames',type=int,nargs='+',default=[1,25,49]);p.add_argument('--samples',type=int,default=128);p.add_argument('--output',default=str(ROOT/'Logs/full-cinematic-renders/oral-pilot-qa-v1'));args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    assert Path(bpy.data.filepath).resolve()==SOURCE.resolve(),'Open the frozen pilot source with -b first'
    evidence=json.loads(SOURCE.with_name('audit.json').read_text());source_hash=hashlib.sha256(SOURCE.read_bytes()).hexdigest();assert source_hash==evidence['blend_sha256'],'Frozen oral source hash changed'
    scene=bpy.context.scene;camera=bpy.data.objects[CAMERAS[args.camera]];assert camera.type=='CAMERA'
    unpacked=[i.name for i in bpy.data.images if i.source=='FILE' and not i.packed_file];assert not unpacked,unpacked
    assert all(scene.frame_start<=f<=scene.frame_end for f in args.frames)
    result={'source':str(SOURCE.relative_to(ROOT)),'source_sha256':source_hash,'all_file_images_packed':True,'camera':camera.name,'frames':args.frames,'fps':scene.render.fps,'fps_base':scene.render.fps_base,'resolution':[1600,1000],'samples':args.samples,'mode':'execute' if args.execute else 'dry','rendered':[]}
    if not args.execute:print('[oral-render] DRY',json.dumps(result),flush=True);return
    prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='OPTIX';prefs.get_devices();devices=[]
    for device in prefs.devices:
        device.use=device.type=='OPTIX' and 'RTX' in device.name
        if device.use:devices.append(device.name)
    assert devices,'An actual RTX OptiX device is required; CPU fallback is refused'
    scene.camera=camera;scene.cycles.device='GPU';scene.cycles.samples=args.samples;scene.cycles.use_denoising=True;scene.cycles.use_adaptive_sampling=True;scene.cycles.adaptive_threshold=.01;scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGB';scene.render.image_settings.color_depth='8'
    folder=Path(args.output).resolve();folder.mkdir(parents=True,exist_ok=True);result['optix_devices']=devices
    for frame in args.frames:
        path=folder/('%s-%06d.png'%(args.camera,frame));assert not path.exists(),'Use a fresh output directory instead of overwriting QA'
        scene.frame_set(frame);scene.render.filepath=str(path);started=time.monotonic();bpy.ops.render.render(write_still=True)
        result['rendered'].append({'frame':frame,'file':str(path),'seconds':time.monotonic()-started,'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==source_hash
    receipt=folder/(args.camera+'-receipt.json');assert not receipt.exists();receipt.write_text(json.dumps(result,indent=2)+'\n');print('[oral-render] COMPLETE',str(receipt),flush=True)


if __name__=='__main__':main()
