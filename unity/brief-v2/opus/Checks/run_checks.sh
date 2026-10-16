#!/usr/bin/env sh
# Re-runs every check and walk on the saved scene Assets/Scenes/IronCitadel.unity and waits for
# Checks/results.txt. Needs this project's editor open (from the project folder: unity open . ; then
# unity status --until-ready --project-path .). Run from anywhere: sh Checks/run_checks.sh
set -e
cd "$(dirname "$0")/.."
before=$(grep -o 'END OF RUN [0-9-]*' Checks/results.txt 2>/dev/null || true)
unity command --timeout 120 eval -- --code 'UnityEditor.EditorApplication.ExecuteMenuItem("Window/General/Game"); IronCitadel.Checks.CheckRunner.RunAll(); return 1;' --timeout 110000
i=0
while [ $i -lt 120 ]; do
  now=$(grep -o 'END OF RUN [0-9-]*' Checks/results.txt 2>/dev/null || true)
  if [ -n "$now" ] && [ "$now" != "$before" ]; then
    grep '^SUMMARY' Checks/results.txt
    exit 0
  fi
  i=$((i + 1))
  sleep 5
done
echo "timed out waiting for Checks/results.txt" >&2
exit 1
