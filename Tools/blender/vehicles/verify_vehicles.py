"""Independent FBX round-trip checks; no Unity, renderer or gameplay acceptance."""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.dont_write_bytecode = True


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    parser=argparse.ArgumentParser();parser.add_argument("--source",type=Path,required=True);parser.add_argument("--out",type=Path,required=True)
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:]);source=args.source.resolve()
    manifest=json.loads((source/"production-manifest.json").read_text())
    for file,pin in manifest["files"].items():
        assert sha(source/file)==pin["sha256"],file
    assert len(manifest["models"])==6 and len(manifest["textures"])==3
    report=[]
    for model in manifest["models"]:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(source/model["file"]),use_image_search=False,use_anim=False)
        objects=list(bpy.context.scene.objects)
        assert all(o.type=="MESH" and o.animation_data is None and o.rigid_body is None for o in objects)
        totals={};opaque_v=[];opaque_f=[];glass_v=[];glass_f=[];roof_top=0;roof_lining=0
        bounds_points=[]
        for ob in objects:
            mesh=ob.data;mesh.calc_loop_triangles()
            lod=next(i for i in range(3) if "_LOD"+str(i) in ob.name)
            totals[lod]=totals.get(lod,0)+len(mesh.loop_triangles)
            assert mesh.uv_layers and len(mesh.uv_layers.active.data)==len(mesh.loops)
            assert all(m and m.name.split(".")[0] in manifest["materials"] for m in mesh.materials)
            points=[Vector((p.x,p.z,p.y)) for p in (ob.matrix_world@v.co for v in mesh.vertices)]
            for point in points:
                assert all(math.isfinite(v) for v in point)
            for tri in mesh.loop_triangles:
                a,b,c=[points[i] for i in tri.vertices]
                assert (b-a).cross(c-a).length > 1e-11,(model["name"],ob.name,tri.index,list(a),list(b),list(c))
                if lod==0 and "_Body_" in ob.name and model["name"]!="VehicleTaxiSign":
                    roof,rr,rf,half_width={"VehicleSedan":(1.45,-.98,.28,.925),"VehicleHatch":(1.49,-1.38,-.02,.88),
                        "VehicleSuv":(1.76,-1.66,.02,.95),"VehicleVan":(1.85,-2.36,.81,.925),"VehicleBoxTruck":(2.91,2.0,3.3,1.18)}[model["name"]]
                    centre=(a+b+c)/3;w=half_width-.23
                    if abs(centre.x)<w*.55 and rr+.15<centre.z<rf-.15:
                        expected=roof+.025*math.sin(math.pi*(centre.x/w+1)*.5)+.012*math.sin(math.pi*(centre.z-rr)/(rf-rr))
                        normal=(ob.matrix_world.to_3x3()@tri.normal).normalized()
                        material=mesh.materials[mesh.polygons[tri.polygon_index].material_index].name.split(".")[0]
                        if material=="VehiclePaint" and abs(centre.y-expected)<.001:
                            assert normal.z>.94,(model["name"],"exterior roof winding",list(normal));roof_top+=1
                        if material=="VehicleInterior" and abs(centre.y-(expected-.018))<.001:
                            assert normal.z<-.94,(model["name"],"interior headliner winding",list(normal));roof_lining+=1
            if lod==0:
                bounds_points.extend(points)
                vertices,faces=(glass_v,glass_f) if "_Glass_" in ob.name else (opaque_v,opaque_f)
                start=len(vertices);vertices.extend(points);faces.extend(tuple(start+i for i in tri.vertices) for tri in mesh.loop_triangles)
        assert totals=={row["lod"]:row["triangles"] for row in model["lods"]},(model["name"],totals)
        if len(totals)==3:
            assert totals[0]>totals[1]>totals[2] and totals[2]<5500
            assert min(roof_top,roof_lining)>=50,(model["name"],"closed roof shell missing",roof_top,roof_lining)
            wheels=[o for o in objects if "_Wheel_" in o.name]
            assert len(wheels)==len(model["wheel_centres"])*3
        actual=[[min(p[i] for p in bounds_points) for i in range(3)],[max(p[i] for p in bounds_points) for i in range(3)]]
        assert max(abs(actual[e][i]-model["bounds_unity"][e][i]) for e in range(2) for i in range(3))<.0003,(model["name"],actual,model["bounds_unity"])
        probes=[];seal_probes=[]
        # Probe only the side panes. Short rays stop before seats/dashboard; a
        # dark painted solid behind a fake window fails the opaque-clear check.
        if model["name"]!="VehicleTaxiSign":
            data={"VehicleSedan":(.925,1.16,.17),"VehicleHatch":(.88,1.19,.14),"VehicleSuv":(.95,1.40,.16),
                  "VehicleVan":(.925,1.45,1.03),"VehicleBoxTruck":(1.18,2.43,3.47)}[model["name"]]
            hw,y,z=data;opaque=BVHTree.FromPolygons(opaque_v,opaque_f,all_triangles=True);glass=BVHTree.FromPolygons(glass_v,glass_f,all_triangles=True)
            for side in (-1,1):
                origin=Vector((side*(hw+.04),y,z));direction=Vector((-side,0,0));length=.25
                assert glass.ray_cast(origin,direction,length)[0] is not None,(model["name"],"missing pane",side)
                assert opaque.ray_cast(origin,direction,length)[0] is None,(model["name"],"opaque window backing",side)
                probes.append(dict(origin=list(origin),direction=list(direction),distance=length))
            belt,roof,upper_z={"VehicleSedan":(.91,1.45,-.58),"VehicleHatch":(.94,1.49,-1.04),"VehicleSuv":(1.10,1.76,-1.24),
                               "VehicleVan":(1.08,1.85,-1.57),"VehicleBoxTruck":(1.97,2.91,2.325)}[model["name"]]
            for side in (-1,1):
                for height,depth in ((belt+.017,z),(roof-.009,upper_z)):
                    origin=Vector((side*(hw+.04),height,depth));direction=Vector((-side,0,0));length=.40
                    assert opaque.ray_cast(origin,direction,length)[0] is not None,(model["name"],"unbacked weather-seal gap",side,height,depth)
                    seal_probes.append(dict(origin=list(origin),direction=list(direction),distance=length))
            if model["name"]=="VehicleVan":
                for height in (belt+.006,roof+.015):
                    origin=Vector((0,height,-2.50));direction=Vector((0,0,1));length=.40
                    assert opaque.ray_cast(origin,direction,length)[0] is not None,(model["name"],"unbacked rear weather seal",height)
                    seal_probes.append(dict(origin=list(origin),direction=list(direction),distance=length))
        report.append(dict(name=model["name"],lod_triangles=totals,bounds_unity=actual,window_aperture_probes=probes,opaque_weather_seal_probes=seal_probes,
                           exterior_roof_up_triangles=roof_top,interior_headliner_down_triangles=roof_lining,roof_shell_thickness_metres=.018 if roof_top else 0))
    for texture in manifest["textures"]:
        image=bpy.data.images.load(str(source/texture["file"]),check_existing=False)
        assert tuple(image.size)==(2048,2048) and image.channels==4
    result=dict(status="CPU_FBX_ROUNDTRIP_GEOMETRY_PASS",models=report,shared_maps=3,size=2048,renders=0,
                source_manifest_sha256=sha(source/"production-manifest.json"),
                limits=["No Unity import/shader/appearance/FPS, navigation or native gameplay result is claimed."])
    args.out.parent.mkdir(parents=True,exist_ok=True);args.out.write_text(json.dumps(result,indent=2)+"\n")
    print(json.dumps({"status":result["status"],"models":len(report),"aperture_probes":sum(len(r["window_aperture_probes"]) for r in report),"opaque_seal_probes":sum(len(r["opaque_weather_seal_probes"]) for r in report)}))


if __name__=="__main__":main()
