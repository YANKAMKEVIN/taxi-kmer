# État du projet — Taxi Kmer

**Phase** : jalon 1 en cours.

**Décidé** (Kevin, 2026-11-11) : nom « Taxi Kmer » ; dépôt séparé de Pokemake ; trois types de course (partagée, dépôt, via une appli fictive) ; monde libre ; style réaliste ; Unity 6.3 LTS (6000.3.28f1, URP) avec réglages bas / moyen / haut ; Street View et internet = référence seulement, textures libres puis photos sur place fournies par Kevin.

**Jalon 1** (validé par Kevin) : dispensaire de Messassi → Étoudi, devant le palais de la présidence ; 5 places ; journée de 10 min ; course partagée de 300 à 800 FCFA selon la distance ; versement de 10 000 FCFA ; corpulence des clients (2 places devant selon la carrure) ; commandes à flèches (pas d'inclinaison) ; client qui ne rentre pas : le joueur le refuse ou fait changer un passager de place ; lieux de clients : dispensaire de Messassi, carrefour / marché de Messassi, Borne Fontaine, pont d'Emana, Emana, Étoudi (devant le palais de la présidence).

**Feuille de route** : `docs/design/roadmap.md` (jalons 2 à 11, proposition à valider par Kevin).

**Jalon 1 en cours** : logique pure faite et testée (33 tests, `tools/test_core.sh`) ; outil d'import OSM écrit ; socle Unity fait (tâche 1.1) : projet URP, qualité Bas / Moyen / Haut, tests verts dans Unity, APK lancé sur le S24 de Kevin ; monde OSM Messassi → Étoudi construit dans Unity (tâche 1.2) ; conduite du taxi sur ce monde (tâche 1.3 : taxi jaune provisoire, commandes tactiles décidées par Kevin, caméra réglable au doigt, klaxon provisoire ; 56 tests EditMode et 3 PlayMode verts). Vérifié sur émulateur Android le 2026-11-11 (jeu lancé, commandes tactiles et marche arrière OK, pas d'erreur) ; essai sur le S24 en attente (injoignable). Clients dans le monde (tâche 1.5, 2026-11-12) : attente aux 6 lieux, annonce destination + prix, klaxon pour accepter, bouton contextuel pour faire monter / descendre, paiement ; 64 tests EditMode, 4 PlayMode et 71 dotnet verts. HUD et bilan de fin de journée (tâche 1.6, 2026-11-12) : ligne « Versement x / 10 000 » à l'écran, à 20:00 on peut encore déposer ses passagers ou « Terminer la journée » à l'arrêt, puis écran de bilan (recettes, versement, carburant, reste) et « Nouvelle journée » ; 80 tests EditMode, 5 PlayMode et 87 dotnet verts. Sélecteur de qualité (tâche 1.7, 2026-11-14, **code écrit en session cloud, pas encore compilé dans Unity**) : bouton « Qualité : Moyen · 30 i/s » à gauche (touche G) qui fait défiler Bas / Moyen / Haut, applique ombres et résolution (assets URP), distance d'affichage, brouillard et images par seconde visées (30 / 30 / 60), choix gardé ; 94 tests dotnet verts, 1 test PlayMode ajouté. Détail : `docs/tech/jalon-01.md` (Avancement).

**Prochaine étape** : sur le Mac, ouvrir Unity, lancer les tests EditMode et PlayMode, construire l'APK et mesurer les images par seconde en Moyen et en Haut (critère de la tâche 1.7 : 30 en Moyen sur un milieu de gamme, 60 en Haut sur le S24) ; puis validation du jalon 1 sur téléphone avec Kevin (une journée complète). Essai sur le S24 et réglage de la conduite (`driving.json`) et des clients (`street.json`) avec Kevin dès que le téléphone est joignable.

**Tests** : `tools/test_core.sh` (dotnet 8 ; pas installé sur le Mac, un SDK temporaire a servi le 2026-11-11) ; PlayMode : `$U -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode.xml` (sans `-nographics`).

**Téléphone** : Galaxy S24 de Kevin joignable en débogage sans fil (`adb mdns services`, puis `adb install -r build/android/TaxiKmer.apk`).

**Émulateur** (sans téléphone) : AVD `TaxiKmer_Test` créé pour le projet (8 Go de données, 4 Go de RAM, 4 cœurs ; l'AVD `Medium_Phone` est trop plein et à 1 cœur) : `~/Library/Android/sdk/emulator/emulator -avd TaxiKmer_Test -no-boot-anim -gpu host`, puis `adb install -r build/android/TaxiKmer.apk`. Ses performances ne représentent pas un téléphone.

**Questions ouvertes** : `vision.md` §9, `roadmap.md` (par jalon).

**Mise à jour Kevin (2026-11-11)** : arrivée du jalon 1 déplacée d’Émana à Étoudi devant le palais ; recherche photo ciblée sur les repères du trajet. Lieux et tracé OSM étendus le jour même (tâche 1.2) ; distances mesurées sur la carte, ordre et dispensaire à relire par Kevin (`jalon-01.md`, « À relire par Kevin »).
