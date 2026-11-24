"""Atomically publish already-verified complete Blender artifacts; preserve Unity .meta files."""
import json, os, sys
from pathlib import Path
project,stage=map(Path,sys.argv[1:3])
report=json.loads((stage/'ArtSource/Riverside/export_verification.json').read_text())
assert report['passed']==8 and all(a['zero_area_triangles']==0 for a in report['assets'])
for source_root,target_root in [(stage/'Resources/Riverside',project/'Assets/Parasite/Resources/Riverside'),(stage/'ArtSource/Riverside',project/'ArtSource/Riverside')]:
    for source in sorted(source_root.rglob('*')):
        if not source.is_file():continue
        target=target_root/source.relative_to(source_root);target.parent.mkdir(parents=True,exist_ok=True)
        # Staging is on the project filesystem. rename replaces each complete file atomically;
        # Unity never observes an FBX or PNG while Blender is still writing its bytes.
        os.replace(source,target)
print('[riverside] PUBLISHED 8 verified models, original PBR textures and packed editable source atomically')
