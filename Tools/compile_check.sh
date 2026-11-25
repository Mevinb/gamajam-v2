#!/bin/bash
# Fast, editor-free C# compile check for Parasite (uses Unity's bundled Roslyn).
# Package assemblies (Input System, Cinemachine, URP, AI Navigation, uGUI) come from Library/ScriptAssemblies,
# which exists after the project has been imported once by the Unity editor.
# usage: Tools/compile_check.sh [runtime|editor|player|all]
set -u
P="$(cd "$(dirname "$0")/.." && pwd)"
E="$HOME/Data/Unity/Editor/6000.3.29f1/Editor/Data"
DOTNET="$E/NetCoreRuntime/dotnet"; CSC="$E/DotNetSdkRoslyn/csc.dll"
SA="$P/Library/ScriptAssemblies"
MODE="${1:-all}"
case "$MODE" in runtime|editor|player|all) ;; *) echo "Usage: $0 [runtime|editor|player|all]" >&2; exit 2;; esac
OUT=$(mktemp -d)
trap 'rm -rf "$OUT"' EXIT
if [ ! -d "$SA" ] || [ ! -f "$SA/Unity.InputSystem.dll" ]; then
  echo "WARN: $SA missing package assemblies — open/import the project in Unity once. Package-type errors will appear."
fi
refs=()
refs+=("-r:$E/NetStandard/ref/2.1.0/netstandard.dll")
for f in "$E"/Managed/UnityEngine/UnityEngine*.dll; do refs+=("-r:$f"); done
pkgs=(Unity.InputSystem Unity.Cinemachine Unity.AI.Navigation Unity.RenderPipelines.Universal.Runtime Unity.RenderPipelines.Core.Runtime UnityEngine.UI Unity.Mathematics Unity.Collections Unity.Burst Unity.TextMeshPro Unity.RenderPipelines.Core.ShaderLibrary Unity.RenderPipelines.GPUDriven.Runtime Unity.RenderPipeline.Universal.ShaderLibrary)
for a in "${pkgs[@]}"; do [ -f "$SA/$a.dll" ] && refs+=("-r:$SA/$a.dll"); done
common=(-nologo -noconfig -nostdlib -langversion:9.0 -nowarn:1701,1702,0414,0649,0169 -target:library -define:UNITY_6000_3_OR_NEWER -define:UNITY_STANDALONE_LINUX -define:UNITY_2023_1_OR_NEWER -define:UNITY_6000_0_OR_NEWER -define:CINEMACHINE_PHYSICS)
rc=0
if [ "$MODE" = runtime ] || [ "$MODE" = editor ] || [ "$MODE" = all ]; then
  mapfile -t src < <(rg --files --hidden --no-ignore "$P/Assets/Parasite/Scripts" -g '*.cs')
  "$DOTNET" "$CSC" "${common[@]}" "${refs[@]}" -out:"$OUT/Parasite.Runtime.dll" "${src[@]}" >"$OUT/runtime.log" 2>&1
  runtime_rc=$?
  grep -E 'error|warning CS0(108|114|618|162)' "$OUT/runtime.log" | sed "s|$P/||" | sort -u | head -80
  [ "$runtime_rc" -eq 0 ] && [ -f "$OUT/Parasite.Runtime.dll" ] && echo "RUNTIME: OK (${#src[@]} files)" || { echo "RUNTIME: FAILED"; rc=1; }
fi
if [ "$MODE" = editor ] || [ "$MODE" = all ]; then
  if [ -f "$OUT/Parasite.Runtime.dll" ]; then
    erefs=("${refs[@]}" "-r:$OUT/Parasite.Runtime.dll")
    for f in "$E"/Managed/UnityEditor*.dll "$E"/Managed/UnityEditor/*.dll; do [ -f "$f" ] && erefs+=("-r:$f"); done
    for a in Unity.AI.Navigation.Editor Unity.RenderPipelines.Universal.Editor Unity.RenderPipelines.Core.Editor Unity.Cinemachine.Editor; do [ -f "$SA/$a.dll" ] && erefs+=("-r:$SA/$a.dll"); done
    mapfile -t esrc < <(rg --files --hidden --no-ignore "$P/Assets/Parasite/Editor" -g '*.cs')
    "$DOTNET" "$CSC" "${common[@]}" -define:UNITY_EDITOR "${erefs[@]}" -out:"$OUT/Parasite.Editor.dll" "${esrc[@]}" >"$OUT/editor.log" 2>&1
    editor_rc=$?
    grep -E 'error' "$OUT/editor.log" | sed "s|$P/||" | sort -u | head -60
    [ "$editor_rc" -eq 0 ] && [ -f "$OUT/Parasite.Editor.dll" ] && echo "EDITOR: OK (${#esrc[@]} files)" || { echo "EDITOR: FAILED"; rc=1; }
  else echo "EDITOR: skipped (runtime failed)"; rc=1; fi
fi
if [ "$MODE" = player ] || [ "$MODE" = all ]; then
  python3 "$P/Tools/compile_check_player.py" --project "$P" --editor-data "$E" --out "$OUT/player/Parasite.Runtime.dll" || rc=1
fi
exit $rc
