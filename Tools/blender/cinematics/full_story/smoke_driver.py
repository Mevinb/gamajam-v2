"""Bounded actual-Blender CPU smoke: cut120/121, mid-frame stop, resume, image integrity and provenance."""
import sys
sys.dont_write_bytecode=True
import datetime,hashlib,json,os,subprocess,time
from pathlib import Path
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
SOURCE=ROOT/'ArtSource/FullCinematics/Opening/OpeningFull.blend'
DRIVER=HERE/'render_story.py'
BLENDER=Path.home()/'Data/Blender/current/blender'
STAMP=datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
OUT=ROOT/'Logs/full-cinematic-renders'/('driver-smoke-'+STAMP)
OUT.mkdir(parents=True,exist_ok=False)
EVIDENCE=OUT/'render-evidence-000120-000121.json'
STOP=OUT/'STOP'
BASE=[str(BLENDER),'-b',str(SOURCE),'-t','2','--python-exit-code','1','--python',str(DRIVER),'--',
      '--cpu-preview','--start','120','--end','121','--samples','4','--out',str(OUT)]
RESULTS=[]


def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def invoke(name,extra=(),stop_during_first=False):
    log=OUT/(name+'.log');began=time.monotonic()
    with log.open('w') as stream:
        process=subprocess.Popen(BASE+list(extra),cwd=ROOT,stdout=stream,stderr=subprocess.STDOUT)
        stopped=False
        while process.poll() is None:
            if stop_during_first and not stopped and 'Fra:120 ' in log.read_text(errors='replace'):
                # Request while Cycles is inside frame120; the driver must finish/publish it before stopping.
                STOP.touch();stopped=True
            if time.monotonic()-began>120:
                process.terminate();process.wait(timeout=10);raise RuntimeError('CPU smoke exceeded its two-minute process limit')
            time.sleep(.01)
    if stop_during_first and not stopped:raise RuntimeError('No actual first-frame render was observed before exit')
    return process.returncode,log,time.monotonic()-began


def check(condition,message):
    if not condition:raise AssertionError(message)
    print('[driver-smoke] PASS '+message,flush=True)
    RESULTS.append(message)


code,log,elapsed=invoke('01-stop',stop_during_first=True)
check(code==0,'Actual CPU renderer exits cleanly after graceful stop')
stopped=json.loads(EVIDENCE.read_text())
check(stopped['status']=='stopped' and not stopped['complete'],'Checkpoint distinguishes stopped from completed')
check([row['frame'] for row in stopped['frames']]==[120],'Only atomic frame120 was published before stop')
check((OUT/'frame-000120.png').is_file() and not (OUT/'frame-000121.png').exists(),'No partial second frame was published')
first=sha(OUT/'frame-000120.png');STOP.unlink()
code,log,elapsed=invoke('02-resume',['--resume'])
check(code==0,'Resume completes the remaining CPU frame')
complete=json.loads(EVIDENCE.read_text());rows={row['frame']:row for row in complete['frames']}
check(complete['complete'] and complete['status']=='complete' and set(rows)=={120,121},'Both requested frames are verified complete')
check(rows[120]['camera']=='Camera 01-rhythm' and rows[121]['camera']=='Camera 02-petrov','Exact packed camera boundary120/121 is respected')
check(rows[120]['camera_marker_frame']==1 and rows[121]['camera_marker_frame']==121,'Checkpoint records actual packed cut markers')
check(sha(OUT/'frame-000120.png')==first and 'VERIFIED RESUME 120' in log.read_text(),'Resume preserves and verifies the existing image')
check(complete['provenance']['backend']=='CPU' and complete['provenance']['render']['threads']==2,'Smoke used CPU backend and two render threads')
original=(OUT/'frame-000120.png').read_bytes();(OUT/'frame-000120.png').write_bytes(original+b'corruption-test')
try:
    code,log,elapsed=invoke('03-image-mismatch',['--resume'])
    check(code!=0 and 'Image differs from checkpoint at frame120' in log.read_text().replace('frame 120','frame120'),'Actual resume rejects a changed image')
finally:
    restore=OUT/'working'/'restore.png';restore.write_bytes(original);os.replace(restore,OUT/'frame-000120.png')
code,log,elapsed=invoke('04-restored-resume',['--resume'])
check(code==0 and json.loads(EVIDENCE.read_text())['complete'],'Restored hashes resume without rerendering')
# An explicit sample change must fail provenance validation, without replacing either image/checkpoint.
args=BASE[:];args[args.index('--samples')+1]='5'
log=OUT/'05-provenance-mismatch.log'
with log.open('w') as stream:code=subprocess.run(args+['--resume'],cwd=ROOT,stdout=stream,stderr=subprocess.STDOUT,timeout=30).returncode
check(code!=0 and 'Render provenance changed' in log.read_text(),'Actual Blender resume rejects changed sampling provenance')
check(sha(OUT/'frame-000120.png')==rows[120]['sha256'] and sha(OUT/'frame-000121.png')==rows[121]['sha256'],'Rejected provenance left both original frames unchanged')
proof=OUT/'job-provenance.json';original_proof=proof.read_bytes();stale=json.loads(original_proof);stale['driver_sha256']='0'*64;proof.write_text(json.dumps(stale))
try:
    code,log,elapsed=invoke('06-driver-mismatch',['--resume'])
    check(code!=0 and 'driver_sha256' in log.read_text(),'Actual Blender resume rejects a stale driver fingerprint')
finally:proof.write_bytes(original_proof)
complete=json.loads(EVIDENCE.read_text())
summary={'status':'passed','cpu_only':True,'source_sha256':sha(SOURCE),'driver_sha256':sha(DRIVER),
         'policy_sha256':sha(HERE/'render_policy.py'),'output_directory':OUT.relative_to(ROOT).as_posix(),
         'checks':RESULTS,'provenance':complete['provenance'],'frames':complete['frames'],
         'raw_logs':[(path.relative_to(ROOT).as_posix()) for path in sorted(OUT.glob('*.log'))]}
(ROOT/'ArtSource/FullCinematics/driver-smoke-evidence.json').write_text(json.dumps(summary,indent=2)+'\n')
print('[driver-smoke] COMPLETE '+str(OUT),flush=True)
