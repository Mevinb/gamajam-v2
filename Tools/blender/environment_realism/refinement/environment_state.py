"""Compact complete-environment semantic snapshot; no I/O or mutation."""
import bpy,hashlib,json

def digest(value):return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':')).encode()).hexdigest()

def numeric(value):
    if isinstance(value,(str,int,float,bool)) or value is None:return value
    if isinstance(value,bpy.types.ID):return {'idType':value.bl_rna.identifier,'name':value.name}
    try:return list(value)
    except TypeError:return str(value)

def mesh_state(mesh):
    mesh.calc_loop_triangles()
    uvnames={layer.name for layer in mesh.uv_layers}
    attributes=[]
    for a in mesh.attributes:
        if a.name in uvnames:continue
        prop={'FLOAT':'value','INT':'value','INT8':'value','INT32_2D':'value','INT32_4D':'value','STRING':'value','BOOLEAN':'value','FLOAT_VECTOR':'vector','FLOAT2':'vector','FLOAT_COLOR':'color','BYTE_COLOR':'color'}.get(a.data_type)
        assert prop is not None,(a.name,a.data_type)
        attributes.append((a.name,a.data_type,a.domain,[numeric(getattr(v,prop)) for v in a.data]))
    return {'name':mesh.name,'positions':[list(v.co) for v in mesh.vertices],'vertexNormals':[list(v.normal) for v in mesh.vertices],'edges':[list(e.vertices) for e in mesh.edges],'faces':[(list(p.vertices),p.material_index,p.use_smooth,list(p.normal)) for p in mesh.polygons],'loops':[(l.vertex_index,l.edge_index) for l in mesh.loops],'cornerNormals':[list(n.vector) for n in mesh.corner_normals],'loopTriangles':[(list(t.vertices),t.polygon_index) for t in mesh.loop_triangles],'materialSlots':[m.name if m else None for m in mesh.materials],'attributesExceptUV':attributes,'uv':{u.name:[list(d.uv) for d in u.data] for u in mesh.uv_layers},'activeUV':mesh.uv_layers.active_index,'uvFlags':[(u.name,u.active_render,u.active_clone) for u in mesh.uv_layers]}

def material_state(m):
    result={'name':m.name,'fake':m.use_fake_user,'nodes':m.use_nodes,'diffuse':list(m.diffuse_color),'metallic':m.metallic,'roughness':m.roughness}
    if m.use_nodes:
        result['nodes']=[{'name':n.name,'type':n.bl_idname,'image':n.image.name if hasattr(n,'image') and n.image else None,'inputs':[(i.identifier,numeric(i.default_value)) for i in n.inputs if hasattr(i,'default_value')],'operation':getattr(n,'operation',None),'blend':getattr(n,'blend_type',None),'interpolation':getattr(n,'interpolation',None),'extension':getattr(n,'extension',None),'projection':getattr(n,'projection',None)} for n in m.node_tree.nodes]
        result['links']=[(l.from_node.name,l.from_socket.identifier,l.to_node.name,l.to_socket.identifier) for l in m.node_tree.links]
    return result

def snapshot():
    return {'objects':{o.name:{'type':o.type,'data':o.data.name if o.data else None,'parent':o.parent.name if o.parent else None,'matrix':[list(r) for r in o.matrix_world],'collections':[c.name for c in o.users_collection],'hideRender':o.hide_render,'hideViewport':o.hide_viewport,'hideSet':o.hide_get(),'modifiers':[(m.name,m.type) for m in o.modifiers]} for o in bpy.data.objects},'meshes':{m.name:mesh_state(m) for m in bpy.data.meshes},'materials':{m.name:material_state(m) for m in bpy.data.materials},'images':{im.name:{'packedSha256':hashlib.sha256(bytes(im.packed_file.data)).hexdigest() if im.packed_file else None,'size':list(im.size),'colorspace':im.colorspace_settings.name,'alphaMode':im.alpha_mode,'source':im.source,'format':im.file_format} for im in bpy.data.images},'collections':{c.name:{'objects':[o.name for o in c.objects],'children':[x.name for x in c.children],'hideRender':c.hide_render,'hideViewport':c.hide_viewport,'hideSelect':c.hide_select} for c in bpy.data.collections},'sceneNames':[s.name for s in bpy.data.scenes],'unitSettings':[(s.name,s.unit_settings.system,s.unit_settings.scale_length) for s in bpy.data.scenes]}
