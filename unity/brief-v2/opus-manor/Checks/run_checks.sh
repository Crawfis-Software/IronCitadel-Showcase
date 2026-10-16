#!/usr/bin/env bash
# Re-runs every check and walk on the saved scene (Assets/Scenes/IronCitadel.unity) and waits for
# Checks/results.txt. Needs the Unity CLI with the Pipeline package (installed in this project).
# Usage, from anywhere:  bash Checks/run_checks.sh
set -u
cd "$(dirname "$0")/.."
if ! unity status --project-path . >/dev/null 2>&1; then
  unity open .
  unity status --until-ready --project-path . --timeout 600 >/dev/null
fi
unity status --until-ready --project-path . >/dev/null
# opens the saved scene from disk, writes Map/map.json from it, then enters play mode to run everything
unity command --timeout 120 eval -- --code 'return Hollowmere.EditorTools.CheckRunner.RunAll();' --timeout 110000
for i in $(seq 1 180); do
  if grep -q '^SUMMARY' Checks/results.txt 2>/dev/null; then break; fi
  sleep 5
done
grep '^SUMMARY' Checks/results.txt || { echo "no SUMMARY line yet; see Checks/results.txt and the Unity console"; exit 1; }
grep -q '^SUMMARY: [0-9]* passed, 0 failed' Checks/results.txt
