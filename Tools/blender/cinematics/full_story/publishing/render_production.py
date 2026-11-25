"""Prepare/inspect by default. Explicit --go starts the approved frozen production; STOP is between frames."""
from pathlib import Path
import sys,argparse,json,hashlib,datetime,subprocess,time,fcntl,shutil,threading
ROOT=Path(__file__).resolve().parents[5];TOOLS=ROOT/'Tools/blender/cinematics/full_story';DRIVER=TOOLS/'render_story.py';SRC=ROOT/'ArtSource/FullCinematics';SAFE=(ROOT/'Logs/full-cinematic-renders').resolve()
sys.path.insert(0,str(TOOLS));import render_policy as policy
p=argparse.ArgumentParser();p.add_argument('--go',action='store_true');p.add_argument('--out');p.add_argument('--reserve-gib',type=float,default=4);p.add_argument('--blender',type=Path,default=Path.home()/'Data/Blender/current/blender');a=p.parse_args()
if a.reserve_gib<2:raise ValueError('Keep at least2GiB reserve outside render working data')
def sha(path):
 h=hashlib.sha256()
 with path.open('rb') as f:
  for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
 return h.hexdigest()
def atomic_json(path,data):
 stage=path.with_suffix(path.suffix+'.writing')
 with stage.open('w') as f:json.dump(data,f,indent=2);f.write('\n');f.flush();__import__('os').fsync(f.fileno())
 stage.replace(path)
opening=json.loads((SRC/'Opening/authoring-audit.json').read_text());lake=json.loads((SRC/'Lake/audit.json').read_text())
spec=[('OpeningFull',SRC/'Opening/OpeningFull.blend',1920,opening['source_sha256']),('LakeArrival',SRC/'Lake/Lake_Arrival.blend',360,lake['arrival']['sha256']),('LakeHold',SRC/'Lake/Lake_Hold.blend',144,lake['hold']['sha256']),('LakePeace',SRC/'Lake/Lake_Peace.blend',168,lake['peace']['sha256']),('LakeDominion',SRC/'Lake/Lake_Dominion.blend',168,lake['dominion']['sha256'])]
order=['LakeArrival','LakeHold','LakePeace','LakeDominion','OpeningFull']
spec=sorted(spec,key=lambda item:order.index(item[0]))
if opening.get('status')!='passed':raise RuntimeError('Current opening audit must pass')
for name,source,count,digest in spec:
 if sha(source)!=digest:raise RuntimeError('Frozen source hash differs from audit: '+name)
stamp=datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ');out=Path(a.out).resolve() if a.out else SAFE/('full-production-'+stamp)
if not out.is_relative_to(SAFE) or out==SAFE:raise RuntimeError('Choose a distinct ignored production directory')
plan={'schema':2,'samples':48,'resolution':[1920,1080],'fps':24,'ray_backend':'OPTIX','denoiser':'OPENIMAGEDENOISE','denoise_gpu':True,'driver_sha256':sha(DRIVER),'policy_sha256':sha(TOOLS/'render_policy.py'),'total_frames':2760,'sources':[{'name':n,'source':str(s.relative_to(ROOT)),'start':1,'end':c,'frames':c,'sha256':h} for n,s,c,h in spec]}
reserve=int(a.reserve_gib*1024**3);bytes_per_frame=4*1024**2

def recorded_count(name,count):
 evidence=out/name/f'render-evidence-000001-{count:06d}.json'
 if not evidence.exists():return 0
 data=json.loads(evidence.read_text());return len({r['frame'] for r in data.get('frames',[]) if 1<=r['frame']<=count})
remaining=sum(count-recorded_count(name,count) for name,source,count,digest in spec)
free=shutil.disk_usage(ROOT).free
budget={'free_bytes':free,'remaining_frames_estimate':remaining,'estimated_bytes_per_frame':bytes_per_frame,'required_working_bytes_estimate':remaining*bytes_per_frame,'reserve_bytes':reserve,'estimated_total_free_needed':remaining*bytes_per_frame+reserve,'ready':free>=remaining*bytes_per_frame+reserve,'note':'4MiB/frame conservative allowance; existing record counts are estimates until hashes verify; disk guard stops safely at reserve'}
print(json.dumps(dict(plan,output_directory=str(out),render_requested=a.go,disk_budget=budget),indent=2),flush=True)
if not a.go:raise SystemExit(0)
if not a.blender.expanduser().is_file():raise RuntimeError('Choose an installed Blender executable with --blender')
if not budget['ready']:raise RuntimeError('Insufficient estimated working space plus reserve; no renderer started')
out.mkdir(parents=True,exist_ok=True);lock=(out/'.production.lock').open('a');fcntl.flock(lock.fileno(),fcntl.LOCK_EX|fcntl.LOCK_NB)
planfile=out/'production-plan.json'
if planfile.exists() and json.loads(planfile.read_text())!=plan:raise RuntimeError('Frozen plan changed; choose a new directory')
atomic_json(planfile,plan)


def verify_complete(folder,count,digest):
 path=folder/f'render-evidence-000001-{count:06d}.json'
 if not path.exists():return None
 data=json.loads(path.read_text())
 if data.get('status')!='complete' or not data.get('complete'):return None
 if data.get('schema')!=2 or data.get('start')!=1 or data.get('end')!=count:raise RuntimeError('Completed checkpoint bounds differ')
 proof=data['provenance'];policy.require_same_provenance(json.loads((folder/'job-provenance.json').read_text()),proof)
 if data['provenance_sha256']!=policy.digest(proof):raise RuntimeError('Completed provenance digest differs')
 sampling=proof['sampling'];render=proof['render']
 if proof['source_sha256']!=digest or proof['driver_sha256']!=plan['driver_sha256'] or proof['policy_sha256']!=plan['policy_sha256']:raise RuntimeError('Completed source/driver/policy differs')
 if proof['backend']!='OPTIX' or proof['device_mode']!='GPU' or sampling['samples']!=48 or sampling['denoiser']!='OPENIMAGEDENOISE' or not sampling.get('denoising_use_gpu') or not sampling.get('use_denoising'):raise RuntimeError('Completed quality profile differs')
 if [render['resolution_x'],render['resolution_y']]!=[1920,1080] or render['resolution_percentage']!=100 or render['fps']!=24 or render['fps_base']!=1:raise RuntimeError('Completed resolution/timing differs')
 rows=data['frames']
 if len(rows)!=count or {r['frame'] for r in rows}!=set(range(1,count+1)):raise RuntimeError('Completed frame set differs')
 for row in rows:
  policy.validate_record(row,row['frame'],proof['camera_schedule']);image=folder/f"frame-{row['frame']:06d}.png"
  if not image.is_file() or image.stat().st_size!=row['bytes'] or sha(image)!=row['sha256']:raise RuntimeError('Completed image hash differs')
 return path

telemetry_path=out/('gpu-telemetry-'+stamp+'.csv');telemetry_file=telemetry_path.open('w');telemetry=subprocess.Popen(['nvidia-smi','--query-gpu=timestamp,name,driver_version,temperature.gpu,utilization.gpu,memory.total,memory.used,power.draw','--format=csv','-lms','1000'],stdout=telemetry_file,stderr=subprocess.STDOUT)
previous_summary=json.loads((out/'production-summary.json').read_text()) if (out/'production-summary.json').exists() else {}
previous_completed={row['name']:row for row in previous_summary.get('jobs',[]) if row.get('status')=='complete'}
summary={'status':'running','plan':plan,'jobs':[],'started_utc':stamp,'orchestrator_sha256':sha(Path(__file__)),'initial_disk_budget':budget}
finished=threading.Event();disk_stop={}
def monitor_disk():
 while not finished.wait(2):
  available=shutil.disk_usage(ROOT).free
  if available<reserve:
   disk_stop.update(free_bytes=available,reserve_bytes=reserve,time_utc=datetime.datetime.now(datetime.timezone.utc).isoformat());(out/'STOP').touch();atomic_json(out/'disk-stop.json',disk_stop);return
monitor=threading.Thread(target=monitor_disk,daemon=True);monitor.start()
def save():atomic_json(out/'production-summary.json',summary)
save()
try:
 for name,source,count,digest in spec:
  if (out/'STOP').exists():summary['status']='stopped';break
  if sha(source)!=digest or sha(DRIVER)!=plan['driver_sha256'] or sha(TOOLS/'render_policy.py')!=plan['policy_sha256']:raise RuntimeError('Frozen render input changed')
  folder=out/name;folder.mkdir(exist_ok=True);evidence=folder/f'render-evidence-000001-{count:06d}.json'
  complete=verify_complete(folder,count,digest)
  if complete:
   row=dict(previous_completed.get(name,{}));row.update(name=name,status='complete',complete_frames=count,verified_existing=True,evidence=str(complete.relative_to(ROOT)))
   summary['jobs'].append(row);save();print('VERIFIED_COMPLETE '+name,flush=True);continue
  if shutil.disk_usage(ROOT).free<reserve+bytes_per_frame:raise RuntimeError('Free space below reserve; renderer not started')
  command=[str(a.blender.expanduser().resolve()),'-b',str(source),'-t','2','--python-exit-code','1','--python',str(DRIVER),'--','--gpu-released','--start','1','--end',str(count),'--samples','48','--denoise-gpu','--out',str(folder),'--stop-file',str(out/'STOP')]
  if evidence.exists():command.append('--resume')
  atomic_json(folder/('command-'+stamp+'.json'),command);began=time.monotonic();print('PRODUCTION_JOB '+name,flush=True)
  with (folder/('render-'+stamp+'.log')).open('w') as log:
   process=subprocess.Popen(command,cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,bufsize=1)
   for line in process.stdout:
    log.write(line);log.flush()
    if line.startswith('[full-render]'):print(name+' '+line.strip(),flush=True)
   code=process.wait()
  row={'name':name,'exit_code':code,'wall_seconds':time.monotonic()-began}
  if evidence.exists():
   proof=json.loads(evidence.read_text());row.update(status=proof['status'],complete_frames=len(proof['frames']),evidence=str(evidence.relative_to(ROOT)))
  summary['jobs'].append(row);save()
  if code:raise RuntimeError(name+' renderer failed')
  if row.get('status')!='complete':summary['status']='stopped';break
  if verify_complete(folder,count,digest) is None:raise RuntimeError('Renderer exit lacks complete verified evidence')
 else:summary['status']='complete'
except BaseException as error:
 summary['status']='failed';summary['error']=str(error);raise
finally:
 finished.set();monitor.join(timeout=3);telemetry.terminate();telemetry.wait(timeout=10);telemetry_file.close();summary['telemetry']=str(telemetry_path.relative_to(ROOT));summary['final_free_bytes']=shutil.disk_usage(ROOT).free
 if disk_stop:summary['disk_stop']=disk_stop
 save();print('PRODUCTION_SUMMARY '+str(out/'production-summary.json'),flush=True)
