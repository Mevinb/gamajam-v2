"""CPU-only reproduction of the seven photographed pupil pivots. Never renders/saves input assets.

nice -n 15 blender --background --factory-startup --threads 2 --python \
  Tools/blender/characters/measure_eye_atlases.py -- --project . --output Logs/fresh-eye-calibration.json
Compare the fresh JSON to eye_atlas_calibration.json; Blender supplies numpy.
These photo centroids are magnification pivots, not bone/optical-axis or gaze targets.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path
import bpy
import numpy as np

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--project', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
root = args.project.resolve()
output = args.output.resolve()
assert not output.exists(), 'Fresh output required; preserve the recorded calibration'
canonical = root / 'Tools/blender/characters/eye_atlas_calibration.json'
data = json.loads(canonical.read_text())
assert data['schema'] == 1 and len(data['atlases']) == 7 and len(data['actors']) == 25
expected_paths = {row['path'] for row in data['atlases']}
actual_paths = {'Eyes/' + p.name for p in (root / 'Assets/Parasite/Art/Characters/Textures/Eyes').glob('*.png')}
assert expected_paths == actual_paths, 'Finite seven-atlas scope changed'
for row in data['atlases']:
    path = root / 'Assets/Parasite/Art/Characters/Textures' / row['path']
    before = hashlib.sha256(path.read_bytes()).hexdigest()
    assert before == row['sha256'], 'Measured photograph changed: ' + row['path']
    image = bpy.data.images.load(str(path), check_existing=False)
    try:
        width, height = image.size
        assert (width, height) == (1024, 1024)
        pixels = np.empty(width * height * 4, np.float32)
        image.pixels.foreach_get(pixels)
        channel = pixels.reshape(height, width, 4)[:, :, :3].max(axis=2)
        y, x = np.mgrid[:height, :width]
        centres = []
        for cx, cy in ((.292, .289), (.707, .704)):
            radius = np.hypot((x + .5) / width - cx, (y + .5) / height - cy)
            base = float(np.quantile(channel[radius < .015], .99))
            ys, xs = np.where((radius < .055) & (channel < base + .02))
            assert len(xs) >= 500, 'Empty/undersized pupil region: ' + row['path']
            centres.extend(((float(xs.mean()) + .5) / width, (float(ys.mean()) + .5) / height))
        assert all(np.isfinite(centres))
        row['centres'] = centres
    finally:
        bpy.data.images.remove(image)
    assert hashlib.sha256(path.read_bytes()).hexdigest() == before
actual_ids = set()
for path in sorted((root / 'Assets/Parasite/Art/Characters/Models').glob('*.json')):
    metadata = json.loads(path.read_text())
    expected = next(a for a in data['actors'] if a['id'] == metadata['id'])
    assert metadata['id'] not in actual_ids and metadata['textures']['eyes'] == expected['atlas']
    assert hashlib.sha256(path.read_bytes()).hexdigest() == expected['metadataSHA256']
    actual_ids.add(metadata['id'])
assert actual_ids == {row['id'] for row in data['actors']}
output.parent.mkdir(parents=True, exist_ok=True)
with output.open('x') as stream:
    stream.write(json.dumps(data, indent=2, allow_nan=False) + '\n')
print('[EyeCalibration] source inputs unchanged; fresh CPU-only measurement:', output)
