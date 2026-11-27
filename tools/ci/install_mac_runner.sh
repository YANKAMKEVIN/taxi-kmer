#!/usr/bin/env bash
# À lancer une fois par Kevin sur son Mac : installe l'exécuteur GitHub Actions de taxi-kmer (étiquette « unity »), qui
# démarre tout seul avec la session. Il fait tourner .github/workflows/unity.yml (tests Unity, parcours, APK).
# Prérequis : gh connecté (gh auth status), Unity 6000.3.28f1 installé et activé dans Unity Hub.
set -euo pipefail
REPO=YANKAMKEVIN/taxi-kmer
DIR="$HOME/actions-runner-taxi-kmer"
VERSION=$(gh api repos/actions/runner/releases/latest --jq .tag_name | sed 's/^v//')
ARCH=$( [[ $(uname -m) == arm64 ]] && echo arm64 || echo x64 )
mkdir -p "$DIR" && cd "$DIR"
if [[ ! -f ./config.sh ]]; then
  echo "Téléchargement de l'exécuteur GitHub $VERSION ($ARCH)…"
  curl -fsSL -o runner.tar.gz "https://github.com/actions/runner/releases/download/v$VERSION/actions-runner-osx-$ARCH-$VERSION.tar.gz"
  tar xzf runner.tar.gz && rm runner.tar.gz
fi
TOKEN=$(gh api -X POST "repos/$REPO/actions/runners/registration-token" --jq .token)
./config.sh --unattended --replace --url "https://github.com/$REPO" --token "$TOKEN" \
  --name "mac-de-kevin" --labels unity --work _work
./svc.sh install
./svc.sh start
echo "Exécuteur installé : https://github.com/$REPO/settings/actions/runners"
