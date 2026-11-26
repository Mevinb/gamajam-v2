"""Read-only CPU audit of all exported hair UV partitions and atlas alpha."""
import json
import sys
from pathlib import Path

import bpy
import numpy as np

ROOT=Path(__file__).resolve().parents[3]
MODELS=ROOT/'Assets/Parasite/Art/Characters/Models'
TEXTURES=ROOT/'Assets/Parasite/Art/Characters/Textures'
report={}
for path in sorted(MODELS.glob('*.json')):
    meta=json.loads(path.read_text())
    for ob in list(bpy.data.objects):bpy.data.objects.remove(ob,do_unlink=True)
    bpy.data.orphans_purge(do_local_ids=True,do_linked_ids=False,do_recursive=True)
    bpy.ops.import_scene.fbx(filepath=str(path.with_suffix('.fbx')))
    mesh=next(o for o in bpy.data.objects if o.type=='MESH' and o.name.endswith('_LOD0'))
    slots={i for i,m in enumerate(mesh.data.materials)if m and m.name.split('.')[0]=='Hair'}
    uv=mesh.data.uv_layers.active.data;top=bottom=cross=0;centres=[]
    for face in mesh.data.polygons:
        if face.material_index not in slots:continue
        coords=[uv[i].uv for i in face.loop_indices];ys=[p.y for p in coords]
        if min(ys)>=.25-1e-5:
            top+=1;centres.append(sum(coords,np.zeros(2))/len(coords))
        elif max(ys)<=.25+1e-5:bottom+=1
        else:cross+=1
    image=bpy.data.images.load(str(TEXTURES/meta['textures']['hairA']),check_existing=False);w,h=image.size
    values=np.empty(w*h*4,np.float32);image.pixels.foreach_get(values);alpha=values.reshape(h,w,4)[...,3]
    scalp=alpha[int(h*.25):];old_visible=scalp>.4;semi=(scalp>.4)&(scalp<.99)
    centre_alpha=[float(alpha[min(h-1,max(0,int(p[1]*h))),min(w-1,max(0,int(p[0]*w)))])for p in centres]
    report[meta['id']]={'hair_slot_faces_top':top,'face_accessory_faces_bottom':bottom,'faces_crossing_quarter_boundary':cross,'scalp_old_clipped_visible_pixels':int(old_visible.sum()),'scalp_pixels_semitransparent_after_blend':int(semi.sum()),'semitransparent_fraction_of_old_opaque_core':float(semi.sum()/max(1,old_visible.sum())),'top_face_centroid_alpha_min':min(centre_alpha)if centre_alpha else None,'top_face_centroids_alpha_0_4_to_0_99':sum(.4<a<.99 for a in centre_alpha),'uv_partition_valid':cross==0}
assert len(report)==25 and all(row['uv_partition_valid']for row in report.values())
out=ROOT/'Logs/skin-realism-20261127/hair-atlas-uv-alpha-audit.json';out.write_text(json.dumps(report,indent=2)+'\n');print('[hair-atlas] AUDIT PASS',len(report),json.dumps(report['LakeGirl']),flush=True)
