#!/bin/bash
# Roslyn compile of Parasite runtime + EditMode/PlayMode test assemblies (no Unity), to catch test-code errors early.
set -uo pipefail
P="$(cd "$(dirname "$0")/../.." && pwd)"
E="$HOME/Data/Unity/Editor/6000.3.29f1/Editor/Data"
DOTNET="$E/NetCoreRuntime/dotnet"; CSC="$E/DotNetSdkRoslyn/csc.dll"; SA="$P/Library/ScriptAssemblies"
OUT=$(mktemp -d -p "${PARASITE_AUDIO_WORK:-$HOME/Data/parasite-work/audio}")
NUNIT=$(find "$P/Library/PackageCache" -name nunit.framework.dll | head -1)
refs=("-r:$E/NetStandard/ref/2.1.0/netstandard.dll" "-r:$E/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll")
for f in "$E"/Managed/UnityEngine/UnityEngine*.dll "$E"/Managed/UnityEditor*.dll; do refs+=("-r:$f"); done
for a in "$SA"/Unity.*.dll "$SA"/UnityEngine.*.dll "$SA"/UnityEditor.*.dll; do [ -f "$a" ] && refs+=("-r:$a"); done
common=(-nologo -noconfig -nostdlib -langversion:9.0 -nowarn:1701,1702,0414,0649,0169 -target:library -define:UNITY_EDITOR -define:UNITY_INCLUDE_TESTS -define:UNITY_6000_0_OR_NEWER)
mapfile -t src < <(find "$P/Assets/Parasite/Scripts" -name '*.cs')
"$DOTNET" "$CSC" "${common[@]}" "${refs[@]}" -out:"$OUT/Parasite.Runtime.dll" "${src[@]}" 2>&1 | grep -E ' error ' | sed "s|$P/||" | head -20
if [ ! -f "$OUT/Parasite.Runtime.dll" ]; then
  echo "RUNTIME: FAILED; test compilation skipped"
  rm -rf "$OUT"
  exit 1
fi
rc=0
for t in EditMode PlayMode; do
  mapfile -t tsrc < <(find "$P/Assets/Parasite/Tests/$t" -name '*.cs')
  "$DOTNET" "$CSC" "${common[@]}" "${refs[@]}" "-r:$NUNIT" "-r:$OUT/Parasite.Runtime.dll" -out:"$OUT/T$t.dll" "${tsrc[@]}" 2>&1 | grep -E ' error ' | sed "s|$P/||" | head -20
  if [ -f "$OUT/T$t.dll" ]; then
    echo "TESTS $t: OK"
  else
    echo "TESTS $t: FAILED"
    rc=1
  fi
done
rm -rf "$OUT"
exit "$rc"
