#!/usr/bin/env bash
# Hollowmere Manor (2026-10-14): seed 1's level (level.json, copied from v2/) built from the Horror Mansion rooms.
# tools/run-seed.sh without the design stage: pick (from the mansion's room index), build (no placements, no
# captures: it writes floor.json), tactics, build again (placements and captures) into IronCitadelV2_HM.unity under
# the builder's mansion profile (BuildMansion), then the fold. The agent editor must be open. Stage times go to
# times.tsv, each stage's output to stage-<name>.txt.
#   bash run-hm.sh [<RUN>]        (RUN defaults to this folder)
set -u
RUN="${1:-C:/Repos/Github/Crawfis-Software/IronCitadel/v2/hm}"
PCG=C:/Repos/Github/Crawfis-Software/PCGTools
P=C:/Repos/Github/Crawfis-Software/MacroTileBuilder-agent
INDEX=C:/Repos/Github/Crawfis-Software/MacroTileBuilder/Assets/CrawfisSoftware/PolygonHorrorMansion/Rooms/rooms-index.json
export UNITY_NO_BANNER=1 UNITY_NO_PAGER=1
T="$RUN/times.tsv"
: > "$T"

stage() {
  local name="$1"; shift
  local t0 t1 rc
  t0=$(date +%s.%N)
  "$@" > "$RUN/stage-$name.txt" 2>&1
  rc=$?
  t1=$(date +%s.%N)
  python -c "print('%s\t%d\t%.1f' % ('$name', $rc, $t1 - $t0))" >> "$T"
  return $rc
}

cd "$PCG" || exit 1
: > "$RUN/times-self.txt"
stage pick python C:/Repos/Github/Crawfis-Software/IronCitadel/kit/pick/pick_rooms_v2.py "$RUN/level.json" --index "$INDEX" || exit 1
rm -f "$RUN/placements.json" "$RUN/tactics.json"
stage build1 unity command --project-path "$P" --timeout 300 eval -- --code \
  "IronCitadelV2.Editor.IronCitadelV2Builder.BuildMansion(\"$RUN\", \"HM\", false); return \"built\";" --timeout 290000 || exit 1
grep -q '"result":"built"' "$RUN/stage-build1.txt" || { echo "build1 failed: see $RUN/stage-build1.txt"; exit 1; }
grep 'done in' "$RUN/build-log.txt" | tail -n 1 | sed 's/^/build1 self: /' >> "$RUN/times-self.txt"
stage tactics dotnet run --project samples/IronCitadelV2/Tactics -- --run "$RUN" || exit 1
stage build2 unity command --project-path "$P" --timeout 300 eval -- --code \
  "IronCitadelV2.Editor.IronCitadelV2Builder.BuildMansion(\"$RUN\", \"HM\", true); return \"built\";" --timeout 290000 || exit 1
grep -q '"result":"built"' "$RUN/stage-build2.txt" || { echo "build2 failed: see $RUN/stage-build2.txt"; exit 1; }
grep 'done in' "$RUN/build-log.txt" | tail -n 1 | sed 's/^/build2 self: /' >> "$RUN/times-self.txt"
stage fold dotnet run --project samples/IronCitadelV2/Design -- report --out "$RUN/" || exit 1
cat "$T"
