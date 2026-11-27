"""Independent decoded RGBA atlas-scope check. CPython + Pillow/numpy; no Blender/GPU."""
from pathlib import Path
import argparse, hashlib, json, os
from PIL import Image
import numpy as np

def main():
    p=argparse.ArgumentParser();p.add_argument('--candidate',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();candidate=a.candidate.resolve();out=a.output.resolve();assert not out.exists();out.mkdir();manifest=json.loads((Path(__file__).resolve().parent/'refinement-manifest.json').read_text());rows=[];sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
    mask=np.ones((2048,2048),bool);mask[:1024,:1024]=False
    for key in 'ANM':
        old=candidate/'ReferenceAtlas'/('EnvironmentRealism_'+key+'.png');new=candidate/'Textures'/old.name;assert sha(old)==manifest['atlasInputs'][key]['originalPackedSha256'] and sha(new)==manifest['atlasInputs'][key]['acceptedSha256'];before=np.array(Image.open(old).convert('RGBA'))[::-1].copy();after=np.array(Image.open(new).convert('RGBA'))[::-1].copy();assert before.shape==after.shape==(2048,2048,4);same=np.array_equal(before[mask],after[mask]);rows.append({'key':key,'originalSha256':sha(old),'acceptedSha256':sha(new),'outsideThreeQuartersAllRGBAExact':bool(same),'outsideBottomUpRGBA256':hashlib.sha256(before[mask].tobytes()).hexdigest(),'acceptedAlphaRange':[int(after[:,:,3].min()),int(after[:,:,3].max())]});assert same
    rel=next(v.split(':',2)[2] for v in Path('/proc/self/cgroup').read_text().splitlines() if v.startswith('0::'));group=Path('/sys/fs/cgroup')/rel.lstrip('/');budget={k:(group/k).read_text().strip() for k in ('cpu.max','memory.max','memory.peak')};budget['nice']=os.getpriority(os.PRIO_PROCESS,0);q,p=budget['cpu.max'].split();assert q!='max' and int(q)/int(p)<=.200001 and int(budget['memory.max'])<=1536*1024**2 and budget['nice']>=19
    (out/'receipt.json').write_text(json.dumps({'status':'COMBINED_ATLAS_OUTSIDE_QUADRANT_ALL_RGBA_PIXEL_EXACT_PASS','scope':'Independent8bit Pillow decode in bottom-up Unity row order; no image writes/rendering/GPU.','rows':rows,'budget':budget},indent=2)+'\n')

if __name__=='__main__':main()
