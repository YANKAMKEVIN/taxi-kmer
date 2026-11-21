# Jalon 8 — Dépôt et appli (plan technique)

> Ouvert le 2026-11-21 en session cloud, **logique pure seulement**. Contenu et critères : `docs/design/roadmap.md` ; vision §4.1 (trois types de course, décision de Kevin du 2026-11-11 ; appli fictive, nom à choisir). `ride_types.json` : `enabled: false`, aucune commande, aucun client de dépôt, commission 0.

**But** : les deux autres types de course.

**Accepté si** (feuille de route) : les trois types de course sont jouables dans la même journée, avec leurs règles de passagers respectées.

## Tâches

| Tâche | État |
|---|---|
| 8.1 Règles de passagers par type | **faite** (`Core/Rides/RideTypes.cs`, `RideRules`) : en course partagée, les clients de la rue montent ; pendant un dépôt ou une course de l'appli, personne d'autre ; un dépôt ou une commande ne commence qu'avec le taxi vide |
| 8.2 Dépôt | **logique faite** : offre du client (prix partagé de la distance × places du taxi), le chauffeur peut demander plus, la chance que le client accepte baisse avec ce qu'il demande en plus |
| 8.3 Appli fictive | **logique faite** (`AppDispatcher`) : commandes au fil de la journée (prise en charge à un lieu, destination, 1 à 3 passagers, prix), proposées quelques secondes puis passées à un autre chauffeur, une à la fois, acceptées taxi vide ; note du client selon l'attente ; commission |
| 8.4 Branchement dans le jeu | **à faire après les décisions de Kevin** : téléphone du chauffeur à l'écran, mode « dépôt » / « appli » dans `Street` (refuser les clients de la rue pendant la course exclusive), prise en charge à l'adresse, note et commission au bilan |

Tests : `Assets/Tests/EditMode/RideTypesTests.cs` (5 tests) ; `DataInvariantsTests.RideTypes_MatchDefaults_OnlySharedUntilKevinDecides`.

## Questions pour Kevin (jalon 8)

1. **Nom de l'appli fictive** (question 1 ; vision §9 question 3) : **A.** Allô Taxi ; **B.** Kmer Ride ; **C.** Mbolo. (`app.appName`)
2. **Commission de l'appli** (question 2) : **A.** 10 % ; **B.** 15 % ; **C.** 20 %. (`app.commission`) Et prix d'une course de l'appli : 1,5 × le prix partagé par défaut (`app.fareFactor`).
3. **Prix types d'un dépôt** (question 2) : **A.** le prix de toutes les places (5 × le prix partagé, par défaut) ; **B.** un forfait par zone (par ex. 1 500 FCFA jusqu'à Emana, 2 500 jusqu'à Étoudi) ; **C.** 3 × le prix partagé, négociable. Et le marchandage : le chauffeur demande plus, ou le client propose moins ?
4. **Fréquence** : combien de commandes de l'appli par heure, et combien de clients veulent un dépôt (`app.ordersPerHour`, `depot.depotClientChance`, à 0) ?
5. **Note du client** : effet sur les commandes suivantes (moins de commandes sous 4 étoiles ?) ou seulement affichée ?
6. Avec l'équilibrage du jalon 3 (une journée de 35 min rapporte 6 600 à 10 500 FCFA, mais le carburant en coûte ≈ 11 400), quelle place pour ces courses plus chères ?
