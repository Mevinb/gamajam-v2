"""Read shipped actor speech, derive CPU audio-energy envelopes; no asset edits."""
import sys
sys.dont_write_bytecode = True
import array
import ast
import hashlib
import json
import math
import subprocess
from pathlib import Path
HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[4]
OUT = PROJECT / 'ArtSource/FullCinematics/Motion'
sys.path.insert(0, str(HERE.parent))
from motion.speech import SpeechPerformance, voice_key

SETTINGS = {'decode_sample_rate':24000,'envelope_hz':100,'rms_window_seconds':0.025,
            'attack_seconds':0.015,'release_seconds':0.055,'normalization_percentile':95,
            'gate_relative_to_p95':0.035,'gate_minimum_rms':0.0015,'aperture_exponent':0.7,
            'terminal_close_seconds':0.035}
manifest_path=PROJECT/'Assets/Parasite/Audio/voice_manifest.json'
plan_path=PROJECT/'Tools/blender/cinematics/full_story/plan.py'
voiceover_path=PROJECT/'Assets/Parasite/Scripts/Audio/VoiceOver.cs'
manifest=json.loads(manifest_path.read_text())
by_key={v['key']:v for v in manifest['lines']}
# Read only the literal CAPTIONS declaration; importing the shot module is unnecessary.
plan=ast.parse(plan_path.read_text())
captions=next(ast.literal_eval(n.value) for n in plan.body if isinstance(n,ast.Assign)
              and any(isinstance(t,ast.Name) and t.id=='CAPTIONS' for t in n.targets))
actors={'DR. HANA PETROV','DR. VALE','MAINTENANCE WORKER'}
selected=[(i,c) for i,c in enumerate(captions) if c[1] in actors and c[2]]
assert len(selected)==8
records=[]
for index,(cue,speaker,line) in selected:
    key=voice_key(speaker,line)
    item=by_key[key]
    assert item['speaker']==speaker and item['line']==line
    path=PROJECT/f'Assets/Parasite/Resources/Audio/Voice/{key}.ogg'
    assert path.exists(),path
    probe=json.loads(subprocess.run(['ffprobe','-v','error','-select_streams','a:0',
        '-show_entries','stream=codec_name,sample_rate,channels,duration,start_time:format=duration',
        '-of','json',str(path)],check=True,capture_output=True,text=True).stdout)
    decoded=subprocess.run(['ffmpeg','-v','error','-nostdin','-i',str(path),'-map','0:a:0',
        '-ac','1','-ar',str(SETTINGS['decode_sample_rate']),'-f','f32le','pipe:1'],
        check=True,capture_output=True).stdout
    pcm=array.array('f');pcm.frombytes(decoded)
    if sys.byteorder!='little':pcm.byteswap()
    assert pcm and all(math.isfinite(v) for v in pcm)
    sr=SETTINGS['decode_sample_rate'];hz=SETTINGS['envelope_hz']
    duration=len(pcm)/sr
    assert abs(duration-float(probe['format']['duration']))<0.02
    # Prefix sums keep a 25ms centred energy window cheap and deterministic.
    sums=[0.0]
    for sample in pcm:sums.append(sums[-1]+sample*sample)
    half=round(SETTINGS['rms_window_seconds']*sr/2)
    raw=[]
    count=math.ceil(duration*hz)+1
    for frame in range(count):
        sample=round(frame*sr/hz)
        first=max(0,sample-half);last=min(len(pcm),sample+half)
        raw.append(math.sqrt(max(0.0,(sums[last]-sums[first])/max(1,last-first))))
    ordered=sorted(raw)
    percentile=ordered[min(len(ordered)-1,round(.95*(len(ordered)-1)))]
    gate=max(SETTINGS['gate_minimum_rms'],percentile*SETTINGS['gate_relative_to_p95'])
    assert percentile>gate
    envelope=[];state=0.0
    for frame,rms in enumerate(raw):
        target=min(1.0,max(0.0,(rms-gate)/(percentile-gate)))**SETTINGS['aperture_exponent']
        tau=SETTINGS['attack_seconds'] if target>state else SETTINGS['release_seconds']
        alpha=1-math.exp(-1/(hz*tau))
        state+=alpha*(target-state)
        close=max(0.0,min(1.0,(duration-frame/hz)/SETTINGS['terminal_close_seconds']))
        envelope.append(round(max(0.0,min(1.0,state*close)),5))
    active=[i/hz for i,v in enumerate(envelope) if v>.1]
    assert active and max(envelope)>.5 and min(envelope)<.05
    nextcue=captions[index+1][0] if index+1<len(captions) else 80
    records.append({'key':key,'speaker':speaker,'line':line,'cue_seconds':cue,'next_cue_seconds':nextcue,
        'duration_seconds':round(duration,8),'ffprobe_duration_seconds':float(probe['format']['duration']),
        'catalogue_duration_seconds':item['duration'],'decoded_samples':len(pcm),'decode_sample_rate':sr,
        'source':str(path.relative_to(PROJECT)),'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
        'catalogue_profile':item['profile'],'catalogue_voice':item['voice'],'catalogue_sources':item['sources'],
        'audio_stream':probe['streams'][0],'envelope_hz':hz,'amplitude':envelope,
        'analysis':{'first_aperture_above_0_1_seconds':active[0],'last_aperture_above_0_1_seconds':active[-1],
                    'rms_p95':percentile,'rms_gate':gate,'max_aperture':max(envelope),
                    'frames_below_0_05':sum(v<.05 for v in envelope),
                    'frames_above_0_1':len(active),'distinct_aperture_values':len(set(envelope))}})
    print('[speech]',speaker,repr(line),key,'decoded',duration,'probe',probe['format']['duration'])
result={'schema':1,'scope':'eight actor opening CAPTIONS lines; no narrator',
    'method':'actual mono decoded speech energy; normalized gated 25ms RMS; 15ms attack/55ms release; 100Hz cache',
    'limits':'Jaw aperture only. No phoneme, viseme, lip-shape or alignment inference. Caller owns audio playback and cue replacement.',
    'settings':SETTINGS,'mapping':'VoiceOver.KeyFor: FNV-1a64 over UTF-8 UPPER(normalized speaker)|normalized line',
    'catalogue':str(manifest_path.relative_to(PROJECT)),
    'catalogue_sha256':hashlib.sha256(manifest_path.read_bytes()).hexdigest(),
    'captions':str(plan_path.relative_to(PROJECT)),'captions_sha256':hashlib.sha256(plan_path.read_bytes()).hexdigest(),
    'lookup':str(voiceover_path.relative_to(PROJECT)),'lookup_sha256':hashlib.sha256(voiceover_path.read_bytes()).hexdigest(),
    'lines':records}
OUT.mkdir(parents=True,exist_ok=True)
cache=OUT/'opening_speech_envelopes.json'
cache.write_text(json.dumps(result,indent=1)+'\n')
# Playback verification is independent of ffmpeg: standard library API/cache only.
performance=SpeechPerformance(PROJECT)
for entry in records:
    speaker=entry['speaker'];line=entry['line'];duration=entry['duration_seconds']
    assert performance.duration(speaker,line)==duration
    assert performance.amplitude(speaker,line,-.01)==0
    assert performance.amplitude(speaker,line,duration)==0
    assert performance.amplitude(speaker,line,duration+1)==0
    for frame in range(math.ceil(duration*240)+1):
        value=performance.amplitude(speaker,line,frame/240)
        assert math.isfinite(value) and 0<=value<=1
    assert entry['source_sha256']==hashlib.sha256((PROJECT/entry['source']).read_bytes()).hexdigest()
for alias,real in [('DrPetrov','DR. HANA PETROV'),('DrVale','DR. VALE'),('MaintEli','MAINTENANCE WORKER'),('MaintenanceWorker','MAINTENANCE WORKER')]:
    line=next(v['line'] for v in records if v['speaker']==real)
    assert performance.amplitude(alias,line,.31)==performance.amplitude(real,line,.31)
assert performance.amplitude('Unknown','Unknown',.1)==0
assert performance.amplitude('DrPetrov',records[0]['line'],float('nan'))==0
verification={'status':'PASS','actor_lines':len(records),'clip_keys_match_catalogue':True,
    'all_sources_ogg':True,'all_pcm_finite':True,'duration_from_decoded_pcm_and_ffprobe':True,
    'all_lookup_values_0_to_1':True,'outside_media_duration_zero':True,'actor_aliases_match':True,
    'cache_bytes':cache.stat().st_size,'cache_sha256':hashlib.sha256(cache.read_bytes()).hexdigest(),
    'no_blender_unity_gpu_or_runtime_asset_writes':True,
    'lines':[{k:v for k,v in entry.items() if k!='amplitude'} for entry in records]}
(OUT/'speech_verification.json').write_text(json.dumps(verification,indent=2)+'\n')
print('[speech] PASS',len(records),'lines',cache.stat().st_size,'bytes')
