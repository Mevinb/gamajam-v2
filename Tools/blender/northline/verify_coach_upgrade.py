"""Independent CPU geometry/slot/aperture checks for a reviewed coach output.

No render path. This verifier rejects the old source schema rather than relaxing
the original eleven-model verifier. Run later, only when Root authorizes Blender.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

sys.dont_write_bytecode = True


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    import bpy
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    ap=argparse.ArgumentParser();ap.add_argument("--project",type=Path,required=True)
    ap.add_argument("--source",type=Path,required=True);ap.add_argument("--out",type=Path,required=True)
    args=ap.parse_args(sys.argv[sys.argv.index("--")+1:])
    project=args.project.resolve();source=args.source.resolve();output=args.out.absolute()
    assert source.is_relative_to(project/"Logs") and source.is_dir()
    assert output==output.resolve() and output.is_relative_to(project/"Logs") and not output.exists()
    baseline=project/"ArtSource/NorthlineRail"
    manifest=json.loads((source/"production-manifest.json").read_text())
    assert manifest["schema"]==2 and manifest["new_textures"]==3 and manifest["unchanged_fbx_count"]==10
    assert manifest["renders"]==0 and manifest["new_imported_colliders"]==0 and manifest["required_runtime_seat_colliders"]==8
    assert sha(baseline/"production-manifest.json")==manifest["baseline_manifest_sha256"]
    original=json.loads((baseline/"production-manifest.json").read_text())
    for file,pin in manifest["files"].items():assert sha(source/file)==pin["sha256"],file
    unchanged=[]
    for row in original["exports"]:
        if row["name"]!="RailDepartureCoach":
            assert sha(source/row["file"])==sha(baseline/row["file"]),row["name"]
            unchanged.append(row["name"])
    assert len(unchanged)==10
    coach=next(r for r in manifest["exports"] if r["name"]=="RailDepartureCoach")
    assert 2508<coach["triangles"]<=12000 and manifest["distinct_triangles"]<=16828
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.threads_mode="FIXED";bpy.context.scene.render.threads=2
    bpy.ops.import_scene.fbx(filepath=str(source/coach["file"]),use_image_search=False,use_anim=False)
    objects=list(bpy.context.scene.objects)
    assert len(objects)==1 and objects[0].type=="MESH" and objects[0].animation_data is None and objects[0].rigid_body is None
    ob=objects[0];mesh=ob.data;mesh.calc_loop_triangles()
    assert not ob.modifiers and len(mesh.vertices)==coach["vertices"] and len(mesh.loop_triangles)==coach["triangles"]
    assert [m.name for m in mesh.materials]==["RailCoachOpaquePBR","RailCoachGlass"]
    raw=[v.co.copy() for v in mesh.vertices]
    points=[Vector((v.x,v.z,v.y)) for v in (ob.matrix_world@v for v in raw)]
    actual=[[min(p[i] for p in points) for i in range(3)],[max(p[i] for p in points) for i in range(3)]]
    expected=((-1.602,.15,-7.0095),(1.602,3.29,7.0095))
    assert max(abs(actual[e][i]-expected[e][i]) for e in range(2) for i in range(3))<.0002
    edges={};neighbors={i:set() for i in range(len(raw))};opaque=[];glass=[];minimum_normal=1
    assert mesh.uv_layers and len(mesh.uv_layers.active.data)==len(mesh.loops)
    for tri in mesh.loop_triangles:
        indices=tuple(tri.vertices);a,b,c=[raw[i] for i in indices]
        assert (b-a).cross(c-a).length*.5>1e-11
        uv=[mesh.uv_layers.active.data[i].uv for i in tri.loops]
        assert abs((uv[1]-uv[0]).cross(uv[2]-uv[0]))*.5>1e-13
        assert all(math.isfinite(x) and 0<=x<=1 for p in uv for x in p)
        assert len({min(7,int(p.y*8)) for p in uv})==1,"A triangle may not bleed across surface trim bands"
        for loop in tri.loops:
            normal=mesh.corner_normals[loop].vector
            assert all(math.isfinite(x) for x in normal) and abs(normal.length-1)<.001
            minimum_normal=min(minimum_normal,normal.dot(tri.normal))
        slot=mesh.polygons[tri.polygon_index].material_index
        assert slot in (0,1)
        (glass if slot else opaque).append(indices)
        for i,j in zip(indices,(*indices[1:],indices[0])):
            edges.setdefault(tuple(sorted((i,j))),[]).append((i,j));neighbors[i].add(j);neighbors[j].add(i)
    assert minimum_normal>.1
    assert len(glass)==120 and len(opaque)==coach["triangles"]-120
    assert all(len(e)==2 and e[0]==tuple(reversed(e[1])) for e in edges.values())
    unseen=set(neighbors);components=[];volumes=[];glass_components=0
    while unseen:
        seed=unseen.pop();component={seed};pending=[seed]
        while pending:
            for neighbor in neighbors[pending.pop()]:
                if neighbor in unseen:unseen.remove(neighbor);component.add(neighbor);pending.append(neighbor)
        components.append(component)
        centre=sum((raw[i] for i in component),Vector())/len(component)
        triangles=[t for t in mesh.loop_triangles if t.vertices[0] in component]
        volume=sum((raw[t.vertices[0]]-centre).dot((raw[t.vertices[1]]-centre).cross(raw[t.vertices[2]]-centre)) for t in triangles)/6
        assert volume>1e-12;volumes.append(volume)
        slots={mesh.polygons[t.polygon_index].material_index for t in triangles}
        assert len(slots)==1,"Manufactured glass and opaque solids must not share vertices"
        if slots=={1}:
            glass_components+=1;assert len(component)==8 and len(triangles)==12
    assert len(components)==coach["closed_solids"] and glass_components==10
    # Real rays test the visual aperture, not a caption/material name or an old
    # black panel. At these probes there must be glass and no opaque backing.
    opaque_tree=BVHTree.FromPolygons(points,opaque,all_triangles=True)
    glass_tree=BVHTree.FromPolygons(points,glass,all_triangles=True)
    probes=[]
    for side in (-1,1):
        for z in (-5.6,-3.6,3.6,5.6):probes.append((Vector((side*1.72,2.17,z)),Vector((-side,0,0)),.40))
        probes.append((Vector((0,2.12,side*7.2)),Vector((0,0,-side)),.55))
    for start,direction,length in probes:
        assert opaque_tree.ray_cast(start,direction,length)[0] is None,"Opaque panel still backs a supposed window"
        assert glass_tree.ray_cast(start,direction,length)[0] is not None,"A real thin glass pane is required"
    layout=json.loads((source/"coach-seat-layout.json").read_text())
    seats=layout["required_runtime_compound_seats"];assert len(seats)==8
    for seat in seats:
        assert tuple(seat["size"])==(.62,1.09,1.44) and abs(seat["centre"][0])==1.09
        assert seat["centre"][2] in (-5.6,-3.6,3.6,5.6)
        assert abs(seat["centre"][2])-seat["size"][2]/2>1.85 and abs(seat["centre"][0])-seat["size"][0]/2>.75
    for suffix in ("A","N","M"):
        image=bpy.data.images.load(str(source/"Textures"/("RailCoach_"+suffix+".png")),check_existing=False)
        assert tuple(image.size)==(2048,2048) and image.channels==4
    imported_triangle_count=len(mesh.loop_triangles)
    # FBX transmission portability is deliberately not assumed. Inspect the
    # packed source shader itself; runtime URP materials have their own checks.
    bpy.ops.wm.open_mainfile(filepath=str(source/"NorthlineRail.blend"),load_ui=False,use_scripts=False)
    source_coach=bpy.data.objects["RailDepartureCoach"]
    assert [m.name for m in source_coach.data.materials]==["RailCoachOpaquePBR","RailCoachGlass"]
    shader=source_coach.data.materials[1].node_tree.nodes.get("Principled BSDF")
    assert shader and shader.inputs["Transmission Weight"].default_value==1 and shader.inputs["Alpha"].default_value==1
    assert abs(shader.inputs["IOR"].default_value-1.5)<.001 and abs(shader.inputs["Roughness"].default_value-.08)<.001
    packed=[]
    for node in source_coach.data.materials[0].node_tree.nodes:
        if node.type=="TEX_IMAGE" and node.image:
            im=node.image;assert im.packed_file
            suffix=Path(im.filepath).stem.rsplit("_",1)[1]
            assert suffix in ("A","N","M")
            assert hashlib.sha256(bytes(im.packed_file.data)).hexdigest()==sha(source/"Textures"/("RailCoach_"+suffix+".png"))
            assert im.colorspace_settings.name==("sRGB" if suffix=="A" else "Non-Color")
            packed.append(suffix)
    assert sorted(packed)==["A","M","N"]
    output.mkdir(parents=True)
    report={"status":"CPU_GEOMETRY_ONLY_PASS","source_manifest_sha256":sha(source/"production-manifest.json"),
            "unchanged_exports":unchanged,"triangles":imported_triangle_count,"glass_triangles":len(glass),
            "closed_components":len(components),"glass_closed_components":glass_components,"minimum_signed_volume":min(volumes),
            "minimum_corner_normal_dot":minimum_normal,"actual_bounds":actual,"aperture_probes":len(probes),"renders":0,
            "limits":["No Unity import/material/GPU/in-game appearance/physics/nav/LOS/seat-input result.","Runtime eight-seat collider/layout proposal remains separately reviewed and unrun."]}
    (output/"coach-verification.json").write_text(json.dumps(report,indent=2)+"\n")
    print(json.dumps(report,indent=2))


if __name__=="__main__":main()
