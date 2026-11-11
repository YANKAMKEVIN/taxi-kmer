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
| Moteur | Unity 6 LTS, pipeline URP (décision de Kevin, 2026-11-11) |
| Langage | C# |
| Qualité | trois niveaux (bas / moyen / haut) via les Quality Settings et les assets URP ; tout effet coûteux doit pouvoir se couper au niveau bas |
| Tests | Unity Test Framework (EditMode pour la logique, PlayMode pour l'intégration) |
| Éditeur | Unity tourne sur le Mac de Kevin ; une session cloud écrit le C# et les données mais ne peut ni ouvrir l'éditeur ni compiler un APK |

Le reste (version exacte, package Android) se fixe à la tâche 1.1.

## Arborescence

```
Assets/Scripts/Core/     logique pure C# (asmdef sans moteur Unity), testable hors Unity
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
python3 -m unittest discover -s tools/osm -p "test_*.py"   # tests de l'import OSM
python3 tools/osm/osm_roads.py fetch --bbox S,O,N,E -o build/osm/zone.osm.json   # téléchargement OSM (réseau requis)
python3 tools/osm/osm_roads.py convert build/osm/zone.osm.json -o Assets/Resources/Data/roads_zone.json
```

Le code de `Assets/Scripts/Core` reste compatible C# 9 (Unity) : pas de `record`, pas d'`init`, pas de bibliothèque absente d'Unity (ex. System.Text.Json, réservé aux tests dotnet de `tools/dotnet/`).
