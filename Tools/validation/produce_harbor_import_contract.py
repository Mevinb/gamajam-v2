#!/usr/bin/env python3
"""Produce a JsonUtility-compatible Harbor import contract from frozen authored evidence and git baseline. Read-only outside --out."""
import argparse,hashlib,json,math,re,subprocess
from pathlib import Path
P=Path(__file__).resolve().parents[2]
EXPECTED_SHA='cb923b5a6b79e29ecb84ad5badd70c1f7ad2dca381b68f7ec3c63e97bda4ebf5'
EXPECTED={'HarborValveBase':848,'HarborValveWheel':456,'HarborGaugeBase':1008,'HarborGaugeNeedle':132,'HarborLiftGate':1076,'HarborConveyor':3020,'HarborSealedCrate':728,'HarborRelayPad':828,'HarborEmitter':232}
sha=lambda b:hashlib.sha256(b).hexdigest()
def main():
 a=argparse.ArgumentParser();a.add_argument('--out',type=Path,default=P/'Logs/harbor-art/import-contract.json');args=a.parse_args()
 manifest_path='ArtSource/Harbor/production-manifest.json';data=(P/manifest_path).read_bytes();assert sha(data)==EXPECTED_SHA,'Frozen production manifest changed'
 m=json.loads(data);assert len(m['exports'])==9 and {r['name']:r['triangles'] for r in m['exports']}==EXPECTED
 models=[]
 for row in m['exports']:
  rel=row['file'];assert rel=='Exports/'+row['name']+'.fbx';source='ArtSource/Harbor/'+rel
  digest=sha((P/source).read_bytes());assert digest==m['files'][rel]['sha256']
  lo,hi=row['bounds'];assert len(lo)==len(hi)==3 and all(math.isfinite(v) for p in (lo,hi) for v in p) and all(lo[i]<hi[i] for i in range(3))
  models.append({'name':row['name'],'sourcePath':source,'assetPath':'Assets/Parasite/Resources/Harbor/'+row['name']+'.fbx','sha256':digest,'triangles':row['triangles'],'min':dict(zip('xyz',lo)),'max':dict(zip('xyz',hi))})
 commit=subprocess.check_output(['git','rev-parse','19bcdba^{commit}'],cwd=P,text=True).strip();baseline=[]
 root='Assets/Parasite/Art/CityGatePilot/'
 for rel in [root+'Textures/CityGatePilot_'+s+'.png'+ext for s in ('A','N','M') for ext in ('','.meta')]+[root+'CityGatePilot_SharedPBR.mat',root+'CityGatePilot_SharedPBR.mat.meta']:
  original=subprocess.check_output(['git','show',commit+':'+rel],cwd=P);baseline.append({'path':rel,'sha256':sha(original),'bytes':len(original)})
 textures=[]
 for suffix,prop in [('A','_BaseMap'),('N','_BumpMap'),('M','_MetallicGlossMap')]:
  rel=root+'Textures/CityGatePilot_'+suffix+'.png';meta=subprocess.check_output(['git','show',commit+':'+rel+'.meta'],cwd=P,text=True)
  guid=re.search(r'^guid: ([a-f0-9]{32})$',meta,re.M).group(1)
  textures.append({'suffix':suffix,'property':prop,'path':rel,'guid':guid,'normal':suffix=='N','sRGB':suffix=='A'})
 atlas=[]
 for row in [m['source_atlas']['gate_blend'],*m['source_atlas']['textures'].values()]:
  b=(P/row['path']).read_bytes();assert sha(b)==row['sha256'];atlas.append({'path':row['path'],'sha256':row['sha256'],'bytes':len(b)})
 contract={'schema':1,'scope':'Null Editor import metadata and source-integrity verification; no rendering/navigation/gameplay proof','sourceManifestPath':manifest_path,'sourceManifestSha256':EXPECTED_SHA,'producerSha256':sha(Path(__file__).read_bytes()),'baselineCommit':commit,'distinctTriangles':8328,'models':models,'unchangedGateFiles':baseline,'sourceAtlasFiles':atlas,'textures':textures}
 args.out.parent.mkdir(parents=True,exist_ok=True);args.out.write_text(json.dumps(contract,indent=2)+'\n');print(json.dumps({'path':str(args.out),'sha256':sha(args.out.read_bytes()),'baselineCommit':commit,'models':len(models),'baselineFiles':len(baseline),'distinctTriangles':sum(r['triangles'] for r in models)},indent=2))
if __name__=='__main__':main()
