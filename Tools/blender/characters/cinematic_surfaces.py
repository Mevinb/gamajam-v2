"""Rebind the four existing character slots for close Cycles photography.

Root scene authoring may call upgrade_character(mesh, 'LakeGirl', project_root).
No geometry, rig, action, slot order, pose, camera or light is changed. Source
materials are copied before editing, and only images with actual disk files
are bound. The same skin bake and colour/cloth atlases serve Unity and Cycles.
"""
import json
from pathlib import Path

import bpy

from skin_profiles import textures_for


def _texture(nt, path, data=False, point=False):
    path = Path(path)
    if not path.is_file():
        raise FileNotFoundError("Character surface texture missing: " + str(path))
    image = bpy.data.images.load(str(path), check_existing=True)
    image.colorspace_settings.name = "Non-Color" if data else "sRGB"
    node = nt.nodes.new("ShaderNodeTexImage")
    node.image = image
    node.extension = "EXTEND"
    node.interpolation = "Closest" if point else "Linear"
    return node


def _math(nt, operation, a, b):
    node = nt.nodes.new("ShaderNodeMath")
    node.operation = operation
    for index, value in enumerate((a, b)):
        if isinstance(value, (int, float)):
            node.inputs[index].default_value = value
        else:
            nt.links.new(value, node.inputs[index])
    return node.outputs[0]


def _normal(nt, path, bsdf, strength):
    tx = _texture(nt, path, data=True)
    normal = nt.nodes.new("ShaderNodeNormalMap")
    normal.space = "TANGENT"
    normal.inputs["Strength"].default_value = strength
    nt.links.new(tx.outputs["Color"], normal.inputs["Color"])
    nt.links.new(normal.outputs["Normal"], bsdf.inputs["Normal"])


def _kind(name):
    # Appended originals: Skin.001. New films: LakeGirl Skin. Imported FBX: Skin.
    stem = name.split(".")[0]
    for kind in ("Skin", "Eyes", "Hair", "Cloth"):
        if stem == kind or stem.endswith(" " + kind) or stem.startswith(kind + " ") or ("_" + kind) in stem:
            return kind
    return None


def upgrade_character(mesh, cid, project_root=None):
    root = Path(project_root) if project_root else Path(__file__).resolve().parents[3]
    folder = root / "Assets/Parasite/Art/Characters"
    meta = json.loads((folder / "Models" / (cid + ".json")).read_text())
    textures = dict(meta["textures"])
    textures.update(textures_for(meta.get("age", 0.5)))
    folder /= "Textures"
    result = {}
    for index, source in enumerate(list(mesh.data.materials)):
        if source is None:
            continue
        kind = _kind(source.name)
        if kind is None:
            raise ValueError("Unrecognised character material slot: " + source.name)
        if kind in result:
            mesh.data.materials[index] = result[kind]
            continue
        material = source.copy()
        # Preserve the original semantic material prefix for motion/contact audits.
        material.name = kind + ".Realism_" + cid
        material.use_nodes = True
        nt = material.node_tree
        nt.nodes.clear()
        output = nt.nodes.new("ShaderNodeOutputMaterial")
        bs = nt.nodes.new("ShaderNodeBsdfPrincipled")
        nt.links.new(bs.outputs[0], output.inputs["Surface"])
        bs.inputs["Metallic"].default_value = 0.0
        bs.inputs["Roughness"].default_value = 0.62
        bs.inputs["IOR"].default_value = 1.45
        bs.inputs["Coat Weight"].default_value = 0.0
        bs.inputs["Subsurface Weight"].default_value = 0.0
        key = {"Skin": "skin", "Eyes": "eyes", "Hair": "hairA", "Cloth": "clothA"}[kind]
        tx = _texture(nt, folder / textures[key])
        colour = tx.outputs["Color"]
        if kind == "Skin":
            _normal(nt, folder / textures["skinN"], bs, 0.75 if meta.get("age", 0.5) < 0.45 else 1.0)
            mask = _texture(nt, folder / textures["skinM"], data=True)
            channels = nt.nodes.new("ShaderNodeSeparateColor")
            nt.links.new(mask.outputs["Color"], channels.inputs[0])
            nt.links.new(_math(nt, "SUBTRACT", 1.0, channels.outputs["Red"]), bs.inputs["Roughness"])
            # Random-walk diffusion distances are millimetres, never centimetres.
            # Actual thin regions receive more scattering, not more diffuse light.
            thin = _math(nt, "SUBTRACT", 1.0, channels.outputs["Blue"])
            weight = _math(nt, "ADD", 0.08, _math(nt, "MULTIPLY", thin, 0.14))
            nt.links.new(weight, bs.inputs["Subsurface Weight"])
            bs.subsurface_method = "RANDOM_WALK"
            bs.inputs["Subsurface Radius"].default_value = (1.0, 0.38, 0.18)
            bs.inputs["Subsurface Scale"].default_value = 0.00125
            bs.inputs["IOR"].default_value = 1.4
            bs.inputs["Specular IOR Level"].default_value = 0.5
            tint = nt.nodes.new("ShaderNodeMixRGB")
            tint.blend_type = "MULTIPLY"
            tint.inputs[0].default_value = 1.0
            tint.inputs[2].default_value = (*meta.get("skinTint", [1, 1, 1]), 1.0)
            nt.links.new(colour, tint.inputs[1])
            colour = tint.outputs[0]
            # AO is deliberately not painted into albedo: Cycles traces occlusion.
        elif kind == "Eyes":
            # The textured iris/sclera retain a broad lobe under the tear film.
            bs.inputs["Roughness"].default_value = 0.26
            bs.inputs["IOR"].default_value = 1.376
            bs.inputs["Coat Weight"].default_value = 1.0
            bs.inputs["Coat Roughness"].default_value = 0.035
            bs.inputs["Coat IOR"].default_value = 1.336
        elif kind == "Hair":
            nt.links.new(tx.outputs["Alpha"], bs.inputs["Alpha"])
            bs.inputs["Anisotropic"].default_value = 0.4
            tangent = nt.nodes.new("ShaderNodeTangent")
            tangent.direction_type = "UV_MAP"
            nt.links.new(tangent.outputs["Tangent"], bs.inputs["Tangent"])
            uv = nt.nodes.new("ShaderNodeTexCoord")
            stretch = nt.nodes.new("ShaderNodeVectorMath")
            stretch.operation = "MULTIPLY"
            stretch.inputs[1].default_value = (180.0, 12.0, 1.0)
            nt.links.new(uv.outputs["UV"], stretch.inputs[0])
            noise = nt.nodes.new("ShaderNodeTexNoise")
            noise.inputs["Scale"].default_value = 1.0
            noise.inputs["Detail"].default_value = 2.0
            nt.links.new(stretch.outputs[0], noise.inputs["Vector"])
            roughness = _math(nt, "ADD", 0.32, _math(nt, "MULTIPLY", noise.outputs["Fac"], 0.20))
            nt.links.new(roughness, bs.inputs["Roughness"])
            if hasattr(material, "surface_render_method"):
                material.surface_render_method = "DITHERED"
        elif kind == "Cloth":
            mask = _texture(nt, folder / textures["clothM"], data=True, point=True)
            channels = nt.nodes.new("ShaderNodeSeparateColor")
            nt.links.new(mask.outputs["Color"], channels.inputs[0])
            ids = _math(nt, "MULTIPLY", channels.outputs["Red"], 3.0)
            for region, name in enumerate(("A", "B", "C"), 1):
                tint = meta.get("tints", {}).get(name)
                if not tint:
                    continue
                distance = _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", ids, region), 0)
                selected = _math(nt, "LESS_THAN", distance, 0.5)
                mix = nt.nodes.new("ShaderNodeMixRGB")
                mix.blend_type = "MULTIPLY"
                mix.inputs[2].default_value = (*tint, 1.0)
                nt.links.new(selected, mix.inputs[0])
                nt.links.new(colour, mix.inputs[1])
                colour = mix.outputs[0]
            roughness = _math(nt, "SUBTRACT", 1.0, channels.outputs["Blue"])
            nt.links.new(_math(nt, "MAXIMUM", roughness, 0.56 if cid == "LakeGirl" else 0.32), bs.inputs["Roughness"])
            nt.links.new(channels.outputs["Green"], bs.inputs["Metallic"])
            # Keep close-view wool relief below the previous repetitive rope look.
            _normal(nt, folder / textures["clothN"], bs, 0.30 if cid == "LakeGirl" else 0.65)
            bs.inputs["Sheen Weight"].default_value = 0.12 if cid == "LakeGirl" else 0.05
            bs.inputs["Sheen Roughness"].default_value = 0.7
        nt.links.new(colour, bs.inputs["Base Color"])
        mesh.data.materials[index] = material
        result[kind] = material
    if set(result) != {"Skin", "Eyes", "Hair", "Cloth"}:
        raise ValueError(cid + " does not contain the expected four character material slots")
    return result
