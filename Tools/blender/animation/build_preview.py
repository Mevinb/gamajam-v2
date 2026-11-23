"""Dev aid: render contact sheets (Workbench) of built actions from a .blend saved by build.py --blend.
usage: blender -b <file.blend> --python build_preview.py -- <outdir> <clip,clip|all> [ncols]"""
import bpy, sys, os
sys.dont_write_bytecode = True
from mathutils import Vector
argv = sys.argv[sys.argv.index('--') + 1:]
outdir = argv[0]; names = argv[1].split(','); ncols = int(argv[2]) if len(argv) > 2 else 8
os.makedirs(outdir, exist_ok=True)
sc = bpy.context.scene
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'SINGLE'
sc.display.shading.single_color = (0.8, 0.7, 0.55); sc.display.shading.show_shadows = True; sc.display.shading.show_cavity = True
sc.render.resolution_x = 300; sc.render.resolution_y = 360
sc.world = bpy.data.worlds.new("W"); sc.world.color = (0.18, 0.2, 0.24)
bpy.ops.mesh.primitive_grid_add(x_subdivisions=20, y_subdivisions=20, size=10)
cam_d = bpy.data.cameras.new("C"); cam = bpy.data.objects.new("C", cam_d); sc.collection.objects.link(cam); sc.camera = cam
def look(eye, target):
    cam.location = eye; cam.rotation_euler = (Vector(target) - Vector(eye)).to_track_quat('-Z', 'Y').to_euler()
acts = [a for a in bpy.data.actions if '|' not in a.name and (names == ['all'] or a.name in names)]
for a in acts:
    arm.animation_data.action = a; arm.animation_data.action_slot = a.slots[0]
    f0, f1 = a.frame_range
    views = (('side', (3.6, 0.0, 1.2)), ('front', (0.6, 3.6, 1.2)), ('q', (-2.4, 2.4, 1.6)))
    for view, off in views:
        for i in range(ncols):
            f = f0 + (f1 - f0) * i / max(1, ncols - 1)
            sc.frame_set(int(f), subframe=f - int(f))
            tgt = Vector((0, 0, 0.8))
            look(tgt + Vector(off) + Vector((0, 0, -0.8)) + Vector((0, 0, 0.0)), tgt)
            sc.render.filepath = os.path.join(outdir, f"{a.name}__{view}_{i:02d}.png")
            bpy.ops.render.render(write_still=True)
