#!/bin/bash
# Rebuild (no captures), print walk 1's waypoints as compiled, run RunAll, wait for Checks/results.txt, print the walk section.
cd "$(dirname "$0")/.."
bash Tools/rebuild.sh nocap || exit 1
unity command --timeout 60 eval -- --code 'var m = typeof(IronCitadel.EditorTools.IronCitadelChecks).GetMethod("BuildWalks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static); var walks = (System.Collections.Generic.List<IronCitadel.WalkDriver.Walk>)m.Invoke(null, null); var sb = new System.Text.StringBuilder(); foreach (var s in walks[0].steps) sb.Append(s.target.x.ToString("F1") + "," + s.target.z.ToString("F1") + " "); return sb.ToString();' 2>&1 | grep -o '"result":"[^"]*"'
rm -f Checks/results.txt
unity command --timeout 120 eval -- --code 'IronCitadel.EditorTools.IronCitadelChecks.RunAll(); return "started";' --timeout 110000 2>&1 | grep -o '"result":"[^"]*"'
for i in $(seq 1 120); do
  if [ -f Checks/results.txt ]; then break; fi
  sleep 5
done
if [ -f Checks/results.txt ]; then tail -n +165 Checks/results.txt; else echo "no results after 10 minutes"; unity command --timeout 20 console -- --tail 8 2>&1 | grep -o '"message":"[^"]*"'; fi
