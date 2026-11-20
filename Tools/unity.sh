#!/bin/bash
# Serialized Unity batch runner: only one Unity process may open the project at a time.
# usage: Tools/unity.sh <logname> <unity args...>   (log goes to Logs/<logname>.log)
set -euo pipefail
P="$(cd "$(dirname "$0")/.." && pwd)"
if [[ $# -lt 1 || ! "$1" =~ ^[a-zA-Z0-9_-]+$ ]]; then
  echo "Usage: Tools/unity.sh <logname> <Unity args...>" >&2
  exit 2
fi
name=$1
shift
mkdir -p "$P/Logs"
source "$P/Tools/display.sh"
exec flock /tmp/parasite-unity.lock "$HOME/Data/Unity/Editor/6000.3.29f1/Editor/Unity" -batchmode -projectPath "$P" -logFile "$P/Logs/$name.log" "$@"
