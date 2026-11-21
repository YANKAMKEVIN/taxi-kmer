# Jalon 7 — La vie à la maison (plan technique)

> Ouvert le 2026-11-21 en session cloud, **mécanisme seulement** : les situations familiales, les montants des charges et les événements de vie sont des décisions de Kevin (questions 1 à 3 de la feuille de route). Contenu et critères : `docs/design/roadmap.md` ; vision §4.3. `home.json` : `enabled: false`, **tous les montants à 0**, événements à 0 %, pas de tontine.

**But** : le bilan réel d'un taximan, famille comprise.

**Accepté si** (feuille de route) : on lit clairement, chaque soir, combien on a gagné, dépensé et mis de côté ; les montants sont dans les données.

## Tâches

| Tâche | État |
|---|---|
| 7.1 Situation familiale et charges | **mécanisme fait** (`Core/Home/Household.cs`) : situations (célibataire ; en couple avec deux enfants), charges dues tous les N jours (loyer chaque mois, nourriture chaque soir, scolarité par enfant), changement de situation |
| 7.2 Écran du soir | **logique faite** (`Household.EveningSummary`) : « Gagné », « Dépensé », « Mis de côté » à partir de la clôture du propriétaire et des lignes de la maison ; affichage Unity (page après le bilan du jour) à faire avec les vrais montants |
| 7.3 Événements de vie | **mécanisme fait** : chance par jour, seulement avec des enfants si besoin, délai avant de revenir ; maladie d'un enfant et deuil en exemples sans coût |
| 7.4 Tontine | **faite** : cotisation à chaque tour, le pot au tour du chauffeur ; sur un cycle, on reçoit ce qu'on a cotisé |
| 7.5 Branchement | **à faire après les décisions de Kevin** : lignes de la maison retirées de l'épargne à la clôture (`DriverCareer`), situation et tontine dans la sauvegarde, écran du soir |

Tests : `Assets/Tests/EditMode/HomeTests.cs` (5 tests, montants d'essai dans le test seulement) ; `DataInvariantsTests.Home_MatchesDefaults_Off_NoAmountInvented` (aucun montant inventé dans les données).

## Questions pour Kevin (jalon 7)

1. **Situation familiale** (question 1) : **A.** choisie au départ ; **B.** imposée (célibataire) puis qui évolue (mariage, naissances) ; **C.** tirée au hasard au départ.
2. **Peut-on perdre** (question 2 ; vision §9 question 9) : **A.** non, l'épargne peut devenir négative sans fin ; **B.** dettes puis expulsion du logement au-delà d'un seuil ; **C.** retour au village (fin de partie) après N jours de dettes.
3. **Montants des charges types à Yaoundé** (question 3) : propositions pour un chauffeur de taxi, à corriger par Kevin. **A.** modeste : chambre 15 000 FCFA/mois, nourriture 1 000/jour, scolarité 5 000/mois par enfant ; **B.** moyen : studio 30 000/mois, nourriture 1 500/jour, scolarité 10 000/mois par enfant, santé 5 000/mois ; **C.** famille nombreuse : maison 50 000/mois, nourriture 3 000/jour, scolarité 15 000/mois par enfant, aide à la famille 10 000/mois. À mettre en face de l'équilibrage du jalon 3 (une journée rapporte 1 500 à 2 600 FCFA avec les valeurs actuelles).
4. **Événements de vie** : lesquels, combien ils coûtent, à quelle fréquence (maladie d'un enfant, rentrée scolaire, deuil, mariage, tontine) ?
5. **Tontine** : le chauffeur en fait-il partie dès le départ ? Membres, cotisation, rythme, et choisit-il son tour ?
