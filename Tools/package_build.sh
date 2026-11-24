#!/usr/bin/env bash
# Package an already-built player without copying the large Unity data directory.
set -euo pipefail

if [[ $# -ne 1 || ( "$1" != linux && "$1" != windows ) ]]; then
  echo "Usage: Tools/package_build.sh <linux|windows>" >&2
  exit 2
fi

project_dir="$(cd "$(dirname "$0")/.." && pwd)"
platform=$1
case "$platform" in
  linux) build_name=Linux; executable=Parasite.x86_64; player_library=UnityPlayer.so ;;
  windows) build_name=Windows; executable=Parasite.exe; player_library=UnityPlayer.dll ;;
esac
build_dir="$project_dir/Builds/$build_name"
release_dir="$project_dir/Builds/Releases"

for file in "$build_dir/$executable" "$build_dir/$player_library" \
  "$project_dir/Docs/RELEASE_README.md" "$project_dir/Docs/CONTROLS.md" \
  "$project_dir/Docs/KNOWN_ISSUES.md" "$project_dir/Docs/THIRD_PARTY_ASSETS.md" \
  "$project_dir/Docs/AFTERLAKE_GUIDE.md" \
  "$project_dir/Docs/COMPLETE_GAME_GUIDE.md" "$project_dir/Docs/HANDOFF.md" \
  "$project_dir/Docs/UPGRADE_TRACKER.md" "$project_dir/Docs/TEST_REPORT.md" \
  "$project_dir/Tools/package_docs.py" \
  "$project_dir/Assets/Parasite/Resources/UI/OFL-Rajdhani.txt" \
  "$project_dir/Assets/Parasite/Audio/KENNEY_LICENSE.txt" \
  "$project_dir/Assets/Parasite/Audio/AUDIO_CREDITS.txt" \
  "$project_dir/Assets/Parasite/Art/Characters/CREDITS.md" \
  "$project_dir/Assets/Parasite/Art/Characters/Animations/CREDITS.md"; do
  if [[ ! -s "$file" ]]; then
    echo "Missing or empty package input: $file" >&2
    exit 1
  fi
done
if [[ ! -s "$build_dir/Parasite_Data/globalgamemanagers" ]]; then
  echo "Missing Unity player data: $build_dir/Parasite_Data/globalgamemanagers" >&2
  exit 1
fi
if [[ "$platform" == linux ]]; then
  if [[ ! -s "$build_dir/BUILD_INFO.json" ]]; then
    echo "Missing build provenance: $build_dir/BUILD_INFO.json" >&2
    exit 1
  fi
  if [[ ! -s "$project_dir/play.sh" ]]; then
    echo "Missing portable launcher source: $project_dir/play.sh" >&2
    exit 1
  fi
  command -v tar >/dev/null
  command -v gzip >/dev/null
else
  command -v zip >/dev/null
fi
command -v sha256sum >/dev/null
command -v python3 >/dev/null

# Call only after the target's build has finished and TEST_REPORT reflects its actual validation.
mkdir -p "$release_dir"
python3 "$project_dir/Tools/package_docs.py" "$project_dir" "$build_dir"
if [[ "$platform" == linux ]]; then
  # The same self-contained launcher runs from the project root and an extracted build.
  cp "$project_dir/play.sh" "$build_dir/play.sh"
  chmod +x "$build_dir/play.sh" "$build_dir/Parasite.x86_64"
fi

# The temporary archive shares the output filesystem, so the final rename is atomic.
# Fast compression limits CPU time; direct archiving avoids a second 400+ MB player copy.
temp_dir="$(mktemp -d "$release_dir/.package-$platform.XXXXXX")"
trap 'rm -rf -- "$temp_dir"' EXIT
if [[ "$platform" == linux ]]; then
  archive_name=PARASITE-Linux.tar.gz
  tar -C "$project_dir/Builds" \
    --exclude='*_BurstDebugInformation_DoNotShip' \
    --exclude='*_BackUpThisFolder_ButDontShipItWithYourGame' \
    -cf - Linux | gzip -1 > "$temp_dir/$archive_name"
  gzip -t "$temp_dir/$archive_name"
else
  archive_name=PARASITE-Windows.zip
  (
    cd "$project_dir/Builds"
    zip -q -1 -r "$temp_dir/$archive_name" Windows \
      -x '*_BurstDebugInformation_DoNotShip/*' '*_BackUpThisFolder_ButDontShipItWithYourGame/*'
  )
  zip -T "$temp_dir/$archive_name" >/dev/null
fi
mv -f -- "$temp_dir/$archive_name" "$release_dir/$archive_name"
(
  cd "$release_dir"
  sha256sum "$archive_name" > "$temp_dir/$archive_name.sha256"
)
mv -f -- "$temp_dir/$archive_name.sha256" "$release_dir/$archive_name.sha256"
echo "Package: $release_dir/$archive_name"
echo "Checksum: $release_dir/$archive_name.sha256"
echo "Bytes: $(wc -c < "$release_dir/$archive_name")"
