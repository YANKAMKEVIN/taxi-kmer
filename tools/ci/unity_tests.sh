#!/usr/bin/env bash
# Lance une plateforme de tests Unity (EditMode ou PlayMode) et résume le résultat ; code de sortie non nul en cas d'échec.
# Usage : tools/ci/unity_tests.sh EditMode|PlayMode   (variable UNITY : chemin de l'éditeur)
set -uo pipefail
cd "$(dirname "$0")/../.."
PLATFORM=$1
UNITY=${UNITY:-/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity}
XML=Logs/$(echo "$PLATFORM" | tr '[:upper:]' '[:lower:]').xml
mkdir -p Logs
ARGS=(-batchmode -projectPath . -runTests -testPlatform "$PLATFORM" -testResults "$XML" -logFile "Logs/$PLATFORM.log")
[[ $PLATFORM == EditMode ]] && ARGS=(-nographics "${ARGS[@]}")
"$UNITY" "${ARGS[@]}"
code=$?
if [[ ! -f $XML ]]; then
  echo "::error::Pas de résultats $PLATFORM (compilation ?)"
  grep -E "error CS" "Logs/$PLATFORM.log" | sort -u | head -30
  exit 1
fi
python3 - "$XML" <<'PY'
import sys, xml.etree.ElementTree as E
root = E.parse(sys.argv[1]).getroot()
print(f"{root.get('total')} tests, {root.get('passed')} réussis, {root.get('failed')} échoués")
for t in root.iter('test-case'):
    if t.get('result') == 'Failed':
        m = t.find('.//message')
        print(f"::error::{t.get('fullname')} : {(m.text or '').strip()[:400] if m is not None else ''}")
PY
exit $code
