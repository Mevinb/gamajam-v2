"""Metre-scale shore dressing for the realistic lake films (Blender only)."""
import math
import random
import bpy
from mathutils import Quaternion, Vector


def surface(name, colour, roughness=0.7):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    bs = material.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*colour, 1)
    bs.inputs['Roughness'].default_value = roughness
    return material


def texture(nt, path, data=False):
    node = nt.nodes.new('ShaderNodeTexImage')
    node.image = bpy.data.images.load(str(path), check_existing=True)
    node.image.colorspace_settings.name = 'Non-Color' if data else 'sRGB'
    return node


def mathnode(nt, op, a, b):
    node = nt.nodes.new('ShaderNodeMath'); node.operation = op
    for i, value in enumerate((a, b)):
        if isinstance(value, (int, float)): node.inputs[i].default_value = value
        else: nt.links.new(value, node.inputs[i])
    return node.outputs[0]


def shore(x):
    return -4.35 - 0.48*math.sin(x*0.19) - 0.22*math.sin(x*0.63)


def pathcentre(y):
    return 2.0 + 1.5*math.sin(y*0.14)


def far_rim(x):
    return -133+7*math.sin(x*.018)+3*math.sin(x*.043)


def far_height(x, y):
    """A continuous shore, wooded foothills and three deeper irregular ridges."""
    d=far_rim(x)-y
    if d<0:return -.12-min(3.0,-d*.17)
    rise=1-math.exp(-d/38)
    slope=d*(.047+.025*math.sin(x*.009+.7)**2)
    folds=rise*(4.5+4*math.sin(x*.018+d*.031)+2.3*math.sin(x*.047-d*.019))
    ridge=12*math.exp(-((d-180-28*math.sin(x*.013))/90)**2)
    return max(-.02,slope+folds+ridge)


def height(x, y):
    d = y-shore(x)
    if d < 0:
        return -0.055-min(1.7, -d*0.20)+0.015*math.sin(x*1.9)*math.sin(y*2.1)
    undulation = 0.17*math.sin(x*0.24+y*0.17)+0.075*math.sin(x*0.67-y*0.41)
    anchor = 1-math.exp(-((x+1.7)**2+(y+0.05)**2)/14)
    return max(-0.008, (0.028+undulation)*anchor+max(0, d-12)*0.008)


def meshobject(name, vertices, faces, material):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces); data.update()
    ob = bpy.data.objects.new(name, data); bpy.context.scene.collection.objects.link(ob)
    if material: data.materials.append(material)
    for p in data.polygons: p.use_smooth = True
    return ob


def metric_uv(ob, name='UVMap'):
    uv = ob.data.uv_layers.get(name) or ob.data.uv_layers.new(name=name)
    for poly in ob.data.polygons:
        n = poly.normal
        axis = max(range(3), key=lambda i: abs(n[i]))
        for loopindex in poly.loop_indices:
            p = ob.matrix_world @ ob.data.vertices[ob.data.loops[loopindex].vertex_index].co
            uv.data[loopindex].uv = (p.y, p.z) if axis == 0 else ((p.x, p.z) if axis == 1 else (p.x, p.y))
    ob.data.uv_layers.active = uv
    return uv


def wood(root):
    mat = surface('Photographed weathered timber - one metre UV', (0.16, 0.12, 0.085))
    nt = mat.node_tree; bs = nt.nodes.get('Principled BSDF'); folder = root/'Assets/Parasite/Art/City/wood'
    uv = nt.nodes.new('ShaderNodeUVMap'); uv.uv_map = 'RealismMetricWoodUV'
    for filename, socket, data in [('wood_albedo.jpg','Base Color',False),('wood_rough.jpg','Roughness',True)]:
        tx = texture(nt, folder/filename, data); nt.links.new(uv.outputs[0], tx.inputs[0]); nt.links.new(tx.outputs[0], bs.inputs[socket])
    tx = texture(nt, folder/'wood_normal.jpg', True); nt.links.new(uv.outputs[0], tx.inputs[0])
    normal = nt.nodes.new('ShaderNodeNormalMap'); normal.inputs['Strength'].default_value = 0.26
    nt.links.new(tx.outputs[0], normal.inputs['Color']); nt.links.new(normal.outputs[0], bs.inputs['Normal'])
    for ob in bpy.data.objects:
        if ob.type != 'MESH' or not ob.name.startswith('CinemaTimber'): continue
        ob.data.materials.clear(); ob.data.materials.append(mat)
        for p in ob.data.polygons: p.material_index = 0
        metric_uv(ob, 'RealismMetricWoodUV')
        bevel = ob.modifiers.new('Timber edge wear 1.5 mm', 'BEVEL'); bevel.width = 0.0015; bevel.segments = 3
        bevel.limit_method = 'ANGLE'; bevel.angle_limit = 0.55
    return mat


def terrain_material(root):
    m = surface('Shore - photographed gravel and living ground', (0.10,0.12,0.055), .9)
    nt=m.node_tree; bs=nt.nodes.get('Principled BSDF')
    tx=texture(nt, root/'Assets/Parasite/Art/City/Ground/Gravel_albedo.jpg')
    uv=nt.nodes.new('ShaderNodeUVMap'); uv.uv_map='UVMap'; nt.links.new(uv.outputs[0],tx.inputs[0])
    attr=nt.nodes.new('ShaderNodeAttribute');attr.attribute_name='ground_response'
    channels=nt.nodes.new('ShaderNodeSeparateColor');nt.links.new(attr.outputs['Color'],channels.inputs[0])
    coords=nt.nodes.new('ShaderNodeTexCoord');noise=nt.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=3.2;noise.inputs['Detail'].default_value=4
    nt.links.new(coords.outputs['Object'],noise.inputs['Vector'])
    ramp=nt.nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.040,.044,.021,1);ramp.color_ramp.elements[1].color=(.115,.133,.047,1)
    nt.links.new(noise.outputs['Fac'],ramp.inputs[0])
    # Living ground retains actual photographed mineral/fibre-sized detail;
    # it is no longer a flat green noise swatch beneath sparse tall blades.
    earth=nt.nodes.new('ShaderNodeMixRGB');earth.blend_type='MULTIPLY';earth.inputs[0].default_value=1;earth.inputs[2].default_value=(.34,.28,.16,1);nt.links.new(tx.outputs[0],earth.inputs[1])
    living=nt.nodes.new('ShaderNodeMixRGB');living.blend_type='MULTIPLY';living.inputs[0].default_value=.72;nt.links.new(ramp.outputs[0],living.inputs[1]);nt.links.new(tx.outputs[0],living.inputs[2])
    patches=nt.nodes.new('ShaderNodeTexNoise');patches.inputs['Scale'].default_value=.72;patches.inputs['Detail'].default_value=3;nt.links.new(coords.outputs['Object'],patches.inputs[0])
    patchmix=nt.nodes.new('ShaderNodeMixRGB');nt.links.new(patches.outputs['Fac'],patchmix.inputs[0]);nt.links.new(earth.outputs[0],patchmix.inputs[1]);nt.links.new(living.outputs[0],patchmix.inputs[2])
    mix=nt.nodes.new('ShaderNodeMixRGB');nt.links.new(channels.outputs['Red'],mix.inputs[0]);nt.links.new(tx.outputs[0],mix.inputs[1]);nt.links.new(patchmix.outputs[0],mix.inputs[2])
    wet=nt.nodes.new('ShaderNodeMixRGB');wet.blend_type='MULTIPLY';wet.inputs[2].default_value=(.42,.47,.49,1)
    nt.links.new(channels.outputs['Green'],wet.inputs[0]);nt.links.new(mix.outputs[0],wet.inputs[1]);nt.links.new(wet.outputs[0],bs.inputs['Base Color'])
    normaltex=texture(nt,root/'Assets/Parasite/Art/City/Ground/Gravel_normal.jpg',True);nt.links.new(uv.outputs[0],normaltex.inputs[0])
    normal=nt.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.42;nt.links.new(normaltex.outputs[0],normal.inputs['Color'])
    micro=nt.nodes.new('ShaderNodeTexNoise');micro.inputs['Scale'].default_value=58;micro.inputs['Detail'].default_value=2;nt.links.new(coords.outputs['Object'],micro.inputs[0])
    bump=nt.nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.003;bump.inputs['Strength'].default_value=.16;nt.links.new(micro.outputs['Fac'],bump.inputs['Height']);nt.links.new(normal.outputs[0],bump.inputs['Normal']);nt.links.new(bump.outputs[0],bs.inputs['Normal'])
    bs.inputs['Roughness'].default_value=.88
    nt.links.new(mathnode(nt,'SUBTRACT',.9,mathnode(nt,'MULTIPLY',channels.outputs['Green'],.3)),bs.inputs['Roughness'])
    return m


def forest_material(root):
    material=surface('Photographed muted forest earth and moss',(.055,.066,.031),.96)
    nt=material.node_tree;bs=nt.nodes.get('Principled BSDF');uv=nt.nodes.new('ShaderNodeUVMap');uv.uv_map='UVMap'
    photo=texture(nt,root/'Assets/Parasite/Art/City/Ground/Gravel_albedo.jpg');nt.links.new(uv.outputs[0],photo.inputs[0])
    tint=nt.nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1;tint.inputs[2].default_value=(.19,.24,.105,1);nt.links.new(photo.outputs[0],tint.inputs[1]);nt.links.new(tint.outputs[0],bs.inputs['Base Color'])
    noise=nt.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=2.8;noise.inputs['Detail'].default_value=3
    coords=nt.nodes.new('ShaderNodeTexCoord');nt.links.new(coords.outputs['Object'],noise.inputs[0])
    bump=nt.nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.012;bump.inputs['Strength'].default_value=.26;nt.links.new(noise.outputs['Fac'],bump.inputs['Height']);nt.links.new(bump.outputs[0],bs.inputs['Normal'])
    return material


def land(root):
    mat=terrain_material(root); nx,ny=200,160;vertices=[];weights=[]
    for j in range(ny+1):
        y=-55+j*130/ny
        for i in range(nx+1):
            x=-90+i*180/nx; d=y-shore(x); vertices.append((x,y,height(x,y)))
            edge=max(0,min(1,(d-.35)/1.4)); path=abs(x-pathcentre(y)); pathmask=max(0,min(1,(path-.65)/.8))
            grass=edge*pathmask; wet=max(0,min(1,1-abs(d)/1.2))
            weights.append((grass,wet,0,1))
    faces=[(j*(nx+1)+i,j*(nx+1)+i+1,(j+1)*(nx+1)+i+1,(j+1)*(nx+1)+i) for j in range(ny) for i in range(nx)]
    ob=meshobject('Rolling shore and visible shallow lake bed',vertices,faces,mat);metric_uv(ob)
    attr=ob.data.color_attributes.new('ground_response','FLOAT_COLOR','POINT')
    for item,value in zip(attr.data,weights):item.color=value
    # Extend well behind the visible shore. The old65m strip ended in a bare,
    # smooth green straight edge that read as a single untextured game plane.
    vertices=[];nx,ny=180,96
    for j in range(ny+1):
        y=-565+j*446/ny
        for i in range(nx+1):
            x=-520+i*1040/nx;vertices.append((x,y,far_height(x,y)))
    faces=[(j*(nx+1)+i,j*(nx+1)+i+1,(j+1)*(nx+1)+i+1,(j+1)*(nx+1)+i) for j in range(ny) for i in range(nx)]
    distant=meshobject('Distant irregular wooded bank',vertices,faces,forest_material(root));metric_uv(distant)
    return ob, distant


def water(length):
    old=bpy.data.objects.get('Physically traced lake')
    if old:bpy.data.objects.remove(old,do_unlink=True)
    nx,ny=160,130;vertices=[]
    for j in range(ny+1):
        y=-190+j*200/ny
        for i in range(nx+1):
            x=-200+i*400/nx;vertices.append((x,y,-.052+.0023*math.sin(x*2.3+y*.7)+.0015*math.sin(y*3.9-x*.8)))
    faces=[(j*(nx+1)+i,j*(nx+1)+i+1,(j+1)*(nx+1)+i+1,(j+1)*(nx+1)+i) for j in range(ny) for i in range(nx)]
    mat=surface('Calm dielectric lake IOR 1.333',(.32,.40,.36),.075);nt=mat.node_tree;bs=nt.nodes.get('Principled BSDF')
    bs.inputs['IOR'].default_value=1.333;bs.inputs['Transmission Weight'].default_value=1.;bs.inputs['Metallic'].default_value=0
    bs.inputs['Base Color'].default_value=(.91,.97,.96,1)
    geo=nt.nodes.new('ShaderNodeNewGeometry');coarse=nt.nodes.new('ShaderNodeTexNoise');coarse.noise_dimensions='4D';coarse.inputs['Scale'].default_value=3.5;coarse.inputs['Detail'].default_value=2
    fine=nt.nodes.new('ShaderNodeTexNoise');fine.inputs['Scale'].default_value=76.;fine.inputs['Detail'].default_value=2
    for node in (coarse,fine):nt.links.new(geo.outputs['Position'],node.inputs['Vector'])
    bump1=nt.nodes.new('ShaderNodeBump');bump1.inputs['Distance'].default_value=.004;bump1.inputs['Strength'].default_value=.32;nt.links.new(coarse.outputs['Fac'],bump1.inputs['Height'])
    bump2=nt.nodes.new('ShaderNodeBump');bump2.inputs['Distance'].default_value=.00065;bump2.inputs['Strength'].default_value=.18;nt.links.new(fine.outputs['Fac'],bump2.inputs['Height']);nt.links.new(bump1.outputs[0],bump2.inputs['Normal']);nt.links.new(bump2.outputs[0],bs.inputs['Normal'])
    # Every film shares exactly the same six-second water motion, including the loop endpoint.
    for frame in sorted(set(list(range(1,length+2,6))+[length+1])):
        coarse.inputs['W'].default_value=.035*(1-math.cos(2*math.pi*(frame-1)/144));coarse.inputs['W'].keyframe_insert('default_value',frame=frame)
    absorption=nt.nodes.new('ShaderNodeVolumeAbsorption');absorption.inputs['Color'].default_value=(.27,.48,.40,1);absorption.inputs['Density'].default_value=.075
    nt.links.new(absorption.outputs[0],nt.nodes.get('Material Output').inputs['Volume'])
    ob=meshobject('Physical calm water with millimetre ripples',vertices,faces,mat)
    return ob


def grass(length):
    rng=random.Random(8157);scene=bpy.context.scene
    material=surface('Living grass blades',(.065,.092,.023),.86);nt=material.node_tree;bs=nt.nodes.get('Principled BSDF')
    attr=nt.nodes.new('ShaderNodeVertexColor');attr.layer_name='blade_colour';nt.links.new(attr.outputs['Color'],bs.inputs['Base Color'])
    bs.inputs['Subsurface Weight'].default_value=.035;bs.inputs['Subsurface Scale'].default_value=.00015
    prototypes=[]
    # Eight small real-geometry clumps replace isolated needle dots. Thousands
    # of linked placements share these meshes and their looped shape keys.
    for variant in range(8):
        vertices=[];faces=[];colours=[]
        for blade in range(rng.randint(120,165)):
            a=rng.random()*math.tau;radius=.29*math.sqrt(rng.random())
            bx,by=math.cos(a)*radius,math.sin(a)*radius
            h=rng.uniform(.035,.115);width=rng.uniform(.0018,.0045)
            angle=rng.random()*math.tau;dx,dy=math.cos(angle),math.sin(angle);bend=rng.uniform(.014,.052)
            start=len(vertices);green=rng.random()
            base=(.037+.047*green,.056+.067*green,.012+.027*green,1)
            dry=rng.random()<.12
            if dry:base=(.115+.035*green,.096+.035*green,.032+.018*green,1)
            for segment in range(3):
                t=segment/2;w=width*(1-t*.96);cx=bx+dx*bend*t*t;cy=by+dy*bend*t*t
                vertices.extend([(cx-dy*w,cy+dx*w,h*t),(cx+dy*w,cy-dx*w,h*t)])
                colours.extend([base,base])
            faces.extend([(start+k*2,start+k*2+1,start+k*2+3,start+k*2+2) for k in range(2)])
        name='Irregular low grass and seedling tufts' if variant==0 else 'Living grass shared clump %02d'%variant
        ob=meshobject(name,vertices,faces,material);prototypes.append(ob)
        layer=ob.data.color_attributes.new('blade_colour','FLOAT_COLOR','POINT')
        for a,c in zip(layer.data,colours):a.color=c
        ob.shape_key_add(name='Basis');breeze=ob.shape_key_add(name='Six second shore breeze')
        for i,p in enumerate(breeze.data):
            t=(i%6//2)/2;p.co.x+=.006*t*t*math.sin(p.co.y*3+variant*.8);p.co.y+=.003*t*t
        for f in sorted(set(list(range(1,length+2,6))+[length+1])):
            breeze.value=.45+.3*math.sin(2*math.pi*(f-1)/144+variant*.65);breeze.keyframe_insert('value',frame=f)
    count=0
    for j in range(65):
        for i in range(102):
            x=-28+i*.55+rng.uniform(-.22,.22);y=-3+j*.55+rng.uniform(-.22,.22)
            if y<shore(x)+.55:continue
            pathgap=.86+.14*math.sin(y*.83)+rng.uniform(-.12,.14)
            if abs(x-pathcentre(y))<pathgap:continue
            if ((x+1.7)/1.85)**2+((y+.05)/.9)**2<1:continue
            density=.73+.18*math.sin(x*.51+y*.33)+.13*math.sin(x*1.13-y*.66)
            if rng.random()>max(.25,min(.98,density)):continue
            proto=prototypes[count%len(prototypes)];ob=proto if count<len(prototypes) else proto.copy()
            if count>=len(prototypes):scene.collection.objects.link(ob);ob.name='Low dense grass carpet %05d'%count
            ob.location=(x,y,height(x,y)-.004)
            slopex=(height(x+.10,y)-height(x-.10,y))/.20;slopey=(height(x,y+.10)-height(x,y-.10))/.20
            normal=Vector((-slopex,-slopey,1)).normalized();ob.rotation_mode='QUATERNION'
            ob.rotation_quaternion=normal.to_track_quat('Z','Y')@Quaternion((0,0,1),rng.random()*math.tau)
            scale=rng.uniform(.82,1.16);ob.scale=(scale,scale,scale*rng.uniform(.86,1.08));count+=1
    scene['realism_grass_linked_instances']=count;scene['realism_grass_unique_meshes']=len(prototypes)
    return prototypes[0]


def leaf_litter():
    rng=random.Random(9247);vertices=[];faces=[];colours=[]
    material=surface('Dry and damp fallen leaves',(.10,.055,.018),.91);nt=material.node_tree;bs=nt.nodes.get('Principled BSDF')
    attr=nt.nodes.new('ShaderNodeVertexColor');attr.layer_name='litter_colour';nt.links.new(attr.outputs['Color'],bs.inputs['Base Color'])
    noise=nt.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=140;noise.inputs['Detail'].default_value=2
    coords=nt.nodes.new('ShaderNodeTexCoord');nt.links.new(coords.outputs['Object'],noise.inputs[0])
    bump=nt.nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.0003;bump.inputs['Strength'].default_value=.22;nt.links.new(noise.outputs['Fac'],bump.inputs['Height']);nt.links.new(bump.outputs[0],bs.inputs['Normal'])
    count=0
    for index in range(5600):
        x=rng.uniform(-28,28);y=rng.uniform(-3,32)
        if y<shore(x)+.50:continue
        onpath=abs(x-pathcentre(y))<.72
        if onpath and rng.random()>.10:continue
        if ((x+1.7)/1.8)**2+((y+.05)/.8)**2<1 and rng.random()>.15:continue
        a=rng.random()*math.tau;dx,dy=math.cos(a),math.sin(a)
        length=rng.uniform(.035,.095);width=length*rng.uniform(.28,.48);start=len(vertices)
        colour=(rng.uniform(.06,.19),rng.uniform(.035,.105),rng.uniform(.012,.038),1)
        for row in range(5):
            t=row/4;half=width*max(.02,math.sin(math.pi*t))*.5
            for across in (-1,0,1):
                lx=(t-.5)*length;ly=across*half
                px=x+dx*lx-dy*ly;py=y+dy*lx+dx*ly
                z=height(px,py)+.002+.003*abs(across)*math.sin(math.pi*t)+.002*t*t
                vertices.append((px,py,z));colours.append(colour)
        for row in range(4):
            for col in range(2):
                a=start+row*3+col;faces.append((a,a+1,a+4,a+3))
        count+=1
    ob=meshobject('Curled fallen leaf litter at real centimetre scale',vertices,faces,material)
    layer=ob.data.color_attributes.new('litter_colour','FLOAT_COLOR','POINT')
    for a,c in zip(layer.data,colours):a.color=c
    bpy.context.scene['realism_leaf_litter_count']=count
    return ob


def planting():
    rng=random.Random(47291);scene=bpy.context.scene
    trees=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Lake park detailed tree')]
    shrubs=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Lake park detailed understory')]
    if len(trees)<2 or not shrubs:raise RuntimeError('Original linked tree and shrub prototypes are required')
    prototypes=trees[:2];shrub=shrubs[0];bpy.context.view_layer.update()
    def record(ob):
        floor=min((ob.matrix_world@Vector(v)).z for v in ob.bound_box)-ob.location.z
        return dict(object=ob,scale=ob.scale.copy(),height=max(.1,ob.dimensions.z),floor=floor)
    records=[record(p) for p in prototypes];shrub_record=record(shrub)
    for ob in trees[2:]+shrubs[1:]:bpy.data.objects.remove(ob,do_unlink=True)
    uses={id(r['object']):0 for r in records+[shrub_record]}
    def instance(data,name,x,y,z,target_height,width=(.84,1.16)):
        proto=data['object'];key=id(proto);used=uses[key];ob=proto if used==0 else proto.copy();uses[key]+=1
        if used:scene.collection.objects.link(ob)
        factor=target_height/data['height'];ob.name=name
        ob.location=(x,y,z-data['floor']*factor)
        ob.scale=(data['scale'].x*factor*rng.uniform(*width),data['scale'].y*factor*rng.uniform(*width),data['scale'].z*factor)
        ob.rotation_euler[2]=rng.random()*math.tau
        return ob
    # Near woods provide real layered bokeh behind the portraits. Keep the
    # human/bench contact patch and the shore/path completely undisturbed.
    clusters=[(-19,14),(15,17),(-30,30),(25,34),(0,35),(-16,48),(35,51)]
    near=0
    for index in range(88):
        cx,cy=clusters[index%len(clusters)];x=cx+rng.gauss(0,4.6);y=cy+rng.gauss(0,4.2)
        instance(records[index%2],'Natural inland tree cluster %03d'%index,x,y,height(x,y)-.04,rng.uniform(6.0,11.7));near+=1
    # Five staggered forest layers follow the actual bank and continue over
    # deeper irregular hills. All 1,010 trees share only two original meshes.
    far=0
    layers=[(180,245,5,18,9,15),(180,300,20,48,10,17),
            (240,360,52,112,11,19),(240,420,118,230,11,20),
            (170,480,235,390,10,19)]
    for layer,(count,extent,d0,d1,h0,h1) in enumerate(layers):
        for index in range(count):
            # Jittered stratification prevents both bare random holes and rows.
            x=-extent+(index+rng.random())*2*extent/count
            y=far_rim(x)-rng.uniform(d0,d1)
            instance(records[(index+layer)%2],'Layered far forest %d %03d'%(layer,index),x,y,far_height(x,y)-.06,rng.uniform(h0,h1));far+=1
    # Irregular low woody edges mask exposed mature trunks at the water. Small
    # trees share the same mesh as the canopy; shrubs share the licensed mesh.
    fringe=0
    for index in range(210):
        x=rng.uniform(-250,250);y=far_rim(x)-rng.uniform(1.2,16)
        data=records[index%2] if index%3==0 else shrub_record
        target=rng.uniform(2.0,4.8) if index%3==0 else rng.uniform(.75,1.8)
        instance(data,'Dense far-bank understory %03d'%index,x,y,far_height(x,y)-.025,target);fringe+=1
    centres=[(-7,4.5),(5,7),(-15,12),(12,14),(-22,25),(22,28),(-11,1.8),(9,1.2)]
    near_shrubs=0
    for index in range(280):
        cx,cy=centres[index%len(centres)];x=cx+rng.gauss(0,1.35);y=cy+rng.gauss(0,1.20)
        if y<shore(x)+.8 or abs(x-pathcentre(y))<1.0:continue
        if ((x+1.7)/2.0)**2+((y+.05)/1.15)**2<1:continue
        instance(shrub_record,'Low clustered shore shrub %03d'%index,x,y,height(x,y)-.008,rng.uniform(.18,.60),(.90,1.25));near_shrubs+=1
    for material in bpy.data.materials:
        if not material.use_nodes or 'Lake reused' not in material.name or 'bark' in material.name:continue
        bs=material.node_tree.nodes.get('Principled BSDF')
        if bs:bs.inputs['Subsurface Weight'].default_value=.06;bs.inputs['Subsurface Scale'].default_value=.00025
    scene['realism_far_forest_linked_trees']=far;scene['realism_far_understory_instances']=fringe
    scene['realism_near_tree_instances']=near;scene['realism_near_low_shrubs']=near_shrubs
    scene['realism_tree_unique_meshes']=len({r['object'].data.as_pointer() for r in records})
    scene['realism_shrub_unique_meshes']=1
    return near+far+fringe+near_shrubs


def edge_details():
    rng=random.Random(9641);scene=bpy.context.scene
    stone=surface('Wet shoreline stones',(.135,.15,.127),.55)
    stem=surface('Reed stem natural olive',(.085,.105,.031),.86)
    leaf=surface('Reed and sedge strap leaves',(.065,.095,.022),.83)
    seed=surface('Dry cattail seed head',(.085,.040,.014),.95)
    # Eight smoothed rock meshes are shared by a varied small shoreline scatter.
    rocks=[]
    for i in range(8):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1)
        proto=bpy.context.object;proto.name='Shared shore rock %02d'%i
        for v in proto.data.vertices:v.co*=rng.uniform(.86,1.12)
        proto.data.materials.append(stone)
        for p in proto.data.polygons:p.use_smooth=True
        rocks.append(proto)
    for i in range(95):
        proto=rocks[i%len(rocks)];ob=proto if i<len(rocks) else proto.copy()
        if i>=len(rocks):scene.collection.objects.link(ob)
        if i<80:
            centre=(-20,-11,6.5,15,23)[i%5];x=centre+rng.gauss(0,.58 if i%3 else 1.1)
        else:x=rng.uniform(-25,25)
        y=shore(x)+rng.uniform(-.35,.55)
        ob.name='Shore pebble %03d'%i;ob.location=(x,y,height(x,y)+.012)
        scale=rng.uniform(1.5,2.4) if i%13==0 else 1
        ob.scale=(scale*rng.uniform(.025,.10),scale*rng.uniform(.025,.085),scale*rng.uniform(.012,.045));ob.rotation_euler=(rng.random(),rng.random(),rng.random()*math.tau)
    vertices=[];faces=[];seed_vertices=[];seed_faces=[];leaf_vertices=[];leaf_faces=[]
    def tube(vs,fs,a,b,radius,sides=6):
        start=len(vs);a,b=Vector(a),Vector(b);axis=(b-a).normalized();right=axis.cross(Vector((0,1,0))).normalized();up=axis.cross(right)
        for p in (a,b):
            for k in range(sides):
                angle=math.tau*k/sides;vs.append(tuple(p+radius*(right*math.cos(angle)+up*math.sin(angle))))
        for k in range(sides):fs.append((start+k,start+(k+1)%sides,start+sides+(k+1)%sides,start+sides+k))
        fs.extend([tuple(start+k for k in range(sides-1,-1,-1)),tuple(start+sides+k for k in range(sides))])
    # Low dense sedge leaves establish believable clumps around a few cattails;
    # the old isolated metre-high bare needle scatter is removed.
    for cluster in range(27):
        x=rng.uniform(-25,25)
        if abs(x+1.7)<2.6:continue
        y=shore(x)-rng.uniform(.04,.28)
        for j in range(rng.randint(14,24)):
            bx=x+rng.uniform(-.24,.24);by=y+rng.uniform(-.12,.12);z=height(bx,by)+.015
            angle=rng.random()*math.tau;dx,dy=math.cos(angle),math.sin(angle);h=rng.uniform(.16,.48);lean=rng.uniform(.09,.26);width=rng.uniform(.008,.017)
            start=len(leaf_vertices)
            for k in range(4):
                t=k/3;w=width*(1-.97*t);cx=bx+dx*lean*t*t;cy=by+dy*lean*t*t
                leaf_vertices.extend([(cx-dy*w,cy+dx*w,z+h*t),(cx+dy*w,cy-dx*w,z+h*t)])
            leaf_faces.extend([(start+k*2,start+k*2+1,start+k*2+3,start+k*2+2) for k in range(3)])
        for j in range(rng.randint(2,5)):
            bx=x+rng.uniform(-.18,.18);by=y+rng.uniform(-.10,.10);z=height(bx,by);h=rng.uniform(.48,.87)
            a=(bx,by,z);b=(bx+.014,by+.008,z+h*.5);c=(bx+.036,by+.012,z+h)
            tube(vertices,faces,a,b,.003);tube(vertices,faces,b,c,.0024)
            if rng.random()<.65:tube(seed_vertices,seed_faces,(c[0],c[1],c[2]-.09),(c[0]+.004,c[1],c[2]-.01),.010,8)
    meshobject('Human-scale broken reed clumps',vertices,faces,stem)
    meshobject('Dense low sedge leaves at the water edge',leaf_vertices,leaf_faces,leaf)
    meshobject('Restrained natural cattail seed heads',seed_vertices,seed_faces,seed)


def lighting(root):
    for ob in list(bpy.data.objects):
        if ob.type=='LIGHT' or ob.name=='Distant warm sun':bpy.data.objects.remove(ob,do_unlink=True)
    world=bpy.data.worlds.new('Belfast photographic sunset environment');world.use_nodes=True;nt=world.node_tree;nt.nodes.clear();out=nt.nodes.new('ShaderNodeOutputWorld');bg=nt.nodes.new('ShaderNodeBackground');bg.inputs['Strength'].default_value=.42
    env=nt.nodes.new('ShaderNodeTexEnvironment');env.image=bpy.data.images.load(str(root/'Assets/Parasite/Art/City/sky/belfast_sunset_puresky_4k.hdr'),check_existing=True)
    coords=nt.nodes.new('ShaderNodeTexCoord');mapping=nt.nodes.new('ShaderNodeMapping');mapping.inputs['Rotation'].default_value[2]=math.radians(105)
    nt.links.new(coords.outputs['Generated'],mapping.inputs[0]);nt.links.new(mapping.outputs[0],env.inputs[0]);nt.links.new(env.outputs[0],bg.inputs[0]);nt.links.new(bg.outputs[0],out.inputs[0]);bpy.context.scene.world=world
    d=bpy.data.lights.new('Restrained physical low sun','SUN');d.energy=1.45;d.color=(1,.88,.73);d.angle=math.radians(.53);ob=bpy.data.objects.new(d.name,d);bpy.context.scene.collection.objects.link(ob);ob.rotation_euler=Vector((-.65,.38,-.38)).to_track_quat('-Z','Y').to_euler()
    d=bpy.data.lights.new('Broad neutral portrait bounce','AREA');d.energy=38;d.shape='DISK';d.size=4.;d.color=(.86,.92,1);ob=bpy.data.objects.new(d.name,d);bpy.context.scene.collection.objects.link(ob);ob.location=(-3.9,-4.6,3.4);ob.rotation_euler=(Vector((-1.7,0,1.0))-ob.location).to_track_quat('-Z','Y').to_euler()


def rebuild(root, length):
    remove=('CinemaStone','CinemaShore','CinemaReed','CinemaTip','Inland park ground','Dominion heartbeat','Distant person','Dominion opposite inhabited bank')
    for ob in list(bpy.data.objects):
        if ob.name.startswith(remove):bpy.data.objects.remove(ob,do_unlink=True)
    wood(root);land(root);water(length);grass(length);leaf_litter();planting();edge_details();lighting(root)
