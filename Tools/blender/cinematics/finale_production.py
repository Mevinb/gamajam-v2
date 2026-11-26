#!/usr/bin/env python3
"""Render, encode and install the revised finales without changing historical film evidence.

All commands use a job under Logs/full-cinematic-renders. Freeze only after rendered
art review; render one film at a time with an explicit released RTX. Sources, driver,
frames and movies are hashed. Existing Resources GUIDs survive publication.
"""
import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[3]
DRIVER = ROOT / 'Tools/blender/cinematics/full_story/render_story.py'
POLICY = DRIVER.with_name('render_policy.py')
BLENDER = Path.home() / 'Data/Blender/current/blender'
FILMS = {
    'LakeArrival': ('Lake/Lake_Arrival.blend', 360),
    'LakeHold': ('Lake/Lake_Hold.blend', 144),
    'LakePeace': ('Lake/Lake_Peace.blend', 168),
    'LakeDominion': ('Lake/Lake_Dominion.blend', 168),
    'NorthlineDeparture': ('Train/Northline_RealisticDeparture_v4.blend', 336),
}


def require(value, message):
    if not value:
        raise RuntimeError(message)


def sha(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for data in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(data)
    return h.hexdigest()


def write(path, value):
    stage = path.with_name(path.name + '.writing')
    stage.write_text(json.dumps(value, indent=2, allow_nan=False) + '\n')
    os.replace(stage, path)


def job_path(value):
    job = Path(value).resolve()
    parent = (ROOT / 'Logs/full-cinematic-renders').resolve()
    require(job.is_relative_to(parent) and job != parent, 'Job must be inside Logs/full-cinematic-renders')
    return job


def freeze(job):
    require(not job.exists(), 'Freeze needs a fresh job directory')
    sources = {}
    for name, (relative, count) in FILMS.items():
        source = ROOT / 'ArtSource/RealisticFinales' / relative
        sources[name] = dict(source=str(source.relative_to(ROOT)), sha256=sha(source), frames=count)
    job.mkdir(parents=True)
    write(job / 'contract.json', dict(schema=1, fps=24, resolution=[1920, 1080], samples=192,
          adaptive_threshold=.008, denoiser='OPENIMAGEDENOISE', denoise_gpu=True,
          driver_sha256=sha(DRIVER), policy_sha256=sha(POLICY), sources=sources))


def contract(job):
    data = json.loads((job / 'contract.json').read_text())
    require(data['schema'] == 1 and set(data['sources']) == set(FILMS), 'Wrong finale contract')
    require(sha(DRIVER) == data['driver_sha256'] and sha(POLICY) == data['policy_sha256'], 'Frozen render tools changed')
    for name, row in data['sources'].items():
        require(row['frames'] == FILMS[name][1] and sha(ROOT / row['source']) == row['sha256'], 'Frozen source changed: ' + name)
    return data


def render(job, name, released):
    require(released, 'Root must explicitly release the RTX before rendering')
    data = contract(job); row = data['sources'][name]; folder = job / name
    # Share the Unity runner lock so an unattended film cannot overlap a later Editor job.
    with open('/tmp/parasite-unity.lock', 'a') as gpu_lock:
        fcntl.flock(gpu_lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        for entry in Path('/proc').iterdir():
            if not entry.name.isdigit():
                continue
            try:
                exe = (entry / 'exe').resolve().name
            except (FileNotFoundError, PermissionError, ProcessLookupError):
                continue
            require(exe != 'Parasite.x86_64', 'A live game is using the GPU; preserve its session')
        command = [str(BLENDER), '-b', str(ROOT / row['source']), '-t', '2', '--python-exit-code', '1',
                   '--python', str(DRIVER), '--', '--gpu-released', '--start', '1', '--end', str(row['frames']),
                   '--out', str(folder), '--samples', str(data['samples']), '--adaptive-threshold', str(data['adaptive_threshold']), '--denoise-gpu']
        if (folder / 'job-provenance.json').exists():
            command.append('--resume')
        with (job / (name + '.render.log')).open('a') as log:
            result = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, pass_fds=(gpu_lock.fileno(),))
        require(result.returncode == 0, 'Blender failed: ' + name)
        evidence_path = folder / f"render-evidence-000001-{row['frames']:06d}.json"
        if json.loads(evidence_path.read_text())['status'] == 'stopped':
            print('FINALE_RENDER_STOPPED', name, 'Remove', folder / 'STOP', 'before resuming', flush=True)
            return False
        validate_frames(job, name)
        return True


def validate_frames(job, name):
    data = contract(job); row = data['sources'][name]; folder = job / name
    count = row['frames']; evidence = json.loads((folder / f'render-evidence-000001-{count:06d}.json').read_text())
    require(evidence['complete'] and evidence['status'] == 'complete', 'Incomplete film: ' + name)
    provenance = evidence['provenance']
    require(provenance['source_sha256'] == row['sha256'] and provenance['driver_sha256'] == data['driver_sha256'] and
            provenance['policy_sha256'] == data['policy_sha256'], 'Render/source/tool mismatch')
    require(provenance['backend'] == 'OPTIX' and provenance['sampling']['samples'] == 192 and
            provenance['sampling']['denoising_use_gpu'] and provenance['sampling']['use_denoising'] and
            provenance['sampling']['use_adaptive_sampling'] and abs(provenance['sampling']['adaptive_threshold'] - .008) < 1e-7 and
            provenance['sampling']['denoiser'] == 'OPENIMAGEDENOISE' and provenance['device_mode'] == 'GPU' and
            [provenance['render'][k] for k in ('resolution_x', 'resolution_y', 'fps')] == [1920, 1080, 24], 'Wrong actual render settings')
    frames = evidence['frames']
    require([r['frame'] for r in frames] == list(range(1, count + 1)), 'Missing/duplicate frame')
    for record in frames:
        path = folder / f"frame-{record['frame']:06d}.png"
        require(path.stat().st_size == record['bytes'] and sha(path) == record['sha256'], 'Frame differs from renderer receipt')
        with path.open('rb') as stream:
            header = stream.read(24)
        require(header[:8] == b'\x89PNG\r\n\x1a\n' and struct.unpack('>II', header[16:24]) == (1920, 1080), 'Wrong frame image format')
    return sha(folder / f'render-evidence-000001-{count:06d}.json')


def movie_info(path, name):
    result = subprocess.run(['ffprobe', '-v', 'error', '-threads', '2', '-count_frames', '-show_streams', '-show_format', '-of', 'json', str(path)], capture_output=True, text=True, check=True)
    require(not result.stderr.strip(), 'ffprobe reported errors')
    data = json.loads(result.stdout); streams = data['streams']
    require(len(streams) == 1 and streams[0]['codec_type'] == 'video', 'Movie must have one silent video stream')
    stream = streams[0]
    require(stream['codec_name'] == 'vp8' and stream['pix_fmt'] == 'yuv420p' and stream['width'] == 1920 and stream['height'] == 1080 and
            stream['avg_frame_rate'] == '24/1' and int(stream['nb_read_frames']) == FILMS[name][1], 'Wrong movie stream contract')
    colour = {key: stream.get(key) for key in ('color_range', 'color_space', 'color_transfer', 'color_primaries')}
    require(colour == dict(color_range='tv', color_space='bt709', color_transfer='bt709', color_primaries='bt709'), 'Wrong movie colour metadata: ' + str(colour))
    require(abs(float(data['format']['duration']) - FILMS[name][1] / 24) <= 1 / 24 + .005, 'Wrong movie duration')
    require(0 < path.stat().st_size < 95_000_000, 'Movie exceeds ordinary source blob budget')
    decoded = subprocess.run(['ffmpeg', '-v', 'error', '-threads', '2', '-i', str(path), '-map', '0:v:0', '-f', 'null', '-'], capture_output=True, text=True)
    require(decoded.returncode == 0 and not decoded.stderr.strip(), 'Full movie decode failed')
    return dict(sha256=sha(path), bytes=path.stat().st_size, frames=FILMS[name][1], seconds=FILMS[name][1] / 24,
                width=1920, height=1080, fps=24, audio_tracks=0, codec='vp8', colour=colour, full_decode='PASS')


def encode(job, name):
    folder = job / name
    with (folder / '.render.lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        inputs = validate_frames(job, name); out = job / 'encoded'; out.mkdir(exist_ok=True)
        target = out / (name + '.webm'); receipt = out / (name + '.json')
        require(not target.exists() and not receipt.exists(), 'Movie already exists; validate it instead of overwriting')
        with tempfile.TemporaryDirectory(prefix=name + '-', dir=out) as temp:
            work = Path(temp); candidate = work / target.name
            base = ['ffmpeg', '-hide_banner', '-loglevel', 'warning', '-nostdin', '-y', '-filter_threads', '2', '-threads', '2',
                    '-framerate', '24', '-start_number', '1', '-i', str(folder / 'frame-%06d.png'), '-map', '0:v:0',
                    '-vf', 'zscale=matrixin=gbr:transferin=iec61966-2-1:primariesin=709:rangein=full:matrix=709:transfer=709:primaries=709:range=limited,format=yuv420p', '-frames:v', str(FILMS[name][1]), '-an',
                    '-c:v', 'libvpx', '-b:v', '16000000', '-deadline', 'good', '-threads', '2', '-pix_fmt', 'yuv420p', '-g', '48',
                    '-color_primaries', 'bt709', '-color_trc', 'bt709', '-colorspace', 'bt709', '-color_range', 'tv', '-passlogfile', str(work / 'pass')]
            for index in (1, 2):
                command = base + ['-cpu-used', '4' if index == 1 else '2', '-pass', str(index)]
                command += ['-f', 'null', os.devnull] if index == 1 else [str(candidate)]
                with (out / f'{name}.pass{index}.log').open('w') as log:
                    result = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT)
                require(result.returncode == 0, 'Movie encoding failed')
            info = movie_info(candidate, name)
            require(validate_frames(job, name) == inputs, 'Render inputs changed while encoding')
            os.replace(candidate, target)
            write(receipt, dict(schema=1, film=name, source_sha256=contract(job)['sources'][name]['sha256'], render_evidence_sha256=inputs, movie=info))


def publish(job):
    data = contract(job); out = job / 'encoded'; validated = {}
    for name in FILMS:
        receipt = json.loads((out / (name + '.json')).read_text())
        require(receipt['source_sha256'] == data['sources'][name]['sha256'] and receipt['render_evidence_sha256'] == validate_frames(job, name), 'Encoded source/evidence mismatch')
        validated[name] = movie_info(out / (name + '.webm'), name)
        require(validated[name] == receipt['movie'], 'Encoded movie changed')
    with open('/tmp/parasite-unity.lock', 'a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        backups = job / 'previous-resources'; backups.mkdir(exist_ok=True)
        destination = ROOT / 'Assets/Parasite/Resources/Cinematics/FullStory'
        journal = job / 'publication-state.json'
        def restore(previous):
            for name, old in previous.items():
                target = destination / (name + '.webm'); current = sha(target) if target.exists() else None
                require(current in (old, validated[name]['sha256']), 'External movie change prevents safe recovery: ' + name)
                if old is None:
                    if target.exists(): target.unlink()
                else:
                    backup = backups / target.name; require(sha(backup) == old, 'Publication recovery backup changed')
                    stage = target.with_suffix('.webm.writing'); shutil.copyfile(backup, stage); os.replace(stage, target)
        if journal.exists():
            state = json.loads(journal.read_text()); previous = state['previous']
            require(state['contract_sha256'] == sha(job / 'contract.json'), 'Publication journal belongs to another contract')
            if state['status'] == 'committed':
                require(all(sha(destination / (n + '.webm')) == validated[n]['sha256'] for n in FILMS), 'Published movie changed')
                return
            restore(previous)
        else:
            previous = {name: sha(destination / (name + '.webm')) if (destination / (name + '.webm')).exists() else None for name in FILMS}
        # Back up every original and stage every new clip before replacing any live resource.
        for name, old in previous.items():
            target = destination / (name + '.webm'); backup = backups / target.name
            if old is not None:
                if not backup.exists(): shutil.copy2(target, backup)
                require(sha(backup) == old, 'Original movie backup differs')
            stage = target.with_suffix('.webm.writing'); shutil.copyfile(out / target.name, stage)
            require(sha(stage) == validated[name]['sha256'], 'Staged movie copy differs')
        state = dict(schema=1, status='prepared', previous=previous, contract_sha256=sha(job / 'contract.json'))
        write(journal, state)
        try:
            for name in FILMS:
                target = destination / (name + '.webm'); os.replace(target.with_suffix('.webm.writing'), target)
            # Existing GUIDs/policies stay unchanged. Unity creates the new train metadata on refresh.
            write(job / 'publication.json', dict(schema=1, films=validated, previous_movie_sha256=previous, contract_sha256=sha(job / 'contract.json')))
            state['status'] = 'committed'; write(journal, state)
        except BaseException:
            restore(previous); state['status'] = 'rolled-back'; write(journal, state)
            raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('freeze', 'render', 'encode', 'publish'))
    parser.add_argument('--job', required=True)
    parser.add_argument('--film', choices=tuple(FILMS))
    parser.add_argument('--gpu-released', action='store_true')
    args = parser.parse_args(); job = job_path(args.job)
    if args.action == 'freeze': freeze(job)
    elif args.action == 'publish': publish(job)
    else:
        require(args.film is not None, 'Choose one --film')
        if args.action == 'render':
            if not render(job, args.film, args.gpu_released): return
        else: encode(job, args.film)
    print('FINALE_' + args.action.upper() + '_PASS', args.film or 'all', flush=True)


if __name__ == '__main__':
    main()
