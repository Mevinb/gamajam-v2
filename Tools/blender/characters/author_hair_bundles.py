"""Portable canonical-input Blender CPU geometry-to-normal/flow authoring.

No bpy render/bake operation is called. Normals come from an actual authored
Blender relief mesh. Geometry differentials project those normals and authored
guide flow into the unchanged character's Mikk frame. No source is saved.
"""
from pathlib import Path
from collections import defaultdict
import argparse
import hashlib
import json
import math
import resource
import sys

import bpy
import numpy as np


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def normalize(values):
    return values / np.maximum(np.linalg.norm(values, axis=-1, keepdims=True), 1e-12)


def save_png(path, values):
    h, w, _ = values.shape
    image = bpy.data.images.new(path.stem, w, h, alpha=True, float_buffer=False)
    image.colorspace_settings.name = 'Non-Color'
    image.pixels.foreach_set(np.ascontiguousarray(values, dtype=np.float32).ravel())
    image.file_format = 'PNG'; image.filepath_raw = str(path); image.save()
    bpy.data.images.remove(image)


def mesh_hash(ob):
    m = ob.data
    data = {'vertices': [list(v.co) for v in m.vertices], 'faces': [list(p.vertices) for p in m.polygons],
            'slots': [p.material_index for p in m.polygons],
            'weights': [[(ob.vertex_groups[g.group].name, g.weight) for g in v.groups] for v in m.vertices],
            'uv': [list(d.uv) for d in m.uv_layers.active.data]}
    return hashlib.sha256(json.dumps(data, sort_keys=True).encode()).hexdigest()


def phase(x, y, c):
    u, v = x / c['referenceWidthMetres'], y / c['referenceLengthMetres']
    return 2 * np.pi * c['bundleCyclesAcrossReference'] * u + .4 * np.sin(2*np.pi*3*u) + .18*np.sin(2*np.pi*9*u) + 1.2*np.sin(2*np.pi*v + .4*np.sin(2*np.pi*2*u))


def height(x, y, c):
    u, v = x / c['referenceWidthMetres'], y / c['referenceLengthMetres']
    amplitude = c['bundleAmplitudeMetres'] * (.9 + .1*np.cos(2*np.pi*7*u))
    return amplitude*np.cos(phase(x,y,c)) + c['lockAmplitudeMetres']*np.cos(2*np.pi*c['lockCyclesAcrossReference']*u + .25*np.sin(2*np.pi*v))


def reference_surface(c):
    nx, ny = c['referenceCrossSegments']+1, c['referenceLengthSegments']+1
    x, y = np.meshgrid(np.linspace(0,c['referenceWidthMetres'],nx), np.linspace(0,c['referenceLengthMetres'],ny))
    z = height(x,y,c)
    mesh = bpy.data.meshes.new('Measured-scale resolved strand bundles relief')
    mesh.vertices.add(nx*ny); mesh.vertices.foreach_set('co',np.stack([x,y,z],axis=-1).astype(np.float32).ravel())
    base = (np.arange(ny-1)[:,None]*nx + np.arange(nx-1)[None,:]).ravel()
    loops = np.stack([base,base+1,base+nx+1,base+nx],axis=-1).astype(np.int32).ravel()
    mesh.loops.add(len(loops)); mesh.loops.foreach_set('vertex_index',loops)
    mesh.polygons.add(len(base)); mesh.polygons.foreach_set('loop_start',np.arange(len(base),dtype=np.int32)*4)
    mesh.polygons.foreach_set('loop_total',np.full(len(base),4,dtype=np.int32)); mesh.polygons.foreach_set('use_smooth',np.ones(len(base),bool)); mesh.update(calc_edges=True)
    normals = np.empty(nx*ny*3,np.float32); mesh.vertices.foreach_get('normal',normals); normals=normals.reshape(ny,nx,3)
    dx=c['referenceWidthMetres']/(nx-1);dy=c['referenceLengthMetres']/(ny-1)
    hx=(z[1:-1,2:]-z[1:-1,:-2])/(2*dx);hy=(z[2:,1:-1]-z[:-2,1:-1])/(2*dy)
    expected=normalize(np.stack([-hx,-hy,np.ones_like(hx)],axis=-1));agreement=np.sum(expected*normals[1:-1,1:-1],axis=-1)
    assert float(agreement.min())>.9999,'Geometry-derived normal sign disagrees with +Y UV convention'
    collection = bpy.data.collections.new('Hair surface pilot reference geometry only')
    ob = bpy.data.objects.new('64mm x192mm relief;14um bundles+22um locks',mesh); collection.objects.link(ob)
    # Actual root-to-tip guide curves; zero bevel, never runtime hair cards.
    for strand in range(c['bundleCyclesAcrossReference']):
        yy = np.linspace(0,c['referenceLengthMetres'],129); xx = np.full_like(yy,(strand+.25)*c['referenceWidthMetres']/c['bundleCyclesAcrossReference'])
        target = 2*np.pi*(strand+.25)
        for _ in range(5):
            eps=1e-6; derivative=(phase(xx+eps,yy,c)-phase(xx-eps,yy,c))/(2*eps)
            xx -= (phase(xx,yy,c)-target)/derivative
        curve=bpy.data.curves.new('Bundle guide %02d'%strand,'CURVE'); curve.dimensions='3D'; line=curve.splines.new('POLY');line.points.add(len(yy)-1)
        line.points.foreach_set('co',np.stack([xx,yy,height(xx,yy,c)+.000002,np.ones_like(xx)],axis=-1).astype(np.float32).ravel())
        collection.objects.link(bpy.data.objects.new('Root-to-tip guide %02d'%strand,curve))
    return normals, collection, {'vertices':nx*ny,'quads':len(base),'guides':c['bundleCyclesAcrossReference'],
                               'physicalWidthM':c['referenceWidthMetres'],'physicalLengthM':c['referenceLengthMetres'],
                               'heightMinM':float(z.min()),'heightMaxM':float(z.max()),'geometryNormalSignAgreementMinDot':float(agreement.min())}


def sample_reference(normals, u, v, scale_u, scale_v, c):
    x=np.mod(u*scale_u,c['referenceWidthMetres']);y=np.mod((v-.25)*scale_v,c['referenceLengthMetres'])
    fx=x/c['referenceWidthMetres']*(normals.shape[1]-1);fy=y/c['referenceLengthMetres']*(normals.shape[0]-1)
    ix=np.floor(fx).astype(int);iy=np.floor(fy).astype(int);tx=(fx-ix)[:,None];ty=(fy-iy)[:,None]
    n=normalize((normals[iy,ix]*(1-tx)+normals[iy,ix+1]*tx)*(1-ty)+(normals[iy+1,ix]*(1-tx)+normals[iy+1,ix+1]*tx)*ty)
    eps=1e-6;px=(phase(x+eps,y,c)-phase(x-eps,y,c))/(2*eps);py=(phase(x,y+eps,c)-phase(x,y-eps,c))/(2*eps)
    dxdy=-py/px
    return n, dxdy


def main():
    p=argparse.ArgumentParser();p.add_argument('--project',type=Path,required=True);p.add_argument('--recipe',type=Path,default=Path(__file__).with_name('lakegirl_hair_surface_recipe.json'));p.add_argument('--output',type=Path,required=True)
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:]);root=args.project.resolve();out=args.output.resolve();assert not out.exists()
    assert root/'Assets'not in [out,*out.parents] and root/'ArtSource'not in [out,*out.parents]
    c=json.loads(args.recipe.read_text());assert list(bpy.app.version)==c['blenderVersion'];source=root/c['sourceBlend'];assert sha(source)==c['sourceSha256']
    # Only the canonical blend and recipe are semantic authoring inputs. Runtime
    # files are optional preservation observations, never texture/geometry inputs.
    observations=[root/'Assets/Parasite/Art/Characters/Models/LakeGirl.fbx',root/'Assets/Parasite/Art/Characters/Models/LakeGirl.json',root/'Assets/Parasite/Art/Characters/Textures/LakeGirl_Hair_A.png',root/'Assets/Parasite/Resources/Characters/Materials/LakeGirl_Hair.mat',root/'Assets/Parasite/Editor/CharacterModelImporter.cs']+[root/('Assets/Parasite/Shaders/'+s)for s in ['Character.shader','CharacterInput.hlsl','CharacterForwardPass.hlsl']]
    missing_observations=[str(x.relative_to(root))for x in observations if not x.is_file()]
    pinned=[source]+[x for x in observations if x.is_file()]
    pins={str(x.relative_to(root)):sha(x)for x in pinned};out.mkdir(parents=True)
    bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False);bpy.context.scene.cycles.device='CPU'
    lods=[bpy.data.objects['LakeGirl_LOD'+str(i)]for i in range(3)];geometry_before=[mesh_hash(o)for o in lods]
    rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');rest_before={b.name:(b.parent.name if b.parent else None,[float(v)for row in b.matrix_local for v in row])for b in rig.data.bones}
    ob=lods[0];m=ob.data;m.calc_tangents(uvmap=m.uv_layers.active.name);uv=m.uv_layers.active.data
    cap=[f for f in m.polygons if f.material_index==2 and all(uv[i].uv.y>=.25 for i in f.loop_indices)];assert len(cap)==3563
    tri=[]
    for f in cap:
        ids=list(f.loop_indices);assert len(ids)==3
        t=np.array([tuple(uv[i].uv)for i in ids]);pos=np.array([tuple(ob.matrix_world@m.vertices[m.loops[i].vertex_index].co)for i in ids]);delta=np.stack([t[1]-t[0],t[2]-t[0]])
        if abs(np.linalg.det(delta))<1e-12:continue
        ju,jv=np.linalg.solve(delta,np.stack([pos[1]-pos[0],pos[2]-pos[0]]))
        tri.append((ids,t,ju,jv))
    # Main shell is the left atlas island; layered ribbons occupy the right.
    main_tri=[t for t in tri if float(t[1][:,0].mean())<.62]
    scale_u=float(np.median([np.linalg.norm(t[2])for t in main_tri]));scale_v=float(np.median([np.linalg.norm(t[3])for t in main_tri]))
    differential=defaultdict(lambda:np.zeros((3,3),np.float64))
    def chart_key(i):return (m.loops[i].vertex_index,round(float(uv[i].uv.x),7),round(float(uv[i].uv.y),7))
    for ids,t,ju,jv in tri:
        weight=max(1e-12,float(np.linalg.norm(np.cross(ju,jv))*abs(np.linalg.det(np.stack([t[1]-t[0],t[2]-t[0]])))))
        for i in ids:
            key=chart_key(i);differential[key][0]+=ju*weight;differential[key][1]+=jv*weight;differential[key][2,0]+=weight
    for value in differential.values():value[:2]/=value[2,0]
    reference,collection,reference_receipt=reference_surface(c)
    bpy.data.libraries.write(str(out/'AuthoredBundleRelief.blend'),{collection},fake_user=True,compress=True)
    size=c['normalResolution'];normal_sum=np.zeros((size,size,3),np.float32);flow_sum=np.zeros((size,size,2),np.float32);hits=np.zeros((size,size),np.float32);reliable_sum=np.zeros_like(hits)
    clipped=0;contributions=0;mirrored=0;max_slope=math.tan(math.radians(c['maxTiltDegrees']-c['encodingTiltMarginDegrees']))
    for ids,t,ju,jv in tri:
        lo=np.maximum(0,np.floor(t.min(axis=0)*size-.5).astype(int));hi=np.minimum(size-1,np.ceil(t.max(axis=0)*size-.5).astype(int))
        xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1),np.arange(lo[1],hi[1]+1));uu=(xx+.5)/size;vv=(yy+.5)/size
        a=t[1]-t[0];b=t[2]-t[0];det=a[0]*b[1]-a[1]*b[0]
        qx=uu-t[0,0];qy=vv-t[0,1];w1=(qx*b[1]-qy*b[0])/det;w2=(a[0]*qy-a[1]*qx)/det;w0=1-w1-w2;inside=(w0>=-1e-6)&(w1>=-1e-6)&(w2>=-1e-6)
        if not inside.any():continue
        x=xx[inside];y=yy[inside];u=uu[inside];v=vv[inside];weights=np.stack([w0[inside],w1[inside],w2[inside]],axis=1)
        nt=normalize(weights@np.array([tuple(m.corner_normals[i].vector)for i in ids]));tt=weights@np.array([tuple(m.loops[i].tangent)for i in ids]);tt=normalize(tt-nt*np.sum(nt*tt,axis=1)[:,None])
        sign=float(m.loops[ids[0]].bitangent_sign);mirrored+=sign<0;bt=np.cross(nt,tt)*sign
        ju=weights@np.array([differential[chart_key(i)][0]for i in ids]);jv=weights@np.array([differential[chart_key(i)][1]for i in ids]);j_u=normalize(ju);j_v=normalize(jv)
        r1=np.sum(tt*j_u,axis=1);s1=np.sum(bt*j_u,axis=1);r2=np.sum(tt*j_v,axis=1);s2=np.sum(bt*j_v,axis=1);det_frame=r1*s2-r2*s1
        safe=det_frame>.15
        ref,dxdy=sample_reference(reference,u,v,scale_u,scale_v,c);hx=-ref[:,0]/ref[:,2];hy=-ref[:,1]/ref[:,2]
        gt=np.divide(hx*s2-hy*s1,det_frame,out=np.zeros_like(hx),where=safe);gb=np.divide(hy*r1-hx*r2,det_frame,out=np.zeros_like(hy),where=safe)
        length=np.sqrt(gt*gt+gb*gb);factor=np.minimum(1,max_slope/np.maximum(length,1e-12));clipped+=int((factor<1).sum());contributions+=len(u);gt*=factor;gb*=factor
        authored=normalize(np.stack([-gt,-gb,np.ones_like(gt)],axis=1));blend=np.clip((v-.25)*size/c['capGuardNormalPixels'],0,1);authored[:,:2]*=blend[:,None];authored=normalize(authored)
        d_uv=-dxdy*scale_v/scale_u;world=ju*d_uv[:,None]-jv;world-=nt*np.sum(world*nt,axis=1)[:,None];world=normalize(world)
        flow=normalize(np.stack([np.sum(world*tt,axis=1),np.sum(world*bt,axis=1)],axis=1))
        normal_sum[y,x]+=authored;flow_sum[y,x]+=flow;hits[y,x]+=1;reliable_sum[y,x]+=safe
    valid=hits>0;normals=np.zeros_like(normal_sum);normals[:,:,2]=1;normals[valid]=normalize(normal_sum[valid]);directions=np.zeros_like(flow_sum);directions[:,:,1]=-1;directions[valid]=normalize(flow_sum[valid])
    coherence=np.zeros_like(hits);coherence[valid]=np.linalg.norm(flow_sum[valid]/hits[valid,None],axis=1);confidence=np.clip((coherence-.75)/.23,0,1);confidence[valid]*=reliable_sum[valid]/hits[valid]
    assert np.isfinite(normals).all()and np.isfinite(directions).all();tilt=np.degrees(np.arccos(np.clip(normals[valid,2],-1,1)));assert float(tilt.max())<=c['maxTiltDegrees']+.001
    nimage=np.ones((size,size,4),np.float32);nimage[:,:,:3]=normals*.5+.5;nimage[:size//4,:,:3]=(.5,.5,1)
    save_png(out/'LakeGirl_HairBundle_N.png',nimage)
    flow_size=c['flowResolution'];assert size==2*flow_size
    weighted=directions*confidence[:,:,None]*valid[:,:,None];small=weighted.reshape(flow_size,2,flow_size,2,2).mean(axis=(1,3));small=normalize(small)
    fimage=np.zeros((flow_size,flow_size,4),np.float32);fimage[:,:,:2]=small*.5+.5;fimage[:,:,2]=confidence.reshape(flow_size,2,flow_size,2).mean(axis=(1,3));fimage[:,:,3]=valid.reshape(flow_size,2,flow_size,2).mean(axis=(1,3));fimage[:flow_size//4]=(.5,.5,0,0)
    save_png(out/'LakeGirl_HairBundle_Flow.png',fimage)
    # Reopen encoded payloads to verify lower-row bytes/channels and normal sign.
    payloads=[];encoded_normal_stats={}
    for filename,res in [('LakeGirl_HairBundle_N.png',size),('LakeGirl_HairBundle_Flow.png',flow_size)]:
        path=out/filename;im=bpy.data.images.load(str(path),check_existing=False);im.colorspace_settings.name='Non-Color';data=np.empty(res*res*4,np.float32);im.pixels.foreach_get(data);data=np.rint(data.reshape(res,res,4)*255).astype(np.uint8)
        expected=np.array([128,128,255,255]if filename.endswith('_N.png')else[128,128,0,0],np.uint8);assert np.array_equal(data[:res//4],np.broadcast_to(expected,data[:res//4].shape));bpy.data.images.remove(im)
        if filename.endswith('_N.png'):
            xy=data[:,:,:2].astype(np.float32)/255*2-1;encoded_tilt=np.degrees(np.arcsin(np.clip(np.linalg.norm(xy,axis=2),0,1)));assert float(encoded_tilt.max())<=c['maxTiltDegrees']
            encoded_normal_stats={'maxBC5XYDecodedTiltDegrees':float(encoded_tilt.max()),'capRmsBC5XYDecodedTiltDegrees':float(np.sqrt(np.mean(encoded_tilt[valid]**2))),'preCompressionOnly':True}
        assert sha(path)==c['expectedPayloads'][filename]['sha256'],'Authored map differs from accepted payload; retain fresh output and revalidate before promotion'
        payloads.append({'file':filename,'sha256':sha(path),'bytes':path.stat().st_size,'dimensions':[res,res],'lowerQuarterExact':True})
    assert geometry_before==[mesh_hash(o)for o in lods];assert pins=={str(x.relative_to(root)):sha(x)for x in pinned}
    assert rest_before=={b.name:(b.parent.name if b.parent else None,[float(v)for row in b.matrix_local for v in row])for b in rig.data.bones}
    receipt={'scope':c['scope'],'sourceInputs':pins,'missingOptionalPreservationObservations':missing_observations,'sourceDataUnchanged':True,'sourceMeshRigUVWeightsSlotsUnchanged':True,'rendered':False,'cyclesBakeUsed':False,'gpuUsed':False,
             'authorSha256':sha(Path(__file__)),'recipeSha256':sha(args.recipe),'blenderVersion':bpy.app.version_string,'referenceGeometry':reference_receipt,'referenceGeometrySha256':sha(out/'AuthoredBundleRelief.blend'),
             'measuredMainMetresPerUV':[scale_u,scale_v],'resolvedMainBundleWidthMM':1000*c['referenceWidthMetres']/c['bundleCyclesAcrossReference'],'mainBundlePixels':c['referenceWidthMetres']/c['bundleCyclesAcrossReference']/scale_u*size,
             'capNormalTiltDegrees':{'rms':float(np.sqrt(np.mean(tilt*tilt))),'median':float(np.median(tilt)),'p95':float(np.percentile(tilt,95)),'max':float(tilt.max())},
             'slopeCappedContributions':clipped,'rasterContributions':contributions,'validNormalPixels':int(valid.sum()),'maxUVOverlap':float(hits.max()),'flowCoherence':{'median':float(np.median(coherence[valid])),'p05':float(np.percentile(coherence[valid],5)),'min':float(coherence[valid].min())},
             'encodedNormalStats':encoded_normal_stats,'unsafeChartPixelContributions':int(contributions-reliable_sum.sum()),
             'mirroredTriangles':mirrored,'flowGPositiveValidPixels':int((directions[valid,1]>0).sum()),'flowConvention':c['flowConvention'],'normalConvention':c['normalConvention'],
             'payloads':payloads,'maxRssMiB':resource.getrusage(resource.RUSAGE_SELF).ru_maxrss/1024,'actualUnityGPUAcceptance':'PENDING; isolated later comparison only'}
    (out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');print('[hair-surface-pilot] AUTHORED',json.dumps(receipt),flush=True)


if __name__=='__main__':main()
