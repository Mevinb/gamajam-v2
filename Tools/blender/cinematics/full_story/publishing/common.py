"""Strict evidence primitives; no renderer, GPU, encoder or live publication is invoked by import."""
from __future__ import annotations
import contextlib, fcntl, hashlib, json, math, os, re, struct
from pathlib import Path
from PIL import Image

PROJECT = Path(__file__).resolve().parents[5]
STAGE = PROJECT / "Logs/full-film-publication"
FILMS = {'OpeningFull':1920, 'LakeArrival':360, 'LakeHold':144, 'LakePeace':168, 'LakeDominion':168}
LAKE = tuple(name for name in FILMS if name != 'OpeningFull')
MAX_BYTES = 95_000_000  # Decimal MB; below GitHub's100MB ordinary-blob boundary.
MANIFEST = 'ArtSource/FullCinematics/source-publication-manifest.json'
DRIVER = 'Tools/blender/cinematics/full_story/render_story.py'
POLICY = 'Tools/blender/cinematics/full_story/render_policy.py'

class InvalidEvidence(RuntimeError): pass

def require(condition, message):
    if not condition: raise InvalidEvidence(message)

def read_json(path):
    try: return json.loads(Path(path).read_text(), parse_constant=lambda x: (_ for _ in ()).throw(ValueError(x)))
    except (OSError, ValueError) as error: raise InvalidEvidence(f'Invalid/missing JSON {path}: {error}') from error

def sha(path):
    result=hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''): result.update(block)
    return result.hexdigest()

def digest(value):
    return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':'),allow_nan=False).encode()).hexdigest()

def render_digest(contract):
    # Documentation inventory can be re-frozen by root without re-encoding unchanged rendered content.
    chain=contract.get('references',{}).get('opening',{}).get('camera_revision_chain')
    return digest({'plan':contract['plan'],'expected_provenance':contract['expected_provenance'],'camera_revision_chain':chain})

def atomic_json(path,value):
    path=Path(path);path.parent.mkdir(parents=True,exist_ok=True)
    temporary=path.with_name('.'+path.name+'.writing')
    with temporary.open('w') as stream:
        json.dump(value,stream,indent=2,allow_nan=False);stream.write('\n');stream.flush();os.fsync(stream.fileno())
    os.replace(temporary,path)

def below(path, root, allow_root=False):
    path=Path(path).resolve();root=Path(root).resolve()
    require(path.is_relative_to(root) and (allow_root or path!=root),f'Path must be a child of {root}: {path}')
    return path

def project_file(relative,project=PROJECT):
    relative=Path(relative)
    require(not relative.is_absolute() and '..' not in relative.parts,f'Unsafe relative source: {relative}')
    require(not (project/relative).is_symlink(),f'Symlink source refused: {relative}')
    path=below(project/relative,project)
    require(path.is_file() and not path.is_symlink(),f'Missing/nonregular source: {path}')
    return path

def normalize_plan(plan):
    sources=plan.get('sources',[])
    require(len(sources)==5 and {s.get('name') for s in sources}==set(FILMS),'Plan must pin exactly the five real films')
    normalized=[]
    for row in sources:
        name=row['name'];count=FILMS[name]
        require(row.get('frames')==count and row.get('start',1)==1 and row.get('end',count)==count,'Wrong frozen frame range: '+name)
        require(re.fullmatch('[0-9a-f]{64}',row.get('sha256','')) is not None,'Invalid source hash: '+name)
        normalized.append({key:row[key] for key in ['name','source','frames','sha256']})
    core={key:plan.get(key) for key in ['samples','resolution','fps','ray_backend','denoiser','denoise_gpu','driver_sha256','policy_sha256','total_frames']}
    require(core['samples']==48 and core['resolution']==[1920,1080] and core['fps']==24,'Production quality must be48samples/1080p24')
    require(core['ray_backend']=='OPTIX' and core['denoiser']=='OPENIMAGEDENOISE' and core['denoise_gpu'] is True,'Wrong production GPU/denoise policy')
    require(core['total_frames']==2760,'Wrong total full-story frame count')
    core['sources']=sorted(normalized,key=lambda s:s['name'])
    return core

def verify_sources(plan, publication_sha=None, project=PROJECT):
    path=project_file(MANIFEST,project);publication=read_json(path)
    if publication_sha: require(sha(path)==publication_sha,'Frozen source publication manifest changed')
    rows=publication.get('files',[])
    require(publication.get('schema')==1 and len(rows)==publication.get('file_count') and len({r['path'] for r in rows})==len(rows),'Invalid source publication inventory')
    for row in rows:
        file=project_file(row['path'],project)
        require(file.stat().st_size==row['bytes'] and sha(file)==row['sha256'],'Source publication bytes changed: '+row['path'])
    require(sha(project_file(DRIVER,project))==plan['driver_sha256'],'Frozen renderer driver changed')
    require(sha(project_file(POLICY,project))==plan['policy_sha256'],'Frozen renderer policy changed')
    inventory={r['path']:r for r in rows}
    for row in plan['sources']:
        file=project_file(row['source'],project)
        require(row['source'] in inventory and inventory[row['source']]['sha256']==row['sha256'] and sha(file)==row['sha256'],'Packed source differs from frozen plan: '+row['name'])
    return {'manifest_sha256':sha(path),'files':len(rows),'plan_sha256':digest(plan)}

def schedules(project=PROJECT):
    opening=read_json(project_file('ArtSource/FullCinematics/Opening/opening-timeline.json',project))
    lake=read_json(project_file('ArtSource/FullCinematics/Lake/manifest.json',project))
    result={'OpeningFull':[{'frame':round(s['start']*24)+1,'marker':s['id'],'camera':'Camera '+s['id']} for s in opening['shots']]}
    for name,kind in [('LakeArrival','arrival'),('LakeHold','hold'),('LakePeace','peace'),('LakeDominion','dominion')]:
        result[name]=[{'frame':s['start'],'marker':s['description'],'camera':s['description']} for s in lake['films'][kind]['shots']]
    for name,rows in result.items():
        require(rows and rows[0]['frame']==1 and len({r['frame'] for r in rows})==len(rows),'Camera schedule must cover first frame: '+name)
        require(rows==sorted(rows,key=lambda r:r['frame']) and rows[-1]['frame']<=FILMS[name],'Invalid camera cut schedule: '+name)
    return result

def reference_profile(provenance,plan,source,expected_schedule):
    require(provenance.get('schema')==2 and provenance.get('source_sha256')==source['sha256'],'Reference profile has wrong source/schema')
    require(provenance.get('driver_sha256')==plan['driver_sha256'] and provenance.get('policy_sha256')==plan['policy_sha256'],'Reference driver/policy changed')
    require(provenance.get('engine')=='CYCLES' and provenance.get('backend')=='OPTIX' and provenance.get('device_mode')=='GPU','Reference is not RTX production')
    devices=provenance.get('devices',[])
    require(len(devices)==1 and devices[0].get('type')=='OPTIX' and 'RTX 4050' in devices[0].get('name','') and bool(devices[0].get('id')),'Reference must identify exactly one RTX4050 OptiX device')
    require(provenance.get('camera_schedule')==expected_schedule and provenance.get('scene_frame_range')==[1,FILMS[source['name']]],'Reference camera schedule/range differs from authored source')
    sampling=provenance.get('sampling',{});render=provenance.get('render',{})
    require(sampling.get('samples')==48 and sampling.get('use_denoising') is True and sampling.get('denoiser')=='OPENIMAGEDENOISE' and sampling.get('denoising_use_gpu') is True,'Reference quality was lowered')
    require(render.get('resolution_x')==1920 and render.get('resolution_y')==1080 and render.get('resolution_percentage')==100 and render.get('fps')==24 and render.get('fps_base')==1,'Reference output timing/size differs')
    require(render.get('threads')==2 and render.get('threads_mode')=='FIXED','Unexpected render CPU threading')
    require(all(render.get('image',{}).get(k)==v for k,v in {'file_format':'PNG','color_mode':'RGB','color_depth':'8'}.items()),'Reference is not RGB8PNG')
    return provenance

def at_frame(schedule,number):
    return next(row for row in reversed(schedule) if row['frame']<=number)

def validate_record(record,number,schedule):
    expected=at_frame(schedule,number)
    require(type(record.get('frame')) is int and record['frame']==number,'Incorrect frame record number')
    require(all(record.get(k)==expected[v] for k,v in [('camera','camera'),('camera_marker','marker'),('camera_marker_frame','frame')]),f'Wrong packed camera cut at frame{number}')
    require(type(record.get('bytes')) is int and record['bytes']>0 and re.fullmatch('[0-9a-f]{64}',record.get('sha256','')) is not None,'Invalid PNG hash/size record')
    matrix=record.get('camera_world_matrix',[])
    require(len(matrix)==4 and all(len(row)==4 and all(type(v) in [int,float] and math.isfinite(v) for v in row) for row in matrix),'Nonfinite/invalid camera matrix')
    require(all(abs(a-b)<1e-5 for a,b in zip(matrix[3],[0,0,0,1])),'Invalid homogeneous camera pose')
    for column in range(3): require(abs(sum(matrix[row][column]**2 for row in range(3))-1)<1e-3,'Camera basis scale is not one')
    for a,b in [(0,1),(0,2),(1,2)]: require(abs(sum(matrix[row][a]*matrix[row][b] for row in range(3)))<1e-3,'Camera basis is not orthogonal')
    require(type(record.get('camera_lens_mm')) in [int,float] and math.isfinite(record['camera_lens_mm']) and .1<record['camera_lens_mm']<1000,'Invalid camera lens')
    sensor=record.get('camera_sensor_mm',[])
    require(len(sensor)==2 and all(isinstance(v,(int,float)) and math.isfinite(v) and v>0 for v in sensor),'Invalid camera sensor')
    dof=record.get('dof',{})
    require('focus_object' in dof and (dof['focus_object'] is None or isinstance(dof['focus_object'],str)),'Missing/invalid DOF focus binding')
    require(type(dof.get('enabled')) is bool and type(dof.get('fstop')) in [int,float] and math.isfinite(dof['fstop']) and dof['fstop']>0,'Invalid camera aperture')
    require(type(dof.get('focus_distance')) in [int,float] and math.isfinite(dof['focus_distance']) and dof['focus_distance']>=0,'Invalid camera focus distance')
    require(not dof['enabled'] or bool(dof.get('focus_object')) or dof['focus_distance']>0,'Enabled DOF needs a focus target or positive distance')
    require(type(record.get('seconds')) in [int,float] and math.isfinite(record['seconds']) and record['seconds']>=0,'Invalid frame duration record')

def verify_png(path,record):
    require(path.is_file() and not path.is_symlink(),'Missing/symlink PNG: '+str(path))
    require(path.stat().st_size==record['bytes'] and sha(path)==record['sha256'],'PNG bytes differ from renderer evidence: '+str(path))
    with path.open('rb') as stream: header=stream.read(33)
    require(header[:8]==b'\x89PNG\r\n\x1a\n' and header[12:16]==b'IHDR','Not a PNG: '+str(path))
    width,height,depth,color,compression,filtering,interlace=struct.unpack('>IIBBBBB',header[16:29])
    require((width,height,depth,color,compression,filtering,interlace)==(1920,1080,8,2,0,0,0),'PNG must be noninterlaced RGB8 at1920x1080')
    with Image.open(path) as image: require(image.mode=='RGB' and image.size==(1920,1080),'Wrong decoded PNG header');image.verify()

@contextlib.contextmanager
def film_lock(folder):
    path=folder/'.render.lock';require(path.is_file(),'Missing renderer lock identity: '+str(path))
    with path.open('r') as lock:
        try: fcntl.flock(lock.fileno(),fcntl.LOCK_SH|fcntl.LOCK_NB)
        except BlockingIOError as error: raise InvalidEvidence('Renderer is still writing '+str(folder)) from error
        yield
