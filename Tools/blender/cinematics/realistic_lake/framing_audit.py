"""CPU-only saved-scene camera/focus/source validation; never renders."""
import hashlib
import json
import sys
from pathlib import Path

import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
sys.path.insert(0,str(HERE))
from author import FILMS, QA, OUT
from portrait import eye_position

report={}
for film,length in FILMS.items():
    path=OUT/('Lake_'+film.title()+'.blend');bpy.ops.wm.open_mainfile(filepath=str(path));scene=bpy.context.scene;rig=bpy.data.objects['Armature'];records=[]
    for frame in QA[film]:
        scene.frame_set(frame)
        marker=max((m for m in scene.timeline_markers if m.frame<=frame),key=lambda m:m.frame);camera=marker.camera
        eye=eye_position(rig);projected=world_to_camera_view(scene,camera,eye)
        # Hand detail shots deliberately frame hands; all other camera eyes must
        # be on screen, in front of the lens, with a measured eye focus target.
        hand='hand' in camera.name
        if not hand:assert 0.04<projected.x<.96 and .06<projected.y<.96 and projected.z>0,(film,frame,list(projected))
        focus=camera.data.dof.focus_object;focus_error=(focus.location-eye).length if not hand else None
        if not hand:assert focus_error<1e-5,(film,frame,focus_error)
        records.append({'frame':frame,'camera':camera.name,'lens_mm':camera.data.lens,'fstop':camera.data.dof.aperture_fstop,'eye_camera_normalized_xyz':list(projected),'focus_eye_error_m':focus_error})
    source=ROOT/scene['original_packed_source'];assert hashlib.sha256(source.read_bytes()).hexdigest()==scene['original_packed_source_sha256']
    report[film]={'original_source_unchanged':True,'sampled_framing':records}
(OUT/'framing-audit.json').write_text(json.dumps(report,indent=2)+'\n')
print('[realistic-lake] FRAMING AUDIT PASS',flush=True)
