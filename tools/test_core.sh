#!/usr/bin/env bash
# Tests de la logique pure hors Unity (dotnet + NUnit). Usage : tools/test_core.sh [filtre dotnet test]
set -euo pipefail
cd "$(dirname "$0")/dotnet"
if [[ $# -gt 0 ]]; then
  dotnet test --nologo -v q --filter "$1"
else
  dotnet test --nologo -v q
fi
