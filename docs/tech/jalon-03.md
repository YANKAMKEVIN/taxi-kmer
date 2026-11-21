# Jalon 3 — Les jours s'enchaînent (plan technique)

> Journée de **35 min** réelles depuis le 2026-11-21 (décision de Kevin, piste B). Ouvert le 2026-11-15, **partie technique seulement** (la sauvegarde), à la demande de Kevin, pendant que le jalon 1 attend sa validation et que le jalon 2 attend l'essai sur téléphone. Contenu et critères : `docs/design/roadmap.md`. Le propriétaire a suivi le 2026-11-15 (décisions de Kevin) ; calendrier, carburant et réputation le 2026-11-21 avec des valeurs par défaut (questions ouvertes réglables dans les données).

**Accepté si** (feuille de route) : on joue 3 jours de suite, on quitte l'appli à n'importe quel moment et on reprend sans perte ; tests de la sauvegarde, y compris fichier corrompu ou version ancienne.

## Tâches

| Tâche | État |
|---|---|
| 3.1 Sauvegarde sûre | **faite** (2026-11-15, session cloud) : `Core/Save` ; 13 tests (coupure de courant à chaque étape d'une écriture, fichier tronqué, modifié, vide, illisible, sans copie de secours, version plus récente, version ancienne, journée sauvegardée puis reprise, bilan ajouté à la carrière) |
| 3.2 Sauvegarde dans le jeu | **écrite, à essayer dans Unity** : `SaveService` (créé par `StreetView`) ; sauvegarde la journée en cours toutes les 30 s et quand l'appli passe en arrière-plan ou se ferme ; enregistre le bilan en fin de journée ; reprise au lancement **coupée** (`resumeOnStart: false` dans `save.json`) jusqu'à la validation du jalon ; test PlayMode de bout en bout (sauvegarder, relancer, reprendre) dans un dossier temporaire |
| 3.3 Journée suivante, épargne et propriétaire | **faite, à essayer dans Unity** (2026-11-15, décisions de Kevin) : l'argent qui reste passe d'un jour à l'autre (épargne) ; horaires du patron (6 h - 20 h, `day.json`), annoncés au début de chaque jour ; versement manqué → le propriétaire menace ; 3 manques sur 7 jours → renvoyé, la carrière s'arrête et le bouton du bilan propose « Nouvelle carrière » ; sinon « Jour suivant » (jour + 1, épargne gardée). `Core/Career/DriverCareer` (8 tests), `owner.json` ; sauvegarde passée en version 2 (épargne, manques, renvoi) avec sa migration testée |
| 3.4 Calendrier | **fait, à essayer dans Unity** (2026-11-21, session cloud) : `Core/Day/Calendar` (5 tests), `calendar.json` ; le jour 1 est un lundi ; chaque jour de la semaine multiplie la cadence d'arrivée des clients (`Street.SpawnRateScale`) et la densité du trafic et des piétons ; dimanche plus calme (×0,6), jours de marché mercredi et samedi (clients ×1,25, trafic ×1,15), annoncés au début de la journée ; « Jour 3 · mer. » dans le HUD, jour de la semaine dans le titre du bilan. Calendrier validé par Kevin le 2026-11-21 |
| 3.5 Carburant et station-service | **fait, à essayer dans Unity** (2026-11-21, session cloud) : `Core/Fuel` (réservoir, station, textes ; 11 tests), `fuel.json` ; le réservoir se vide en roulant et passe d'un jour à l'autre ; à l'arrêt à la station-service, le bouton contextuel achète pour 2 000 FCFA de carburant à chaque appui (sans dépasser le plein) ; le bilan compte le carburant **acheté** dans la journée (et non plus brûlé) ; réservoir vide : ~~le taxi ne dépasse plus 8 km/h~~ panne sèche avec choix « pousser » ou « technicien du quartier » (2026-11-21) ; jauge à droite de l'heure (rouge sous 15 %) ; station provisoire (auvent rouge et blanc) ; sauvegarde version 3 (réservoir, carburant acheté) avec sa migration testée ; test PlayMode de la station |
| 3.6 Réputation | **faite, à essayer dans Unity** (2026-11-21, session cloud) : `Core/Career/Reputation` (7 tests), `reputation.json` ; score de 0 à 100 (50 au départ) qui passe d'un jour à l'autre ; monte à chaque course payée, baisse quand un client accepté abandonne, quand des passagers descendent sans arriver (accident, journée terminée avec eux), quand on renverse un piéton, et en roulant trop vite avec des passagers ; **visible** (décision de Kevin du 2026-11-21 ; réglable par `visible`) : « Réputation 52 » à droite de la jauge de versement et une ligne dans le bilan ; sauvegarde version 4 avec sa migration testée. **Effets branchés** (tranché par Kevin le 21/11) : voir « Effets de la réputation » |

## Choix techniques

- **Ce qui est sauvegardé** (`SaveData`, version 1) : numéro du jour ; bilans des journées terminées ; journée en cours : heure, recettes, courses, kilomètres, dépenses imprévues, passagers à bord (destination, prix, carrure, banquette), passagers partis sans payer, position et sens du taxi, graine des clients. Les clients qui attendent au bord de la route, le trafic et les piétons ne sont pas sauvegardés : ils sont recréés (rien d'argent n'en dépend). Rien de ce qui attend une décision de Kevin (dette, réputation, épargne).
- **Écriture sûre** : fichier temporaire écrit jusqu'au disque, l'ancienne sauvegarde devient la copie de secours, puis le temporaire la remplace. Coupure à n'importe quel moment : on retrouve l'ancienne ou la nouvelle sauvegarde, jamais rien.
- **Lecture** : empreinte FNV-1a 64 bits du contenu ; fichier abîmé → mis de côté (`partie-abimee-N.json`, jamais effacé) et reprise du temporaire complet ou de la copie de secours ; rien de lisible → nouvelle partie ; sauvegarde d'une version plus récente du jeu → rien n'est chargé ni écrasé.
- **Versions** : `SaveMigrations.CurrentVersion` ; un champ ajouté prend sa valeur par défaut tout seul ; une étape de migration n'est écrite que si le sens d'un champ change.
- **Fichiers** : `Application.persistentDataPath/sauvegarde/partie.json` (+ `.bak`, `.tmp`). Les tests n'y touchent jamais (dossier temporaire).
- La logique est en C# pur (`ISaveStorage`, `ISaveSerializer`) : testée hors Unity avec un disque en mémoire qui simule les coupures, et System.Text.Json ; dans Unity, `FileSaveStorage` et JsonUtility (les tests EditMode utilisent JsonUtility dans Unity).

## Valeurs par défaut du propriétaire (`owner.json`, à régler avec Kevin)

- Renvoyé après **3 versements manqués sur les 7 derniers jours** (les manques plus anciens sont oubliés).
- Le versement est dû en entier : si la recette du jour (moins carburant et frais imprévus) ne suffit pas, le chauffeur **complète avec son épargne** ; ce qui manque encore est un versement manqué (choix de Claude, à confirmer).
- Carburant et frais imprévus se paient d'abord ; s'ils dépassent la recette, l'épargne paie (elle peut devenir négative).
- Taxi saisi par la gendarmerie → le propriétaire renvoie le chauffeur (choix de Claude, à confirmer).
- Après un renvoi : nouvelle carrière (jour 1, épargne 0) ; l'ancienne sauvegarde reste en copie de secours.
- La carrière (jour, épargne, propriétaire) se lit toujours au lancement ; seule la reprise d'une journée en cours reste coupée (`resumeOnStart`).

## Valeurs par défaut (`save.json`)

Sauvegarde automatique toutes les 30 s réelles ; aussi à la mise en arrière-plan et à la fermeture ; reprise au lancement coupée.

## Calendrier (`calendar.json`, validé par Kevin le 2026-11-21)

- Le jour 1 de la carrière est un **lundi** (`firstWeekday`).
- **Dimanche plus calme** : clients et trafic ×0,6.
- **Jours de marché : mercredi et samedi** (« marché de Messassi ») : clients ×1,25, trafic ×1,15 (validé par Kevin).
- Le patron fait travailler tous les jours, **dimanche compris** (calme mais travaillé, validé par Kevin) ; les coefficients ×0,6 / ×1,25 / ×1,15 restent réglables.

## Valeurs par défaut du carburant (`fuel.json`, à régler avec Kevin)

- Réservoir de **40 L** ; une carrière commence avec **12 L** ; ensuite le niveau passe d'un jour à l'autre.
- Consommation : 8 L/100 km (day.json) **×8** (`consumptionScale`), car une journée de jeu ne fait que quelques kilomètres : environ 5 L par journée, soit à peu près 4 300 FCFA à 840 FCFA/L. C'est plus que l'ancien calcul à la distance (≈ 500 FCFA par jour) : l'équilibre recettes / versement / carburant est à revoir avec Kevin.
- Un appui à la station achète pour **2 000 FCFA** (on « met 2 000 »), payé sur la recette du jour.
- **Une station**, « Station-service d'Emana », à 1 850 m du dispensaire (entre le pont d'Emana et Emana) : **position provisoire**, à remplacer par une vraie station repérée par Kevin.
- **Panne sèche** (décision de Kevin, 2026-11-21 : « On pousse, ou on appelle un technicien du quartier ») : la règle des 8 km/h est remplacée par un choix. Le taxi s'arrête (ni marche avant ni marche arrière) et deux boutons s'affichent hors de la station-service (touches P et T au clavier) :
  - **Pousser le taxi** : il avance **au pas, 3 km/h** (`pushKmh`) jusqu'à ce qu'on remette du carburant ; 400 m jusqu'à la station = 8 min réelles, plus de 3 h de jeu ;
  - **Appeler un technicien du quartier** : il arrive après **45 min de jeu** (`technicianDelayMinutes`, temps qui passe d'un coup comme à l'hôpital), verse **5 L** (`technicianLitres`) et prend **2 000 FCFA de déplacement** (`technicianFeeFcfa`) plus le carburant au prix du litre, soit **6 200 FCFA** carburant compris, une ligne « Technicien du quartier (carburant compris) » au bilan ; puis on repart.
  - À la station-service, le bouton contextuel propose toujours d'acheter du carburant. Si l'heure de fin passe pendant l'attente du technicien et que le taxi est vide, la journée se termine.
  - **Passagers : ils descendent sans payer** (tranché par Kevin le 21/11) : dès que le réservoir est vide, tous les passagers descendent, leurs courses vont aux « passagers partis sans payer » du bilan et chacun compte comme passager non déposé pour la réputation (-2, valeur existante) ; message « Panne sèche : 2 passagers descendent sans payer » (`fuel.json` : `passengersLeaveWhenDry`, `Street.DryTankLeft` ; 2 tests).
  - Valeurs par défaut de Claude (à régler avec Kevin) : vitesse en poussant, délai et prix du technicien. Logique : `FuelTank` (`Stranded`, `Pushing`, `StartPushing`), `TechnicianCall`, `Shift.CallTechnician`, `Street.DryTankChoice / PushTaxi / CallTechnician` (5 tests) ; affichage `StreetView.UpdateDryTank` (**non exécuté dans Unity**). La sauvegarde ne garde pas le choix « pousser » : après une reprise, le choix est reproposé.

## Valeurs par défaut de la réputation (`reputation.json`, à régler avec Kevin)

- **Visible** (`visible: true`) : **validé par Kevin le 2026-11-21** ; `false` la cacherait (elle compterait toujours).
- Score de 0 à 100, 50 au départ ; paliers : très mauvaise, mauvaise (20), correcte (40), bonne (60), excellente (80).
- +1 par client payé ; -2 par client accepté qui abandonne ; -2 par passager descendu sans arriver ; -10 piéton blessé léger, -25 blessé grave ; avec des passagers, au-delà de 60 km/h, -1 toutes les 10 s.

## Effets de la réputation (tranché par Kevin le 21/11)

Kevin a retenu les quatre pistes : **clients plus ou moins patients, prix acceptés, attitude du propriétaire, contrôles de police**. Branché avec des coefficients par défaut (`reputation.json` → `effects`, `ReputationEffects`), facteur = 1 + (score - 50) × coefficient, borné :

| Effet | Où | Coefficient par défaut | Score 0 | 50 | 100 |
|---|---|---|---|---|---|
| Patience d'un client accepté | `Street.AcceptedPatienceSeconds` | +1 % par point | ×0,5 (15 s) | ×1 (30 s) | ×1,5 (45 s) |
| Prix acceptés : chance qu'un client négocie (jalon 5) | négociation du jalon 5 (`haggleChance` × facteur) | -1,2 % par point | ×1,6 | ×1 | ×0,4 |
| Attitude du propriétaire : manques tolérés avant le renvoi | `DriverCareer.CloseDay(summary, effects)` | +1 dès 80, -1 sous 20 | 2 manques | 3 | 4 (dès 80) |
| Contrôles de police : chance d'être arrêté (jalon 4) | `PoliceSchedule.Update(…, stopFactor)` | -0,6 % par point | ×1,3 | ×1 | ×0,7 |

`effects.enabled: false` coupe tout. Tests : `ReputationTests` (+4 : facteurs bornés, effets coupés, clients plus ou moins patients dans la rue, propriétaire plus ou moins tolérant). **À régler avec Kevin** : tous les coefficients.

## Versement de 7 000 FCFA (tranché par Kevin le 21/11)

`day.json` → `versement: 7000` (et la valeur par défaut de `DayConfig`). Carburant, prix et durée inchangés. Simulation refaite (`tools/test_core.sh Balance`, même chauffeur automatique, 30 journées par durée, 40 km/h, réservoir plein, carburant de `fuel.json` ×8) :

| Durée | Recette moy. (min - max) | Carburant brûlé | Reste après carburant | **Versement de 7 000 atteint, carburant compris** | Recette seule ≥ 7 000 |
|---|---|---|---|---|---|
| 30 min | 7 566 (6 400 - 8 900) | 9 748 | -2 181 | 0 / 30 | 22 / 30 |
| **35 min (day.json)** | 8 636 (6 600 - 10 500) | 11 400 | -2 763 | **0 / 30 (0 %)** | **29 / 30 (97 %)** |
| 40 min | 10 533 (8 400 - 12 300) | 12 969 | -2 435 | 0 / 30 | 30 / 30 |

Selon la consommation (35 min, 10 journées) : à ×1, le versement est atteint carburant compris dans 3 / 10 journées à 30 km/h, 4 / 10 à 40 km/h et 10 / 10 à 50 km/h ; à ×4 et ×8, dans aucune. Semaine avec le propriétaire (×8, 40 km/h) : versement manqué chaque jour (recette 8 200 à 9 200, carburant acheté 10 000 à 14 000), **renvoyé le 3ᵉ jour**.

**Pour Kevin** : avec 7 000, la recette seule couvre le versement presque toujours (97 % des journées de 35 min), mais **le carburant ×8 coûte toujours plus que la recette** (≈ 540 FCFA par km pour ≈ 400 de recette) : la part des journées où le versement est atteint, recette et carburant compris, reste **0 %**. Le seul levier restant est la consommation (`consumptionScale` : à ×1, ≈ 40 % des journées à 40 km/h) ou les prix ; rien d'autre n'a été changé.

## Équilibrage, piste B : journée de 35 minutes (décision de Kevin, 2026-11-21)

**Décision de Kevin** : piste B, journée allongée à 30-40 min réelles ; **carburant : garder le réalisme actuel** (consommation inchangée : 8 L/100 km ×8), on équilibre ailleurs. Appliqué : `day.json` → `dayLengthRealSeconds: 2100` (**35 min**, valeur par défaut choisie dans la fourchette ; 1 800 à 2 400 se règlent dans les données). Aucune autre valeur changée (ni versement, ni prix, ni carburant).

Simulation refaite (`BalanceSimulationTests.DayLength_VersementReachable_Report`, même chauffeur automatique que ci-dessous, 40 km/h, réservoir plein, carburant de `fuel.json`, **30 journées par durée**) :

| Durée | Courses | Recette moy. (min - max) | km | Carburant brûlé | Reste après carburant | Versement atteint après carburant | Recette seule ≥ 10 000 |
|---|---|---|---|---|---|---|---|
| 10 min (avant) | 6,2 | 2 243 (1 400 - 3 100) | 6,1 | 3 291 | -1 048 | **0 / 30** | 0 / 30 |
| 30 min | 20,8 | 7 566 (6 400 - 8 900) | 18,1 | 9 748 | -2 181 | **0 / 30** | 0 / 30 |
| **35 min (day.json)** | 23,1 | 8 636 (6 600 - 10 500) | 21,2 | 11 400 | **-2 763** | **0 / 30** | 3 / 30 |
| 40 min | 28,1 | 10 533 (8 400 - 12 300) | 24,1 | 12 969 | -2 435 | **0 / 30** | 21 / 30 |

Selon la vitesse (35 min, 10 journées chacune) :

| Vitesse | Courses | Recette | km | Carburant ×1 | ×4 | ×8 (actuel) | Reste ×8 | Journées où le versement est atteint (×8) |
|---|---|---|---|---|---|---|---|---|
| 30 km/h | 20,3 | 7 430 | 16,1 | 1 093 | 4 347 | 8 691 | -1 261 | 0 / 10 |
| 40 km/h | 22,1 | 8 260 | 21,3 | 1 423 | 5 718 | 11 437 | -3 177 | 0 / 10 |
| 50 km/h | 31,2 | 11 620 | 25,5 | 1 719 | 6 854 | 13 725 | -2 105 | 0 / 10 |

Semaine avec le propriétaire (35 min, 40 km/h, départ à 12 L, station sous 25 %) :

| Jour | Courses | Recette | Carburant acheté | Versement remis | Manque | Épargne | Propriétaire |
|---|---|---|---|---|---|---|---|
| 1 lun. | 25 | 9 200 | 14 000 | 0 | 10 000 | -4 800 | menace |
| 2 mar. | 23 | 8 200 | 10 000 | 0 | 10 000 | -6 600 | menace |
| 3 mer. | 25 | 9 000 | 12 000 | 0 | 10 000 | -9 600 | renvoyé |

**Pour Kevin : le versement de 10 000 FCFA reste inatteignable, dans 0 % des journées** (0 sur 30 à 35 min, 0 sur 120 toutes durées confondues). Aucune autre valeur n'a été changée, comme demandé. Pourquoi, précisément :
- La recette est multipliée par 3,8 (2 243 → 8 636), mais le carburant aussi (3 291 → 11 400) : les deux sont proportionnels aux kilomètres. Allonger la journée ne change donc pas leur rapport.
- **Au kilomètre, le carburant coûte plus que ce que rapportent les courses** : 8 L/100 km ×8 × 840 FCFA = **≈ 540 FCFA par km**, contre **≈ 400 FCFA de recette par km** (3 passagers à bord en moyenne, 300 à 800 FCFA la course sur une portion de 6 km). Chaque kilomètre fait perdre ≈ 140 FCFA, quelle que soit la durée.
- La recette seule (sans compter le carburant) dépasse 10 000 FCFA dans 10 % des journées de 35 min (3 sur 30) et 70 % des journées de 40 min (21 sur 30) : la durée suffirait si le carburant était compté à ×1 (1 400 FCFA par jour à 40 km/h ; à 50 km/h, 5 journées sur 10 couvrent alors le versement).
- Le ×8 avait été choisi pour qu'une journée de **10 min** consomme comme une vraie journée (≈ 5 L). Avec 35 min, la même échelle fait brûler ≈ 13,6 L pour 21 km de jeu, soit ce que consomme un vrai taxi sur ≈ 170 km : c'est « réaliste » pour une journée entière, mais la recette, elle, reste celle de 21 km.
- Conséquence avec les valeurs par défaut du propriétaire (3 manques sur 7 jours) : **renvoyé le 3ᵉ jour**, comme avant.

Ce qui permettrait d'atteindre le versement (à trancher par Kevin, **rien n'est appliqué**) : baisser `consumptionScale` (×1 à ×2 ; contraire au « réalisme actuel »), ou baisser le versement (`day.json`), ou augmenter les prix (`fares.json`), ou allonger la portion (plus de clients par km ; jalons suivants).

Ce qui dépend de la durée de la journée (vérifié le 2026-11-21) :
- **Horloge affichée** : 6 h → 20 h en 35 min, soit **1 h de jeu = 150 s réelles** (avant : 43 s) ; `DayClock` lit `dayLengthRealSeconds`, rien d'autre à changer. Tests mis à jour (`DayTests`, `HudGaugesTests`, `ShiftTests` lisent la durée des données au lieu de 600 s).
- **Rappels de fin de journée** : en minutes de jeu (dernière heure à 19 h, rappel après 20 h) : inchangés, la dernière heure dure maintenant 150 s réelles.
- **Apparition des clients** : `street.json` compte en secondes réelles (un client toutes les 20 s par lieu, patience de 30 s) : environ **3,5 fois plus de clients par journée**, à la même cadence qu'avant ; inchangé.
- **Temps perdus** (hôpital, police, garage, technicien) : en minutes de jeu, donc la même part de la journée ; en temps réel, 3,5 fois plus longs.
- **Imprévus de la route** (jalon 6, coupé) : comptés au kilomètre, donc 3,5 fois plus par journée ; `RoadTests` simule désormais des journées de la durée des données : **≈ 8 crevaisons et 8 pannes par semaine** (avant : 2,5), à revoir avec la question 1 du jalon 6.
- **Dialogues** (jalon 5, coupé) : ≈ 160 bulles par journée au rythme du test ; il faut ≈ 24 répliques par situation au lieu de 8 pour moins d'une répétition sur quatre (`jalon-05.md`).
- **Sauvegarde** : toutes les 30 s réelles, inchangée ; une journée en cours sauvegardée avec l'ancienne durée reprendrait plus tôt dans la journée (reprise coupée par `resumeOnStart: false`, sans effet aujourd'hui).

## Équilibrage : simulation de journées types (2026-11-21, session cloud, journée de 10 min, avant la décision)

`tools/dotnet/BalanceSimulationTests.cs` : un chauffeur automatique joue des journées sur les vraies données (arrêts, prix, places, clients, carburant, propriétaire, calendrier) : aller-retour dispensaire ↔ Étoudi à vitesse constante, il accepte tout client qui annonce sa destination, s'arrête 4 s pour faire monter ou descendre, 8 s pour faire demi-tour, passe à la station sous 25 % du réservoir. Ni trafic, ni feux, ni accident : une journée « propre », plutôt optimiste. Moyenne sur 10 journées ; rapports dans `tools/dotnet/bin/Debug/net8.0/equilibrage/` (`tools/test_core.sh Balance`). **Aucune valeur n'a été changée.**

| Vitesse | Courses | À bord (moy. sur 5) | Recette | km | Carburant ×1 | ×4 | ×8 (actuel) | Reste ×8 |
|---|---|---|---|---|---|---|---|---|
| 30 km/h | 4,6 | 3,2 | 1 500 | 4,6 | 313 | 1 245 | 2 491 | -991 |
| 40 km/h | 5,7 | 3,0 | 2 020 | 6,1 | 410 | 1 648 | 3 295 | -1 275 |
| 50 km/h | 7,0 | 2,9 | 2 630 | 7,4 | 497 | 1 983 | 3 972 | -1 342 |

Constats :
- **Le versement de 10 000 FCFA est hors d'atteinte avec les valeurs actuelles, même sans carburant.** Une journée dure 600 s réelles : à 40 km/h, c'est à peine plus d'un aller simple dispensaire → Étoudi (6,1 km). Même taxi plein toute la journée (5 places au lieu de 3 en moyenne), la recette plafonne vers 3 500 FCFA.
- Le carburant ×8 (`consumptionScale`) coûte 2 500 à 4 000 FCFA par journée, soit **plus que la recette** : chaque journée fait perdre de l'argent. ×8 rend la consommation d'une « vraie » journée de taxi, alors que la recette, elle, reste celle d'une journée de 10 minutes : les deux ne sont pas à la même échelle.
- Sur une semaine aux valeurs par défaut (3 manques sur 7 jours → renvoi), **le chauffeur est renvoyé le 3ᵉ jour (mercredi), quoi qu'il fasse.**

| Jour | Courses | Recette | Carburant acheté | Versement remis | Manque | Épargne | Propriétaire |
|---|---|---|---|---|---|---|---|
| 1 lun. | 7 | 2 800 | 0 | 2 800 | 7 200 | 0 | menace |
| 2 mar. | 6 | 2 000 | 12 000 | 0 | 10 000 | -10 000 | menace |
| 3 mer. | 6 | 2 000 | 0 | 2 000 | 8 000 | -10 000 | renvoyé |

Pistes à trancher par Kevin (une seule suffit, toutes se règlent dans les données) :
- **A. Versement à l'échelle de la journée de jeu** : environ 2 000 FCFA (`day.json` : `versement`), carburant ×1 à ×2 ; la journée reste de 10 minutes.
- **B. Journée plus longue** : 30 à 40 min réelles (`dayLengthRealSeconds` 1 800 à 2 400) pour 3 à 4 allers-retours ; versement à environ 7 000, carburant ×4.
- **C. Tout à l'échelle « réelle »** : prix des courses ×3 à ×4 (`fares.json`) en gardant 10 000 et le carburant ×8 ; les prix affichés ne sont alors plus les vrais prix de Yaoundé (300 à 800).

## Questions pour Kevin (jalon 3)

1. ~~Réputation visible ou cachée ?~~ → **visible**, validé par Kevin (2026-11-21).
2. ~~Effets de la réputation~~ → **tranché par Kevin le 21/11** : clients plus ou moins patients, prix acceptés, attitude du propriétaire, contrôles de police ; branchés avec des coefficients par défaut (« Effets de la réputation »). Reste à régler : les coefficients.
3. ~~Panne sèche~~ → **« On pousse, ou on appelle un technicien du quartier »** (Kevin, 2026-11-21), fait ; **les passagers descendent sans payer** (tranché par Kevin le 21/11), fait. Reste à régler : vitesse en poussant, délai et prix du technicien (`fuel.json`).
4. **Station-service** : laquelle, et où, sur la portion Messassi → Étoudi ?
5. ~~Jours de marché et jour de repos~~ → **calendrier par défaut validé par Kevin** (2026-11-21) : marché le mercredi et le samedi, dimanche plus calme mais travaillé.
6. ~~Équilibre recettes / carburant / versement : piste A, B ou C~~ → **piste B, journée de 35 min, carburant inchangé** (Kevin, 2026-11-21) ; **versement d'environ 7 000 FCFA** (tranché par Kevin le 21/11), appliqué. **Reste ouvert** : la recette seule couvre 7 000 dans 97 % des journées, mais recette et carburant compris, 0 % (le carburant ×8 coûte ≈ 540 FCFA par km pour ≈ 400 de recette) : baisser `consumptionScale` ou monter les prix ? Voir « Versement de 7 000 FCFA ».
7. Niveau de concurrence des taxis (question 4 du jalon 2) : réglable dans `traffic.json`.
