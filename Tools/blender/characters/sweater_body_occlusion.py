"""Non-destructive garment occlusion for the two proven covered-torso skin patches.

Call only on the reviewed joined LakeGirl cinematic bind mesh, before subdivision.
Raw vertices/topology/UVs/weights/materials remain unchanged. This is not overpaint.
"""
import bpy,hashlib,array
from mathutils import Vector
from mathutils.bvhtree import BVHTree
GROUP='Sweater - covered torso skin occlusion'
MODIFIER='Sweater body occlusion - covered skin only'
CENTRES=((-0.095,-0.1355,1.165),(0.095,-0.1355,1.165))
RADIUS=.035
EXPECTED_VERTEX_POSITION_SHA='467aad218b861486e113204e60e48d46dbed067d99af5aa629afdac887d6d80a'

def apply(hero):
 mesh=hero.data
 assert mesh.shape_keys is None and len(mesh.vertices)==11769 and len(mesh.polygons)==19631
 a=array.array('f',[0])* (len(mesh.vertices)*3);mesh.vertices.foreach_get('co',a)
 assert hashlib.sha256(a.tobytes()).hexdigest()==EXPECTED_VERTEX_POSITION_SHA
 assert GROUP not in hero.vertex_groups and MODIFIER not in hero.modifiers
 skin={p.index for p in mesh.polygons if mesh.materials[p.material_index].name.startswith('Skin.')}
 used={v:set() for v in range(len(mesh.vertices))}
 for p in mesh.polygons:
  for v in p.vertices:used[v].add(p.material_index)
 centres=[Vector(v) for v in CENTRES]
 selected=[v.index for v in mesh.vertices if used[v.index]=={0} and min((v.co-c).length for c in centres)<RADIUS]
 assert 10<len(selected)<500
 # Every selected bind-space skin vertex projects into the existing sweater front.
 cloth=[list(p.vertices) for p in mesh.polygons if mesh.materials[p.material_index].name.startswith('Cloth.')]
 tree=BVHTree.FromPolygons([v.co.copy() for v in mesh.vertices],cloth)
 covered=[]
 for index in selected:
  point=mesh.vertices[index].co;hit,normal,face,distance=tree.ray_cast(point+Vector((0,-.3,0)),Vector((0,1,0)),.6)
  assert hit is not None and abs(hit.y-point.y)<.04,(index,'not covered by actual sweater',list(point),list(hit) if hit else None)
  covered.append({'vertex':index,'skin':list(point),'cloth':list(hit),'clothYMinusSkinY':hit.y-point.y})
 # A conventional inverted Mask retains all other vertices, including all cloth.
 group=hero.vertex_groups.new(name=GROUP);group.add(selected,1.0,'REPLACE')
 mod=hero.modifiers.new(MODIFIER,'MASK');mod.vertex_group=GROUP;mod.invert_vertex_group=True;mod.threshold=.5;mod.use_smooth=False
 with bpy.context.temp_override(object=hero,active_object=hero):bpy.ops.object.modifier_move_to_index(modifier=mod.name,index=0)
 assert hero.modifiers[0]==mod
 removed=[p.index for p in mesh.polygons if any(v in set(selected) for v in p.vertices)]
 assert set(removed)<=skin
 return {'method':'Inverted vertex-group Mask before original Armature/Subsurf; raw mesh unchanged','centresBindSpace':CENTRES,'radiusMetres':RADIUS,'selectedVertices':selected,'projectedCoveredVertices':covered,'maskedOriginalFaces':removed,'rawVertexChanges':[],'rawFaceChanges':[],'allOtherMaterialsExcludedFromMask':True}
