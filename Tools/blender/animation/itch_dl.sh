#!/bin/bash
# usage: itch_dl.sh <user> <slug> <outdir>  — downloads free (name-your-price, $0) uploads via the public "No thanks" flow.
set -euo pipefail
U=$1; S=$2; OUT=$3; mkdir -p "$OUT"; CJ=$(mktemp -p "$OUT")
A="Mozilla/5.0"
P=$(curl -s -A "$A" -c $CJ -b $CJ "https://$U.itch.io/$S/purchase")
TOK=$(python3.12 -c "import re,sys;print(re.search(r'name=\"csrf_token\" value=\"([^\"]+)\"',sys.stdin.read()).group(1))" <<<"$P")
DLU=$(curl -s -A "$A" -c $CJ -b $CJ -X POST --data-urlencode "csrf_token=$TOK" "https://$U.itch.io/$S/download_url" | python3.12 -c "import json,sys;print(json.load(sys.stdin)['url'])")
D=$(curl -s -A "$A" -c $CJ -b $CJ "$DLU")
python3.12 - "$D" <<'PY' > $CJ.ids
import re,sys
s=sys.argv[1]
for m in re.finditer(r'data-upload_id="(\d+)".*?title="([^"]+)"',s,re.S): print(m.group(1)+"\t"+m.group(2))
PY
cat $CJ.ids
while IFS=$'\t' read -r id name; do
  [[ -n "${ONLY:-}" && "$name" != *"$ONLY"* ]] && continue
  URL=$(curl -s -A "$A" -c $CJ -b $CJ -X POST --data-urlencode "csrf_token=$TOK" "https://$U.itch.io/$S/file/$id?source=game_download" | python3.12 -c "import json,sys;print(json.load(sys.stdin)['url'])")
  curl -s -A "$A" "$URL" -o "$OUT/$name"; ls -la "$OUT/$name"
done < $CJ.ids
rm -f $CJ $CJ.ids
