"""Original physical island architecture. CPU Blender, two threads, ignored staging only; never publishes Assets."""
import ast,bpy,bmesh,math,os,sys,random,json,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
import numpy as np
ARGS=sys.argv[sys.argv.index('--')+1:]
P,STAGE=map(os.path.abspath,ARGS[:2])
assert Path(STAGE).is_relative_to(Path(P)/'Logs'),'Island work must stay in ignored Logs staging until review'
OUT=os.path.join(STAGE,'Resources/Island');SOURCE=os.path.join(STAGE,'ArtSource/Island');TEX=os.path.join(OUT,'Textures')
os.makedirs(TEX,exist_ok=True);os.makedirs(SOURCE,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
MATS={};RNG=random.Random(71941);ASSETS=[];ACTIVE=None
# Reuse only function definitions: the proven helper's top-level generator/publication never executes.
helper=Path(P)/'Tools/blender/riverside/build_props.py'
functions=[node for node in ast.parse(helper.read_text()).body if isinstance(node,ast.FunctionDef)]
exec(compile(ast.Module(body=functions,type_ignores=[]),str(helper),'exec'),globals())
HELPER_SHA=hashlib.sha256(helper.read_bytes()).hexdigest()

def image(name,data):
    h,w,_=data.shape
    im=bpy.data.images.new(name,width=w,height=h,alpha=True,is_data=name.endswith(('_N','_M')))
    im.pixels.foreach_set(data.astype(np.float32).ravel());im.filepath_raw=os.path.join(TEX,name+'.png');im.file_format='PNG';im.save()
    return im

def island_textures():
    n=512;y,x=np.mgrid[0:n,0:n]/n;rng=np.random.default_rng(71941);noise=rng.normal(0,1,(n,n))
    radius=np.fft.fftfreq(n)[:,None]**2+np.fft.fftfreq(n)[None,:]**2
    broad=np.fft.ifft2(np.fft.fft2(noise)*np.exp(-radius*1000)).real;broad/=max(broad.std(),1e-6)
    specs=[('Concrete',(.32,.35,.32)),('Oxide',(.12,.26,.27)),('Timber',(.30,.25,.18)),('Clinic',(.73,.77,.70)),('Fabric',(.58,.65,.56)),('Metal',(.38,.42,.44))]
    for key,rgb in specs:
        metal=np.zeros((n,n));h=noise*.0007;rough=.7+noise*.02;variation=.96+noise*.025
        if key=='Concrete':
            pits=noise<-1.75;h=broad*.007+noise*.001+pits*.002;variation=.93+broad*.07+noise*.025;rough=.84+broad*.025
        elif key=='Oxide':
            chips=(broad+noise*.32)>1.65;variation=.96+broad*.035+noise*.02;h=chips*.008+noise*.0004;rough=.5+chips*.32+noise*.025;metal=.2+chips*.45
        elif key=='Timber':
            grain=np.sin(x*math.tau*47+np.sin(y*math.tau*3)*1.2+broad*.08);h=grain*.004+noise*.0005;variation=.92+grain*.13+broad*.045;rough=.76+grain*.045
        elif key=='Clinic':
            variation=.98+broad*.012+noise*.008;rough=.31+noise*.015
        elif key=='Fabric':
            weave=np.sin(x*math.tau*173)*np.sin(y*math.tau*173);h=weave*.001+noise*.0003;variation=.97+weave*.035+noise*.012;rough=.87+noise*.015
        else:
            brush=np.sin(y*math.tau*137);h=brush*.0004+noise*.0004;variation=.97+brush*.012+noise*.017;rough=.43+noise*.025;metal=np.ones((n,n))*.82
        data=np.ones((n,n,4));data[:,:,:3]=np.clip(variation[:,:,None]*np.array(rgb),0,1)
        if key=='Oxide':data[:,:,:3][chips]=np.array((.25,.15,.085))
        albedo=image('Island'+key+'_A',data)
        dy,dx=np.gradient(h);normal=np.ones((n,n,4));normal[:,:,0]=np.clip(.5-dx*15,0,1);normal[:,:,1]=np.clip(.5-dy*15,0,1);image('Island'+key+'_N',normal)
        packed=np.ones((n,n,4));packed[:,:,:3]=metal[:,:,None];packed[:,:,3]=np.clip(1-rough,0,1);image('Island'+key+'_M',packed)
        mat=material('Island'+key,rgb,float(np.mean(rough)),float(np.mean(metal)),albedo)
        tree=mat.node_tree;bs=tree.nodes.get('Principled BSDF')
        normal_node=tree.nodes.new('ShaderNodeTexImage');normal_node.image=bpy.data.images['Island'+key+'_N']
        normal_map=tree.nodes.new('ShaderNodeNormalMap');normal_map.inputs['Strength'].default_value=.65
        tree.links.new(normal_node.outputs['Color'],normal_map.inputs['Color']);tree.links.new(normal_map.outputs['Normal'],bs.inputs['Normal'])
        packed_node=tree.nodes.new('ShaderNodeTexImage');packed_node.image=bpy.data.images['Island'+key+'_M']
        separate=tree.nodes.new('ShaderNodeSeparateColor');tree.links.new(packed_node.outputs['Color'],separate.inputs['Color']);tree.links.new(separate.outputs['Red'],bs.inputs['Metallic'])
        invert=tree.nodes.new('ShaderNodeMath');invert.operation='SUBTRACT';invert.inputs[0].default_value=1
        tree.links.new(packed_node.outputs['Alpha'],invert.inputs[1]);tree.links.new(invert.outputs[0],bs.inputs['Roughness'])
    material('IslandDark',(.035,.05,.055),.55,.5);material('IslandSafety',(.76,.54,.16),.5,.1)
    material('IslandGlass',(.08,.17,.19),.14,.25);material('IslandSignal',(.48,.13,.055),.55,.1)

# Geometry functions are filled from the approved spatial contract, never inferred mission expansion.

if '--materials-only' in ARGS:
    island_textures();bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'IslandMaterials.blend'),compress=True)
    manifest={'scope':'Original island PBR library; spatial contract and architecture not published','size':512,'channels':{'A':'sRGB albedo','N':'linear tangent-space normal','M':'linear RGB metallic / alpha smoothness'},'export_helper_sha256':HELPER_SHA,'materials':sorted(MATS)}
    with open(os.path.join(SOURCE,'material_manifest.json'),'w') as file:json.dump(manifest,file,indent=2)
    print('[island] MATERIAL_LIBRARY_READY',len(MATS),'materials; no rendering or live publication',flush=True)

COLLISIONS={};ANCHORS={};CLEARANCES={}
def solid(name,p,d,mat='IslandConcrete',bevel=.02):
    o=box(name,p,d,mat,bevel)
    COLLISIONS.setdefault(ACTIVE.name,[]).append({'name':name,'center':p,'size':d})
    return o

def note(name,p):ANCHORS.setdefault(ACTIVE.name,{})[name]=list(p)

def culvert():
    new_asset('IslandTidalCulvert')
    solid('Continuous flush culvert footing',(0,-.10,6.9),(3.8,.20,13.8))
    solid('Culvert west wall',(-.82,.60,6.9),(.24,1.2,13.8))
    solid('Culvert east wall entry',(.82,.60,2.65),(.24,1.2,5.3))
    solid('Culvert east wall exit',(.82,.60,10.4),(.24,1.2,6.8))
    solid('Covered low corridor roof',(0,1.32,6.9),(1.9,.24,12))
    # A lateral refuge bay keeps its lip flush; the agreed safe anchor remains at the main-lane/bay threshold.
    solid('Dry refuge bay east wall',(1.72,.60,6.15),(.20,1.2,1.9))
    solid('Dry refuge bay south wall',(1.23,.60,5.25),(1.18,1.2,.20))
    solid('Dry refuge bay north wall',(1.23,.60,7.05),(1.18,1.2,.20))
    solid('Dry refuge bay roof',(1.22,1.32,6.15),(1.24,.24,2.0))
    for z in [.9,3.3,5.7,8.1,10.5,12.9]:
        # Ribs are on the outer structure, never inside the blob's clear cross-section.
        for side in [-1,1]:box('External concrete buttress',(side*1.03,.65,z),(.18,1.3,.20),'IslandConcrete',.025)
        box('Outer arch coping',(0,1.50,z),(2.24,.14,.24),'IslandConcrete',.025)
    for z in [.45,13.35]:
        for i in range(9):box('Flush anti-slip apron rib',(-.6+i*.15,.009,z),(.055,.018,.7),'IslandMetal',.003)
    for side in [-1,1]:
        cyl('Protected service conduit',(side*.96,.91,1),(side*.96,.91,12.8),.042,'IslandOxide',12)
        for z in [1,4.1,8,11.5]:box('Conduit saddle',(side*.98,.91,z),(.11,.16,.055),'IslandMetal',.005)
    for z in [5.4,6.85]:box('Refuge threshold safety inlay',(1.16,.012,z),(.8,.024,.07),'IslandSafety',.003)
    note('Entry',(0,0,0));note('Exit',(0,0,13.8));note('SafeAlcove',(1.2,0,6.15));note('TideGate',(0,.55,5));note('Indicator',(0,1.2,.8))
    CLEARANCES[ACTIVE.name]={'human_passage':False,'blob_passage':True,'clear_width':1.4,'clear_height':1.2,'covered_length':12,'outer_length':13.8,'max_apron_step':.018,'lane_x':[-.7,.7],'lane_y':[0,1.2],'lane_z':[.9,12.9]}
    finish_asset('IslandTidalCulvert')

def tide_indicator():
    new_asset('IslandTideIndicator')
    solid('Float gauge wall bracket',(0,.65,.04),(.42,1.25,.12),'IslandOxide')
    for x in [-.17,.17]:cyl('Guard rail',(x,.1,-.13),(x,1.22,-.13),.022,'IslandMetal',12)
    cyl('Glass level tube',(0,.12,-.13),(0,1.18,-.13),.068,'IslandGlass',20)
    cyl('Mechanical float',(0,.12,-.13),(0,.43,-.13),.055,'IslandOxide',16)
    for i in range(9):box('Embossed level graduation',(.11,.19+i*.11,-.19),(.07 if i%2==0 else .04,.018,.018),'IslandClinic',.002)
    for y in [.30,.69,1.08]:box('Warning lens',( -.11,y,-.16),(.08,.08,.045),'IslandSignal' if y>1 else 'IslandSafety',.01)
    note('Front',(0,.65,-.22));CLEARANCES[ACTIVE.name]={'mount':'outside culvert clear lane','no_navigation_role':True}
    finish_asset('IslandTideIndicator')

def clinic():
    new_asset('IslandRefugeClinic')
    solid('Clinic floor',(0,-.10,0),(14,.20,12),'IslandConcrete')
    solid('Clinic west wall',(-6.90,1.65,0),(.20,3.3,12),'IslandClinic')
    solid('Clinic east wall',(6.90,1.65,0),(.20,3.3,12),'IslandClinic')
    solid('Clinic rear wall',(0,1.65,5.9),(14,3.3,.20),'IslandClinic')
    solid('Clinic front west',(-4.15,1.65,-5.9),(5.7,3.3,.20),'IslandClinic')
    solid('Clinic front east',(4.15,1.65,-5.9),(5.7,3.3,.20),'IslandClinic')
    solid('Clinic entrance lintel',(0,2.98,-5.9),(2.6,.64,.20),'IslandOxide')
    # Roof sits above 3.3m clear volume. A recessed clerestory breaks the large flat roof physically.
    solid('Clinic west roof',(-4.35,3.43,0),(5.3,.26,12),'IslandOxide')
    solid('Clinic east roof',(4.35,3.43,0),(5.3,.26,12),'IslandOxide')
    for z in [-4.8,-2.4,0,2.4,4.8]:box('Clerestory cross beam',(0,3.43,z),(3.4,.16,.12),'IslandMetal')
    box('Raised clerestory glazing',(0,3.56,0),(3.4,.09,11.7),'IslandGlass',.015)
    for x in [-6.82,6.82]:
        for z in [-3,1.2,4.2]:
            box('Deep clinic window reveal',(x,1.9,z),(.045,1.15,1.6),'IslandOxide',.01)
            box('Clinic window glass',(x,1.9,z),(.06,.98,1.43),'IslandGlass',.008)
    for x in [-1.39,1.39]:box('Open entrance jamb',(x,1.3,-5.95),(.16,2.6,.1),'IslandOxide',.01)
    box('Entrance threshold',(0,.012,-5.55),(2.6,.024,.62),'IslandMetal',.004)
    # Treatment bays are behind the survivor destinations, never on their arrival slots or the primary aisle.
    for x in [-4.8,4.8]:
        solid('Treatment bed underframe',(x,.40,3.6),(2.0,.25,2.7),'IslandMetal')
        box('Clean canvas mattress',(x,.63,3.6),(1.9,.24,2.6),'IslandFabric',.055)
        box('Raised pillow',(x,.81,4.45),(1.45,.18,.65),'IslandClinic',.07)
        for z in [2.6,4.6]:
            for side in [-.72,.72]:cyl('Bed leg',(x+side,.03,z),(x+side,.35,z),.04,'IslandMetal')
        cyl('Saline support',(x-1.0,.05,4.5),(x-1.0,2.25,4.5),.025,'IslandMetal',12)
        cyl('Saline arm',(x-1.0,2.23,4.5),(x-.5,2.23,4.5),.02,'IslandMetal',12)
        box('Sealed fluid bag',(x-.55,1.9,4.5),(.25,.45,.12),'IslandGlass',.04)
    solid('Supply cabinet',(-5.85,.85,-2.7),(1.3,1.7,.65),'IslandOxide')
    for y in [.5,1.15]:box('Cabinet grip',(-5.85,y,-3.05),(.36,.045,.06),'IslandMetal',.008)
    solid('Clinic handwash plinth',(5.9,.46,-3),(1.3,.92,.7),'IslandClinic')
    box('Handwash basin',(5.9,.96,-3),(1.1,.13,.57),'IslandMetal',.05)
    cyl('Water tap',(5.9,1.0,-2.8),(5.9,1.32,-2.8),.024,'IslandMetal',12)
    cyl('Tap spout',(5.9,1.32,-2.8),(5.9,1.32,-3.03),.024,'IslandMetal',12)
    for x in [-3,0,3]:
        for z in [-1.65,-.35]:box('Clear refuge-slot floor inlay',(x,.009,z),(1.35,.018,.04),'IslandSafety',.002)
    box('Front clinic identity band',(0,3.04,-6.02),(5.8,.40,.055),'IslandOxide',.012)
    text('Physical clinic lettering','REFUGE / CLINIC',(0,3.04,-6.06),.19,'IslandClinic')
    note('Door',(0,0,-6));note('Approach',(0,0,-9))
    for i,x in enumerate([-3,0,3],1):note('TravelerSafe'+str(i),(x,0,-1))
    CLEARANCES[ACTIVE.name]={'door_width':2.6,'door_height':2.6,'primary_aisle_width':3.2,'primary_aisle_x':[-1.6,1.6],'survivor_clear_radius':1.2,'survivor_slots':[[-3,0,-1],[0,0,-1],[3,0,-1]],'front':'-Z','roof_clear_height':3.3}
    finish_asset('IslandRefugeClinic')

def handoff_gate():
    new_asset('IslandHandoffGate')
    for x in [-1.65,1.65]:
        solid('Handoff steel upright',(x,1.32,0),(.20,2.64,.28),'IslandOxide')
        box('Recessed foundation plate',(x,.025,0),(.20,.05,.60),'IslandMetal',.005)
        for z in [-.22,.22]:cyl('Recessed anchor bolt',(x,.04,z),(x,.075,z),.033,'IslandMetal',12)
        for y in [.5,1.1,1.7]:box('Post safety strip',(x,y,-.15),(.145,.16,.025),'IslandSafety',.004)
    solid('Handoff overhead beam',(0,2.75,0),(3.5,.22,.28),'IslandMetal')
    box('Lane boundary left',(-1.45,.012,0),(.06,.024,1.8),'IslandSafety',.003)
    box('Lane boundary right',(1.45,.012,0),(.06,.024,1.8),'IslandSafety',.003)
    note('Crossing',(0,0,0));note('Front',(0,0,-1));note('Rear',(0,0,1))
    CLEARANCES[ACTIVE.name]={'total_width':3.5,'clear_width':3.1,'clear_height':2.64,'depth':2,'human_passage':True,'front':'-Z','no_console':True}
    finish_asset('IslandHandoffGate')

def service_latch():
    new_asset('IslandServiceLatch')
    solid('Mechanical latch backplate',(0,.75,.03),(.6,1.45,.12),'IslandOxide')
    cyl('Latch spindle',(0,.88,-.05),(0,.88,-.22),.075,'IslandMetal',20)
    ring('Mechanical handwheel',(0,.88,-.25),.22,.025,'IslandMetal',axis='Z')
    for a in [0,math.tau/3,math.tau*2/3]:cyl('Handwheel spoke',(0,.88,-.25),(.20*math.cos(a),.88+.20*math.sin(a),-.25),.018,'IslandMetal',12)
    cyl('Visible gate linkage',(0,.88,.13),(0,1.42,.13),.025,'IslandMetal',12)
    box('Mechanical travel witness',(.2,1.1,-.055),(.08,.30,.04),'IslandSafety',.005)
    note('Grip',(0,.88,-.28));CLEARANCES[ACTIVE.name]={'front':'-Z','interaction':'immediate mechanical latch; no held console','floor_footprint':[.6,.6]}
    finish_asset('IslandServiceLatch')

def flare_cache():
    new_asset('IslandFlareCache')
    solid('Waterproof rescue case',(0,.30,0),(1.18,.60,.66),'IslandOxide',.055)
    box('Weather-sealed case lid',(0,.64,0),(1.2,.10,.69),'IslandClinic',.035)
    for x in [-.39,.39]:
        box('Over-center lid latch',(x,.54,-.36),(.12,.2,.06),'IslandMetal',.012)
        box('Case rubber foot',(x,-.015,0),(.14,.03,.5),'IslandDark',.006)
    for x in [-.22,.22]:cyl('Carry handle mount',(x,.7,0),(x,.82,0),.024,'IslandMetal',12)
    cyl('Carry handle',(-.22,.82,0),(.22,.82,0),.034,'IslandDark',16)
    box('Case signal identifier',(0,.32,-.36),(.55,.16,.025),'IslandSignal',.008)
    text('Embossed relief identifier','RESCUE',(0,.32,-.38),.10,'IslandClinic')
    note('PickupGrip',(0,.82,0));note('Front',(0,.5,-.42));CLEARANCES[ACTIVE.name]={'reserved_space':[4,4],'pickup':'human; carrier ownership remains gameplay state'}
    finish_asset('IslandFlareCache')

def flare():
    new_asset('IslandSignalFlare')
    cyl('Signal flare body',(0,.035,0),(0,.37,0),.048,'IslandSignal',24)
    cyl('Flare grip',(0,0,0),(0,.13,0),.052,'IslandDark',20)
    cyl('Sealed safety cap',(0,.37,0),(0,.44,0),.055,'IslandClinic',24)
    ring('Pull ring',(0,.465,0),.036,.007,'IslandMetal',axis='X',segments=20)
    note('Grip',(0,.08,0));note('Emission',(0,.44,0));CLEARANCES[ACTIVE.name]={'inert_until':'protected departure after all boarding gates pass','carry_axis':'+Y'}
    finish_asset('IslandSignalFlare')

def flare_mount():
    new_asset('IslandFlareMount')
    for a in [0,math.tau/3,math.tau*2/3]:
        foot=(.45*math.cos(a),.025,.45*math.sin(a));cyl('Signal tripod leg',foot,(0,.73,0),.025,'IslandMetal',12)
        box('Tripod foot pad',foot,(.12,.04,.12),'IslandDark',.01)
    cyl('Rescue flare receiver',(0,.72,0),(0,1.0,0),.065,'IslandOxide',20)
    ring('Socket locking collar',(0,.92,0),.069,.013,'IslandMetal',segments=24)
    note('FlareSocket',(0,1.0,0));CLEARANCES[ACTIVE.name]={'maximum_radius':.51,'reserved_anchor':[32,.15,37],'main_jetty_lane_center_x':34,'no_console':True}
    finish_asset('IslandFlareMount')

if '--materials-only' not in ARGS:
    contract_path=Path(P)/'Logs/island-layout-contract.json';contract=json.loads(contract_path.read_text())
    assert contract['navigation']['culvert_inner_width']==1.4 and contract['navigation']['culvert_inner_height']==1.2
    island_textures();culvert();tide_indicator();clinic();handoff_gate();service_latch();flare_cache();flare();flare_mount()
    # Keep a separate editable lineup. Export origins and all recorded collision/interaction recipes remain local.
    for i,asset in enumerate(ASSETS):
        for obj in bpy.data.collections[asset['asset']].objects:obj.location.x+=i*26
    # Pack while paths are absolute: the new blend has no source-relative base until its first save.
    bpy.ops.file.pack_all()
    for im in bpy.data.images:
        if im.source=='FILE' or im.filepath:im.filepath='//'+os.path.relpath(im.filepath,SOURCE)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'IslandArchitecture.blend'),compress=True)
    manifest={'status':'Ignored review staging; not integrated into game','units':'Unity metres X right/Y up/Z north','world_origin':contract['world_origin'],'contract_source_sha256':hashlib.sha256(contract_path.read_bytes()).hexdigest(),'export_helper_sha256':HELPER_SHA,'assets':ASSETS,'collision_recipe':COLLISIONS,'interaction_anchors':ANCHORS,'navigation_clearances':CLEARANCES,'ferry_policy':'Reuse verified Resources/Riverside/RiversideFerry at contract origins/yaws; no new moving-platform mechanics','texture_channels':{'A':'sRGB albedo','N':'linear tangent normal','M':'linear RGB metallic / alpha smoothness'}}
    with open(os.path.join(SOURCE,'asset_manifest.json'),'w') as file:json.dump(manifest,file,indent=2)
    print('[island] ARCHITECTURE_READY',len(ASSETS),'assets; no rendering or live publication',flush=True)
