# État du projet — Taxi Kmer

Court par règle (`CLAUDE.md`) ; l'historique détaillé est dans `docs/tech/journal.md`.

## Où en est le projet (23/11)

- **Décidé** (Kevin, 11/11) : « Taxi Kmer », Unity 6.3 LTS (6000.3.28f1, URP), Android d'abord, style réaliste, monde libre, Yaoundé reconnaissable (OSM).
- **Jalon 1** (Messassi → Étoudi, une journée de taxi) : code fait, **validation sur téléphone en attente**. Équilibrage tranché (21/11) : journée de 35 min, carburant ×1, courses de 600 à 1 600 FCFA, versement de 6 000 FCFA (versement atteint ≈ 69 % des journées, cible 60-80 %).
- **Jalons 2 à 6 et radio actifs** (trafic, piétons, sauvegarde v6, carrière, police, dialogues, route, radio sans musique) ; **jalons 7 à 9** en logique pure, coupés en attendant Kevin.
- **Série d'idées du 23/11** (`claude/idees-50`) : 36 améliorations faites ou commencées (voir `journal.md`) ; presque tout l'affichage est **compilé hors Unity mais jamais exécuté** : liste à cocher dans `docs/a-faire-mac.md`.
- **Décisions de Kevin du 23/11 au soir** : relief de Yaoundé **activé** (`WorldBuilder.applyTerrain`), avec trafic, piétons, clients et tout ce qui est posé au sol sur les collines (vérifications en jeu : `docs/a-faire-mac.md`, idée 8).
- **Tests** : 520+ tests dotnet (`tools/test_core.sh`), tests Python (`tools/osm`, `tools/texts`), compilation hors Unity (`tools/check_unity_compile.sh`) ; CI GitHub verte.

## Prochaine étape

1. Sur le Mac : ouvrir Unity, lancer EditMode et PlayMode, suivre `docs/a-faire-mac.md` (une ligne par panneau jamais exécuté), construire l'APK et l'AAB.
2. Sur le S24 : jouer une journée complète (validation du jalon 1), mesurer les i/s en Moyen et en Haut avec le trafic, régler conduite, caméra et frein à main.
3. Valider les textes « proposés » avec la page `tools/texts/textes_validation.py page` (répliques, radio), puis `apply`.

## À trancher par Kevin

- Textes « proposés » : 195 + 24 répliques (dont histoires de passagers, événements de ville, vendeurs), 25 textes radio, noms des 4 stations, textes de l'interface (`ui_texts.json`), messages du propriétaire (`phone.json`).
- Valeurs par défaut des nouveautés : poursuite (`police.json` → `chase`), événements de ville (fréquence, ton), vendeurs aux feux (idée à valider), objectifs du jour et leurs primes (`goals.json`, coupées), nom de l'appli de courses, reprise de la journée au lancement (`save.json` → `resumeOnStart`).
- Questions ouvertes des jalons : `vision.md` §9, `roadmap.md`, `jalon-0N.md` ; niveau de concurrence des taxis (`traffic.json` → `competitorPickupChance`) ; vraie station-service et garage.

## Pratique

- Tests : `tools/test_core.sh` (dotnet 8) ; PlayMode : `$U -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode.xml` (sans `-nographics`).
- Téléphone : Galaxy S24 en débogage sans fil (`adb mdns services`, puis `adb install -r build/android/TaxiKmer.apk`).
- Émulateur : AVD `TaxiKmer_Test` (`~/Library/Android/sdk/emulator/emulator -avd TaxiKmer_Test -no-boot-anim -gpu host`) ; ses performances ne représentent pas un téléphone.
