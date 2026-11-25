#!/usr/bin/env python3
"""Dormant root-controlled atomic publication. Default is evidence recheck/dry-run; no automatic live copying."""
import argparse, ctypes, datetime, shutil, tempfile
from common import *
from bundle import recheck,names_for

def exchange(source,destination):
    libc=ctypes.CDLL(None,use_errno=True)
    function=getattr(libc,'renameat2',None)
    require(function is not None,'Atomic directory exchange unavailable; live destination left untouched')
    function.argtypes=[ctypes.c_int,ctypes.c_char_p,ctypes.c_int,ctypes.c_char_p,ctypes.c_uint];function.restype=ctypes.c_int
    status=function(-100,os.fsencode(source),-100,os.fsencode(destination),2) # RENAME_EXCHANGE
    if status: raise OSError(ctypes.get_errno(),'Atomic directory exchange failed; destination left untouched')

def publish(bundle_path,approved_sha=None,go=False):
    bundle_path=below(bundle_path,PROJECT/'Logs');data=recheck(bundle_path);current_sha=sha(bundle_path)
    destination=PROJECT/'Assets/Parasite/Resources/Cinematics/FullStory'
    if not go:
        print('DRY_RUN_READY',data['group'],'bundle_sha256='+current_sha,'target='+str(destination));return
    require(approved_sha==current_sha,'Root must explicitly supply the reviewed bundle SHA256')
    names=names_for(data['group'])
    with Path('/tmp/parasite-unity.lock').open('a') as lock:
        try:fcntl.flock(lock.fileno(),fcntl.LOCK_EX|fcntl.LOCK_NB)
        except BlockingIOError as error:raise InvalidEvidence('Serialized Unity job is active; publication refused') from error
        if destination.exists():
            require(destination.is_dir() and not destination.is_symlink(),'Invalid live FullStory destination')
            allowed={name+suffix for name in FILMS for suffix in ['.webm','.webm.meta']}
            require({p.name for p in destination.iterdir()}<=allowed,'Unexpected live files; refusing replacement')
            require(data['group']=='full' or not (destination/'OpeningFull.webm').exists(),'An existing full opening may only be replaced with a reviewed full-five bundle')
        destination.parent.mkdir(parents=True,exist_ok=True)
        temporary=Path(tempfile.mkdtemp(prefix='.FullStory-publish-',dir=destination.parent));swapped=False
        try:
            for name in names:
                target=temporary/(name+'.webm');shutil.copy2(bundle_path.parent/(name+'.webm'),target)
                require(sha(target)==data['movies'][name]['movie']['sha256'],'Copy hash changed')
                with target.open('rb') as stream:os.fsync(stream.fileno())
                meta=destination/(name+'.webm.meta')
                if meta.is_file():shutil.copy2(meta,temporary/meta.name) # Preserve existing Unity GUIDs.
            if destination.exists():exchange(temporary,destination);swapped=True
            else:os.replace(temporary,destination)
            backup=None
            if swapped:
                backups=STAGE/'publication-backups';backups.mkdir(parents=True,exist_ok=True)
                backup=backups/datetime.datetime.now(datetime.timezone.utc).strftime('FullStory-%Y%m%dT%H%M%S%fZ')
                os.replace(temporary,backup)
            report={'schema':1,'group':data['group'],'bundle_sha256':current_sha,'destination':str(destination),'backup':str(backup) if backup else None,
                'movie_sha256':{name:data['movies'][name]['movie']['sha256'] for name in names},'status':'ATOMIC_COPY_COMPLETE',
                'limits':'Not a release or Unity acceptance claim. Root still owns actual decoder/UI/audio/skip and final artifact checks.'}
            path=STAGE/'publications'/('published-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')+'.json');atomic_json(path,report)
            print('ATOMIC_PUBLICATION_COMPLETE',data['group'],path)
        finally:
            # Delete only this invocation's unpublished temporary data. Exchanged old assets are retained as backups.
            if temporary.exists() and not swapped:shutil.rmtree(temporary)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--bundle',required=True);p.add_argument('--approved-bundle-sha');p.add_argument('--go',action='store_true')
    a=p.parse_args();publish(a.bundle,a.approved_bundle_sha,a.go)
