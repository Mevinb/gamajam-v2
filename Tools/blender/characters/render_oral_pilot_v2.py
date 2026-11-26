"""Explicit root-run wrapper for the separate compact V2 source; dry by default."""
from pathlib import Path
import sys

HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
import render_oral_pilot as recipe

recipe.SOURCE=recipe.ROOT/'ArtSource/Characters/OralPilotV2/LakeGirl_OralPilotV2.blend'
if '--output' not in sys.argv:
    sys.argv.extend(['--output',str(recipe.ROOT/'Logs/full-cinematic-renders/oral-pilot-qa-v2')])
if __name__=='__main__':recipe.main()
