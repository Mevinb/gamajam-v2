#!/bin/bash
# Source this from launchers. Remote agent shells may lack the desktop's Xwayland environment.
# Preserve explicitly supplied display/auth settings, including Xvfb sessions.
if [[ -z "${DISPLAY:-}" && -S /tmp/.X11-unix/X0 ]]; then
  export DISPLAY=:0
fi
if [[ -z "${XAUTHORITY:-}" && "${DISPLAY:-}" =~ ^:0(\.0)?$ ]]; then
  for parasite_xauth in "/run/user/$(id -u)"/.mutter-Xwaylandauth.*; do
    if [[ -r "$parasite_xauth" ]]; then
      export XAUTHORITY="$parasite_xauth"
      break
    fi
  done
  unset parasite_xauth
fi
export __NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia __VK_LAYER_NV_optimus=NVIDIA_only
