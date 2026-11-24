"""Pure-Python validation for long cinematic jobs; no Blender, GPU or render work."""
import hashlib,json,re
from pathlib import Path

MAX_SAMPLES=4096


def output_path(project, requested):
    safe=(Path(project)/'Logs/full-cinematic-renders').resolve()
    result=Path(requested).resolve()
    if not result.is_relative_to(safe) or result==safe:
        raise ValueError('Choose a job subdirectory under ignored Logs/full-cinematic-renders, never ArtSource or Assets')
    return result


def validate_request(start,end,samples,scene_start,scene_end):
    values=(start,end,samples,scene_start,scene_end)
    if any(type(value) is not int for value in values):
        raise ValueError('Frame bounds and sample count must be integers')
    if scene_start>scene_end or start>end or start<scene_start or end>scene_end:
        raise ValueError(f'Requested frames {start}–{end} must lie within packed scene {scene_start}–{scene_end}')
    if not 1<=samples<=MAX_SAMPLES:
        raise ValueError(f'Samples must be in the range 1–{MAX_SAMPLES}')


def camera_schedule(markers,scene_start):
    bound=[]
    for marker in markers:
        if not marker.get('camera'):continue
        if type(marker['frame']) is not int or not marker.get('marker'):
            raise ValueError('Camera markers need an integer frame and a name')
        if marker.get('camera_type')!='CAMERA' or not marker.get('linked_to_scene',False):
            raise ValueError(f"Invalid packed camera binding: {marker.get('marker')}")
        bound.append({key:marker[key] for key in ('frame','marker','camera')})
    bound.sort(key=lambda marker:marker['frame'])
    if not bound or bound[0]['frame']>scene_start:
        raise ValueError('Packed camera markers must cover the first scene frame')
    if len({marker['frame'] for marker in bound})!=len(bound):
        raise ValueError('Multiple camera bindings at one frame are ambiguous')
    return bound


def camera_at_frame(schedule,frame):
    active=None
    for marker in schedule:
        if marker['frame']>frame:break
        active=marker
    if active is None:raise ValueError(f'No packed camera binding covers frame {frame}')
    return active


def digest(value):
    raw=json.dumps(value,sort_keys=True,separators=(',',':'),allow_nan=False).encode('utf8')
    return hashlib.sha256(raw).hexdigest()


def require_same_provenance(previous,current):
    if digest(previous)!=digest(current):
        changed=sorted(key for key in set(previous)|set(current) if previous.get(key)!=current.get(key))
        raise ValueError('Render provenance changed; use a new job directory: '+', '.join(changed))


def validate_record(record,frame,schedule):
    expected=camera_at_frame(schedule,frame)
    if record.get('frame')!=frame or record.get('camera')!=expected['camera'] or record.get('camera_marker')!=expected['marker'] or record.get('camera_marker_frame')!=expected['frame']:
        raise ValueError(f'Checkpoint camera/frame differs from packed source at frame {frame}')
    if not isinstance(record.get('sha256'),str) or re.fullmatch('[0-9a-f]{64}',record['sha256']) is None or type(record.get('bytes')) is not int or record['bytes']<=0:
        raise ValueError(f'Incomplete image checkpoint for frame {frame}')
