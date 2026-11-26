"""Long-job render driver. All working images/checkpoints stay in ignored Logs/full-cinematic-renders.
GPU work requires --gpu-released after root's profiling release. No runtime Assets are changed.

blender -b ArtSource/FullCinematics/Opening/OpeningFull.blend -t 2 --python Tools/blender/cinematics/full_story/render_story.py -- --gpu-released --start 1 --end 24 --out Logs/full-cinematic-renders/opening-pilot

Create <job>/STOP to stop gracefully after the current atomic frame. Remove it, then use --resume with the
same bounds/configuration. Each frame records the active packed camera binding, image hash and provenance.
"""
import sys
sys.dont_write_bytecode=True
import argparse,fcntl,hashlib,json,math,os,time
from pathlib import Path
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
import render_policy as policy

ROOT=HERE.parents[3]
SOURCE_ROOTS=tuple((ROOT/path).resolve() for path in ('ArtSource/FullCinematics','ArtSource/RealisticFinales'))
SCHEMA=2


def sha(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
    return h.hexdigest()


def atomic_json(path,value,working):
    stage=working/(path.name+'.writing')
    with stage.open('w') as stream:
        json.dump(value,stream,indent=2,allow_nan=False);stream.write('\n');stream.flush();os.fsync(stream.fileno())
    os.replace(stage,path)


def scalar_settings(obj,names):
    result={}
    for name in names:
        if hasattr(obj,name):
            value=getattr(obj,name)
            if isinstance(value,(bool,int,float,str)) or value is None:result[name]=value
    return result


def packed_camera_schedule(scene):
    markers=[]
    for marker in scene.timeline_markers:
        camera=marker.camera
        markers.append({'frame':int(marker.frame),'marker':marker.name,'camera':camera.name if camera else None,
                        'camera_type':camera.type if camera else None,
                        'linked_to_scene':bool(camera and scene.objects.get(camera.name)==camera)})
    return policy.camera_schedule(markers,int(scene.frame_start))


def select_packed_camera(bpy,scene,schedule,frame):
    scene.frame_set(frame)
    if packed_camera_schedule(scene)!=schedule:
        raise RuntimeError('Camera bindings changed during frame evaluation')
    expected=policy.camera_at_frame(schedule,frame)
    camera=scene.objects.get(expected['camera'])
    if camera is None or camera.type!='CAMERA':raise RuntimeError('Packed camera is missing from active scene')
    # Select explicitly from the packed binding rather than trusting frame_set's implicit camera switching.
    scene.camera=camera;bpy.context.view_layer.update()
    if scene.camera!=camera:raise RuntimeError('The active camera does not match the packed timeline binding')
    data=camera.data
    record={'camera':camera.name,'camera_marker':expected['marker'],'camera_marker_frame':expected['frame'],
            'camera_world_matrix':[[float(value) for value in row] for row in camera.matrix_world],
            'camera_lens_mm':float(data.lens),'camera_sensor_mm':[float(data.sensor_width),float(data.sensor_height)],
            'dof':{'enabled':bool(data.dof.use_dof),'fstop':float(data.dof.aperture_fstop),
                   'focus_object':data.dof.focus_object.name if data.dof.focus_object else None,
                   'focus_distance':float(data.dof.focus_distance)}}
    return camera,record


def parse_args():
    argv=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    parser=argparse.ArgumentParser(description=__doc__)
    mode=parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--gpu-released',action='store_true');mode.add_argument('--cpu-preview',action='store_true')
    parser.add_argument('--start',type=int,required=True);parser.add_argument('--end',type=int,required=True)
    parser.add_argument('--out',required=True);parser.add_argument('--samples',type=int,default=48)
    parser.add_argument('--adaptive-threshold',type=float);parser.add_argument('--adaptive-min-samples',type=int)
    parser.add_argument('--denoiser',choices=['OPENIMAGEDENOISE','OPTIX'],default='OPENIMAGEDENOISE')
    parser.add_argument('--denoise-gpu',action='store_true');parser.add_argument('--no-denoise',action='store_true')
    parser.add_argument('--resume',action='store_true');parser.add_argument('--stop-file')
    return parser.parse_args(argv)


def run(args):
    import bpy
    output=policy.output_path(ROOT,args.out)
    source=Path(bpy.data.filepath).resolve()
    if not any(source.is_relative_to(root) for root in SOURCE_ROOTS) or not source.is_file():
        raise ValueError('Open a packed source under ArtSource/FullCinematics or ArtSource/RealisticFinales before running this driver')
    # Reload the exact hashed packed source; an interactive in-memory edit must not masquerade as those bytes.
    source_sha=sha(source);bpy.ops.wm.open_mainfile(filepath=str(source))
    if sha(source)!=source_sha:raise RuntimeError('Source bytes changed while loading')
    source_stat=source.stat();source_identity=(source_stat.st_size,source_stat.st_mtime_ns)
    scene=bpy.context.scene
    for image in bpy.data.images:
        if image.users and image.source=='FILE' and not (image.packed_file or len(image.packed_files)):
            raise ValueError('Full-film source must pack image dependencies: '+image.name)
    policy.validate_request(args.start,args.end,args.samples,int(scene.frame_start),int(scene.frame_end))
    if scene.render.fps!=24 or abs(scene.render.fps_base-1)>1e-8:
        raise ValueError('Packed full-story sources must retain their authored 24 fps timing')
    if args.cpu_preview and (args.denoise_gpu or args.denoiser=='OPTIX'):
        raise ValueError('CPU previews cannot request GPU denoising')
    if args.adaptive_threshold is not None and (not math.isfinite(args.adaptive_threshold) or not 0<=args.adaptive_threshold<=1):
        raise ValueError('Adaptive threshold must be finite and in [0,1]')
    if args.adaptive_min_samples is not None and not 0<=args.adaptive_min_samples<=args.samples:
        raise ValueError('Adaptive minimum samples must be in [0,samples]')
    schedule=packed_camera_schedule(scene)
    scene.render.engine='CYCLES';scene.render.use_persistent_data=True;scene.cycles.samples=args.samples
    scene.cycles.use_denoising=not args.no_denoise;scene.cycles.denoiser=args.denoiser
    if args.denoise_gpu and not hasattr(scene.cycles,'denoising_use_gpu'):raise ValueError('This Blender build has no GPU denoising control')
    if hasattr(scene.cycles,'denoising_use_gpu'):scene.cycles.denoising_use_gpu=bool(args.denoise_gpu)
    if args.adaptive_threshold is not None:scene.cycles.adaptive_threshold=args.adaptive_threshold
    if args.adaptive_min_samples is not None:scene.cycles.adaptive_min_samples=args.adaptive_min_samples
    if scene.cycles.adaptive_min_samples>args.samples:
        raise ValueError('Packed adaptive minimum exceeds the requested sample limit')
    scene.render.fps=24;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGB';scene.render.image_settings.color_depth='8'
    scene.render.threads_mode='FIXED';scene.render.threads=2
    enabled=[]
    if args.cpu_preview:
        scene.cycles.device='CPU';backend='CPU';scene.render.resolution_x=480;scene.render.resolution_y=270
        enabled=[{'name':'CPU','type':'CPU'}]
    else:
        preferences=bpy.context.preferences.addons['cycles'].preferences
        preferences.compute_device_type='OPTIX';preferences.get_devices();backend=preferences.compute_device_type
        for device in preferences.devices:
            device.use=device.type=='OPTIX' and '4050' in device.name
            if device.use:enabled.append({'name':device.name,'type':device.type,'id':getattr(device,'id',None)})
        if len(enabled)!=1:raise RuntimeError('Exactly one RTX 4050 OptiX device is required; CPU fallback is disabled')
        scene.cycles.device='GPU';scene.render.resolution_x=1920;scene.render.resolution_y=1080
    build_hash=bpy.app.build_hash.decode('utf8','replace') if isinstance(bpy.app.build_hash,bytes) else str(bpy.app.build_hash)
    provenance={
        'schema':SCHEMA,'source_sha256':source_sha,'scene':scene.name,'view_layer':bpy.context.view_layer.name,
        'driver_sha256':sha(Path(__file__)),'policy_sha256':sha(Path(policy.__file__)),
        'blender':{'version':list(bpy.app.version),'version_string':bpy.app.version_string,'build_hash':build_hash},
        'engine':scene.render.engine,'backend':backend,'devices':enabled,'device_mode':scene.cycles.device,
        'scene_frame_range':[int(scene.frame_start),int(scene.frame_end)],'camera_schedule':schedule,
        'sampling':scalar_settings(scene.cycles,[
            'samples','use_adaptive_sampling','adaptive_threshold','adaptive_min_samples','use_denoising','denoiser',
            'denoising_use_gpu','denoising_input_passes','denoising_prefilter','denoising_quality','seed','use_animated_seed','sample_offset',
            'max_bounces','diffuse_bounces','glossy_bounces','transmission_bounces','volume_bounces','transparent_max_bounces',
            'sample_clamp_direct','sample_clamp_indirect','light_sampling_threshold','use_light_tree','use_fast_gi','use_guiding',
            'volume_step_rate','volume_max_steps','motion_blur_position','rolling_shutter_type','rolling_shutter_duration']),
        'render':dict(scalar_settings(scene.render,['fps','fps_base','resolution_x','resolution_y','resolution_percentage',
                     'threads_mode','threads','use_motion_blur','motion_blur_shutter','use_persistent_data']),
                      image=scalar_settings(scene.render.image_settings,['file_format','color_mode','color_depth','compression','color_management'])),
        'color':dict(scalar_settings(scene.view_settings,['view_transform','look','exposure','gamma','use_curve_mapping']),
                     display_device=scene.display_settings.display_device),
    }
    output.mkdir(parents=True,exist_ok=True);working=output/'working';working.mkdir(exist_ok=True)
    stop=Path(args.stop_file).resolve() if args.stop_file else output/'STOP'
    # A lock prevents concurrent chunks from publishing into one job folder.
    with (output/'.render.lock').open('a') as lock:
        try:fcntl.flock(lock.fileno(),fcntl.LOCK_EX|fcntl.LOCK_NB)
        except BlockingIOError:raise RuntimeError('Another renderer owns this output directory')
        global_proof=output/'job-provenance.json'
        if global_proof.is_file():policy.require_same_provenance(json.loads(global_proof.read_text()),provenance)
        else:
            if list(output.glob('frame-*.png')) or list(output.glob('render-evidence-*.json')):
                raise RuntimeError('Existing images lack current job provenance; choose a new output directory')
            atomic_json(global_proof,provenance,working)
        evidence=output/f'render-evidence-{args.start:06d}-{args.end:06d}.json'
        header={'schema':SCHEMA,'source':str(source),'provenance':provenance,'provenance_sha256':policy.digest(provenance),
                'start':args.start,'end':args.end,'stop_file':str(stop)}
        records=[]
        if evidence.is_file():
            if not args.resume:raise RuntimeError('Checkpoint exists; use --resume or a new job directory')
            previous=json.loads(evidence.read_text())
            if previous.get('schema')!=SCHEMA or previous.get('start')!=args.start or previous.get('end')!=args.end:
                raise ValueError('Checkpoint schema/frame bounds changed')
            policy.require_same_provenance(previous.get('provenance',{}),provenance)
            if previous.get('provenance_sha256')!=policy.digest(provenance):raise ValueError('Checkpoint provenance digest differs')
            records=previous['frames']
        if len({record['frame'] for record in records})!=len(records):raise ValueError('Duplicate frame records in checkpoint')
        by_frame={record['frame']:record for record in records}
        for number,record in by_frame.items():
            if not args.start<=number<=args.end:raise ValueError('Checkpoint contains an out-of-range frame')
            policy.validate_record(record,number,schedule)
        def checkpoint(status,error=None):
            atomic_json(evidence,dict(header,status=status,complete=status=='complete',frames=sorted(records,key=lambda row:row['frame']),error=error),working)
        checkpoint('running')
        try:
            for number in range(args.start,args.end+1):
                if stop.exists():
                    checkpoint('stopped');print('[full-render] GRACEFUL STOP between frames',flush=True);return
                stat=source.stat()
                if (stat.st_size,stat.st_mtime_ns)!=source_identity:raise RuntimeError('Packed source changed during the render job')
                camera,camera_record=select_packed_camera(bpy,scene,schedule,number)
                final=output/f'frame-{number:06d}.png'
                if number in by_frame:
                    existing=by_frame[number]
                    if not final.is_file() or final.stat().st_size!=existing['bytes'] or sha(final)!=existing['sha256']:
                        raise RuntimeError(f'Image differs from checkpoint at frame {number}')
                    print('[full-render] VERIFIED RESUME',number,camera.name,flush=True);continue
                if final.exists():raise RuntimeError(f'Untracked/overlapping output frame {number}; use a distinct chunk directory')
                stage=working/final.name;scene.render.filepath=str(stage);began=time.monotonic()
                bpy.ops.render.render(write_still=True)
                if scene.camera!=camera or packed_camera_schedule(scene)!=schedule:
                    raise RuntimeError('Camera binding changed while rendering; refusing to publish frame')
                if not stage.is_file() or stage.stat().st_size==0:raise RuntimeError('Renderer did not produce a complete PNG')
                record=dict(camera_record,frame=number,seconds=time.monotonic()-began,bytes=stage.stat().st_size,sha256=sha(stage))
                policy.validate_record(record,number,schedule)
                os.replace(stage,final);records.append(record);by_frame[number]=record;checkpoint('running')
                print('[full-render]',json.dumps(record),flush=True)
            checkpoint('complete')
        except BaseException as error:
            checkpoint('failed',str(error));raise


if __name__=='__main__':run(parse_args())
