#!/bin/bash
# Refresh assets, wait for the editor to come back from the reload, rebuild the Iron Citadel scene, render the captures.
# Run from the project root with the editor open (via the PowerShell tool, so sleep is allowed).
cd "$(dirname "$0")/.."
unity command --timeout 120 eval -- --code "UnityEditor.AssetDatabase.Refresh(); return 1;" >/dev/null 2>&1
sleep 4
for i in $(seq 1 30); do
  out=$(unity command --timeout 20 console_status 2>&1)
  if echo "$out" | grep -q '"compilationFailed"'; then
    if echo "$out" | grep -q '"compiling":false'; then break; fi
  fi
  sleep 3
done
cf=$(echo "$out" | grep -o '"compilationFailed":[a-z]*')
echo "compile: $cf (after $i polls)"
if echo "$cf" | grep -q true; then unity command console -- --level error --tail 20 2>&1 | grep -o '"message":"[^"]*"'; exit 1; fi
if [ -z "$cf" ]; then echo "editor not responding"; exit 1; fi
for i in 1 2 3; do
  res=$(unity command --timeout 300 eval -- --code "IronCitadel.EditorTools.IronCitadelBuilder.Build(); return System.IO.File.ReadAllText(\"Tools/build_result.txt\");" --timeout 280000 2>&1 | grep -o '"result":"[^"]*"')
  if echo "$res" | grep -q built; then echo "$res"; break; fi
  echo "build attempt $i failed: $res"; sleep 5
done
unity command console -- --level error --tail 10 2>&1 | grep -o '"message":"[^"]*"' | head -5
if [ "$1" != "nocap" ]; then
unity command --timeout 240 eval -- --code "IronCitadel.EditorTools.IronCitadelCaptures.RenderAll(); return \"captures done\";" --timeout 230000 2>&1 | grep -o '"result":"[^"]*"'
fi
