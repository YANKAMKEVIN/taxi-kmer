#!/usr/bin/env bash
# Compile le code Unity hors Unity (voir tools/unity_check/UnityCheck.csproj). Usage : tools/check_unity_compile.sh
set -euo pipefail
cd "$(dirname "$0")/.."
REF=build/unityref
if [[ ! -f $REF/lib/netstandard2.0/UnityEngine.CoreModule.dll ]]; then
  mkdir -p $REF
  curl -sS -f -o $REF/modules.nupkg https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg
  python3 -c "import zipfile;zipfile.ZipFile('$REF/modules.nupkg').extractall('$REF')"
fi
# API Unity 6 absentes des références 2021.3 : ignorées.
UNITY6='linearVelocity|linearDamping|angularDamping'
out=$(dotnet build tools/unity_check/UnityCheck.csproj -nologo -v q 2>&1 || true)
errors=$(echo "$out" | grep -E ' error ' | grep -Ev "$UNITY6" | sed 's| \[.*||' | sort -u || true)
if [[ -n $errors ]]; then
  echo "$errors"
  exit 1
fi
echo "Code Unity : compilation OK (hors API Unity 6 non vérifiables : $UNITY6)."
