"""Portable PARASITE lake films. Never renders on invocation without a mode.

  blender -b --python author_lake.py -- build
  blender -b --python author_lake.py -- preview [arrival|hold|peace|dominion|all]
  blender -b --python author_lake.py -- audit

Build uses the existing packed EndingFilm.blend read-only. Preview is explicitly
Cycles CPU / two threads / 480x270 / eight samples. Saved deliverables use the
future 1080p Cycles GPU quality profile, but this script has no GPU render mode.
"""
from pathlib import Path
import bpy
import math
import json
import sys
import hashlib
import time
from mathutils import Vector, Quaternion

ROOT = Path(__file__).resolve().parents[5]
OUT = ROOT / 'ArtSource/FullCinematics/Lake'
SOURCE = ROOT / 'ArtSource/Cinematics/EndingFilm.blend'
FPS = 24
FILMS = {'arrival': 360, 'hold': 144, 'peace': 168, 'dominion': 168}
PREVIEWS = {'arrival': [1, 144, 264, 360], 'hold': [1, 73, 144],
            'peace': [40, 102, 168], 'dominion': [48, 96, 168]}
RAD = math.pi / 180


def key(obj, prop, value, frame):
    setattr(obj, prop, value)
    obj.keyframe_insert(prop, frame=frame)


def mat(name, color, roughness=.65, emission=0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*color, 1)
    b.inputs['Roughness'].default_value = roughness
    b.inputs['Emission Color'].default_value = (*color, 1)
    b.inputs['Emission Strength'].default_value = emission
    return m


def smooth_mesh(o):
    for poly in o.data.polygons:
        poly.use_smooth = True
    return o


def sphere(name, p, scale, material, segments=16, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=p)
    o = bpy.context.object
    o.name = name
    o.scale = scale
    o.data.materials.append(material)
    return smooth_mesh(o)


def area(name, p, target, energy, color, size):
    d = bpy.data.lights.new(name, 'AREA')
    d.energy, d.color, d.shape, d.size = energy, color, 'DISK', size
    o = bpy.data.objects.new(name, d)
    bpy.context.scene.collection.objects.link(o)
    o.location = p
    o.rotation_euler = (Vector(target) - o.location).to_track_quat('-Z', 'Y').to_euler()
    return o


def camera(name, start, end, target_start, target_end, lens, frames, aperture=5.6):
    d = bpy.data.cameras.new(name)
    d.lens, d.sensor_width = lens, 36
    d.dof.use_dof = True
    d.dof.aperture_fstop = aperture
    o = bpy.data.objects.new(name, d)
    bpy.context.scene.collection.objects.link(o)
    for f, p, t in [(frames[0], start, target_start), (frames[1], end, target_end)]:
        o.location = p
        o.rotation_euler = (Vector(t) - o.location).to_track_quat('-Z', 'Y').to_euler()
        d.dof.focus_distance = (Vector(t) - o.location).length
        o.keyframe_insert('location', frame=f)
        o.keyframe_insert('rotation_euler', frame=f)
        d.keyframe_insert('dof.focus_distance', frame=f)
    mark = bpy.context.scene.timeline_markers.new(name, frame=frames[0])
    mark.camera = o
    return o


HOLD_CAM = (-.28, -2.62, 1.48)
HOLD_TARGET = (-1.70, -.10, .94)


def ease(t):
    t = max(0, min(1, t))
    return t*t*(3-2*t)


def envelope(t, begin, apex, end):
    return ease((t-begin)/(apex-begin)) if t < apex else 1-ease((t-apex)/(end-apex))


def foliage_material(name, color_path, alpha_path=None):
    m = mat(name, (.18,.22,.09), .8)
    nt = m.node_tree
    bs = nt.nodes.get('Principled BSDF')
    tx = nt.nodes.new('ShaderNodeTexImage')
    tx.image = bpy.data.images.load(str(color_path),check_existing=True)
    nt.links.new(tx.outputs['Color'],bs.inputs['Base Color'])
    if alpha_path is not None:
        mask = nt.nodes.new('ShaderNodeTexImage')
        mask.image = bpy.data.images.load(str(alpha_path),check_existing=True)
        mask.image.colorspace_settings.name = 'Non-Color'
        alpha = mask.outputs['Color']
    else:
        alpha = tx.outputs['Alpha']
    threshold = nt.nodes.new('ShaderNodeMath')
    threshold.operation = 'GREATER_THAN'
    threshold.inputs[1].default_value = .4
    nt.links.new(alpha,threshold.inputs[0])
    nt.links.new(threshold.outputs[0],bs.inputs['Alpha'])
    return m


def inland_tree_line(scene):
    """Reuse project detailed branch/leaf-card trees and licensed CC0 shrub."""
    tree_dir = ROOT/'Assets/Parasite/Art/City/Foliage/Trees'
    bpy.ops.mesh.primitive_plane_add(size=2,location=(0,83,-.035))
    land = bpy.context.object
    land.name = 'Inland park ground beyond original shore'
    land.scale = (85,60,1)
    land.data.materials.append(bpy.data.materials['CinemaShore'])
    prototypes = []
    for species,leaf in [('Plane','Green'),('Linden','Light')]:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=str(tree_dir/('Tree'+species+'.fbx')))
        imported = list(set(bpy.data.objects)-before)
        tree = next(o for o in imported if o.type=='MESH' and 'LOD0' in o.name)
        for o in imported:
            if o != tree:
                bpy.data.objects.remove(o,do_unlink=True)
        bark = mat('Lake reused '+species+' bark',(.12,.09,.06),.9)
        tex = bark.node_tree.nodes.new('ShaderNodeTexImage')
        tex.image = bpy.data.images.load(str(tree_dir/('Bark'+species+'_albedo.jpg')),check_existing=True)
        bark.node_tree.links.new(tex.outputs['Color'],bark.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
        leaves = foliage_material('Lake reused '+leaf+' leaves',tree_dir/('TreeLeaves'+leaf+'_albedo.png'))
        for slot in tree.material_slots:
            slot.material = bark if slot.material and slot.material.name.startswith('Bark') else leaves
        tree.name = 'Lake park detailed '+species+' prototype'
        prototypes.append(tree)
    for i in range(20):
        proto = prototypes[i%2]
        o = proto if i < 2 else proto.copy()
        if i >= 2:
            scene.collection.objects.link(o)
        o.name = 'Lake park detailed tree %02d'%i
        o.location = (-39+i*4.15,18+(i%3)*3.2,0)
        factor = .79+.16*math.sin(i*1.9)**2
        o.scale = (factor,factor,factor)
        o.rotation_euler[2] = i*2.17
    shrub_dir = ROOT/'Assets/Parasite/Art/City/Props/shrub_04'
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(shrub_dir/'shrub_04.fbx'))
    imported = list(set(bpy.data.objects)-before)
    source = next(o for o in imported if o.type=='MESH')
    source.parent = None
    for o in imported:
        if o != source:
            bpy.data.objects.remove(o,do_unlink=True)
    leaf = foliage_material('Lake reused CC0 shrub leaves',shrub_dir/'shrub_04_diff_1k.png',shrub_dir/'shrub_04_mask.png')
    source.data.materials.clear()
    source.data.materials.append(leaf)
    source.rotation_euler = (0,0,0)
    bpy.context.view_layer.update()
    height = source.dimensions.z
    factor = 2.1/max(.1,height)
    source.scale *= factor
    for i in range(75):
        o = source if i==0 else source.copy()
        if i:
            scene.collection.objects.link(o)
        o.name = 'Lake park detailed understory %02d'%i
        o.location = (-38+i*1.05,12.0+.6*math.sin(i*1.7),0)
        o.rotation_euler[2] = i*2.17
    for material in list(bpy.data.materials):
        if material.users==0:
            bpy.data.materials.remove(material)
    for img in list(bpy.data.images):
        if img.users==0 and img.source=='FILE':
            bpy.data.images.remove(img)


def baseline(scene):
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    scene = bpy.context.scene
    scene.name = 'Lake full ending'
    scene.timeline_markers.clear()
    # Preserve the authored sitting pose, then remove the old three-second insert.
    for o in bpy.data.objects:
        o.animation_data_clear()
        if o.type == 'CAMERA':
            bpy.data.objects.remove(o, do_unlink=True)
    for m in bpy.data.materials:
        if m.use_nodes:
            m.node_tree.animation_data_clear()
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'GPU'
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.cycles.use_adaptive_sampling = True
    scene.cycles.adaptive_threshold = .025
    scene.cycles.max_bounces = 7
    scene.cycles.transmission_bounces = 4
    scene.cycles.transparent_max_bounces = 8
    scene.render.resolution_x, scene.render.resolution_y = 1920, 1080
    scene.render.resolution_percentage = 100
    scene.render.fps, scene.render.fps_base = FPS, 1
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGB'
    scene.render.image_settings.color_depth = '8'
    scene.render.film_transparent = False
    scene.render.use_file_extension = True
    scene.render.use_persistent_data = True
    scene.render.threads_mode, scene.render.threads = 'FIXED', 2
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.exposure = .5
    scene.render.film_transparent = False
    # A broad warm bounce makes the face readable from the lake side without
    # flattening the physical low-sun rim and reflected water.
    area('Human warm bounce', (-3.2, -3.8, 3.4), (-1.7, -.12, 1.0), 100, (1, .78, .58), 3.5)
    area('Quiet eye reflection', (1.0, -2.5, 2.4), (-1.7, -.12, 1.2), 35, (.69, .80, 1), 2.2)
    # Better photographic water at close range; retain the original traced surface.
    water = bpy.data.materials['Ray traced sunset water']
    bs = water.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Roughness'].default_value = .17
    for o in bpy.data.objects:
        if o.type == 'MESH' and o.name.startswith('LakeGirl_LOD'):
            o.hide_render = not o.name.endswith('LOD0')
    inland_tree_line(scene)
    return scene


def animate_environment(scene, film, length):
    water = bpy.data.materials['Ray traced sunset water']
    noise = next(n for n in water.node_tree.nodes if n.type == 'TEX_NOISE')
    # Periodic W animation has matching value AND tangent at the hold boundary.
    for f in range(1, length+2, 6):
        w = .06*(1-math.cos(2*math.pi*(f-1)/144))
        noise.inputs['W'].default_value = w
        noise.inputs['W'].keyframe_insert('default_value', frame=f)
    noise.inputs['W'].default_value = .06*(1-math.cos(2*math.pi*length/144))
    noise.inputs['W'].keyframe_insert('default_value', frame=length+1)
    for name in ['CinemaReed_Mesh', 'CinemaTip_Mesh']:
        o = bpy.data.objects.get(name)
        if not o:
            continue
        o.shape_key_add(name='Basis')
        sk = o.shape_key_add(name='Quiet shore breeze')
        for i, v in enumerate(o.data.vertices):
            h = max(0, min(1, v.co.z / 2.5))
            sk.data[i].co.x += .07*h*h*math.sin(v.co.y*1.7+.4)
            sk.data[i].co.y += .025*h*h
        for f in range(1, length+2, 6):
            sk.value = .5+.45*math.sin(2*math.pi*(f-1)/144)
            sk.keyframe_insert('value', frame=f)
        sk.value = .5+.45*math.sin(2*math.pi*length/144)
        sk.keyframe_insert('value', frame=length+1)


def animate_girl(scene, film, length):
    rig = bpy.data.objects['Armature']
    base = {b.name: b.rotation_euler.copy() for b in rig.pose.bones}
    loc = {b.name: b.location.copy() for b in rig.pose.bones}
    names = ['Hips', 'Spine', 'Chest', 'UpperChest', 'Neck', 'Head', 'LeftEye', 'RightEye',
             'LeftShoulder', 'RightShoulder', 'LeftUpperArm', 'RightUpperArm',
             'LeftLowerArm', 'RightLowerArm', 'LeftHand', 'RightHand']
    fingers = [b.name for b in rig.pose.bones if any(s in b.name for s in ['Index', 'Middle', 'Ring', 'Little', 'Thumb'])]
    names += fingers
    # Regular dense keys make this editable in Blender and independent of drivers.
    frames = sorted(set(list(range(1, length+2, 6))+[length, length+1]))
    for f in frames:
        sec = (f-1)/FPS
        if film == 'arrival':
            # Neutral attention: slow look toward a shore sound, then back to water.
            gaze = envelope(sec, 3.8, 6.3, 10.0)
            pitch = -1.8*envelope(sec, 7.0, 9.0, 13.0)
            yaw, tilt = 8*gaze, 1.2*gaze
            hand = envelope(sec, 5.2, 7.8, 11.8)
            breath = math.sin(2*math.pi*sec/6)
            settle = ease((sec-11)/4)
            yaw *= 1-settle
            tilt *= 1-settle
            hand *= 1-settle
            breath *= 1-settle
        elif film == 'hold':
            phase = 2*math.pi*(f-1)/144
            breath, yaw, pitch, tilt, hand = math.sin(phase), .6*math.sin(phase), 0, .25*math.sin(phase), .12*math.sin(phase)
        elif film == 'peace':
            release = ease((sec-.35)/3.0)
            breath = math.sin(2*math.pi*sec/6)
            yaw, pitch, tilt, hand = -4*release, 3.4*release, -2*release, -.8*release
        else:
            deliberate = ease((sec-.4)/1.9)
            breath = .65*math.sin(2*math.pi*sec/6)
            yaw, pitch, tilt, hand = -10*deliberate, -3.5*deliberate, 0, deliberate
        for name in names:
            b = rig.pose.bones[name]
            b.rotation_mode = 'XYZ'
            b.rotation_euler = base[name]
            b.location = loc[name]
            if name == 'Head':
                b.rotation_euler.x += pitch*RAD
                b.rotation_euler.y += tilt*RAD
                b.rotation_euler.z += yaw*RAD
            elif name == 'Neck':
                b.rotation_euler.z += .28*yaw*RAD
                b.rotation_euler.x += .18*pitch*RAD
            elif name in ['LeftEye', 'RightEye']:
                b.rotation_euler.z += .18*yaw*RAD
                b.rotation_euler.x += .18*pitch*RAD
            elif name in ['Chest', 'UpperChest']:
                b.rotation_euler.x += .55*breath*RAD
                if film == 'peace':
                    b.rotation_euler.x += 1.1*release*RAD
            elif name == 'Spine':
                b.location.z += .0022*breath
            elif name.endswith('Shoulder'):
                b.rotation_euler.y += (.3*breath + (-1.5*release if film == 'peace' else 0))*RAD
            elif name in ['LeftLowerArm', 'RightLowerArm']:
                sign = 1 if name.startswith('Left') else -1
                b.rotation_euler.x += (-2.3 if film == 'peace' else 3.5)*hand*RAD
                b.rotation_euler.y += sign*1.2*hand*RAD
            elif name in ['LeftHand', 'RightHand']:
                sign = 1 if name.startswith('Left') else -1
                b.rotation_euler.y += sign*4*hand*RAD
                b.rotation_euler.z += sign*2*hand*RAD
            elif name in fingers:
                # Initial hands stay relaxed. Only the chosen Dominion curls them.
                curl = 29*hand if film == 'dominion' else (4*hand if film == 'peace' else 1.5*hand)
                if 'Thumb' in name:
                    curl *= .45
                b.rotation_euler.x += curl*RAD
            b.keyframe_insert('rotation_euler', frame=f)
            if name == 'Spine':
                b.keyframe_insert('location', frame=f)
    rig['performance'] = {'arrival': 'neutral gaze and resting hand readjustment; periodic breathing',
                         'hold': 'six second periodic breathing, eyes and loose hands; no outcome',
                         'peace': 'exhale, open relaxed palms, softened gaze, ordinary eyes',
                         'dominion': 'deliberate look, fingers curl, eyes activate after explicit choice'}[film]
    return rig


def dominion_reveal(scene, rig, length):
    # Shader mask uses the existing iris texture: darker iris receives the green
    # emission while pale sclera stays ordinary. Nothing is added to neutral files.
    eye = bpy.data.materials['Eyes'].copy()
    eye.name = 'Dominion chosen - iris only'
    eye_bs = eye.node_tree.nodes.get('Principled BSDF')
    tex = next(n for n in eye.node_tree.nodes if n.type == 'TEX_IMAGE')
    ramp = eye.node_tree.nodes.new('ShaderNodeValToRGB')
    ramp.name = 'Dark iris mask - preserve white sclera'
    ramp.color_ramp.elements[0].position = .12
    ramp.color_ramp.elements[0].color = (1, 1, 1, 1)
    ramp.color_ramp.elements[1].position = .52
    ramp.color_ramp.elements[1].color = (0, 0, 0, 1)
    eye.node_tree.links.new(tex.outputs['Color'], ramp.inputs['Fac'])
    mult = eye.node_tree.nodes.new('ShaderNodeMath')
    mult.operation = 'MULTIPLY'
    eye.node_tree.links.new(ramp.outputs['Color'], mult.inputs[0])
    eye.node_tree.links.new(mult.outputs[0], eye_bs.inputs['Emission Strength'])
    eye_bs.inputs['Emission Color'].default_value = (.10, 1.0, .18, 1)
    for f, strength in [(1, 0), (24, 0), (48, 2.5), (72, 4), (168, 3.1)]:
        mult.inputs[1].default_value = strength
        mult.inputs[1].keyframe_insert('default_value', frame=f)
    for o in bpy.data.objects:
        if o.name.startswith('LakeGirl_LOD'):
            o.data.materials[1] = eye
    # A quiet opposite bank, deliberately introduced only by the chosen branch.
    earth = mat('Dominion distant bank', (.12, .145, .11), .92)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -47, -.04))
    bank = bpy.context.object
    bank.name = 'Dominion opposite inhabited bank'
    bank.scale = (104, 11, .25)
    bank.data.materials.append(earth)
    cloth = mat('Distant people muted charcoal', (.08, .10, .085), .9)
    skin = mat('Distant people quiet skin', (.22, .18, .13), .8)
    # Original low polygon distant people, sufficient only for the distant reveal.
    # The living signals are human chest positions, with an authored two-beat pulse.
    for i in range(42):
        x = -43 + i*2.1 + .36*math.sin(i*2.3)
        y = -43.5 - (i % 4)*1.7
        height = 1.5+.12*math.sin(i*1.6)
        root = bpy.data.objects.new('Distant person %02d' % (i+1), None)
        scene.collection.objects.link(root)
        root.location = (x, y, .12)
        for suffix, p, scale, material in [
            ('body', (x,y,.12+height*.50), (.19,.13,height*.32), cloth),
            ('head', (x,y,.12+height*.90), (.13,.12,.16), skin),
            ('left leg', (x-.08,y,.12+height*.17), (.065,.075,height*.18), cloth),
            ('right leg', (x+.08,y,.12+height*.17), (.065,.075,height*.18), cloth),
        ]:
            o = sphere('Distant person %02d %s' % (i+1,suffix), p, scale, material, 8, 4)
        pulse_mat = mat('Chosen network heartbeat %02d' % (i+1), (.07,.72,.12), .35)
        bs = pulse_mat.node_tree.nodes.get('Principled BSDF')
        heart = sphere('Dominion heartbeat %02d' % (i+1), (x,y+.16,.12+height*.65), (.095,.095,.095), pulse_mat, 12, 6)
        heart['boundary'] = 'ONLY after explicit Dominion; no neutral/Peace signal'
        for f in range(1, length+1, 3):
            sec = (f-1)/FPS
            activation = ease((sec-3.5)/1.6)
            phase = (sec*1.03-i*.018) % 1
            beat = math.exp(-((phase-.12)/.055)**2)+.62*math.exp(-((phase-.31)/.06)**2)
            strength = activation*(1.7+9*beat)
            bs.inputs['Emission Strength'].default_value = strength
            bs.inputs['Emission Strength'].keyframe_insert('default_value', frame=f)
            s = .082 + .045*activation*beat
            key(heart, 'scale', (s,s,s), f)
    # The sun cools only after the chosen gesture; no apocalyptic spectacle.
    sun = bpy.data.lights['Golden hour sunlight']
    for f,c,e in [(1,(1,.64,.34),1.5), (84,(.69,.79,.61),1.1), (168,(.49,.65,.53),.9)]:
        sun.color, sun.energy = c,e
        sun.keyframe_insert('color', frame=f)
        sun.keyframe_insert('energy', frame=f)


SHOTS = {
 'arrival': [
  (1,120,'01 Shore arrival - human scale', (1.2,8.6,1.64),(.4,5.6,1.56),(-.8,-10,1.02),(-1.3,-4,.95),35),
  (121,240,'02 A person at the water',(-3.2,-2.4,1.49),(-2.7,-1.85,1.42),(-1.71,-.12,1.06),(-1.71,-.12,1.06),51),
  (241,360,'03 Neutral attention - choice approach',(-.08,-3.18,1.55),HOLD_CAM,HOLD_TARGET,HOLD_TARGET,48),
 ],
 'hold': [(1,144,'CHOICE HOLD - no automatic outcome',HOLD_CAM,HOLD_CAM,HOLD_TARGET,HOLD_TARGET,48)],
 'peace': [
  (1,72,'PEACE chosen - the breath releases',HOLD_CAM,(-.58,-2.35,1.44),HOLD_TARGET,(-1.7,-.12,1.02),48),
  (73,120,'PEACE - open hands',(-1.04,-1.62,1.06),(-1.14,-1.52,1.08),(-1.76,-.23,.73),(-1.76,-.23,.73),58),
  (121,168,'PEACE - a quiet human shore',(1.2,6.2,2.10),(2.9,8.5,2.65),(-1.4,-9,.95),(-.7,-12,.85),36),
 ],
 'dominion': [
  (1,60,'DOMINION chosen - deliberate gaze',HOLD_CAM,(-1.10,-1.27,1.34),HOLD_TARGET,(-1.72,-.15,1.22),56),
  (61,108,'DOMINION - the deliberate finger curl',(-2.45,-1.60,1.05),(-2.35,-1.48,1.03),(-1.76,-.23,.73),(-1.76,-.23,.73),55),
  (109,168,'DOMINION - crane toward many distant hearts',(.6,4.8,2.9),(1.6,17.5,10.5),(-1.1,-14,1.0),(0,-37,1.2),33),
 ]
}


def build(film):
    scene = baseline(bpy.context.scene)
    length = FILMS[film]
    scene.frame_start, scene.frame_end = 1, length
    scene['film'] = film
    scene['duration_seconds'] = length/FPS
    scene['explicit_choice_boundary'] = 'neutral' if film in ['arrival','hold'] else 'play ONLY after explicit '+film.upper()
    scene['loop_period_frames'] = 144 if film == 'hold' else 0
    scene['original_source'] = 'ArtSource/Cinematics/EndingFilm.blend (opened read-only)'
    animate_environment(scene, film, length)
    rig = animate_girl(scene, film, length)
    if film == 'dominion':
        dominion_reveal(scene, rig, length)
    if film == 'peace':
        bounce = bpy.data.lights['Human warm bounce']
        for f, energy in [(1,100), (96,133), (168,133)]:
            key(bounce, 'energy', energy, f)
    for a,b,name,p,q,t,u,lens in SHOTS[film]:
        cam = camera(name,p,q,t,u,lens,(a,b), 7.1 if 'crane' in name else 5.6)
        if a == 1:
            scene.camera = cam
    scene.frame_set(1)
    scene.render.filepath = str(OUT / 'frames' / film / 'frame-')
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.file.pack_all()
    path = OUT / ('Lake_'+film.title()+'.blend')
    bpy.ops.wm.save_as_mainfile(filepath=str(path), compress=True)
    print('[lake] AUTHORED', film, length, path, flush=True)


def audit():
    reports = {}
    for film,length in FILMS.items():
        path = OUT / ('Lake_'+film.title()+'.blend')
        bpy.ops.wm.open_mainfile(filepath=str(path))
        s = bpy.context.scene
        packed = [i.name for i in bpy.data.images if i.source == 'FILE' and not i.packed_file]
        hearts = [o.name for o in bpy.data.objects if o.name.startswith('Dominion heartbeat')]
        assert (s.frame_end,s.render.fps,s.render.resolution_x,s.render.resolution_y)==(length,24,1920,1080)
        assert not packed, packed
        assert path.stat().st_size < 100_000_000
        if film != 'dominion':
            assert not hearts
            assert not any(m.name.startswith('Dominion chosen') for m in bpy.data.materials)
        else:
            assert len(hearts)==42
        rig = bpy.data.objects['Armature']
        s.frame_set(1)
        first = {b.name: list(b.matrix_basis) for b in rig.pose.bones}
        water_noise = next(n for n in bpy.data.materials['Ray traced sunset water'].node_tree.nodes if n.type == 'TEX_NOISE')
        first_water = water_noise.inputs['W'].default_value
        first_wind = {name:bpy.data.objects[name].data.shape_keys.key_blocks['Quiet shore breeze'].value
                      for name in ['CinemaReed_Mesh','CinemaTip_Mesh']}
        delta = 0
        environment_delta = 0
        if film == 'hold':
            s.frame_set(145)
            delta = max(abs(b.matrix_basis[i][j]-first[b.name][i][j]) for b in rig.pose.bones for i in range(4) for j in range(4))
            assert delta < 1e-5, delta
            environment_delta = max([abs(water_noise.inputs['W'].default_value-first_water)] +
                [abs(bpy.data.objects[name].data.shape_keys.key_blocks['Quiet shore breeze'].value-value)
                 for name,value in first_wind.items()])
            assert environment_delta < 1e-5, environment_delta
        motion = {}
        for name in ['Head','Chest','LeftHand','RightHand','LeftIndexProximal']:
            values = []
            for frame in range(1,length+1,6):
                s.frame_set(frame)
                values.append(tuple(rig.pose.bones[name].rotation_euler))
            motion[name] = [round((max(v[axis] for v in values)-min(v[axis] for v in values))/RAD,4) for axis in range(3)]
        reports[film] = {'source':str(path.relative_to(ROOT)), 'bytes':path.stat().st_size,
                         'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
                         'frame_count':length,'packed_external_images':not packed,
                         'camera_cuts':len(s.timeline_markers), 'heartbeat_count':len(hearts),
                         'hold_endpoint_matrix_max_delta':delta,'hold_environment_endpoint_max_delta':environment_delta,
                         'bone_euler_range_degrees_xyz':motion,'saved_quality_samples':s.cycles.samples}
    (OUT/'audit.json').write_text(json.dumps(reports,indent=2)+'\n')
    print('[lake] AUDIT PASS', json.dumps(reports),flush=True)


def preview(film):
    source = OUT/('Lake_'+film.title()+'.blend')
    bpy.ops.wm.open_mainfile(filepath=str(source))
    s = bpy.context.scene
    s.cycles.device = 'CPU'
    s.cycles.samples = 8
    s.render.threads_mode, s.render.threads = 'FIXED', 2
    s.render.resolution_x, s.render.resolution_y = 480,270
    s.render.resolution_percentage = 100
    folder = OUT/'previews'
    folder.mkdir(exist_ok=True)
    evidence = {'source':str(source.relative_to(ROOT)), 'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
                'engine':'CYCLES','device':'CPU','threads':2,'samples':8,'resolution':[480,270],'frames':[]}
    for f in PREVIEWS[film]:
        s.frame_set(f)
        s.render.filepath = str(folder/('%s_%04d_cpu.png'%(film,f)))
        started = time.monotonic()
        bpy.ops.render.render(write_still=True)
        png = Path(s.render.filepath)
        evidence['frames'].append({'frame':f,'png':png.name,'seconds':round(time.monotonic()-started,3),
                                    'sha256':hashlib.sha256(png.read_bytes()).hexdigest()})
        print('[lake] CPU PREVIEW',film,f,s.render.filepath,flush=True)
    (folder/(film+'_evidence.json')).write_text(json.dumps(evidence,indent=2)+'\n')


def manifest():
    value = {'format':'PARASITE full lake cinematic authoring v1','fps':FPS,'resolution':[1920,1080],
      'render_profile':{'engine':'Cycles','saved_device':'GPU','samples':48,'adaptive_threshold':.025,
                        'denoise':True,'preview_device':'CPU','preview_threads':2,'preview_samples':8,'preview_resolution':[480,270]},
      'trigger_contract':{'arrival':'After original campaign extraction only; skip may advance to explicit choice screen.',
                         'hold':'Repeat while awaiting a deliberate player choice. Waiting, timeout or skip NEVER chooses.',
                         'peace':'Only after explicit Peace. Then original summary.',
                         'dominion':'Only after explicit Dominion. Then original summary.',
                         'continuation':'Later Continue is a separate runtime action; these sources do not alter campaign state.'},
      'source_provenance':{'read_only_source':str(SOURCE.relative_to(ROOT)),
                           'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
                           'reused':'Project original lakeside shore, bench, jetty, textured LakeGirl humanoid rig, sun and water; detailed procedural city trees and the existing CC0 shrub_04; all images packed.',
                           'new':'Original keyed performance/cameras/periodic wind, park planting/ground layout, procedural distant bank and 42 distant human silhouettes.',
                           'existing_foliage_credits':'Docs/THIRD_PARTY_ASSETS.md: CC0 Poly Haven Bark Platanus (Dimitrios Savva), Bark Brown 02 (Rob Tuytel), Shrub 04 (Rico Cilliers), and CC0 ambientCG LeafSet atlases (Lennart Demes).' },
      'films':{film:{'source':'Lake_'+film.title()+'.blend','seconds':n/FPS,'frames':n,
                     'shots':[{'start':a,'end':b,'description':name} for a,b,name,*_ in SHOTS[film]],
                     'preview_frames':PREVIEWS[film]} for film,n in FILMS.items()},
      'render_estimate':{'unique_frames':sum(FILMS.values()),'first_play_with_one_branch_frames':360+144+168,
                         'encoded_h264_at_12_to_20_mbps_MB':{'all_four':[52.5,87.5],'arrival':[22.5,37.5],'hold':[9,15],'each_branch':[10.5,17.5]},
                         'unmeasured_RTX4050_seconds_per_frame_range':[1,5],
                         'unmeasured_RTX4050_total_minutes_range':[14,70],
                         'notes':'840 unique frames. GPU timing is an estimate, not a measured benchmark; no GPU or final movie render performed by author script. PNG intermediate size depends on content (reserve 1-3 GB).'},
      'verification_limits':['CPU still previews validate composition and pose; real-time motion is not visually certified by stills.',
                             'Full quality RTX render, final codec encode/decode, audio mix and runtime branch wiring remain with integrating agent.']}
    (OUT/'manifest.json').write_text(json.dumps(value,indent=2)+'\n')


def redress():
    """Fast final park-density iteration; equivalent to a fresh full build."""
    for film in FILMS:
        path = OUT/('Lake_'+film.title()+'.blend')
        bpy.ops.wm.open_mainfile(filepath=str(path))
        scene = bpy.context.scene
        shrubs = [o for o in bpy.data.objects if o.name.startswith('Lake park detailed understory')]
        source = next(o for o in shrubs if o.name.endswith('00'))
        for o in shrubs:
            if o != source:
                bpy.data.objects.remove(o,do_unlink=True)
        source.rotation_euler = (0,0,0)
        bpy.context.view_layer.update()
        source.scale *= 2.1/max(.1,source.dimensions.z)
        for i in range(75):
            o = source if i==0 else source.copy()
            if i:
                scene.collection.objects.link(o)
            o.name = 'Lake park detailed understory %02d'%i
            o.location = (-38+i*1.05,12.0+.6*math.sin(i*1.7),0)
            o.rotation_euler[2] = i*2.17
        scene.frame_set(1)
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.file.pack_all()
        bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
    manifest()
    audit()


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    args = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['build']
    if args[0] == 'build':
        for film in (FILMS if len(args)<2 or args[1]=='all' else [args[1]]):
            build(film)
        manifest()
        audit()
    elif args[0] == 'preview':
        films = FILMS if len(args)<2 or args[1]=='all' else [args[1]]
        for film in films:
            preview(film)
    elif args[0] == 'audit':
        audit()
    elif args[0] == 'redress':
        redress()
    else:
        raise SystemExit('Use build, preview [film|all], or audit; GPU rendering is intentionally not implemented here.')


if __name__ == '__main__':
    main()
