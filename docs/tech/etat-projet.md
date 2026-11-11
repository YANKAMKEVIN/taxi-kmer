# État du projet — Taxi Kmer

**Phase** : jalon 1 en cours.

**Décidé** (Kevin, 2026-11-11) : nom « Taxi Kmer » ; dépôt séparé de Pokemake ; trois types de course (partagée, dépôt, via une appli fictive) ; monde libre ; style réaliste ; Unity 6.3 LTS (6000.3.28f1, URP) avec réglages bas / moyen / haut ; Street View et internet = référence seulement, textures libres puis photos sur place fournies par Kevin.

**Jalon 1** (validé par Kevin) : dispensaire de Messassi → Étoudi, devant le palais de la présidence ; 5 places ; journée de 10 min ; course partagée de 300 à 800 FCFA selon la distance ; versement de 10 000 FCFA ; corpulence des clients (2 places devant selon la carrure) ; commandes à flèches (pas d'inclinaison) ; client qui ne rentre pas : le joueur le refuse ou fait changer un passager de place ; lieux de clients : dispensaire de Messassi, carrefour / marché de Messassi, Borne Fontaine, pont d'Emana, Emana, Étoudi (devant le palais de la présidence).

**Feuille de route** : `docs/design/roadmap.md` (jalons 2 à 11, proposition à valider par Kevin).

**Jalon 1 en cours** : logique pure faite et testée (33 tests, `tools/test_core.sh`) ; outil d'import OSM écrit ; socle Unity fait (tâche 1.1) : projet URP, qualité Bas / Moyen / Haut, tests verts dans Unity, APK lancé sur le S24 de Kevin ; monde OSM Messassi → Étoudi construit dans Unity (tâche 1.2) ; conduite du taxi sur ce monde (tâche 1.3 : taxi jaune provisoire, commandes tactiles décidées par Kevin, caméra réglable au doigt, klaxon provisoire ; 56 tests EditMode et 3 PlayMode verts). Essai sur téléphone des tâches 1.2 et 1.3 en attente (S24 injoignable le 2026-11-11). Détail : `docs/tech/jalon-01.md` (Avancement).

**Prochaine étape** : installer l'APK (conduite) quand le téléphone est joignable et faire régler la conduite par Kevin (`driving.json`) ; tâche 1.5 (clients dans le monde : klaxon pour accepter, arrêt, montée, dépose).

**Tests** : `tools/test_core.sh` (dotnet 8 ; pas installé sur le Mac, un SDK temporaire a servi le 2026-11-11) ; PlayMode : `$U -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode.xml` (sans `-nographics`).

**Téléphone** : Galaxy S24 de Kevin joignable en débogage sans fil (`adb mdns services`, puis `adb install -r build/android/TaxiKmer.apk`).

**Questions ouvertes** : `vision.md` §9, `roadmap.md` (par jalon).

**Mise à jour Kevin (2026-11-11)** : arrivée du jalon 1 déplacée d’Émana à Étoudi devant le palais ; recherche photo ciblée sur les repères du trajet. Lieux et tracé OSM étendus le jour même (tâche 1.2) ; distances mesurées sur la carte, ordre et dispensaire à relire par Kevin (`jalon-01.md`, « À relire par Kevin »).
