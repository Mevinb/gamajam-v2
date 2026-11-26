"""Small CPU Cycles material/composition review of the packed Blender source."""
import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def main():
    parser=argparse.ArgumentParser();parser.add_argument("--source",type=Path,required=True);parser.add_argument("--model",default="Sedan");parser.add_argument("--out",type=Path,required=True)
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:])
    bpy.ops.wm.open_mainfile(filepath=str(args.source/"Vehicles.blend"),load_ui=False,use_scripts=False)
    scene=bpy.context.scene
    for collection in bpy.data.collections:collection.hide_render=collection.name!="Vehicle"+args.model
    for ob in scene.objects:ob.hide_render="_LOD1" in ob.name or "_LOD2" in ob.name or ob.name.startswith("VehicleTaxiSign")
    world=bpy.data.worlds.new("Physical review daylight");world.use_nodes=True;scene.world=world
    sky=world.node_tree.nodes.new("ShaderNodeTexSky");sky.sky_type="NISHITA";sky.sun_elevation=math.radians(28);sky.sun_rotation=math.radians(140);sky.altitude=70
    world.node_tree.links.new(sky.outputs["Color"],world.node_tree.nodes.get("Background").inputs["Color"])
    world.node_tree.nodes.get("Background").inputs["Strength"].default_value=.09
    bpy.ops.mesh.primitive_plane_add(size=200)
    ground=bpy.context.object;ground.name="Review road only";ground.location.z=-.003
    material=bpy.data.materials.new("Review neutral asphalt");material.use_nodes=True;bsdf=material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value=(.055,.06,.066,1);bsdf.inputs["Roughness"].default_value=.84;ground.data.materials.append(material)
    camera=bpy.data.cameras.new("CPU review camera");ob=bpy.data.objects.new("CPU review camera",camera);scene.collection.objects.link(ob);scene.camera=ob
    is_truck=args.model=="BoxTruck"
    ob.location=(8,-11,5.0) if is_truck else (5.3,-6.6,3.1)
    target=Vector((0,0,1.40 if is_truck else .76));ob.rotation_euler=(target-ob.location).to_track_quat("-Z","Y").to_euler();camera.lens=55
    scene.render.engine="CYCLES";scene.cycles.device="CPU";scene.cycles.samples=24;scene.cycles.use_denoising=True
    scene.render.threads_mode="FIXED";scene.render.threads=2;scene.render.resolution_x=960;scene.render.resolution_y=640;scene.render.resolution_percentage=100
    scene.view_settings.view_transform="AgX";scene.view_settings.look="AgX - Medium High Contrast"
    scene.render.image_settings.file_format="PNG";args.out.parent.mkdir(parents=True,exist_ok=True);scene.render.filepath=str(args.out)
    bpy.ops.render.render(write_still=True)


if __name__=="__main__":main()
