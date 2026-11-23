"""PARASITE humanoid animation build (Blender 4.5, headless).

usage: blender -b --factory-startup --python build.py -- --src <sources dir> --out <staging dir> [--only A,B] [--blend out.blend]

Sources (see fetch_sources.sh): Quaternius Universal Animation Library 1+2 [Standard] (CC0) and a few
CMU Graphics Lab Motion Capture Database trials (ASF/AMC). Every clip is rebuilt on the UAL skeleton and
exported as one FBX per clip (skeleton only, 30 fps) for Unity's Humanoid importer.
"""
import bpy, sys, os, json, time, argparse
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rig as rigmod
import posing
import clips


def parse():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--src', required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--only', default='')
    ap.add_argument('--blend', default='')
    ap.add_argument('--no-export', action='store_true')
    ap.add_argument('--preview-mannequins', action='store_true')
    return ap.parse_args(argv)


def import_ual(src):
    ual1 = os.path.join(src, 'ual1', 'Universal Animation Library[Standard]', 'Unity', 'UAL1_Standard.fbx')
    ual2 = os.path.join(src, 'ual2', 'Universal Animation Library 2[Standard]', 'Unity', 'UAL2_Standard.fbx')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=ual1)
    arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
    keep = {arm.name} | {o.name for o in bpy.data.objects if o.parent == arm}
    acts = {}
    for a in list(bpy.data.actions):
        acts['UAL1/' + a.name.split('|')[-1]] = a
    before = set(bpy.data.objects.keys())
    bpy.ops.import_scene.fbx(filepath=ual2)
    for a in list(bpy.data.actions):
        if a not in acts.values():
            acts['UAL2/' + a.name.split('|')[-1]] = a
    for name in set(bpy.data.objects.keys()) - before:
        bpy.data.objects.remove(bpy.data.objects[name])
    for n, a in acts.items():
        a.use_fake_user = True
    return arm, acts


def export_clip(arm, act, path):
    arm.animation_data.action = act
    arm.animation_data.action_slot = act.slots[0]
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    f0, f1 = act.frame_range
    sc = bpy.context.scene
    sc.frame_start = int(f0); sc.frame_end = int(f1)
    sc.render.fps = 30
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'ARMATURE'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', use_armature_deform_only=False,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=float(os.environ.get("ANIM_SIMPLIFY", "1.0")),
        use_mesh_modifiers=False, path_mode='STRIP')


def export_mannequin(arm, path):
    """Rest-pose UAL mannequin (mesh + skeleton, no animation) for previews."""
    arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.rotation_quaternion = (1, 0, 0, 0); pb.location = (0, 0, 0); pb.scale = (1, 1, 1)
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    for o in bpy.data.objects:
        if o.parent == arm and o.type == 'MESH':
            o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', bake_anim=False, path_mode='STRIP')


def main():
    args = parse()
    t0 = time.time()
    os.makedirs(args.out, exist_ok=True)
    arm, acts = import_ual(args.src)
    R = rigmod.Rig(arm)
    samplers = {k.split('/', 1)[1]: rigmod.ActionSampler(R, a) for k, a in acts.items()}
    P = posing.Poser(R, samplers)
    ctx = clips.Context(R, P, samplers, args.src)
    only = set(x for x in args.only.split(',') if x)
    # clips that other clips are derived from are always built first
    deps = {'Walk_Possessed': {'Walk'}, 'Run_Possessed': {'Run'}, 'Panic_Flee_Loop': {'Run'},
            'Captured_Enter': {'Captured_Loop'}, 'Captured_Exit': {'Captured_Enter', 'Captured_Loop'},
            'Captured_Collapse': {'Captured_Loop'}}
    for k, v in deps.items():
        if k in only:
            only |= v
    manifest = []
    for spec in clips.CLIPS:
        if only and spec.name not in only:
            continue
        t1 = time.time()
        poses, meta = spec.fn(ctx)
        ctx.built[spec.name] = poses
        act = rigmod.write_action(R, spec.name, poses)
        frames = len(poses) - 1
        info = dict(name=spec.name, frames=frames, seconds=round(frames / 30.0, 4), loop=spec.loop,
                    source=spec.source, **meta)
        manifest.append(info)
        if not args.no_export:
            export_clip(arm, act, os.path.join(args.out, spec.name + '.fbx'))
        print(f"[anim] {spec.name:22s} {frames:4d}f loop={spec.loop} {meta} ({time.time() - t1:.1f}s)")
    if args.preview_mannequins:
        os.makedirs(os.path.join(args.out, 'Preview'), exist_ok=True)
        export_mannequin(arm, os.path.join(args.out, 'Preview', 'UAL_Mannequin.fbx'))
    if not only:
        with open(os.path.join(args.out, 'manifest.json'), 'w') as f:
            json.dump(manifest, f, indent=1)
    if args.blend:
        bpy.ops.wm.save_as_mainfile(filepath=args.blend)
    print(f"[anim] done {len(manifest)} clips in {time.time() - t0:.1f}s")


main()
