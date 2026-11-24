"""CPU-only round-trip FBX checks: units, origin, material slots, finite mesh/UV data."""
import bpy, os, sys, json, math
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:];P=os.path.abspath(args[0]);stage=os.path.abspath(args[1]) if len(args)>1 else None
source=os.path.join(stage or P,'ArtSource/Riverside');out=os.path.join(stage,'Resources/Riverside') if stage else os.path.join(P,'Assets/Parasite/Resources/Riverside')
verification_path=os.path.join(source,'export_verification.json')
if os.path.exists(verification_path):os.unlink(verification_path)
manifest=json.load(open(os.path.join(source,'asset_manifest.json')));checks=[]
for asset in manifest['assets']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path=os.path.join(out,asset['asset']+'.fbx');bpy.ops.import_scene.fbx(filepath=path,use_image_search=False,use_anim=False)
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];bpy.context.view_layer.update()
    points=[o.matrix_world @ vertex.co for o in objects for vertex in o.data.vertices]
    lo=[min(p[i] for p in points) for i in range(3)];hi=[max(p[i] for p in points) for i in range(3)]
    # Actual Unity importer mapping was measured with asymmetric material bounds: X=BlenderX, Y=BlenderZ, Z=BlenderY.
    actual={'min':[lo[0],lo[2],lo[1]],'max':[hi[0],hi[2],hi[1]]};error=max(abs(actual[edge][i]-asset['unity_bounds_metres'][edge][i]) for edge in ['min','max'] for i in range(3))
    assert error<.002,(asset['asset'],'unit/origin bounds mismatch',actual,asset['unity_bounds_metres'])
    triangles=sum(sum(len(poly.vertices)-2 for poly in o.data.polygons) for o in objects)
    assert triangles==asset['triangles'],(asset['asset'],'triangle count changed',triangles,asset['triangles'])
    slots=sorted(set(m.name for o in objects for m in o.data.materials if m))
    assert slots==asset['materials'],(asset['asset'],'material slots changed',slots,asset['materials'])
    assert len(objects)==asset['meshes']
    for obj in objects:
        assert obj.data.uv_layers,(asset['asset'],obj.name,'missing UVs')
        assert all(math.isfinite(c) for vertex in obj.data.vertices for c in vertex.co)
        assert all(math.isfinite(c) for uv in obj.data.uv_layers.active.data for c in uv.uv)
        assert not obj.modifiers,(asset['asset'],'unapplied modifier')
        assert all(len(poly.vertices)==3 for poly in obj.data.polygons),(asset['asset'],'export must freeze its triangulation')
        obj.data.calc_loop_triangles()
        for triangle in obj.data.loop_triangles:
            a,b,c=[obj.data.vertices[i].co for i in triangle.vertices]
            assert (b-a).cross(c-a).length*.5>1e-9,(asset['asset'],obj.name,'zero-area triangle')
    # Reflection must correct winding too: each closed console housing must still face outward.
    if asset['asset'] in ['IndustrialConsole','RadioConsole','ClinicConsole']:
        expected='RiverClinicEnamel' if asset['asset']=='ClinicConsole' else 'RiverMarinePaint'
        housing=next(o for o in objects if any(material.name==expected for material in o.data.materials))
        center=sum((Vector(corner) for corner in housing.bound_box),Vector())/8
        assert all(poly.normal.dot(poly.center-center)>0 for poly in housing.data.polygons),(asset['asset'],'reflected housing has inward-facing winding')
    result={'asset':asset['asset'],'pass':True,'bounds_max_error_metres':error,'triangles':triangles,'materials':slots,'all_faces_triangulated':True,'zero_area_triangles':0};checks.append(result)
    print('[riverside-verify]',json.dumps(result),flush=True)
result={'verification':'CPU Blender FBX round-trip, no Unity or GPU rendering','assets':checks,'passed':len(checks),'total_triangles':sum(a['triangles'] for a in checks)}
with open(os.path.join(source,'export_verification.json'),'w') as f:json.dump(result,f,indent=2)
print('[riverside-verify] PASS',len(checks),'exports,',result['total_triangles'],'triangles',flush=True)
