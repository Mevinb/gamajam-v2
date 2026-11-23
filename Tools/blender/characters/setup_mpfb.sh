#!/bin/bash
# One-time (idempotent) setup of the isolated Blender profile used by the character pipeline:
#   MPFB 2.0.18 extension (GPL, extensions.blender.org) + MakeHuman asset packs (CC0 / CC-BY, see ASSETS.md).
# Everything lives under $PARASITE_CHAR_WORK (default ~/Data/parasite-work/characters); nothing touches ~/.config/blender.
set -euo pipefail
WORK=${PARASITE_CHAR_WORK:-$HOME/Data/parasite-work/characters}
BLENDER=${BLENDER:-$HOME/Data/Blender/current/blender}
UA="Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36"
mkdir -p "$WORK/packs" "$WORK/blender_user"
export BLENDER_USER_RESOURCES="$WORK/blender_user"
EXT="$WORK/blender_user/extensions/user_default/mpfb"
DATA="$WORK/blender_user/extensions/.user/user_default/mpfb/data"
if [ ! -f "$EXT/blender_manifest.toml" ]; then
  Z="$WORK/add-on-mpfb-v2.0.18.zip"
  [ -f "$Z" ] || curl -sL -A "$UA" -o "$Z" "https://extensions.blender.org/download/sha256:c075b3e9b94b8e5556a5f03f4653602d02ea306de08126e8f55ed6dee28a2dad/add-on-mpfb-v2.0.18.zip"
  echo "c075b3e9b94b8e5556a5f03f4653602d02ea306de08126e8f55ed6dee28a2dad  $Z" | sha256sum -c -
  "$BLENDER" -b --command extension install-file -r user_default -e "$Z"
fi
mkdir -p "$DATA"
# pack-dir:zip-name  (asset pages: https://files.makehumancommunity.org/asset_packs/<pack-dir>/)
PACKS="makehuman_system_assets:makehuman_system_assets_cc0 skins01:skins01_cc0 skins02:skins02_cc0 eyebrows01:eyebrows01_cc0
eyelashes01:eyelashes01_cc0 hair01:hair01_cc0 shirts01:shirts01_cc0 pants01:pants01_cc0 shoes01:shoes01_cc0 glasses01:glasses01_cc0
suits01:suits01_cc0 bodyparts05:bodyparts05_cc0 hats01:hats01_cc0 system_hair_materials01:system_hair_materials01_cc0
system_clothes_materials01:system_clothes_materials01_cc0 suits03:suits03_cc-by hats03:hats03_cc-by pants02:pants02_ccby
shoes02:shoes02_ccby shirts02:shirts02_ccby shirts03:shirts03_ccby hair03:hair03_ccby hair02:hair02_ccby"
for p in $PACKS; do
  d=${p%%:*}; z=${p##*:}.zip
  stamp="$DATA/.installed_${z%.zip}"
  [ -f "$stamp" ] && continue
  [ -f "$WORK/packs/$z" ] || curl -sL -A "$UA" -o "$WORK/packs/$z" "https://files.makehumancommunity.org/asset_packs/$d/$z"
  unzip -q -o "$WORK/packs/$z" -d "$DATA"
  touch "$stamp"
  rm -f "$WORK/packs/$z"   # keep the disk footprint small; re-downloaded only if the stamp is removed
done
echo "MPFB + asset packs ready in $DATA"
