# Taxi Kmer

Jeu 3D sur la vie d'un taximan à Yaoundé (tronçon Messassi → Bastos au départ, puis extension, puis Douala). Style **réaliste** (lieux reconnaissables). Cible principale : Android (surtout des téléphones courants, comparables à ceux d'Europe), avec réglages de qualité bas / moyen / haut ; web en démo.

Kevin est le **directeur du jeu** : il conçoit, Claude implémente. Toute décision de game design non documentée → la lui poser, ne pas l'inventer.

## À lire avant de travailler

- `docs/design/vision.md` — **source de vérité du design** (piliers, boucles, mécaniques, questions ouvertes).
- `docs/tech/etat-projet.md` — où en est le projet, prochaine étape (court, mis à jour à chaque tâche terminée).

## Règles de travail

1. Données (types de course, clients, prix, charges, dialogues) = fichiers de données, jamais codées en dur dans la logique.
2. Logique de jeu séparée de la présentation (testable sans scène).
3. Petites questions (durées, tailles, couleurs…) → valeur par défaut notée dans le commit et `etat-projet.md` ; seules les vraies décisions de design vont à Kevin.
4. Monde : Yaoundé **reconnaissable** (routes OpenStreetMap, ODbL à créditer ; lieux emblématiques faits main). Jamais de données 3D Google ou d'autres sources sous licence incompatible.

## Décisions techniques

| Sujet | Valeur |
|---|---|
| Moteur | Unity 6.3 LTS (**6000.3.28f1**), pipeline URP 17.3 (décision de Kevin, 2026-11-11) |
| Langage | C# |
| Qualité | trois niveaux (bas / moyen / haut) via les Quality Settings et les assets URP ; tout effet coûteux doit pouvoir se couper au niveau bas |
| Tests | Unity Test Framework (EditMode pour la logique, PlayMode pour l'intégration) |
| Éditeur | Unity tourne sur le Mac de Kevin ; une session cloud écrit le C# et les données mais ne peut ni ouvrir l'éditeur ni compiler un APK |

Android : identifiant `com.yankamkevin.taxikmer`, Android 8 minimum (API 26), IL2CPP, ARM64, paysage. Niveaux de qualité : `Assets/Settings/Rendering/URP-{Bas,Moyen,Haut}.asset`, Moyen par défaut ; tout se régénère avec `ProjectSetup.Run`.

## Arborescence

```
Assets/Scripts/Core/     logique pure C# (asmdef sans moteur Unity), testable hors Unity ; Core/World = géométrie du monde, Core/Traffic = trafic et piétons
Assets/Scripts/World/    construction du monde OSM dans Unity (WorldBuilder), caméra provisoire, crédit OSM
Assets/Editor/           outils d'éditeur (configuration du projet, build Android)
Assets/Settings/         assets URP par niveau de qualité
Assets/Scenes/           scènes (Boot = scène de démarrage provisoire)
Assets/Tests/EditMode/   tests NUnit de la logique (Unity Test Framework et dotnet)
Assets/Resources/Data/   fichiers de données JSON (lus par JsonUtility dans Unity)
tools/dotnet/            projet dotnet qui compile Core + tests hors Unity
tools/osm/               import des routes OpenStreetMap
docs/design/             vision, feuille de route
docs/tech/               état du projet, plans de jalons
```

## Commandes

```bash
tools/test_core.sh                     # tests de la logique hors Unity (dotnet 8 + NUnit), aussi lancés par la CI
tools/check_unity_compile.sh           # compile le code Unity hors Unity (références UnityEngine 2021.3 + stubs) : pour les sessions cloud
U=/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity
$U -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile Logs/tests.log
$U -batchmode -nographics -projectPath . -buildTarget Android -executeMethod TaxiKmer.EditorTools.BuildAndroid.Run -quit -logFile Logs/build.log   # → build/android/TaxiKmer.apk
$U -batchmode -nographics -projectPath . -executeMethod TaxiKmer.EditorTools.ProjectSetup.Run -quit -logFile Logs/setup.log   # réapplique qualité, lecteur Android, scène Boot
python3 -m unittest discover -s tools/osm -p "test_*.py"   # tests de l'import OSM
# Monde OSM du jalon 1 (le Python de Xcode échoue en HTTPS avec Overpass : téléchargement par curl)
Q=$(python3 -c "import sys;sys.path.insert(0,'tools/osm');import osm_roads;print(osm_roads.overpass_query((3.905,11.505,3.952,11.535)))")
curl -s -f -A taxi-kmer-osm-import --data-urlencode "data=$Q" https://overpass-api.de/api/interpreter -o build/osm/messassi_etoudi.osm.json
python3 tools/osm/osm_roads.py convert build/osm/messassi_etoudi.osm.json -o build/osm/roads_full.json
python3 tools/osm/osm_route.py build/osm/roads_full.json tools/osm/landmarks_messassi_etoudi.json --stops Assets/Resources/Data/stops_messassi_etoudi.json --world Assets/Resources/Data/world_messassi_etoudi.json --corridor 250
python3 tools/osm/osm_traffic.py Assets/Resources/Data/world_messassi_etoudi.json   # trajectoires du trafic par sens (à relancer après osm_route.py)
$U -batchmode -projectPath . -executeMethod TaxiKmer.EditorTools.WorldSnapshots.Run -quit -logFile Logs/snap.log   # captures dans build/snapshots (sans -nographics)
```

Le batch mode refuse un projet déjà ouvert dans l'éditeur : fermer Unity avant ces commandes.

Le code de `Assets/Scripts/Core` reste compatible C# 9 (Unity) : pas de `record`, pas d'`init`, pas de bibliothèque absente d'Unity (ex. System.Text.Json, réservé aux tests dotnet de `tools/dotnet/`).
