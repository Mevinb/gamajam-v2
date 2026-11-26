"""Dielectric paint/concrete/glass, scanned metre-scale depot surfaces and real metal."""
import bpy


def plain(name,colour,roughness=.6,metallic=0,coat=0):
    m=bpy.data.materials.new(name);m.use_nodes=True;b=m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value=(*colour,1);b.inputs['Roughness'].default_value=roughness;b.inputs['Metallic'].default_value=metallic
    b.inputs['Coat Weight'].default_value=coat;b.inputs['Coat Roughness'].default_value=.24
    return m


def scan(root,name,folder,prefix,metres,normal_strength=.5):
    m=plain(name,(.25,.25,.25));nt=m.node_tree;bs=nt.nodes.get('Principled BSDF')
    uv=nt.nodes.new('ShaderNodeUVMap');uv.uv_map='MetricUV'
    scale=nt.nodes.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs['Scale'].default_value=1/metres;nt.links.new(uv.outputs[0],scale.inputs[0])
    def tex(kind,data=False):
        path=next((root/folder/(prefix+'_'+kind+ext) for ext in ('.jpg','.png') if (root/folder/(prefix+'_'+kind+ext)).is_file()),None)
        if path is None:return None
        tx=nt.nodes.new('ShaderNodeTexImage');tx.image=bpy.data.images.load(str(path),check_existing=True);tx.image.colorspace_settings.name='Non-Color' if data else 'sRGB';nt.links.new(scale.outputs[0],tx.inputs[0]);return tx
    al=tex('albedo');nt.links.new(al.outputs[0],bs.inputs['Base Color'])
    normal=tex('normal',True)
    if normal:
        n=nt.nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=normal_strength;nt.links.new(normal.outputs[0],n.inputs['Color']);nt.links.new(n.outputs[0],bs.inputs['Normal'])
    rough=tex('rough',True)
    if rough:nt.links.new(rough.outputs[0],bs.inputs['Roughness'])
    else:
        mask=tex('mask',True)
        if mask:
            inv=nt.nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1;nt.links.new(mask.outputs['Alpha'],inv.inputs[1]);nt.links.new(inv.outputs[0],bs.inputs['Roughness'])
    return m


def weathered(name,base,rough=.6,metal=0):
    m=plain(name,base,rough,metal);nt=m.node_tree;bs=nt.nodes.get('Principled BSDF')
    geo=nt.nodes.new('ShaderNodeTexCoord');noise=nt.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=45;noise.inputs['Detail'].default_value=3
    nt.links.new(geo.outputs['Object'],noise.inputs[0])
    ramp=nt.nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(*[v*.70 for v in base],1);ramp.color_ramp.elements[1].color=(*[min(1,v*1.18) for v in base],1)
    nt.links.new(noise.outputs['Fac'],ramp.inputs[0]);nt.links.new(ramp.outputs[0],bs.inputs['Base Color'])
    bump=nt.nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.0004;bump.inputs['Strength'].default_value=.28;nt.links.new(noise.outputs['Fac'],bump.inputs['Height']);nt.links.new(bump.outputs[0],bs.inputs['Normal'])
    return m


def make(root):
    result={
        'concrete':scan(root,'Concrete aggregate and real formwork scale','Assets/Parasite/Art/Lab/concrete','concrete',2.0),
        # The image contains 20 courses: 1.5 m / 20 = a plausible 75 mm brick course.
        'brick':scan(root,'Scanned depot masonry - 75 mm courses','Assets/Parasite/Art/City/brick','brick',1.5,.45),
        'ballast':scan(root,'Photographed ballast under actual angular stone','Assets/Parasite/Art/City/Ground','Gravel',1.6,.65),
        'asphalt':scan(root,'Scanned damp service road','Assets/Parasite/Art/City/Ground','Road',6.,.6),
        'wood':scan(root,'Weathered timber pallets','Assets/Parasite/Art/City/wood','wood',1.2,.45),
        'steel':weathered('Brushed running steel',(.39,.41,.42),.24,1),
        'rust':weathered('Oxidised rail web and fasteners',(.115,.064,.035),.75,.52),
        'dark':weathered('Powder coated bogie steel',(.028,.037,.042),.55,0),
        'rubber':plain('Rubber pads and bump stops',(.011,.013,.014),.87),
        'paint':weathered('Depot painted structural steel',(.15,.20,.19),.53,0),
        'roof':weathered('Galvanised corrugated roof',(.24,.26,.25),.56,.78),
        'yellow':weathered('Faded safety ochre',(.60,.34,.065),.72,0),
        'ceramic':plain('Glazed ceramic insulators',(.31,.23,.16),.19),
        'copper':plain('Contact copper and bronze',(.32,.15,.07),.28,1),
    }
    for i,c in enumerate(((.18,.18,.17),(.25,.23,.21),(.125,.14,.15),(.29,.28,.25))):result['stone'+str(i)]=weathered('Angular granite ballast '+str(i),c,.86)
    # Broad vertical rain/grime variation is separate from the physically scaled scan.
    brick=result['brick'];nt=brick.node_tree;bs=nt.nodes.get('Principled BSDF');old=bs.inputs['Base Color'].links[0].from_socket
    uv=nt.nodes.new('ShaderNodeUVMap');uv.uv_map='MetricUV';stretch=nt.nodes.new('ShaderNodeVectorMath');stretch.operation='MULTIPLY';stretch.inputs[1].default_value=(1.8,.13,1)
    nt.links.new(uv.outputs[0],stretch.inputs[0]);noise=nt.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=1;noise.inputs['Detail'].default_value=4;nt.links.new(stretch.outputs[0],noise.inputs[0])
    ramp=nt.nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.35,.38,.34,1);ramp.color_ramp.elements[1].color=(.82,.81,.74,1);nt.links.new(noise.outputs['Fac'],ramp.inputs[0])
    mul=nt.nodes.new('ShaderNodeMixRGB');mul.blend_type='MULTIPLY';mul.inputs[0].default_value=.7;nt.links.new(old,mul.inputs[1]);nt.links.new(ramp.outputs[0],mul.inputs[2]);nt.links.new(mul.outputs[0],bs.inputs['Base Color'])
    bs.inputs['Roughness'].default_value=.86
    glass=plain('Real closed dielectric depot glass',(.98,.99,1),.006);bs=glass.node_tree.nodes.get('Principled BSDF');bs.inputs['Transmission Weight'].default_value=1;bs.inputs['IOR'].default_value=1.5;result['glass']=glass
    lamp=plain('Warm fixture diffuser',(.93,.72,.41),.4);bs=lamp.node_tree.nodes.get('Principled BSDF');bs.inputs['Emission Color'].default_value=(1,.66,.30,1);bs.inputs['Emission Strength'].default_value=3.;result['lamp']=lamp
    return result
