#!/usr/bin/env python3
"""Exact eight-line island VO reproducer. Dry by default; CPU/offline candidates below Logs only.
Use the existing audio venv directly, never the installer wrapper:
  $PARASITE_AUDIO_WORK/venv/bin/python Tools/audio/gen_island_voice.py --out Logs/island-voice-regeneration/review01 [--go]
"""
from pathlib import Path
import argparse,hashlib,json,os,re,sys,time

def digest(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as source:
        for block in iter(lambda:source.read(1024*1024),b''):h.update(block)
    return h.hexdigest()
def key(speaker,line):
    norm=lambda s:re.sub(r'\s+',' ',re.sub(r'<[^>]+>','',s)).strip()
    h=0xcbf29ce484222325
    for b in (norm(speaker).upper()+'|'+norm(line)).encode():h=((h^b)*0x100000001b3)&0xffffffffffffffff
    return f'vo_{h:016x}'
def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--project',type=Path,default=Path(__file__).resolve().parents[2])
    ap.add_argument('--inventory',type=Path)
    ap.add_argument('--out',type=Path,required=True)
    ap.add_argument('--go',action='store_true')
    args=ap.parse_args();root=args.project.resolve()
    inventory=args.inventory or root/'Tools/audio/island_voice_lines.json'
    data=json.loads(inventory.read_text());out=args.out.resolve()
    if not out.is_relative_to((root/'Logs').resolve()):raise SystemExit('Candidates must stay below ignored Logs; live publication is unsupported.')
    if digest(root/'Tools/audio/gen_voice.py')!=data['generator_sha256'] or digest(root/'Tools/audio/common.py')!=data['dsp_sha256']:
        raise SystemExit('Reviewed generator/DSP changed; review the inventory before rendering.')
    cast_file=root/'Tools/audio/voice_cast.json'
    if (digest(cast_file) if cast_file.exists() else None)!=data['cast_overrides_sha256']:raise SystemExit('Reviewed casting changed.')
    assert len(data['lines'])==8 and len({e['key'] for e in data['lines']})==8
    source_hashes={}
    for e in data['lines']:
        if key(e['speaker'],e['line'])!=e['key']:raise SystemExit('Inventory key mismatch: '+e['key'])
        source=root/e['source'];text=source.read_text()
        literals=[json.loads('"'+m+'"') for m in re.findall(r'"((?:[^"\\]|\\.)*)"',text)]
        if e['speaker'] not in literals or e['line'] not in literals:raise SystemExit('Runtime literal changed: '+e['key'])
        source_hashes[e['source']]=digest(source)
    work=Path(os.environ.get('PARASITE_AUDIO_WORK',str(Path.home()/'Data/parasite-work/audio')))
    cache=work/'hf/hub/models--hexgrad--Kokoro-82M'
    if (cache/'refs/main').read_text().strip()!=data['model_revision']:raise SystemExit('Cached model revision differs; no download is allowed.')
    snapshot=cache/'snapshots'/data['model_revision']
    for rel in data['cache_files_sha256']:
        if not (snapshot/rel).is_file():raise SystemExit('Missing offline cache input: '+rel)
    print(json.dumps(dict(mode='render' if args.go else 'DRY RUN',keys=[e['key'] for e in data['lines']],device='CPU',threads=2,network='disabled',output=str(out)),indent=2),flush=True)
    if not args.go:return
    if out.exists() and any(out.iterdir()):raise SystemExit('Use a new empty output; existing clips are never overwritten.')
    for rel,expected in data['cache_files_sha256'].items():
        if digest(snapshot/rel)!=expected:raise SystemExit('Cached model/voice content mismatch: '+rel)
    os.environ.update(CUDA_VISIBLE_DEVICES='',HF_HUB_OFFLINE='1',TRANSFORMERS_OFFLINE='1',HF_DATASETS_OFFLINE='1',HF_HOME=str(work/'hf'),
      OMP_NUM_THREADS='2',MKL_NUM_THREADS='2',OPENBLAS_NUM_THREADS='2',NUMBA_NUM_THREADS='2',TOKENIZERS_PARALLELISM='false',
      PYTHONDONTWRITEBYTECODE='1',NUMBA_CACHE_DIR=str(work/'numba-cache'))
    sys.dont_write_bytecode=True
    import socket
    def offline(*a,**k):raise RuntimeError('Network/install fallback is disabled.')
    class OfflineSocket(socket.socket):
        def connect(self,*a,**k):return offline(*a,**k)
        def connect_ex(self,*a,**k):return offline(*a,**k)
    socket.socket=OfflineSocket;socket.create_connection=offline
    import torch,spacy.cli
    torch.set_num_threads(2);torch.set_num_interop_threads(2);spacy.cli.download=offline
    sys.path.insert(0,str(root/'Tools/audio'))
    import gen_voice as gv
    if gv.RENDER_VERSION!=data['render_version']:raise RuntimeError('Render version differs.')
    for name,profile in data['profiles'].items():
        if gv.profile(name)!=profile:raise RuntimeError('DSP/casting profile differs: '+name)
    out.mkdir(parents=True,exist_ok=True);began=time.monotonic();tts=gv.Tts()
    devices=sorted({str(p.device) for p in tts.pipes['a'].model.parameters()})
    if devices!=['cpu']:raise RuntimeError('CPU-only invariant failed: '+str(devices))
    from importlib import metadata
    packages={name:metadata.version(name) for name in data['reference_environment']}
    records=[]
    provenance=dict(inventory_sha256=digest(inventory),driver_sha256=digest(__file__),source_hashes=source_hashes,actual_devices=devices,
      torch_threads=torch.get_num_threads(),torch_interop_threads=torch.get_num_interop_threads(),network='disabled',render_inputs=data,packages=packages,clips=records,publication_performed=False)
    for e in data['lines']:
        started=time.monotonic()
        audio=gv.render(tts,gv.tts_text(e['line']),gv.profile(e['profile']),seed=int(e['key'][3:11],16)&0xffff,max_dur=e['render_fit_window_seconds']-.15)
        temp=out/(e['key']+'.tmp.ogg');final=out/(e['key']+'.ogg')
        gv.write_voice(str(temp),audio);os.replace(temp,final)
        seconds=len(audio)/gv.VSR;actual=digest(final)
        records.append(dict(e,duration_seconds=round(seconds,6),sha256=actual,bytes=final.stat().st_size,render_seconds=round(time.monotonic()-started,3),reference_hash_equal=actual==e['reference_ogg_sha256']))
        (out/'generation-provenance.json').write_text(json.dumps(provenance,indent=2)+'\n')
        print(e['key'],round(seconds,3),'seconds',flush=True)
    provenance['elapsed_seconds']=round(time.monotonic()-began,3)
    (out/'generation-provenance.json').write_text(json.dumps(provenance,indent=2)+'\n')
    fragment=[dict(key=e['key'],speaker=e['speaker'],line=e['line'],profile=e['profile'],voice=gv.profile(e['profile'])['voice'],
      duration=round(e['duration_seconds'],3),window=e['reference_runtime_window_seconds'],sources=[e['source']],render=gv.RENDER_VERSION) for e in records]
    (out/'voice_manifest.fragment.json').write_text(json.dumps(dict(lines=fragment),indent=1)+'\n')
    print('Candidates only. Validate complete decode/levels/duration and review any reference hash drift before root publishes.',flush=True)
if __name__=='__main__':main()
