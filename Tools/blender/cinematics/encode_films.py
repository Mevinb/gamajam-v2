"""Encode already-complete Cycles frame sequences and record derived render/codec evidence."""
from pathlib import Path
import ast, hashlib, json, os, re, subprocess, sys
P=Path(__file__).resolve().parents[3]
NAMES={'opening':'ContainmentOpening','ending':'LakesideEnding'}

def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
    return h.hexdigest()

for kind in sys.argv[1:] or NAMES:
    name=NAMES[kind]; frames=P/'Logs'/'cinematic-films'/kind; log=P/'Logs'/f'blender-film-{kind}-render.log'
    text=log.read_text(); assert f'[film] COMPLETE {kind} render' in text, 'A successful full Cycles render is required'
    source_frames=[frames/f'frame-{i:04d}.png' for i in range(1,73)]
    assert all(p.is_file() for p in source_frames), 'All72 actual source frames must exist'
    device_line=next(line[len('[film] DEVICE '):] for line in text.splitlines() if line.startswith('[film] DEVICE '))
    devices=ast.literal_eval(device_line); enabled=[{'name':n,'backend':t} for n,t,on in devices if on]
    assert len(enabled)==1 and enabled[0]['backend']=='OPTIX' and '4050' in enabled[0]['name'], 'RTX4050 OptiX render evidence required'
    output=P/'Assets'/'Parasite'/'Resources'/'Cinematics'/'Films'/f'{name}.webm';output.parent.mkdir(parents=True,exist_ok=True)
    temporary=frames/f'{name}.encode.webm'
    subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-y','-framerate','24','-i',str(frames/'frame-%04d.png'),'-frames:v','72','-c:v','libvpx','-deadline','good','-cpu-used','2','-threads','4','-b:v','4500k','-crf','8','-pix_fmt','yuv420p','-an',str(temporary)],check=True)
    probe=json.loads(subprocess.check_output(['ffprobe','-v','error','-count_frames','-select_streams','v:0','-show_entries','stream=codec_name,width,height,r_frame_rate,nb_read_frames:format=duration,size','-of','json',str(temporary)]))
    stream=probe['streams'][0];assert stream['codec_name']=='vp8' and stream['nb_read_frames']=='72';assert stream['width']==1280 and stream['height']==720;assert abs(float(probe['format']['duration'])-3)<.02
    os.replace(temporary,output) # Publish only a complete validated clip; Unity cannot import a partial encode.
    source=P/'ArtSource'/'Cinematics'/('OpeningFilm.blend' if kind=='opening' else 'EndingFilm.blend')
    evidence={'film':name,'renderer':'Blender Cycles','enabled_render_devices':enabled,'samples':32,'adaptive_threshold':.04,'denoising':True,'video':probe,'video_sha256':sha(output),'editable_source':source.relative_to(P).as_posix(),'source_sha256':sha(source),'render_log':log.relative_to(P).as_posix(),'render_log_sha256':sha(log),'source_frame_1_sha256':sha(source_frames[0]),'source_frame_72_sha256':sha(source_frames[-1])}
    evidence_path=P/'ArtSource'/'Cinematics'/f'{name}.render.json';evidence_path.write_text(json.dumps(evidence,indent=2)+'\n')
    print(json.dumps({'film':name,'codec':stream['codec_name'],'frames':72,'duration':3,'bytes':output.stat().st_size,'evidence':evidence_path.relative_to(P).as_posix()}))
