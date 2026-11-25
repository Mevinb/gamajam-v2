#!/usr/bin/env python3
"""Small real archives verify portability, refusal boundaries and source preservation; no Unity/player project is launched."""
import copy,hashlib,importlib.util,json,os,shutil,stat,subprocess,tarfile,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
spec=importlib.util.spec_from_file_location('package_candidate',Path(__file__).resolve().parents[1]/'package_candidate.py');tool=importlib.util.module_from_spec(spec);spec.loader.exec_module(tool)
sha=lambda path:hashlib.sha256(Path(path).read_bytes()).hexdigest()
class PackageCandidateTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.project=Path(self.temp.name)/'Project with spaces';self.candidate=self.project/'Builds/Candidates/full-story-fixture';self.overlay=self.project/'Logs/docs';self.out=self.project/'Builds/Releases/PARASITE-fixture.tar.gz'
  self.candidate.mkdir(parents=True);self.overlay.mkdir(parents=True)
  for name,data in {'Parasite_Data/globalgamemanagers':b'global data','Parasite_Data/Managed/Parasite.Runtime.dll':b'fixture runtime','UnityPlayer.so':b'fixture shared library','PARASITE_BurstDebugInformation_DoNotShip/debug.txt':b'do not ship','PARASITE_BackUpThisFolder_ButDontShipItWithYourGame/backup.txt':b'do not ship'}.items():
   p=self.candidate/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(data)
  shutil.copyfile('/bin/true',self.candidate/'Parasite.x86_64');(self.candidate/'Parasite.x86_64').chmod(0o755)
  (self.candidate/'play.sh').write_text('#!/bin/sh\ncd "$(dirname "$0")" || exit 1\nexec ./Parasite.x86_64 "$@"\n');(self.candidate/'play.sh').chmod(0o644)
  for name in tool.DOCS_REQUIRED:
   p=self.overlay/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_text('# Preview docs\n')
  (self.overlay/'README.md').write_text('# Fixture preview\n[Controls](CONTROLS.md) [Licences](Licenses/README.md)\n')
  (self.overlay/'NATIVE_SCOPE.md').write_text('# Preview scope\n23 missions. Selected native checks only; no full27, rendered graphics or human acceptance claim.\n')
  self.evidence=self.project/'Logs/full-story-candidate/build-runs/fixture';self.evidence.mkdir(parents=True);self.source=self.evidence/'source-snapshot-before.json';self.source.write_text('[]\n');self.manifest=self.evidence/'build-output-hashes.json';self.record_original()
 def record_original(self):
  # The original build's published records intentionally precede BUILD_INFO, as the actual builder does.
  rows=[]
  for p in sorted(self.candidate.rglob('*')):
   if p.name=='BUILD_INFO.json':continue
   s=p.lstat();r={'path':p.relative_to(self.candidate).as_posix(),'mode':stat.S_IMODE(s.st_mode),'uid':s.st_uid,'gid':s.st_gid,'type':'directory' if p.is_dir() else 'file'}
   if p.is_file():r.update(bytes=s.st_size,sha256=sha(p),mtime_ns=s.st_mtime_ns)
   rows.append(r)
  original={'files':rows,'tree_sha256':tool.canonical(rows),'total_bytes':sum(x.get('bytes',0) for x in rows),'entries':len(rows)};self.manifest.write_text(json.dumps(original))
  info={'version':'0.6.0-preview','diagnostic_name':self.candidate.name,'build_exit_code':0,'build_summary':{'result':'Succeeded','errors':0},'source_snapshot':str(self.source),'source_snapshot_sha256':sha(self.source),'output_hashes':str(self.manifest),'output_tree_sha256':original['tree_sha256']};(self.candidate/'BUILD_INFO.json').write_text(json.dumps(info))
  state={'status':'READY_NOT_RUN','candidate_path':str(self.candidate),'candidate_build_info_sha256':sha(self.candidate/'BUILD_INFO.json'),'native_build_exit_code':0,'null_backend_marker_seen':True,'accepted_output_override':True,'source_unchanged_during_build':True,'preview_unchanged':True,'candidate_output_tree_sha256':original['tree_sha256']};(self.evidence/'runner-state.json').write_text(json.dumps(state))
  (self.overlay/'NATIVE_SCOPE.md').write_text('# Preview scope\n0.6.0-preview / '+self.candidate.name+' / '+sha(self.candidate/'Parasite_Data/Managed/Parasite.Runtime.dll')+'\n23 missions; selected checks only. No full27, rendered graphics or human acceptance claim.\n')
 def run_package(self,go=True):return tool.package(self.project,self.candidate,self.out,self.overlay,go)
 def assert_no_archive(self):self.assertFalse(self.out.exists())
 def test_archive_reextracts_and_every_payload_hash_mode_matches(self):
  before=tool.snapshot(self.candidate);docs_before=tool.snapshot(self.overlay);result=self.run_package()
  self.assertEqual(result['status'],'PASS_READ_ONLY_PACKAGE');self.assertEqual(before,tool.snapshot(self.candidate));self.assertEqual(docs_before,tool.snapshot(self.overlay));self.assertEqual((self.candidate/'play.sh').stat().st_mode&0o777,0o644)
  self.assertEqual(Path(str(self.out)+'.sha256').read_text().split()[0],sha(self.out))
  external=json.loads(Path(str(self.out)+'.manifest.json').read_text());extract=self.project/'extracted portable folder';extract.mkdir()
  with tarfile.open(self.out,'r:gz') as archive:
   names=archive.getnames();self.assertFalse(any('BurstDebug' in x or 'BackUpThis' in x for x in names));self.assertTrue(all(not x.startswith('/') and '..' not in Path(x).parts for x in names))
  subprocess.run(['tar','-xzf',str(self.out),'-C',str(extract)],check=True)
  root=extract/external['archive_root']
  for row in external['archive_entries']:
   p=root/row['path'];self.assertEqual(p.stat().st_mode&0o777,row['archive_mode'])
   if row['type']=='file':self.assertEqual(sha(p),row['sha256']);self.assertEqual(p.stat().st_size,row['bytes'])
  self.assertEqual(subprocess.run([str(root/'play.sh')],cwd='/',check=False).returncode,0)
  self.assertEqual(json.loads((root/'PACKAGE_MANIFEST.json').read_text())['version'],'0.6.0-preview')
 def test_dry_run_writes_nothing(self):
  self.assertEqual(self.run_package(False)['status'],'DRY_RUN_NOT_PACKAGED');self.assertFalse(self.out.parent.exists())
 def test_missing_original_runtime_is_refused(self):
  (self.candidate/'Parasite_Data/Managed/Parasite.Runtime.dll').unlink()
  with self.assertRaises(tool.PackageError):self.run_package()
  self.assert_no_archive()
 def test_unrecorded_file_is_refused(self):
  (self.candidate/'surprise.sh').write_text('echo unexpected\n')
  with self.assertRaisesRegex(tool.PackageError,'extra/unrecorded'):self.run_package()
  self.assert_no_archive()
 def test_even_recorded_unexpected_top_level_file_is_refused(self):
  (self.candidate/'unexpected.sh').write_text('echo unexpected\n');self.record_original()
  with self.assertRaisesRegex(tool.PackageError,'outside runtime allowlist'):self.run_package()
 def test_modified_original_bytes_or_manifest_identity_is_refused(self):
  (self.candidate/'UnityPlayer.so').write_bytes(b'changed')
  with self.assertRaises(tool.PackageError):self.run_package()
  self.record_original();m=json.loads(self.manifest.read_text());m['files'][0]['mode']^=1;self.manifest.write_text(json.dumps(m))
  with self.assertRaisesRegex(tool.PackageError,'manifest identity'):self.run_package()
 def test_traversal_entry_is_refused_even_with_recomputed_manifest_identity(self):
  m=json.loads(self.manifest.read_text());m['files'][0]['path']='../escape';m['tree_sha256']=tool.canonical(m['files']);self.manifest.write_text(json.dumps(m));info=json.loads((self.candidate/'BUILD_INFO.json').read_text());info['output_tree_sha256']=m['tree_sha256'];(self.candidate/'BUILD_INFO.json').write_text(json.dumps(info));state=json.loads((self.evidence/'runner-state.json').read_text());state['candidate_build_info_sha256']=sha(self.candidate/'BUILD_INFO.json');state['candidate_output_tree_sha256']=m['tree_sha256'];(self.evidence/'runner-state.json').write_text(json.dumps(state))
  with self.assertRaisesRegex(tool.PackageError,'Unsafe entry'):self.run_package()
  self.assert_no_archive()
 def test_symlink_escape_and_internal_links_are_both_refused(self):
  (self.overlay/'external.txt').symlink_to('/etc/passwd')
  with self.assertRaisesRegex(tool.PackageError,'Symbolic links'):self.run_package()
  (self.overlay/'external.txt').unlink();(self.overlay/'internal.txt').symlink_to('CONTROLS.md')
  with self.assertRaisesRegex(tool.PackageError,'Symbolic links'):self.run_package()
 def test_docs_cannot_override_runtime_or_omit_scope(self):
  (self.overlay/'play.sh').write_text('bad')
  with self.assertRaisesRegex(tool.PackageError,'collides'):self.run_package()
  (self.overlay/'play.sh').unlink();(self.overlay/'NATIVE_SCOPE.md').unlink()
  with self.assertRaisesRegex(tool.PackageError,'missing'):self.run_package()
 def test_broken_or_escaping_local_docs_links_are_refused(self):
  for text in ('[missing](missing.md)','[escape](../../outside.md)','[absolute](/etc/passwd)'):
   (self.overlay/'README.md').write_text(text)
   with self.assertRaises(tool.PackageError):self.run_package()
 def test_existing_output_and_late_publication_collision_never_overwrite(self):
  self.out.parent.mkdir();self.out.write_bytes(b'keep existing')
  with self.assertRaisesRegex(tool.PackageError,'already exists'):self.run_package()
  self.assertEqual(self.out.read_bytes(),b'keep existing');self.out.unlink();original_link=os.link
  def raced(source,destination):
   if Path(destination)==self.out:self.out.write_bytes(b'concurrent existing');raise FileExistsError('late collision')
   original_link(source,destination)
  with patch.object(tool.os,'link',side_effect=raced):
   with self.assertRaises(FileExistsError):self.run_package()
  self.assertEqual(self.out.read_bytes(),b'concurrent existing');self.assertEqual(list(self.out.parent.iterdir()),[self.out])
 def test_candidate_changed_during_packaging_is_not_published_or_restored(self):
  original=tool.write_archive
  def changed(*args,**kwargs):
   original(*args,**kwargs);(self.candidate/'UnityPlayer.so').write_bytes(b'changed by fixture, never restore input')
  with patch.object(tool,'write_archive',side_effect=changed):
   with self.assertRaisesRegex(tool.PackageError,'tree changed'):self.run_package()
  self.assert_no_archive();self.assertEqual((self.candidate/'UnityPlayer.so').read_bytes(),b'changed by fixture, never restore input')
 def test_build_info_forgery_or_incomplete_native_build_receipt_is_refused(self):
  info=json.loads((self.candidate/'BUILD_INFO.json').read_text());info['version']='0.7.0-preview';(self.candidate/'BUILD_INFO.json').write_text(json.dumps(info))
  with self.assertRaisesRegex(tool.PackageError,'originally published'):self.run_package()
  self.record_original();state=json.loads((self.evidence/'runner-state.json').read_text());state['null_backend_marker_seen']=False;(self.evidence/'runner-state.json').write_text(json.dumps(state))
  with self.assertRaisesRegex(tool.PackageError,'Null build'):self.run_package()
 def test_stale_scope_for_a_different_runtime_is_refused(self):
  scope=self.overlay/'NATIVE_SCOPE.md';scope.write_text(scope.read_text().replace(sha(self.candidate/'Parasite_Data/Managed/Parasite.Runtime.dll'),'0'*64))
  with self.assertRaisesRegex(tool.PackageError,'exact candidate/version/runtime'):self.run_package()
  self.assert_no_archive()
 def test_only_private_candidate_and_fresh_release_paths_accepted(self):
  with self.assertRaises(tool.PackageError):tool.package(self.project,self.project/'Builds/Linux',self.out,self.overlay)
  with self.assertRaises(tool.PackageError):tool.package(self.project,self.candidate,self.project/'outside.tar.gz',self.overlay)
if __name__=='__main__':unittest.main(verbosity=2)
