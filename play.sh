#!/usr/bin/env bash
# One portable launch policy for the project and extracted Linux releases.
# Explicit Unity API flags win; PARASITE_GRAPHICS_API selects auto/vulkan/glcore/default.
set -euo pipefail
launcher_dir="$(cd "$(dirname "$0")" && pwd)"
if [[ -s "$launcher_dir/Parasite.x86_64" ]]; then
  player_dir="$launcher_dir"
elif [[ -s "$launcher_dir/Builds/Linux/Parasite.x86_64" ]]; then
  player_dir="$launcher_dir/Builds/Linux"
else
  echo "PARASITE Linux player not found beside play.sh or in Builds/Linux." >&2
  exit 1
fi

# Preserve supplied desktop/display settings, including explicit Xvfb sessions.
if [[ -z "${XDG_RUNTIME_DIR:-}" && -d "/run/user/$(id -u)" ]]; then
  export XDG_RUNTIME_DIR="/run/user/$(id -u)"
fi
if [[ -z "${XDG_CACHE_HOME:-}" ]]; then export XDG_CACHE_HOME="$HOME/.cache"; fi
if [[ -z "${XDG_CONFIG_HOME:-}" ]]; then export XDG_CONFIG_HOME="$HOME/.config"; fi
if [[ -z "${DISPLAY:-}" && -S /tmp/.X11-unix/X0 ]]; then export DISPLAY=:0; fi
if [[ -z "${XAUTHORITY:-}" && "${DISPLAY:-}" =~ ^:0(\.0)?$ ]]; then
  for parasite_xauth in "/run/user/$(id -u)"/.mutter-Xwaylandauth.*; do
    if [[ -r "$parasite_xauth" ]]; then export XAUTHORITY="$parasite_xauth"; break; fi
  done
fi

has_nvidia=false
if command -v nvidia-smi >/dev/null 2>&1 && nvidia-smi -L >/dev/null 2>&1; then
  has_nvidia=true
  # Default to PRIME offload on NVIDIA hybrid laptops; retain deliberate overrides.
  export __NV_PRIME_RENDER_OFFLOAD="${__NV_PRIME_RENDER_OFFLOAD:-1}"
  export __GLX_VENDOR_LIBRARY_NAME="${__GLX_VENDOR_LIBRARY_NAME:-nvidia}"
  export __VK_LAYER_NV_optimus="${__VK_LAYER_NV_optimus:-NVIDIA_only}"
fi

graphics_api="${PARASITE_GRAPHICS_API:-auto}"
case "$graphics_api" in
  auto|vulkan|glcore|default) ;;
  *) printf 'Invalid PARASITE_GRAPHICS_API=%s; choose auto, vulkan, glcore, or default.\n' "$graphics_api" >&2; exit 2 ;;
esac
explicit_api=false
for argument in "$@"; do
  case "$argument" in
    -force-vulkan|-force-glcore|-force-glcore[0-9]*) explicit_api=true ;;
  esac
done
api_arguments=()
if [[ "$explicit_api" == false ]]; then
  case "$graphics_api" in
    vulkan) api_arguments=(-force-vulkan) ;;
    glcore) api_arguments=(-force-glcore) ;;
    auto) if [[ "$has_nvidia" == true ]]; then api_arguments=(-force-vulkan); fi ;;
    default) ;; # Ask Unity to select its normal graphics API, while retaining GPU offload.
  esac
fi
cd "$player_dir"
exec ./Parasite.x86_64 "${api_arguments[@]}" "$@"
