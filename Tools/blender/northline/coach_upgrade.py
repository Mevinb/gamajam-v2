"""Staged coach-only Blender authoring. Dry by default; no render path.

Uses the canonical Northline source, generates an upgraded coach and three
original trim maps in a fresh Logs output, copies the other ten FBXs unchanged.
No live Assets, canonical source, Unity, GPU/Cycles render or collision import.
"""
import argparse
import hashlib
import json
import math
import shutil
import sys
import time
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import coach_geometry as recipe
import coach_surfaces as surfaces

SOURCE_PINS = {
    "NorthlineRail.blend":"af01208fc8b41799bbfcd5fa3862cf197290cd8582bb22443de2c469b9cab69f",
    "production-manifest.json":"19ffc57f79c8a693339f863e1d90ce285625ec6215e9db8353f0c32237c3ed60",
}


def sha(path):
    h=hashlib.sha256()
    with Path(path).open("rb") as f:
        for part in iter(lambda:f.read(1024*1024),b""):
            h.update(part)
    return h.hexdigest()


def validate_inputs(project):
    root=project/"ArtSource/NorthlineRail"
    for name,pin in SOURCE_PINS.items():
        assert sha(root/name)==pin, "Canonical source differs: "+name
    manifest=json.loads((root/"production-manifest.json").read_text())
    assert len(manifest["exports"])==11 and manifest["distinct_triangles"]==7336
    for row in manifest["exports"]:
        pin=manifest["files"][row["file"]]["sha256"]
        assert sha(root/row["file"])==pin, row["name"]
    return root,manifest


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--project",type=Path,required=True)
    parser.add_argument("--out",type=Path,required=True)
    parser.add_argument("--go",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else sys.argv[1:])
    project=args.project.resolve()
    output=args.out.absolute()
    assert output==output.resolve() and output.is_relative_to(project/"Logs"), "Fresh real path under project Logs required"
    assert not output.exists(), "Existing output is never overwritten"
    root,baseline=validate_inputs(project)
    dry=recipe.plan()
    if not args.go:
        print(json.dumps({"status":"DRY_ONLY_NO_OUTPUT","plan":dry,"textures_generated":0,"Blender_required":False},indent=2))
        return
    # Root must first review/serialize this explicit authoring run. Importing bpy
    # occurs only after --go; the stdlib dry plan never launches Blender.
    import bpy
    import bmesh
    from mathutils import Matrix,Vector
    assert bpy.app.background and bpy.app.version[:2]==(4,5), "Reviewed Blender 4.5 background run required"
    started=time.monotonic()
    watched={str(p):sha(p) for p in root.rglob("*") if p.is_file()}
    assert shutil.disk_usage(project/"Logs").free>=512*1024*1024, "At least 512MiB free on output volume required"
    output.mkdir(parents=True)
    exports=output/"Exports";exports.mkdir()
    bpy.ops.wm.open_mainfile(filepath=str(root/"NorthlineRail.blend"),load_ui=False,use_scripts=False)
    scene=bpy.context.scene
    scene.render.threads_mode="FIXED";scene.render.threads=2
    scene.render.engine="CYCLES";scene.cycles.device="CPU"
    scene.cycles.use_denoising=False
    bpy.context.preferences.filepaths.save_version=0
    texture_paths=surfaces.write_atlas(output/"Textures")
    images=[]
    for i,path in enumerate(texture_paths):
        im=bpy.data.images.load(str(path),check_existing=False)
        im.colorspace_settings.name="sRGB" if i==0 else "Non-Color"
        im.pack();im.filepath="//Textures/"+path.name;images.append(im)
    opaque=bpy.data.materials.new("RailCoachOpaquePBR");opaque.use_nodes=True
    nodes=opaque.node_tree.nodes;links=opaque.node_tree.links
    principled=nodes.get("Principled BSDF")
    textures=[]
    for i,im in enumerate(images):
        n=nodes.new("ShaderNodeTexImage");n.image=im;n.interpolation="Linear";n.extension="EXTEND";textures.append(n)
    links.new(textures[0].outputs["Color"],principled.inputs["Base Color"])
    normal=nodes.new("ShaderNodeNormalMap");normal.inputs["Strength"].default_value=1
    links.new(textures[1].outputs["Color"],normal.inputs["Color"]);links.new(normal.outputs["Normal"],principled.inputs["Normal"])
    separate=nodes.new("ShaderNodeSeparateColor");separate.mode="RGB"
    links.new(textures[2].outputs["Color"],separate.inputs["Color"]);links.new(separate.outputs["Red"],principled.inputs["Metallic"])
    inverse=nodes.new("ShaderNodeMath");inverse.operation="SUBTRACT";inverse.inputs[0].default_value=1
    links.new(textures[2].outputs["Alpha"],inverse.inputs[1]);links.new(inverse.outputs[0],principled.inputs["Roughness"])
    glass=bpy.data.materials.new("RailCoachGlass");glass.use_nodes=True
    p=glass.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value=(.87,.94,.96,1)
    p.inputs["Metallic"].default_value=0;p.inputs["Roughness"].default_value=.08
    p.inputs["IOR"].default_value=1.5;p.inputs["Transmission Weight"].default_value=1
    # Closed 8/12mm panes use real transmission in Blender. Runtime URP alpha
    # transparency is a separately declared approximation, not ray refraction.
    data={"v":[],"f":[],"uv":[],"material":[],"smooth":[]};pieces=[]
    unity_to_blender=lambda q:Vector((q[0],-q[2],q[1]))

    class Geometry:
        def append(self,bm,name,centre,band,glass=False,smooth=False,uv_provider=None):
            bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
            if bm.calc_volume(signed=True)<0:
                bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
            volume=bm.calc_volume(signed=True)
            assert volume>1e-12 and all(len(e.link_faces)==2 for e in bm.edges),(name,volume)
            bm.verts.ensure_lookup_table();bm.faces.ensure_lookup_table();bm.normal_update()
            vertices={v:v.co+Vector(centre) for v in bm.verts}
            lo=[min(v[i] for v in vertices.values()) for i in range(3)]
            hi=[max(v[i] for v in vertices.values()) for i in range(3)]
            offset=len(data["v"]);indices={v:offset+i for i,v in enumerate(bm.verts)}
            data["v"].extend(tuple(unity_to_blender(vertices[v])) for v in bm.verts)
            for face in bm.faces:
                data["f"].append(tuple(indices[v] for v in face.verts))
                if uv_provider:
                    coords=uv_provider(face)
                else:
                    axis=max(range(3),key=lambda i:abs(face.normal[i]));axes=[i for i in range(3) if i!=axis]
                    u,v=sorted(axes,key=lambda i:hi[i]-lo[i],reverse=True)
                    density=min(128,2016/max(hi[u]-lo[u],1e-6),224/max(hi[v]-lo[v],1e-6))
                    coords=[((16+(vertices[q][u]-lo[u])*density)/2048,
                             (band*256+16+(vertices[q][v]-lo[v])*density)/2048) for q in face.verts]
                data["uv"].append(coords);data["material"].append(1 if glass else 0)
                data["smooth"].append(smooth and len(face.verts)==4)
            pieces.append({"group":"DepartureCoach","name":name,"band":surfaces.BANDS[band],"glass":glass,
                           "bounds_min":lo,"bounds_max":hi,"source_vertices":len(bm.verts),"outward_signed_volume":volume})
            bm.free()
        def box(self,name,centre,size,band,bevel=0,segments=1,glass=False):
            bm=bmesh.new();bmesh.ops.create_cube(bm,size=1)
            for v in bm.verts:
                v.co=Vector(tuple(v.co[i]*size[i] for i in range(3)))
            if bevel:
                bmesh.ops.bevel(bm,geom=list(bm.edges),offset=min(bevel,min(size)*.25),segments=segments,affect="EDGES")
            self.append(bm,name,centre,band,glass)
        def tube(self,name,start,end,radius,band,sides=8):
            bm=bmesh.new();direction=Vector(end)-Vector(start)
            bmesh.ops.create_cone(bm,cap_ends=True,cap_tris=False,segments=sides,radius1=radius,radius2=radius,depth=direction.length)
            rotation=Vector((0,0,1)).rotation_difference(direction.normalized())
            for v in bm.verts:v.co=rotation@v.co
            self.append(bm,name,(Vector(start)+Vector(end))/2,band,smooth=True)
        def wheel(self,name,centre,sides):
            profile=((- .11,.20),(-.09,.25),(-.055,.25),(.04,.245),(.085,.24),(.10,.21),(.10,.09),(-.11,.09))
            profile_v=(12,60,80,110,150,180,200,214)
            bm=bmesh.new();vertices={};uvs={};side=-1 if centre[0]<0 else 1
            for i in range(sides):
                angle=2*math.pi*i/sides
                for j,(x,radius) in enumerate(profile):
                    v=bm.verts.new((side*x,radius*math.cos(angle),radius*math.sin(angle)))
                    vertices[i,j]=v;uvs[v]=(i/sides,profile_v[j],j)
            for i in range(sides):
                for j in range(len(profile)):
                    bm.faces.new((vertices[i,j],vertices[(i+1)%sides,j],vertices[(i+1)%sides,(j+1)%len(profile)],vertices[i,(j+1)%len(profile)]))
            def coords(face):
                values=[uvs[v] for v in face.verts]
                seam=max(u for u,v,j in values)-min(u for u,v,j in values)>.5
                closing_profile={j for u,v,j in values}=={0,7}
                # The closing annular web stays in the oxidized strip instead
                # of interpolating back through the polished running tread.
                result=[]
                for u,v,j in values:
                    value=(214 if j==7 else 222) if closing_profile else v
                    result.append(((16+(u+(1 if seam and u<.5 else 0))*1.58*128)/2048,
                                   (3*256+16+value)/2048))
                return result
            self.append(bm,name,centre,3,smooth=True,uv_provider=coords)
        def spring(self,name,centre,radius,height,wire,turns,steps,sides):
            bm=bmesh.new();vertices={}
            for i in range(steps+1):
                angle=2*math.pi*turns*i/steps
                for j in range(sides):
                    theta=2*math.pi*j/sides;r=radius+wire*math.cos(theta)
                    vertices[i,j]=bm.verts.new((r*math.cos(angle),-height/2+height*i/steps+wire*math.sin(theta),r*math.sin(angle)))
            for i in range(steps):
                for j in range(sides):
                    bm.faces.new((vertices[i,j],vertices[i+1,j],vertices[i+1,(j+1)%sides],vertices[i,(j+1)%sides]))
            for i in (0,steps):bm.faces.new([vertices[i,j] for j in range(sides)])
            self.append(bm,name,centre,2,smooth=True)

    recipe.build(Geometry())
    mesh=bpy.data.meshes.new("RailDepartureCoach_v2");mesh.from_pydata(data["v"],[],data["f"]);mesh.update()
    mesh.materials.append(opaque);mesh.materials.append(glass)
    uv=mesh.uv_layers.new(name="CoachTrimPlanar70to128pxm")
    for face,coords,material,smooth in zip(mesh.polygons,data["uv"],data["material"],data["smooth"]):
        face.material_index=material;face.use_smooth=smooth
        for loop,coord in zip(face.loop_indices,coords):uv.data[loop].uv=coord
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.update();mesh.calc_loop_triangles()
    assert len(mesh.loop_triangles)<=recipe.TRIANGLE_CAP
    points=[(v.co.x,v.co.z,-v.co.y) for v in mesh.vertices]
    bounds=[[min(v[i] for v in points) for i in range(3)],[max(v[i] for v in points) for i in range(3)]]
    assert max(abs(bounds[e][i]-recipe.BOUNDS[e][i]) for e in range(2) for i in range(3))<.00001,bounds
    original=bpy.data.objects["RailDepartureCoach"]
    assert original.parent is None and all(abs(original.matrix_world[i][j]-(1 if i==j else 0))<1e-8 for i in range(4) for j in range(4)), "Original coach pivot/transform differs"
    old_mesh=original.data
    for ob in bpy.data.objects:
        if ob.type=="MESH" and ob.data==old_mesh:ob.data=mesh
    bpy.ops.object.select_all(action="DESELECT")
    temporary=original.copy();temporary.data=mesh.copy();scene.collection.objects.link(temporary)
    temporary.matrix_world=Matrix.Identity(4);temporary.data.transform(Matrix.Diagonal((1,-1,1,1)));temporary.data.flip_normals()
    temporary.select_set(True);bpy.context.view_layer.objects.active=temporary
    path=exports/"RailDepartureCoach.fbx"
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={"MESH"},apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",bake_space_transform=True,axis_forward="-Z",axis_up="Y",use_mesh_modifiers=True,
        add_leaf_bones=False,bake_anim=False,path_mode="STRIP",use_custom_props=True)
    bpy.data.objects.remove(temporary,do_unlink=True)
    records=[]
    for old in baseline["exports"]:
        row=dict(old)
        if old["name"]!="RailDepartureCoach":
            shutil.copyfile(root/old["file"],output/old["file"])
            assert sha(root/old["file"])==sha(output/old["file"])
        else:
            row.update(vertices=len(mesh.vertices),triangles=len(mesh.loop_triangles),bounds_unity=bounds,
                       closed_solids=len(pieces),material_slots=[opaque.name,glass.name])
            row.pop("material",None)
        records.append(row)
    old_pieces=json.loads((root/"piece-map.json").read_text())
    (output/"piece-map.json").write_text(json.dumps([p for p in old_pieces if p["group"]!="DepartureCoach"]+pieces,indent=2)+"\n")
    (output/"coach-seat-layout.json").write_text(json.dumps({"schema":1,"required_runtime_compound_seats":recipe.SEATS,
        "protected_aisle":recipe.PROTECTED_AISLE,"central_passage":recipe.CENTRAL_PASSAGE},indent=2)+"\n")
    bpy.ops.wm.save_as_mainfile(filepath=str(output/"NorthlineRail.blend"),compress=True)
    for file,pin in watched.items():assert sha(file)==pin,"Input source changed during authoring: "+file
    files={str(p.relative_to(output)):{"bytes":p.stat().st_size,"sha256":sha(p)} for p in output.rglob("*") if p.is_file()}
    manifest={"schema":2,"status":"SOURCE_ONLY_RUNTIME_AND_VISUAL_ACCEPTANCE_PENDING","baseline_manifest_sha256":SOURCE_PINS["production-manifest.json"],
              "author_script_sha256":sha(__file__),"source_atlas":baseline["source_atlas"],"exports":records,
              "distinct_triangles":sum(r["triangles"] for r in records),"new_textures":3,"new_imported_colliders":0,
              "required_runtime_seat_colliders":8,"coach_texture_contract":{"size":2048,"maps":["A:sRGB RGB/opaque A","N:linear tangent normal","M:linear R metallic/A smoothness"],"bands":surfaces.BANDS},
              "unchanged_fbx_count":10,"closed_solids":sum(r["closed_solids"] for r in records),"files":files,
              "blender":bpy.app.version_string,"renders":0,"elapsed_seconds":round(time.monotonic()-started,3)}
    assert manifest["distinct_triangles"]<=16828
    assert sum(row["bytes"] for row in files.values())<128*1024*1024, "Source output exceeded 128MiB budget"
    (output/"production-manifest.json").write_text(json.dumps(manifest,indent=2)+"\n")
    print("[NorthlineCoach] SOURCE AUTHORING COMPLETE; runtime/material/seat-collision/visual checks remain pending",manifest["distinct_triangles"])


if __name__=="__main__":
    main()
