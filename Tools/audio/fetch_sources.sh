#!/usr/bin/env bash
# Download the CC0 source material used by gen_sfx.py / gen_ambience.py into $PARASITE_AUDIO_WORK (not shipped):
#   Kenney (kenney.nl, CC0): impact-sounds, interface-sounds, sci-fi-sounds, rpg-audio, digital-audio, ui-audio
#   OpenGameArt (CC0): IgnasD "High traffic road sounds", Thimras "Park Ambiences", Brandon Morris "Dog barking mono"
set -euo pipefail
WORK="${PARASITE_AUDIO_WORK:-$HOME/Data/parasite-work/audio}"
UA="Mozilla/5.0 (X11; Linux x86_64)"
mkdir -p "$WORK/kenney" "$WORK/oga"
cd "$WORK/kenney"
for p in impact-sounds interface-sounds sci-fi-sounds rpg-audio digital-audio ui-audio; do
  [ -d "$p/Audio" ] && continue
  id=$(printf "assets/$p" | xxd -p | tr -d '\n')
  url=$(curl -sL -A "$UA" -H "Referer: https://kenney.nl/assets/$p" -H "Origin: https://kenney.nl" -H "Sec-Fetch-Site: same-origin" \
        -H "Sec-Fetch-Mode: cors" "https://kenney.nl/download-link/$id" | sed -E 's/.*"url":"([^"]+)".*/\1/; s/\\\//\//g')
  curl -sL -A "$UA" -H "Referer: https://kenney.nl/assets/$p" "$url" -o "$p.zip"
  mkdir -p "$p" && unzip -qo "$p.zip" -d "$p" && rm -f "$p.zip"
done
cd "$WORK/oga"
get() { [ -f "$2" ] || curl -sL -A "$UA" -o "$2" "$1"; }
get "https://opengameart.org/sites/default/files/gatve%20Varniu_3.ogg" traffic_ignasd.ogg
get "https://opengameart.org/sites/default/files/park_ambience_wind.wav" park_ambience_wind.wav
get "https://opengameart.org/sites/default/files/park_ambience_birds.wav" park_ambience_birds.wav
get "https://opengameart.org/sites/default/files/dog_barking_mono.wav" dog_barking_mono.wav
echo "sources ready in $WORK"
