"""Prepare reviewable LakeGirl-only Unity input/policy under Logs; never edits Assets."""
import argparse
import difflib
import hashlib
import json
import re
import shutil
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def replace_once(text,old,new):
    assert text.count(old)==1,('Importer baseline changed',old[:100]);return text.replace(old,new,1)


def importer_policy(text):
    text=replace_once(text,'public bool unique;','public bool unique;\n            public bool oralAnatomy;')
    text=replace_once(text,'public static void Rebuild()\n        {','public static void Rebuild() => RebuildInternal(false);\n\n        static void RebuildInternal(bool lakeGirlOnly)\n        {')
    text=replace_once(text,'if (m == null || string.IsNullOrEmpty(m.id)) continue;','if (m == null || string.IsNullOrEmpty(m.id)) continue;\n                if (lakeGirlOnly && m.id != "LakeGirl") continue;')
    text=replace_once(text,'metas.Sort((a, b) => string.CompareOrdinal(a.id, b.id));','metas.Sort((a, b) => string.CompareOrdinal(a.id, b.id));\n            if (lakeGirlOnly && (metas.Count != 1 || !metas[0].oralAnatomy))\n                throw new System.InvalidOperationException("LakeGirl oral pilot metadata is not installed.");')
    text=replace_once(text,'ConfigureTexture(TexturesDir + "/Skin_VeinMask.png", TexKind.Data, 2048);','if (!lakeGirlOnly) ConfigureTexture(TexturesDir + "/Skin_VeinMask.png", TexKind.Data, 2048);')
    text=replace_once(text,'ConfigureTexture(TexturesDir + "/" + m.textures.skin, TexKind.SkinAlbedo, 2048);','if (!lakeGirlOnly)\n                {\n                ConfigureTexture(TexturesDir + "/" + m.textures.skin, TexKind.SkinAlbedo, 2048);')
    text=replace_once(text,'ConfigureTexture(TexturesDir + "/" + m.textures.eyes, TexKind.EyeAlbedo, 1024);','ConfigureTexture(TexturesDir + "/" + m.textures.eyes, TexKind.EyeAlbedo, 1024);\n                }')
    for field,normal,oral,size,oral_size in [('clothA','Albedo','OralAlbedo',2048,4096),('clothN','Normal','OralNormal',2048,4096),('clothM','Mask','OralMask',1024,2048)]:
        old=f'ConfigureTexture(TexturesDir + "/" + m.textures.{field}, TexKind.{normal}, {size});'
        new=f'ConfigureTexture(TexturesDir + "/" + m.textures.{field}, m.oralAnatomy ? TexKind.{oral} : TexKind.{normal}, m.oralAnatomy ? {oral_size} : {size});'
        text=replace_once(text,old,new)
    text=replace_once(text,'ConfigureTexture(TexturesDir + "/" + m.textures.hairA, TexKind.Hair, 2048);','if (!lakeGirlOnly) ConfigureTexture(TexturesDir + "/" + m.textures.hairA, TexKind.Hair, 2048);')
    text=replace_once(text,'catalog.entries.Clear();','if (!lakeGirlOnly) catalog.entries.Clear();\n            else if (!catalog.entries.Exists(e => e.id == "LakeGirl"))\n                throw new System.InvalidOperationException("Existing LakeGirl catalog entry is required for the pilot.");')
    text=replace_once(text,'if (entry != null) { catalog.entries.Add(entry); ok++; }','if (entry != null)\n                    {\n                        if (lakeGirlOnly) catalog.entries[catalog.entries.FindIndex(e => e.id == entry.id)] = entry;\n                        else catalog.entries.Add(entry);\n                        ok++;\n                    }')
    marker='        static Avatar LoadAvatar(string fbx)'
    addition='''        // Explicit pilot entry: reuse shared maps and leave other 24 catalog entries/prefabs alone.
        public static void RebuildLakeGirlOralPilotBatch()
        {
            try { RebuildInternal(true); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

'''
    text=replace_once(text,marker,addition+marker)
    text=replace_once(text,'enum TexKind { Albedo, SkinAlbedo, EyeAlbedo, Normal, SkinNormal, SkinSurface, Mask, Hair, Data }','enum TexKind { Albedo, SkinAlbedo, EyeAlbedo, Normal, SkinNormal, SkinSurface, Mask, Hair, Data, OralAlbedo, OralNormal, OralMask }')
    text=replace_once(text,'bool skinSurface = kind == TexKind.SkinNormal || kind == TexKind.SkinSurface;','bool skinSurface = kind == TexKind.SkinNormal || kind == TexKind.SkinSurface;\n            bool oral = kind == TexKind.OralAlbedo || kind == TexKind.OralNormal || kind == TexKind.OralMask;\n            bool mask = kind == TexKind.Mask || kind == TexKind.OralMask;\n            if (oral) ti.npotScale = TextureImporterNPOTScale.None;')
    text=replace_once(text,'kind == TexKind.Normal || kind == TexKind.SkinNormal ? TextureImporterType.NormalMap','kind == TexKind.Normal || kind == TexKind.SkinNormal || kind == TexKind.OralNormal ? TextureImporterType.NormalMap')
    text=replace_once(text,'ti.sRGBTexture = kind == TexKind.Albedo || portraitAlbedo || kind == TexKind.Hair;','ti.sRGBTexture = kind == TexKind.Albedo || portraitAlbedo || kind == TexKind.OralAlbedo || kind == TexKind.Hair;')
    text=replace_once(text,'ti.mipmapEnabled = kind != TexKind.Mask;','ti.mipmapEnabled = !mask;')
    text=replace_once(text,'ti.filterMode = kind == TexKind.Mask ? FilterMode.Point : FilterMode.Trilinear;','ti.filterMode = mask ? FilterMode.Point : FilterMode.Trilinear;')
    text=replace_once(text,'ti.anisoLevel = kind == TexKind.Mask ? 0 : 4;','ti.anisoLevel = mask ? 0 : 4;')
    text=replace_once(text,'ti.textureCompression = kind == TexKind.Mask || kind == TexKind.Hair ?','ti.textureCompression = mask || kind == TexKind.Hair ?')
    text=replace_once(text,'if (skinSurface || portraitAlbedo)','if (skinSurface || portraitAlbedo || oral)')
    text=replace_once(text,'format = kind == TexKind.SkinNormal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7,','format = kind == TexKind.SkinNormal || kind == TexKind.OralNormal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7,')
    start=text.index('                if (!lakeGirlOnly)\n                {\n                ConfigureTexture(TexturesDir + "/" + m.textures.skin')
    start=text.index('\n',text.index('                {',start))+1
    end=text.index('                }\n                ConfigureTexture(TexturesDir + "/" + m.textures.clothA',start)
    text=text[:start]+''.join('    '+line if line.strip() else line for line in text[start:end].splitlines(True))+text[end:]
    return text


def main():
    p=argparse.ArgumentParser();p.add_argument('--stage',default='Logs/unity-oral-pilot-lakegirl-v2-stage');args=p.parse_args();stage=(ROOT/args.stage).resolve();assert ROOT/'Logs' in stage.parents and not stage.exists();stage.mkdir(parents=True)
    roundtrip=json.loads((ROOT/'Logs/skin-realism-20261127/oral-v2-fbx-roundtrip.json').read_text());pilot=ROOT/'ArtSource/Characters/OralPilotV2';audit=json.loads((pilot/'audit.json').read_text());assert sha(pilot/'LakeGirl_OralPilotV2.fbx')==roundtrip['pilot_fbx_sha256']==audit['fbx_sha256']
    models=ROOT/'Assets/Parasite/Art/Characters/Models';textures=ROOT/'Assets/Parasite/Art/Characters/Textures';resources=ROOT/'Assets/Parasite/Resources/Characters';source=ROOT/'Assets/Parasite/Editor/CharacterModelImporter.cs'
    pins=[]
    for path in [source,*[ROOT/'Assets/Parasite/Shaders'/n for n in ['Character.shader','CharacterInput.hlsl','CharacterForwardPass.hlsl']],ROOT/'Assets/Parasite/Scripts/Characters/HumanoidRig.cs',models/'LakeGirl.fbx',models/'LakeGirl.fbx.meta',models/'LakeGirl.json',models/'LakeGirl.json.meta',resources/'Prefabs/LakeGirl.prefab',resources/'Prefabs/LakeGirl.prefab.meta',resources/'CharacterCatalog.asset',resources/'CharacterCatalog.asset.meta']:
        assert path.is_file(),path;pins.append(path)
    meta=json.loads((models/'LakeGirl.json').read_text());guid_moves=[];incoming=[]
    for key in ['clothA','clothN','clothM']:
        live=textures/meta['textures'][key];pins.extend([live,live.with_name(live.name+'.meta')]);target=live.with_suffix('.png');guid=re.search(r'^guid: (\w+)$',live.with_name(live.name+'.meta').read_text(),re.M).group(1);guid_moves.append({'from':str(live.relative_to(ROOT)),'to':str(target.relative_to(ROOT)),'guid':guid,'assetDatabaseMoveRequired':live!=target});meta['textures'][key]=str(target.relative_to(textures));src=pilot/'Textures'/('LakeGirl_OralPilotV2_Cloth_'+key[-1]+'.png');incoming.append((src,'incoming/Textures/'+target.name,str(target.relative_to(ROOT))))
    for role in ['Skin','Eyes','Hair','Cloth']:
        path=resources/'Materials'/('LakeGirl_'+role+'.mat');pins.extend([path,path.with_name(path.name+'.meta')])
    for path in list(pins):
        sidecar=path.with_name(path.name+'.meta')
        if path.suffix!='.meta' and sidecar.is_file() and sidecar not in pins:pins.append(sidecar)
    baseline={str(p.relative_to(ROOT)):sha(p)for p in pins};meta['oralAnatomy']=True;meta['lodTris']=[roundtrip['lods'][str(i)]['pilotTriangles']for i in range(3)];meta['assets']+=[{'kind':'teeth','name':'teeth_base'},{'kind':'tongue','name':'tongue01'}];meta['oralPilot']={'revision':2,'outsideGeometryPreserved':True,'clothVScale':[32,33],'albedoNormalDimensions':[2048,2112],'maskDimensions':[1024,1056],'guardPixelsA_N':8,'guardPixelsM':4,'sourceBlendSha256':audit['blend_sha256']}
    model_out=stage/'incoming/Models';model_out.mkdir(parents=True);(model_out/'LakeGirl.json').write_text(json.dumps(meta,indent=1)+'\n');incoming.append((pilot/'LakeGirl_OralPilotV2.fbx','incoming/Models/LakeGirl.fbx','Assets/Parasite/Art/Characters/Models/LakeGirl.fbx'))
    incoming_info=[]
    for src,rel,target in incoming:
        dest=stage/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dest);incoming_info.append({'file':rel,'target':target,'sha256':sha(dest),'bytes':dest.stat().st_size})
    incoming_info.append({'file':'incoming/Models/LakeGirl.json','target':'Assets/Parasite/Art/Characters/Models/LakeGirl.json','sha256':sha(model_out/'LakeGirl.json'),'bytes':(model_out/'LakeGirl.json').stat().st_size})
    original=source.read_text();candidate=importer_policy(original);policy=stage/'policy';policy.mkdir();(policy/'CharacterModelImporter.cs').write_text(candidate);patch=''.join(difflib.unified_diff(original.splitlines(True),candidate.splitlines(True),fromfile='a/Assets/Parasite/Editor/CharacterModelImporter.cs',tofile='b/Assets/Parasite/Editor/CharacterModelImporter.cs'));(policy/'CharacterModelImporter.patch').write_text(patch)
    # Record root's current hair policy and prove the candidate keeps it verbatim.
    hair=original[original.index('            var hair = Mat('):original.index('            var cloth = Mat(')];assert hair in candidate;(policy/'pinned-root-hair-block.txt').write_text(hair)
    other={}
    for path in models.glob('*.json'):
        if path.stem=='LakeGirl':continue
        cid=path.stem;paths=[path,path.with_name(path.name+'.meta'),path.with_suffix('.fbx'),path.with_suffix('.fbx.meta'),resources/'Prefabs'/(cid+'.prefab'),resources/'Prefabs'/(cid+'.prefab.meta')]
        for role in ['Skin','Eyes','Hair','Cloth']:paths.extend([resources/'Materials'/(cid+'_'+role+'.mat'),resources/'Materials'/(cid+'_'+role+'.mat.meta')])
        own=json.loads(path.read_text())
        for key in ['clothA','clothN','clothM','hairA']:
            texture=textures/own['textures'][key];paths.extend([texture,texture.with_name(texture.name+'.meta')])
        for item in paths:
            if item.is_file():other[str(item.relative_to(ROOT))]=sha(item)
    backup=stage/'baseline'
    for path in pins:
        dest=backup/path.relative_to(ROOT);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,dest)
    verify=stage/'verification';verify.mkdir();shutil.copy2(ROOT/'Logs/skin-realism-20261127/oral-v2-fbx-roundtrip.json',verify/'fbx-roundtrip.json');shutil.copy2(pilot/'audit.json',verify/'oral-authoring.json')
    guids={str(path.relative_to(ROOT)):re.search(r'^guid: (\w+)$',path.read_text(),re.M).group(1)for path in pins if path.suffix=='.meta'}
    manifest={'scope':'LakeGirl-only isolated Unity integration stage; Assets unchanged; other24 not rolled out','project':str(ROOT),'baselineLiveSha256':baseline,'existingGuidPins':guids,'other24Pins':other,'incoming':incoming_info,'guidPreservingTextureMoves':guid_moves,'importerCandidateSha256':sha(policy/'CharacterModelImporter.cs'),'importerBaseSha256':sha(source),'newLodTriangles':meta['lodTris'],'rootHairBlockUnchanged':True,'rootHairState':'Transparent SrcAlpha/OneMinusSrcAlpha, ZWrite1; scalp alpha saturate(a/.4) only in forward; shadow/depth cutoff.4','unityEntry':'Parasite.EditorTools.CharacterModelImporter.RebuildLakeGirlOralPilotBatch','unityImportExecuted':False,'fbxRoundtrip':roundtrip}
    assert all(sha(ROOT/path)==value for path,value in baseline.items()),'Live source changed while staging';(stage/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');print('[oral-stage] READY',str(stage),json.dumps({'incoming':len(incoming_info),'lodTriangles':meta['lodTris'],'pinnedFiles':len(baseline),'other24PinnedFiles':len(other)}))


if __name__=='__main__':main()
