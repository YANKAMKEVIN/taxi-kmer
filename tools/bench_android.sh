#!/usr/bin/env bash
# Banc de performance sur le téléphone branché (adb) : installe l'APK, dépose le fichier banc.txt (le jeu lance alors le
# pilote automatique et mesure chaque niveau de qualité), attend la fin, rapatrie les résultats.
# Usage : tools/bench_android.sh [chemin de l'APK] [alloc]   (défaut : build/android/TaxiKmer.apk ; « alloc » : allocations seulement)
set -euo pipefail
cd "$(dirname "$0")/.."
APK=${1:-build/android/TaxiKmer.apk}
PKG=com.yankamkevin.taxikmer
ADB=${ADB:-$(command -v adb || echo "$HOME/Library/Android/sdk/platform-tools/adb")}
DATA=/sdcard/Android/data/$PKG/files
OUT=build/banc
mkdir -p "$OUT"

# Le jeu se met en pause écran éteint ou verrouillé : le banc ne mesurerait rien.
"$ADB" shell input keyevent KEYCODE_WAKEUP
if "$ADB" shell dumpsys window | grep -q "isKeyguardShowing=true"; then
  echo "Le téléphone est verrouillé : déverrouille-le (et laisse l'écran allumé) puis relance." >&2
  exit 2
fi
"$ADB" shell svc power stayon usb >/dev/null 2>&1 || true
"$ADB" install -r "$APK" >/dev/null
"$ADB" shell am force-stop $PKG
"$ADB" shell mkdir -p $DATA
"$ADB" shell rm -f $DATA/banc-resultats.txt
echo "${2:-tout}" | "$ADB" shell "cat > $DATA/banc.txt"
"$ADB" logcat -c
"$ADB" shell monkey -p $PKG -c android.intent.category.LAUNCHER 1 >/dev/null
echo "Banc lancé sur $("$ADB" shell getprop ro.product.model | tr -d '\r') (environ 5 minutes)…"
for _ in $(seq 1 120); do
  sleep 5
  if "$ADB" shell "test -f $DATA/banc-resultats.txt" 2>/dev/null; then break; fi
done
"$ADB" shell rm -f $DATA/banc.txt
stamp=$(date +%Y%m%d-%H%M)
"$ADB" pull $DATA/banc-resultats.txt "$OUT/banc-$stamp.txt" >/dev/null 2>&1 || { echo "Pas de résultats (voir adb logcat -s Unity)"; exit 1; }
"$ADB" logcat -d -s Unity | grep -E "Exception|Error" | head -20 > "$OUT/erreurs-$stamp.txt" || true
"$ADB" shell am force-stop $PKG
"$ADB" shell svc power stayon false >/dev/null 2>&1 || true
cat "$OUT/banc-$stamp.txt"
[[ -s "$OUT/erreurs-$stamp.txt" ]] && { echo "Erreurs dans le journal :"; cat "$OUT/erreurs-$stamp.txt"; }
exit 0
