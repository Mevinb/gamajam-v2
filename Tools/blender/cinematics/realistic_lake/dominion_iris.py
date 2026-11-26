"""Measured UV iris annuli; only the explicit Dominion branch may use these."""
import json
import hashlib
import shutil
from pathlib import Path
import argparse
import sys

import bpy
import numpy as np

ROOT=Path(__file__).resolve().parents[4]
CENTRES=((.29138493650047037,.28946511384348544),(.7072312441819875,.7015827721375378))
EXPECTED_V4='19f61eef69e1f6cd63cec6d6e2fac79848ab7e8bc203a0f1b455c5008a6ae27f'


def mathnode(nt,operation,a,b):
    n=nt.nodes.new('ShaderNodeMath');n.operation=operation
    for index,value in enumerate((a,b)):
        if isinstance(value,(int,float)):n.inputs[index].default_value=value
        else:nt.links.new(value,n.inputs[index])
    return n.outputs[0]


def ramp(nt,value,lo,hi,start,end):
    n=nt.nodes.new('ShaderNodeMapRange');n.interpolation_type='SMOOTHSTEP';n.clamp=True
    for name,v in [('From Min',lo/1024),('From Max',hi/1024),('To Min',start),('To Max',end)]:n.inputs[name].default_value=v
    nt.links.new(value,n.inputs['Value']);return n.outputs['Result']


def apply_annulus(material,length):
    nt=material.node_tree;bs=nt.nodes.get('Principled BSDF');texture=next(n for n in nt.nodes if n.type=='TEX_IMAGE'and n.image.name.startswith('brown'))
    # In the hash-pinned V4 source the only eye-material animation is its old
    # reveal. Remove it and its luminance ramp; body/gaze animation is untouched.
    nt.animation_data_clear()
    for node in list(nt.nodes):
        if node.name.startswith(('Explicit Dominion iris mask','Explicit Dominion reveal only','Dominion measured')):nt.nodes.remove(node)
    for socket in ('Emission Strength','Base Color'):
        for link in list(bs.inputs[socket].links):nt.links.remove(link)
    uv=nt.nodes.new('ShaderNodeUVMap');uv.uv_map='UVMap';uv.name='Dominion measured UV; same coordinates as photograph'
    masks=[]
    for index,centre in enumerate(CENTRES):
        distance=nt.nodes.new('ShaderNodeVectorMath');distance.operation='DISTANCE';distance.name='Dominion measured iris%d distance'%index;distance.inputs[1].default_value=(*centre,0);nt.links.new(uv.outputs[0],distance.inputs[0])
        inner=ramp(nt,distance.outputs['Value'],42,46,0,1);outer=ramp(nt,distance.outputs['Value'],112,116,1,0);masks.append(mathnode(nt,'MULTIPLY',inner,outer))
    mask=mathnode(nt,'MAXIMUM',masks[0],masks[1]);mask.node.name='Dominion measured annulus; pupil and sclera protected'
    reveal=nt.nodes.new('ShaderNodeMath');reveal.name='Explicit Dominion reveal only';reveal.operation='MULTIPLY';nt.links.new(mask,reveal.inputs[0]);nt.links.new(reveal.outputs[0],bs.inputs['Emission Strength'])
    bs.inputs['Emission Color'].default_value=(.015,.22,.045,1)
    for frame,value in [(1,0),(24,0),(48,.16),(84,.25),(length,.20)]:reveal.inputs[1].default_value=value;reveal.inputs[1].keyframe_insert('default_value',frame=frame)
    tinted=nt.nodes.new('ShaderNodeMixRGB');tinted.blend_type='MULTIPLY';tinted.inputs[0].default_value=1;tinted.inputs[2].default_value=(.25,1.8,.75,1);nt.links.new(texture.outputs['Color'],tinted.inputs[1])
    colour=nt.nodes.new('ShaderNodeMixRGB');colour.name='Dominion measured iris colour; original pupil/sclera';nt.links.new(mathnode(nt,'MULTIPLY',reveal.outputs[0],2.6),colour.inputs[0]);nt.links.new(texture.outputs['Color'],colour.inputs[1]);nt.links.new(tinted.outputs[0],colour.inputs[2]);nt.links.new(colour.outputs[0],bs.inputs['Base Color'])
    material['iris_reveal_revision']=2;material['iris_centres_uv']=json.dumps(CENTRES);material['iris_inner_fade_px']='42–46';material['iris_outer_fade_px']='112–116';material['iris_emission_peak']=.25


def mask_audit():
    y,x=np.mgrid[0:1024,0:1024];mask=np.zeros((1024,1024),np.float64);pupil=np.zeros_like(mask,dtype=bool);sclera=np.ones_like(mask,dtype=bool)
    def smooth(t):t=np.clip(t,0,1);return t*t*(3-2*t)
    for cx,cy in CENTRES:
        r=np.hypot(x-cx*1024,y-cy*1024);mask=np.maximum(mask,smooth((r-42)/4)*(1-smooth((r-112)/4)));pupil|=r<=42;sclera&=r>=116
    assert float(mask[pupil].max())==0 and float(mask[sclera].max())==0
    return {'centresUV':CENTRES,'atlasSize':[1024,1024],'innerFadePx':[42,46],'outerFadePx':[112,116],'pupilMaskMax':float(mask[pupil].max()),'outsideIrisMaskMax':float(mask[sclera].max()),'nonzeroMaskPixels':int((mask>0).sum()),'peakEmissionStrength':.25,'emissionLinearRGB':[.015,.22,.045],'maskedColourMultiplier':[.25,1.8,.75],'photographLuminanceUsedForMask':False}


def main():
    p=argparse.ArgumentParser();p.add_argument('--apply',action='store_true');args=p.parse_args(sys.argv[sys.argv.index('--')+1:]);folder=ROOT/'ArtSource/RealisticFinales/Lake';path=folder/'Lake_Dominion.blend';oldhash=hashlib.sha256(path.read_bytes()).hexdigest();assert oldhash==EXPECTED_V4,'Dominion has changed since coordinated review';other={name:hashlib.sha256((folder/name).read_bytes()).hexdigest()for name in ['Lake_Arrival.blend','Lake_Hold.blend','Lake_Peace.blend']};audit=mask_audit()
    if not args.apply:print('[dominion-iris] DRY',json.dumps(audit),flush=True);return
    backup=ROOT/'Logs/realistic-lake-author/dominion-v4-iris-before';backup.mkdir(exist_ok=True)
    for source in [path,folder/'dominion-authoring.json',folder/'audit.json']:
        target=backup/source.name;assert not target.exists(),'Preserve the existing backup';shutil.copy2(source,target)
    bpy.ops.wm.open_mainfile(filepath=str(path));scene=bpy.context.scene;assert scene['film']=='dominion';hero=bpy.data.objects['LakeGirl_LOD0'];eyes=next(m for m in hero.data.materials if m.name.startswith('Eyes.'));apply_annulus(eyes,168);scene['dominion_iris_revision']=2;scene.frame_set(1);bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
    newhash=hashlib.sha256(path.read_bytes()).hexdigest();assert all(hashlib.sha256((folder/name).read_bytes()).hexdigest()==sha for name,sha in other.items())
    receipt=json.loads((folder/'dominion-authoring.json').read_text());receipt['sha256']=newhash;receipt['bytes']=path.stat().st_size;receipt['dominion_iris_revision']=2;receipt['previous_v4_sha256']=oldhash;receipt['iris_mask_audit']=audit;(folder/'dominion-authoring.json').write_text(json.dumps(receipt,indent=2)+'\n')
    aggregate=json.loads((folder/'audit.json').read_text());aggregate['films']['dominion'].update({'sha256':newhash,'bytes':path.stat().st_size,'dominion_iris_revision':2,'iris_mask_audit':audit});aggregate['total_blend_bytes']=sum(v['bytes']for v in aggregate['films'].values());(folder/'audit.json').write_text(json.dumps(aggregate,indent=2)+'\n')
    report={'before':oldhash,'after':newhash,'otherThreeHashesUnchanged':other,'mask':audit,'source_only':True,'rendered':False};(ROOT/'Logs/realistic-lake-author/dominion-iris-annulus-patch.json').write_text(json.dumps(report,indent=2)+'\n');print('[dominion-iris] PATCHED',json.dumps(report),flush=True)


if __name__=='__main__':main()
