#!/bin/bash
# Serialized Unity batch runner: only one Unity process may open the project at a time.
# usage: Tools/unity.sh <logname> <unity args...>   (log goes to Logs/<logname>.log)
P="$(cd "$(dirname "$0")/.." && pwd)"; name=$1; shift
export __NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia __VK_LAYER_NV_optimus=NVIDIA_only
exec flock /tmp/parasite-unity.lock "$HOME/Data/Unity/Editor/6000.3.29f1/Editor/Unity" -batchmode -projectPath "$P" -logFile "$P/Logs/$name.log" "$@"
