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
  "$project_dir/Docs/TEST_REPORT.md" \
  "$project_dir/Assets/Parasite/Resources/UI/OFL-Rajdhani.txt" \
  "$project_dir/Assets/Parasite/Audio/KENNEY_LICENSE.txt"; do
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
  command -v tar >/dev/null
  command -v gzip >/dev/null
else
  command -v zip >/dev/null
fi

# Call only after the target's build has finished and TEST_REPORT reflects its actual validation.
mkdir -p "$build_dir/Licenses" "$release_dir"
cp "$project_dir/Docs/RELEASE_README.md" "$build_dir/README.md"
for doc in CONTROLS KNOWN_ISSUES THIRD_PARTY_ASSETS TEST_REPORT; do
  cp "$project_dir/Docs/$doc.md" "$build_dir/$doc.md"
done
cp "$project_dir/Assets/Parasite/Resources/UI/OFL-Rajdhani.txt" "$build_dir/Licenses/"
cp "$project_dir/Assets/Parasite/Audio/KENNEY_LICENSE.txt" "$build_dir/Licenses/"
if [[ "$platform" == linux ]]; then
  cat > "$build_dir/play.sh" <<'LAUNCHER'
#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
exec ./Parasite.x86_64 "$@"
LAUNCHER
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
echo "Package: $release_dir/$archive_name"
echo "Bytes: $(wc -c < "$release_dir/$archive_name")"
