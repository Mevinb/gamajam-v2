#!/usr/bin/env python3
"""Package a verified private Linux candidate without changing it. Dry by default; --go publishes a fresh archive."""
from __future__ import annotations
import argparse,gzip,hashlib,io,json,os,re,shutil,stat,tarfile,tempfile,uuid
from datetime import datetime,timezone
from pathlib import Path,PurePosixPath
from urllib.parse import unquote,urlsplit

class PackageError(RuntimeError):pass
TOP_FILES={'Parasite.x86_64','UnityPlayer.so','play.sh','BUILD_INFO.json','libdecor-0.so.0','libdecor-cairo.so'}
REQUIRED={'Parasite.x86_64','UnityPlayer.so','play.sh','BUILD_INFO.json','Parasite_Data/globalgamemanagers','Parasite_Data/Managed/Parasite.Runtime.dll'}
DOCS_REQUIRED={'README.md','CONTROLS.md','NATIVE_SCOPE.md','THIRD_PARTY_ASSETS.md','Licenses/README.md'}
EXCLUDED=re.compile(r'^.+_(?:BurstDebugInformation_DoNotShip|BackUpThisFolder_ButDontShipItWithYourGame)$')
LINK=re.compile(r'!?\[[^\]\n]*\]\((<[^>\n]+>|[^\s)]+)[^)]*\)')
MANIFEST_NAME='PACKAGE_MANIFEST.json'

def require(condition,message):
 if not condition:raise PackageError(message)
def digest(path):
 h=hashlib.sha256()
 with Path(path).open('rb') as f:
  for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
 return h.hexdigest()
def canonical(value):return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':')).encode()).hexdigest()
def json_bytes(value):return (json.dumps(value,indent=2)+'\n').encode()
def no_duplicate_keys(pairs):
 result={}
 for key,value in pairs:
  require(key not in result,'Duplicate JSON key: '+key);result[key]=value
 return result
def read_json(path):
 try:return json.loads(Path(path).read_text(),object_pairs_hook=no_duplicate_keys)
 except (ValueError,OSError) as error:raise PackageError('Invalid JSON '+str(path)+': '+str(error)) from error
def relative_name(value):
 require(isinstance(value,str) and value and '\\' not in value and not any(ord(c)<32 for c in value),'Unsafe entry name: '+repr(value))
 path=PurePosixPath(value);require(not path.is_absolute() and all(p not in ('','.','..') for p in value.split('/')) and str(path)==value,'Unsafe entry path: '+value)
 return path

def canonical_path(project,path):
 require(isinstance(path,(str,Path)),'Missing/invalid path argument');path=Path(path);path=path if path.is_absolute() else project/path
 require(path.absolute()==path.resolve(),'Symlink/alias path is not accepted: '+str(path));return path.resolve()
def snapshot(root):
 rows=[];seen=set()
 for path in [root,*sorted(root.rglob('*'))]:
  s=path.lstat();name='.' if path==root else path.relative_to(root).as_posix()
  if name!='.':relative_name(name)
  require(name.casefold() not in seen,'Case-insensitive duplicate input path: '+name);seen.add(name.casefold())
  require(not stat.S_ISLNK(s.st_mode),'Symbolic links are not accepted in package inputs: '+str(path))
  require(not s.st_mode&(stat.S_ISUID|stat.S_ISGID),'Privileged input mode rejected: '+str(path))
  row={'path':name,'mode':stat.S_IMODE(s.st_mode),'uid':s.st_uid,'gid':s.st_gid,'mtime_ns':s.st_mtime_ns,'device':s.st_dev,'inode':s.st_ino}
  if stat.S_ISDIR(s.st_mode):row['type']='directory'
  else:
   require(stat.S_ISREG(s.st_mode),'Special input file rejected: '+str(path));row.update(type='file',bytes=s.st_size,sha256=digest(path))
  rows.append(row)
 return {'entries':rows,'tree_sha256':canonical(rows),'bytes':sum(x.get('bytes',0) for x in rows)}

def validate_candidate(project,candidate,before):
 info=read_json(candidate/'BUILD_INFO.json');require(isinstance(info,dict),'BUILD_INFO must be an object');require(digest(candidate/'BUILD_INFO.json')==next(r['sha256'] for r in before['entries'] if r['path']=='BUILD_INFO.json'),'BUILD_INFO changed during validation');require(info.get('diagnostic_name')==candidate.name,'BUILD_INFO candidate name mismatch')
 require(isinstance(info.get('version'),str) and re.fullmatch(r'\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?',info['version']),'Missing/unsafe BUILD_INFO version')
 summary=info.get('build_summary',{});require(isinstance(summary,dict),'Invalid build summary');require(info.get('build_exit_code')==0 and summary.get('result')=='Succeeded' and summary.get('errors')==0,'Candidate lacks a successful zero-error native build receipt')
 evidence=project/'Logs/full-story-candidate/build-runs'/candidate.name.removeprefix('full-story-')
 output=canonical_path(project,info.get('output_hashes',''));source=canonical_path(project,info.get('source_snapshot',''))
 require(output==evidence/'build-output-hashes.json' and source==evidence/'source-snapshot-before.json','BUILD_INFO evidence paths do not identify this candidate build run')
 require(output.is_file() and source.is_file(),'Missing/nonregular original build evidence')
 require(digest(source)==info.get('source_snapshot_sha256'),'Original source snapshot SHA mismatch')
 state_path=canonical_path(project,evidence/'runner-state.json');require(state_path.is_file(),'Missing/nonregular original runner receipt');state=read_json(state_path);require(isinstance(state,dict),'Invalid original runner receipt')
 require(state.get('status')=='READY_NOT_RUN' and state.get('candidate_path')==str(candidate) and state.get('candidate_build_info_sha256')==digest(candidate/'BUILD_INFO.json'),'BUILD_INFO is not the originally published private candidate identity')
 require(state.get('native_build_exit_code')==0 and state.get('null_backend_marker_seen') is True and state.get('accepted_output_override') is True and state.get('source_unchanged_during_build') is True and state.get('preview_unchanged') is True,'Original runner does not prove the completed immutable-source Null build')
 require(state.get('candidate_output_tree_sha256')==info.get('output_tree_sha256'),'Runner/output manifest identity mismatch')
 original=read_json(output);require(isinstance(original,dict),'Invalid original output manifest');rows=original.get('files');require(isinstance(rows,list) and original.get('tree_sha256')==info.get('output_tree_sha256')==canonical(rows),'Original output manifest identity mismatch')
 require(original.get('entries')==len(rows) and original.get('total_bytes')==sum(r.get('bytes',0) for r in rows),'Original output manifest totals mismatch')
 actual={r['path']:r for r in before['entries'] if r['path']!='.'};expected={};folded=set()
 for row in rows:
  name=str(relative_name(row.get('path')));require(name.casefold() not in folded,'Duplicate/case-colliding original output entry: '+name);folded.add(name.casefold());expected[name]=row
  require(row.get('type') in ('file','directory'),'Original output manifest contains a link/special file: '+name)
  require(name in actual,'Missing original candidate output: '+name)
  for key in ('type','mode','uid','gid'):
   require(row.get(key)==actual[name].get(key),'Original output '+key+' mismatch: '+name)
  if row['type']=='file':
   for key in ('bytes','sha256','mtime_ns'):require(row.get(key)==actual[name].get(key),'Original file '+key+' mismatch: '+name)
 require('BUILD_INFO.json' not in expected,'Original output manifest must precede BUILD_INFO publication')
 require(set(actual)==set(expected)|{'BUILD_INFO.json'},'Candidate contains extra/unrecorded files: '+str(sorted(set(actual)-set(expected)-{'BUILD_INFO.json'})))
 require(REQUIRED<=set(actual),'Missing required player files: '+str(sorted(REQUIRED-set(actual))))
 selected=[];excluded=[]
 for row in before['entries']:
  name=row['path']
  if name=='.':continue
  top=PurePosixPath(name).parts[0]
  if EXCLUDED.fullmatch(top):excluded.append(name);continue
  require(top=='Parasite_Data' or (name in TOP_FILES and row['type']=='file'),'Unexpected candidate path outside runtime allowlist: '+name)
  selected.append(row)
 require(all(actual[x]['type']=='file' and actual[x]['bytes']>0 for x in REQUIRED),'Required player file is empty or not a regular file')
 return info,selected,excluded,{'path':str(output),'sha256':digest(output),'source_snapshot_path':str(source),'source_snapshot_sha256':digest(source),'runner_state_path':str(state_path),'runner_state_sha256':digest(state_path)}

def overlay_entries(overlay,before,runtime):
 names={r['path'].casefold() for r in runtime};selected=[]
 for row in before['entries']:
  name=row['path']
  if name=='.':continue
  require(name.casefold() not in names and PurePosixPath(name).parts[0].casefold() not in {x.casefold() for x in TOP_FILES|{'Parasite_Data',MANIFEST_NAME}},'Docs overlay collides with reserved/runtime path: '+name)
  if row['type']=='file':require(Path(name).suffix.lower() in ('.md','.txt','.png','.jpg','.jpeg','.pdf','.json'),'Unexpected docs file type: '+name)
  selected.append(row)
 files={r['path'] for r in selected if r['type']=='file'};require(DOCS_REQUIRED<=files,'Fresh docs overlay is missing: '+str(sorted(DOCS_REQUIRED-files)))
 return selected

def validate_links(overlay,docs,all_entries):
 files={r['path'] for r in all_entries if r['type']=='file'};verified=0
 for row in docs:
  if row['type']!='file' or not row['path'].lower().endswith('.md'):continue
  data=(overlay/row['path']).read_bytes();require(hashlib.sha256(data).hexdigest()==row['sha256'],'Docs changed during link validation: '+row['path'])
  for match in LINK.finditer(data.decode('utf-8')):
   target=match[1].strip('<>');url=urlsplit(target)
   if url.scheme or url.netloc:
    require(url.scheme in ('http','https','mailto'),'Nonportable document link scheme: '+target);continue
   if not url.path:continue
   decoded=unquote(url.path);require(not decoded.startswith('/') and '\\' not in decoded,'Absolute/unsafe document link: '+target)
   # Normalize '..' only within the virtual single archive root.
   parts=list(PurePosixPath(row['path']).parent.parts)
   for part in decoded.split('/'):
    if part in ('','.'):continue
    if part=='..':require(parts,'Document link escapes archive root: '+target);parts.pop()
    else:parts.append(part)
   require('/'.join(parts) in files,'Broken packaged document link in '+row['path']+': '+target);verified+=1
 return verified

def archive_mode(row):return 0o755 if row['type']=='directory' or row['path'] in ('play.sh','Parasite.x86_64') or row['mode']&0o111 else 0o644
def same_stat(s,row):
 return stat.S_ISREG(s.st_mode) and s.st_dev==row['device'] and s.st_ino==row['inode'] and s.st_size==row['bytes'] and stat.S_IMODE(s.st_mode)==row['mode'] and s.st_mtime_ns==row['mtime_ns']
def open_input(root,row,root_identity):
 # Open relative components without following links, including a parent replaced during packaging.
 descriptor=os.open(root,os.O_RDONLY|os.O_DIRECTORY|os.O_NOFOLLOW)
 try:
  actual=os.fstat(descriptor);require(actual.st_dev==root_identity['device'] and actual.st_ino==root_identity['inode'],'Input root was replaced while packaging')
  parts=PurePosixPath(row['path']).parts
  for part in parts[:-1]:
   child=os.open(part,os.O_RDONLY|os.O_DIRECTORY|os.O_NOFOLLOW,dir_fd=descriptor);os.close(descriptor);descriptor=child
  fd=os.open(parts[-1],os.O_RDONLY|os.O_NOFOLLOW,dir_fd=descriptor)
 finally:os.close(descriptor)
 f=os.fdopen(fd,'rb')
 if not same_stat(os.fstat(f.fileno()),row):f.close();raise PackageError('Input file changed/replaced before archive read: '+row['path'])
 return f
class HashReader:
 def __init__(self,source):self.source=source;self.hash=hashlib.sha256();self.bytes=0
 def read(self,n=-1):
  b=self.source.read(n);self.hash.update(b);self.bytes+=len(b);return b

def tar_info(name,row):
 t=tarfile.TarInfo(name);t.uid=t.gid=0;t.uname=t.gname='';t.mtime=row['mtime_ns']//1_000_000_000;t.mode=archive_mode(row)
 t.type=tarfile.DIRTYPE if row['type']=='directory' else tarfile.REGTYPE;t.size=row.get('bytes',0) if row['type']=='file' else 0;return t

def write_archive(path,prefix,payload,roots,snapshots,manifest):
 with path.open('xb') as raw:
  with gzip.GzipFile(filename='',mode='wb',fileobj=raw,compresslevel=1,mtime=0) as zipped:
   with tarfile.open(fileobj=zipped,mode='w|',format=tarfile.PAX_FORMAT) as archive:
    rootrow={'type':'directory','mtime_ns':0,'mode':0o755};archive.addfile(tar_info(prefix,rootrow))
    for row in payload:
     name=prefix+'/'+row['path'];info=tar_info(name,row)
     if row['type']=='directory':archive.addfile(info);continue
     with open_input(roots[row['origin']],row,snapshots[row['origin']]['entries'][0]) as source:
      reader=HashReader(source);archive.addfile(info,reader)
      require(reader.bytes==row['bytes'] and reader.hash.hexdigest()==row['sha256'] and same_stat(os.fstat(source.fileno()),row),'Input changed during archive read: '+row['path'])
    info=tarfile.TarInfo(prefix+'/'+MANIFEST_NAME);info.mode=0o644;info.mtime=0;info.size=len(manifest);archive.addfile(info,io.BytesIO(manifest))
  raw.flush();os.fsync(raw.fileno())

def verify_archive(path,prefix,payload,manifest):
 expected={prefix+'/'+r['path']:r for r in payload};seen=set()
 with gzip.open(path,'rb') as compressed:
  with tarfile.open(fileobj=compressed,mode='r|') as archive:
   for member in archive:
    require(member.name not in seen,'Duplicate archive entry: '+member.name);seen.add(member.name);relative_name(member.name)
    if member.name==prefix:require(member.isdir() and member.mode==0o755,'Invalid archive root');continue
    if member.name==prefix+'/'+MANIFEST_NAME:
     require(member.isfile() and archive.extractfile(member).read()==manifest,'Internal package manifest mismatch');continue
    require(member.name in expected,'Unexpected archive entry: '+member.name);row=expected[member.name]
    require(member.mode==archive_mode(row) and (member.isdir() if row['type']=='directory' else member.isfile()),'Archive type/mode mismatch: '+member.name)
    if row['type']=='file':
     h=hashlib.sha256();length=0
     with archive.extractfile(member) as f:
      for b in iter(lambda:f.read(1024*1024),b''):h.update(b);length+=len(b)
     require(length==row['bytes'] and h.hexdigest()==row['sha256'],'Archive payload hash mismatch: '+member.name)
  while compressed.read(1024*1024):pass # Consume gzip trailer/CRC even after tar's end markers.
 require(seen==set(expected)|{prefix,prefix+'/'+MANIFEST_NAME},'Archive entry inventory mismatch')

def publish_fresh(pairs):
 published=[]
 try:
  # Hard-link publication is atomic and refuses existing names; temp files are on the same release filesystem.
  for source,destination in pairs:os.link(source,destination);published.append((source,destination))
 except BaseException:
  for source,destination in reversed(published):
   if destination.exists() and destination.stat().st_ino==source.stat().st_ino:destination.unlink()
  raise

def package(project,candidate,out,docs_overlay,go=False):
 project=Path(project).resolve();candidate=canonical_path(project,candidate);out=canonical_path(project,out);overlay=canonical_path(project,docs_overlay)
 require(candidate.is_dir() and candidate.parent==project/'Builds/Candidates' and candidate.name.startswith('full-story-'),'Input must be an explicit private Builds/Candidates/full-story-* directory')
 release=project/'Builds/Releases';require(out.parent==release and re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]*\.tar\.gz',out.name),'Output must be a fresh .tar.gz directly under Builds/Releases')
 require(overlay.is_dir() and not overlay.is_relative_to(candidate) and not candidate.is_relative_to(overlay),'Docs overlay must be a separate directory')
 destinations={key:Path(str(out)+suffix) for key,suffix in [('archive',''),('sha256','.sha256'),('manifest','.manifest.json'),('before','.inputs-before.json'),('after','.inputs-after.json'),('receipt','.receipt.json')]}
 for path in destinations.values():require(not path.exists() and not path.is_symlink(),'Fresh output already exists: '+str(path))
 before={'candidate':snapshot(candidate),'docs':snapshot(overlay)}
 info,runtime,excluded,evidence=validate_candidate(project,candidate,before['candidate']);docs=overlay_entries(overlay,before['docs'],runtime)
 payload=[dict(row,origin=origin) for origin,rows in [('candidate',runtime),('docs',docs)] for row in rows];payload.sort(key=lambda row:row['path'])
 require(len({r['path'].casefold() for r in payload})==len(payload),'Case-colliding archive entries')
 scope=(overlay/'NATIVE_SCOPE.md').read_text();runtime_sha=next(r['sha256'] for r in runtime if r['path']=='Parasite_Data/Managed/Parasite.Runtime.dll')
 require(all(token in scope for token in (info['version'],candidate.name,runtime_sha)),'NATIVE_SCOPE.md does not identify this exact candidate/version/runtime')
 links=validate_links(overlay,docs,payload);prefix=out.name.removesuffix('.tar.gz');relative_name(prefix)
 entries=[{'path':r['path'],'type':r['type'],'origin':r['origin'],'source_mode':r['mode'],'archive_mode':archive_mode(r),**({k:r[k] for k in ('bytes','sha256')} if r['type']=='file' else {})} for r in payload]
 internal={'schema':1,'scope':'Portable private preview package; packaging does not establish native/gameplay/rendered acceptance. See NATIVE_SCOPE.md.','version':info['version'],'candidate':candidate.name,'build_info_sha256':digest(candidate/'BUILD_INFO.json'),'original_output_manifest_sha256':evidence['sha256'],'entries':entries}
 manifest=json_bytes(internal)
 if not go:return {'status':'DRY_RUN_NOT_PACKAGED','candidate':str(candidate),'out':str(out),'version':info['version'],'entries':len(payload)+2,'payload_bytes':sum(r.get('bytes',0) for r in payload),'excluded':excluded,'verified_local_doc_links':links,'input_tree_sha256':{k:v['tree_sha256'] for k,v in before.items()}}
 release.mkdir(parents=True,exist_ok=True)
 require(shutil.disk_usage(release).free>sum(r.get('bytes',0) for r in payload)*1.05+16*1024**2,'Insufficient free space for a streamed worst-case archive')
 with tempfile.TemporaryDirectory(prefix='.package-candidate-',dir=release) as temporary:
  temp=Path(temporary);archive=temp/out.name;write_archive(archive,prefix,payload,{'candidate':candidate,'docs':overlay},before,manifest);verify_archive(archive,prefix,payload,manifest)
  after={'candidate':snapshot(candidate),'docs':snapshot(overlay)}
  require(before==after,'Candidate or docs full hash/mode/mtime tree changed while packaging; no archive published')
  require(digest(Path(evidence['path']))==evidence['sha256'] and digest(Path(evidence['source_snapshot_path']))==evidence['source_snapshot_sha256'] and digest(Path(evidence['runner_state_path']))==evidence['runner_state_sha256'],'Original build evidence changed while packaging')
  archive_hash=digest(archive);receipt={'schema':1,'status':'PASS_READ_ONLY_PACKAGE','created_utc':datetime.now(timezone.utc).isoformat(),'tool_sha256':digest(Path(__file__)),'version':info['version'],'candidate':str(candidate),'out':str(out),'archive_sha256':archive_hash,'archive_bytes':archive.stat().st_size,'candidate_unchanged':True,'docs_overlay_unchanged':True,'input_tree_before_sha256':{k:v['tree_sha256'] for k,v in before.items()},'input_tree_after_sha256':{k:v['tree_sha256'] for k,v in after.items()},'verified_local_doc_links':links,'excluded_candidate_paths':excluded,'original_evidence':evidence,'scope':'Archive integrity and source immutability only. Native execution after extraction remains separate; consult NATIVE_SCOPE.md.'}
  external=dict(internal,archive_sha256=archive_hash,archive_root=prefix,internal_manifest_sha256=hashlib.sha256(manifest).hexdigest(),archive_entries=entries+[{'path':MANIFEST_NAME,'type':'file','bytes':len(manifest),'sha256':hashlib.sha256(manifest).hexdigest(),'archive_mode':0o644}])
  contents={'sha256':(archive_hash+'  '+out.name+'\n').encode(),'manifest':json_bytes(external),'before':json_bytes(before),'after':json_bytes(after),'receipt':json_bytes(receipt)}
  pairs=[]
  for key,data in contents.items():
   staged=temp/destinations[key].name;staged.write_bytes(data);pairs.append((staged,destinations[key]))
  pairs.append((archive,out));publish_fresh(pairs)
 return receipt

def main():
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--project',type=Path,default=Path(__file__).resolve().parents[1]);parser.add_argument('--candidate',type=Path,required=True);parser.add_argument('--out',type=Path,required=True);parser.add_argument('--docs-overlay',type=Path,required=True);parser.add_argument('--go',action='store_true');args=parser.parse_args()
 try:result=package(args.project,args.candidate,args.out,args.docs_overlay,args.go)
 except (PackageError,OSError,tarfile.TarError) as error:parser.exit(1,'Packaging failed: '+str(error)+'\n')
 print(json.dumps(result,indent=2))
if __name__=='__main__':main()
