#!/bin/bash
# Launch the PARASITE Linux build on the NVIDIA GPU (PRIME offload). Extra args are passed through, e.g. ./play.sh -showfps
set -euo pipefail
P="$(cd "$(dirname "$0")" && pwd)"
source "$P/Tools/display.sh"
cd "$P/Builds/Linux"
exec ./Parasite.x86_64 "$@"
