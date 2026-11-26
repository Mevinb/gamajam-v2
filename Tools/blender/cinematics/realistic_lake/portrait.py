"""Rig-preserving hair fibres, eye focus pins and branch-only iris signal."""
import math
import random
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def eye_position(rig):
    return sum((rig.matrix_world@rig.pose.bones[name].matrix.translation for name in ('LeftEye','RightEye')),Vector())/2


def refine_eye_uv(hero):
    """Keep the photographed iris, expose the sclera by correcting iris scale.

    The shared low-poly MPFB atlas covers a large iris on the small eye opening.
    Expand only its central UV field by1.42, so the visible iris diameter falls
    to70% while the scleral/outer atlas boundary stays unchanged. No eyelid,
    globe, rig, UV of any other material or image pixels are modified.
    """
    slots={i for i,m in enumerate(hero.data.materials) if m and m.name.split('.')[0]=='Eyes'}
    uv=hero.data.uv_layers.active.data;count=0
    for poly in hero.data.polygons:
        if poly.material_index not in slots:continue
        for index in poly.loop_indices:
            value=uv[index].uv;centre=Vector((.707,.704)) if value.x+value.y>1 else Vector((.292,.289))
            delta=value-centre;radius=delta.length
            fade=max(0,min(1,(radius-.15)/.12));fade=fade*fade*(3-2*fade)
            value[:]=centre+delta*(1+.42*(1-fade));count+=1
    hero['cinematic_iris_uv_central_scale']=1.42
    return count


def foot_support(hero,rig):
    """Lower seated subject and bench together onto measured boot sole points."""
    scene=bpy.context.scene;scene.frame_set(1);evaluated=hero.evaluated_get(bpy.context.evaluated_depsgraph_get());data=evaluated.to_mesh()
    try:
        ids={v.index for v in hero.data.vertices if any(hero.vertex_groups[g.group].name in ('LeftFoot','RightFoot','LeftToes','RightToes') and g.weight>.25 for g in v.groups)}
        if not ids:raise RuntimeError('Boot support requires the original foot weights')
        import scenery
        points=[evaluated.matrix_world@data.vertices[index].co for index in ids]
        gaps=sorted(p.z-scenery.height(p.x,p.y) for p in points)
        adjustment=max(0,gaps[0]-.006)
    finally:evaluated.to_mesh_clear()
    rig.location.z-=adjustment
    for name in ('CinemaTimber_Mesh.001','CinemaSteel_Mesh','CinemaDark_Mesh.001'):
        ob=bpy.data.objects.get(name)
        if ob:ob.location.z-=adjustment
    bpy.context.view_layer.update()
    return {'minimum_original_sole_ground_gap_m':gaps[0], 'bench_and_actor_lowered_m':adjustment,'intended_minimum_sole_clearance_m':.006}


def refine_hair_cap(material):
    """Fine coherent surface relief underneath the actual strand geometry."""
    nt=material.node_tree;bs=nt.nodes.get('Principled BSDF')
    bs.inputs['Anisotropic'].default_value=.75;bs.inputs['Specular IOR Level'].default_value=.35
    # Replace the cap's broad glossy highlight with broken directional fibres.
    for link in list(bs.inputs['Roughness'].links):nt.links.remove(link)
    bs.inputs['Roughness'].default_value=.50
    coord=nt.nodes.new('ShaderNodeTexCoord');stretch=nt.nodes.new('ShaderNodeVectorMath');stretch.operation='MULTIPLY';stretch.inputs[1].default_value=(1750,9,1);nt.links.new(coord.outputs['UV'],stretch.inputs[0])
    noise=nt.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=1;noise.inputs['Detail'].default_value=2;nt.links.new(stretch.outputs[0],noise.inputs['Vector'])
    bump=nt.nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.00018;bump.inputs['Strength'].default_value=.38;nt.links.new(noise.outputs['Fac'],bump.inputs['Height']);nt.links.new(bump.outputs[0],bs.inputs['Normal'])


def hair_fibres(hero, rig):
    rng=random.Random(19173);dg=bpy.context.evaluated_depsgraph_get();evaluated=hero.evaluated_get(dg);data=evaluated.to_mesh()
    try:
        world=[evaluated.matrix_world@v.co for v in data.vertices]
        slots={i for i,m in enumerate(hero.data.materials) if m and m.name.split('.')[0]=='Hair'}
        eye=eye_position(rig);triangles=[];areas=[];total=0
        data.calc_loop_triangles()
        for tri in data.loop_triangles:
            if tri.material_index not in slots:continue
            p=[world[i] for i in tri.vertices];centre=sum(p,Vector())/3
            if centre.z<eye.z-.31 or centre.z>eye.z+.25 or abs(centre.x-eye.x)>.22:continue
            # Exclude front eyebrow/lash triangles using their eye-height location.
            if centre.y<eye.y+.025 and abs(centre.z-eye.z)<.045:continue
            a=(p[1]-p[0]).cross(p[2]-p[0]).length/2
            if a<1e-8:continue
            total+=a;triangles.append(tuple(tri.vertices));areas.append(total)
        if not triangles:raise RuntimeError('No actual scalp/long-hair surface for fibres')
        bvh=BVHTree.FromPolygons(world,triangles,all_triangles=True)
        head=rig.matrix_world@rig.pose.bones['Head'].matrix;inverse=head.inverted()
        anchor=bpy.data.objects.new('Hair fibre pin - original Head bone',None);bpy.context.scene.collection.objects.link(anchor);anchor.matrix_world=head
        con=anchor.constraints.new('COPY_TRANSFORMS');con.target=rig;con.subtarget='Head';con.target_space='WORLD';con.owner_space='WORLD'
        # Native Curves are Cycles hair primitives, not beveled mesh tubes.
        # Their tangent/radius/strand randomness feed the physical hair BSDF.
        curves=bpy.data.hair_curves.new('Native coherent human hair groom')
        ob=bpy.data.objects.new('Head-pinned fine hair and restrained flyaways',curves);bpy.context.scene.collection.objects.link(ob);ob.parent=anchor
        mat=bpy.data.materials.new('Native Huang dark brown human hair');mat.use_nodes=True;nt=mat.node_tree;nt.nodes.clear();out=nt.nodes.new('ShaderNodeOutputMaterial');bs=nt.nodes.new('ShaderNodeBsdfHairPrincipled');bs.model='HUANG';bs.parametrization='COLOR';bs.inputs['Color'].default_value=(.036,.022,.013,1);bs.inputs['Roughness'].default_value=.30;bs.inputs['Aspect Ratio'].default_value=.85;bs.inputs['IOR'].default_value=1.55;nt.links.new(bs.outputs[0],out.inputs[0]);curves.materials.append(mat)
        info=nt.nodes.new('ShaderNodeHairInfo');variation=nt.nodes.new('ShaderNodeValToRGB');variation.color_ramp.elements[0].color=(.0025,.0018,.0012,1);variation.color_ramp.elements[1].color=(.013,.0085,.0055,1);nt.links.new(info.outputs['Random'],variation.inputs[0]);nt.links.new(variation.outputs[0],bs.inputs['Color'])
        strands=[];radii=[]
        def addstrand(points,radius,fly=False):
            if len(points)<3:return
            strands.append([inverse@p for p in points]);radii.append([radius*(1-.92*(i/(len(points)-1))**1.5) for i in range(len(points))])
        import bisect
        for strand in range(24000):
            tri=triangles[bisect.bisect_left(areas,rng.random()*total)];p=[world[i] for i in tri];r=math.sqrt(rng.random());s=rng.random();point=p[0]*(1-r)+p[1]*(r*(1-s))+p[2]*(r*s)
            nearest,normal,_,_=bvh.find_nearest(point);normal.normalize();fly=strand>=23400;length=rng.uniform(.08,.21);positions=[];fly_lift=rng.uniform(.006,.018)
            for step in range(14):
                t=step/13
                if step:
                    flow=Vector((.08*math.sin(strand),.10,-1.0));flow-=normal*flow.dot(normal)
                    if flow.length<.12:flow=Vector((point.x-eye.x,point.y-eye.y+.07,-.03));flow-=normal*flow.dot(normal)
                    flow.normalize();candidate=point+flow*length/13;new,n,_,distance=bvh.find_nearest(candidate)
                    if new is not None and distance<.008 and (new-point).length>.0001:point=new;normal=n.normalized()
                    elif fly:point=candidate
                    else:break
                lift=.0002+(fly_lift*math.sin(math.pi*t) if fly else .0003*math.sin(math.pi*t))
                positions.append(point+normal*lift)
            addstrand(positions,rng.uniform(.000035,.000055) if not fly else .000030,fly)

        # Coherent locks begin along a real central part, sweep over the crown
        # and fall down the sides. The millimetre-thick clumps break the cap's
        # smooth outline while every fibre retains human hair diameter.
        top=max(world[i].z for tri in triangles for i in tri)
        guided=0
        for side in (-1,1):
            for guide in range(30):
                probe=Vector((eye.x+side*.004,eye.y+.015+guide*.0052,top+.02))
                point,normal,_,_=bvh.find_nearest(probe);normal.normalize();guidepoints=[];normals=[]
                for step in range(27):
                    t=step/26
                    if step:
                        flow=Vector((side*max(.08,.85-1.3*t),.09,-(.12+1.6*t)));flow-=normal*flow.dot(normal)
                        if flow.length<.05:flow=Vector((side*.4,.05,-.9))
                        flow.normalize();candidate=point+flow*.014;new,n,_,distance=bvh.find_nearest(candidate)
                        if new is not None and distance<.008 and (new-point).length>.0001:point=new;normal=n.normalized()
                        else:break
                    guidepoints.append(point.copy());normals.append(normal.copy())
                if len(guidepoints)<4:continue
                for fibre in range(120):
                    spread=rng.uniform(-.0045,.0045);lift=rng.uniform(.0002,.0006);phase=rng.random()*math.tau;points=[];last=rng.randint(max(4,len(guidepoints)*2//3),len(guidepoints))
                    for step,(p,n) in enumerate(zip(guidepoints[:last],normals[:last])):
                        t=step/max(1,last-1);tangent=(guidepoints[min(len(guidepoints)-1,step+1)]-guidepoints[max(0,step-1)]).normalized();across=n.cross(tangent).normalized()
                        offset=spread*(.2+.8*math.sin(math.pi*t*.8));fluff=.00035*math.sin(t*12+phase)
                        nearest,surface_normal,_,_=bvh.find_nearest(p+across*offset)
                        points.append(nearest+surface_normal.normalized()*max(.00015,lift+fluff*.3))
                    addstrand(points,rng.uniform(.000035,.000055));guided+=1

        # A few loose temple wisps follow the face boundary instead of forming
        # one hard painted hairline. They do not cross the iris/pupil region.
        framing=0
        for side in (-1,1):
            for guide in range(8):
                probe=Vector((eye.x+side*(.061+guide*.003),eye.y+.004,eye.z+.10+guide*.006))
                point,normal,_,_=bvh.find_nearest(probe);normal.normalize();guidepoints=[];normals=[]
                for step in range(23):
                    t=step/22
                    if step:
                        flow=Vector((side*.10,.05,-1));flow-=normal*flow.dot(normal)
                        if flow.length<.05:flow=Vector((side*.3,.1,-1))
                        flow.normalize();candidate=point+flow*.010;new,n,_,distance=bvh.find_nearest(candidate)
                        if new is not None and distance<.008 and (new-point).length>.0001:point=new;normal=n.normalized()
                        else:break
                    guidepoints.append(point.copy());normals.append(normal.copy())
                if len(guidepoints)<4:continue
                for fibre in range(50):
                    lift=rng.uniform(.0005,.0025);spread=rng.uniform(-.002,.002);points=[];last=rng.randint(max(4,len(guidepoints)*2//3),len(guidepoints))
                    for step,(p,n) in enumerate(zip(guidepoints[:last],normals[:last])):
                        t=step/max(1,last-1);nearest,surface_normal,_,_=bvh.find_nearest(p+Vector((side*spread,0,0)));points.append(nearest+surface_normal.normalized()*(.0002+lift*math.sin(math.pi*t)**2))
                    addstrand(points,rng.uniform(.000040,.000065));framing+=1
        curves.add_curves([len(p) for p in strands]);curves.set_types(type='CATMULL_ROM')
        positions=[c for strand in strands for p in strand for c in p]
        curves.attributes['position'].data.foreach_set('vector',positions)
        radius=curves.attributes.new('radius','FLOAT','POINT');radius.data.foreach_set('value',[r for strand in radii for r in strand])
        ob['strand_count']=len(strands);ob['flyaway_count']=600;ob['guided_part_side_strands']=guided;ob['face_framing_strands']=framing;ob['diameter_metres_range']=[.000060,.000130];ob['native_hair_primitives']=True;ob['groom_conforms_to_render_subdivision']=2
        return ob
    finally:evaluated.to_mesh_clear()


def focus_pin(scene, rig, length):
    pin=bpy.data.objects.new('Eye focus pin - measured original eye bones',None);scene.collection.objects.link(pin)
    for f in range(1,length+2):
        scene.frame_set(f);pin.location=eye_position(rig);pin.keyframe_insert('location',frame=f)
    scene.frame_set(1)
    return pin


def dominion_iris(material,length):
    nt=material.node_tree;bs=nt.nodes.get('Principled BSDF');tx=next(n for n in nt.nodes if n.type=='TEX_IMAGE' and n.image.name.startswith('brown'))
    ramp=nt.nodes.new('ShaderNodeValToRGB');ramp.name='Explicit Dominion iris mask; pale sclera stays ordinary';ramp.color_ramp.elements[0].position=.12;ramp.color_ramp.elements[0].color=(1,1,1,1);ramp.color_ramp.elements[1].position=.52;ramp.color_ramp.elements[1].color=(0,0,0,1);nt.links.new(tx.outputs['Color'],ramp.inputs[0])
    mult=nt.nodes.new('ShaderNodeMath');mult.name='Explicit Dominion reveal only';mult.operation='MULTIPLY';nt.links.new(ramp.outputs[0],mult.inputs[0]);nt.links.new(mult.outputs[0],bs.inputs['Emission Strength']);bs.inputs['Emission Color'].default_value=(.09,.45,.15,1)
    for f,value in [(1,0),(24,0),(48,.65),(84,1.15),(length,.85)]:mult.inputs[1].default_value=value;mult.inputs[1].keyframe_insert('default_value',frame=f)


def camera(scene,name,start,end,targets,lens,frames,focus,aperture):
    data=bpy.data.cameras.new(name);data.lens=lens;data.sensor_width=36;data.dof.use_dof=True;data.dof.aperture_fstop=aperture;data.dof.focus_object=focus;data.dof.aperture_blades=9
    ob=bpy.data.objects.new(name,data);scene.collection.objects.link(ob);ob.rotation_mode='QUATERNION';previous=None
    for f,p,target in [(frames[0],start,targets[0]),(frames[1],end,targets[1])]:
        ob.location=p;q=(Vector(target)-ob.location).to_track_quat('-Z','Y')
        if previous is not None and previous.dot(q)<0:q.negate()
        previous=q.copy();ob.rotation_quaternion=q;ob.keyframe_insert('location',frame=f);ob.keyframe_insert('rotation_quaternion',frame=f)
    marker=scene.timeline_markers.new(name,frame=frames[0]);marker.camera=ob
    if frames[0]==1:scene.camera=ob
    return ob
