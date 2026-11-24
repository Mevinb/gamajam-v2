"""CPU-only FBX roundtrip checks before Unity publication: valid faces and asymmetric authored anchors.
Unity's imported coordinates are (Blender X, Blender Z, Blender Y) for these export settings,
confirmed by actual Unity EditMode bounds logs rather than assumed from FBX dimensions.
"""
import bpy, sys
from pathlib import Path
from mathutils import Vector
root=Path(sys.argv[sys.argv.index('--')+1])

def close(value,expected,tolerance=.03):
    assert abs(value-expected)<=tolerance, f'expected {expected}, got {value}'

for asset in ['ContainmentLaboratory','ServiceConduit','LakesideBench','LakesideShore']:
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(root/(asset+'.fbx')))
    bad=0;anchors={};tris=0
    for ob in bpy.context.scene.objects:
        if ob.type!='MESH':continue
        ob.data.calc_loop_triangles();tris+=len(ob.data.loop_triangles)
        for tri in ob.data.loop_triangles:
            a,b,c=[ob.matrix_world@ob.data.vertices[i].co for i in tri.vertices]
            if (b-a).cross(c-a).length<1e-12:bad+=1
        material=ob.data.materials[0].name.split('.')[0] if ob.data.materials else ''
        pts=[ob.matrix_world@vert.co for vert in ob.data.vertices]
        low=Vector(tuple(min(point[i] for point in pts) for i in range(3)))
        high=Vector(tuple(max(point[i] for point in pts) for i in range(3)));center=(low+high)*.5
        anchors[material]=(center.x,center.z,center.y)
    assert bad==0, f'{asset}: {bad} zero-area exported triangles'
    if asset=='ServiceConduit':
        center=anchors['CinemaWarm'];close(center[0],.53);close(center[2],5,.1)
    if asset=='LakesideShore':
        center=anchors['CinemaTimber'];close(center[0],6,.05);close(center[2],5.22,.1)
        assert -12.3<anchors['CinemaShore'][2]<-11.5
    print('[cinematic-verify]',asset,'PASS',tris,'triangles; zero invalid faces; asymmetric anchors',anchors,flush=True)

# Saved source paths must resolve from their final repository location, not the temporary generation directory.
project=Path(__file__).resolve().parents[3]
final_source=project/'ArtSource/Cinematics'
final_textures=(project/'Assets/Parasite/Resources/Cinematics/Textures').resolve()
for source in sorted((root.parent/'Source').glob('*.blend')):
    bpy.ops.wm.open_mainfile(filepath=str(source))
    count=0
    for image in bpy.data.images:
        if not image.filepath:continue
        assert image.filepath.startswith('//'), f'Non-portable source image: {source.name} {image.filepath}'
        destination=(final_source/image.filepath[2:]).resolve()
        assert destination.parent==final_textures, f'Temporary or wrong source image path: {image.filepath}'
        assert (root/'Textures'/destination.name).is_file()
        count+=1
    print('[cinematic-verify]',source.name,'PASS',count,'repo-relative image paths',flush=True)
