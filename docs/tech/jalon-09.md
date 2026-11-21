# Jalon 9 — Mon propre taxi (plan technique)

> Ouvert le 2026-11-21 en session cloud, **calculs seulement** : modèles, prix, prêt et charges du propriétaire sont des décisions de Kevin. Contenu et critères : `docs/design/roadmap.md` ; vision §4.6. `taxi_market.json` : `enabled: false`, **aucune offre**, entretien à 0.

**But** : premier palier de progression : devenir propriétaire.

**Accepté si** (feuille de route) : on peut atteindre l'achat en jouant normalement, et la vie de propriétaire change vraiment l'économie.

## Tâches

| Tâche | État |
|---|---|
| 9.1 Marché des véhicules | **structure faite** (`Core/Career/TaxiMarket.cs`) : offres (modèle, neuf ou d'occasion, état, prix) triées par prix ; **aucune offre** dans les données |
| 9.2 Achat : épargne, prêt, tontine | **calculs faits** (`TaxiPurchase`) : comptant, apport minimum et remboursement quotidien du prêt (intérêts compris), jours d'épargne nécessaires ; la tontine du jalon 7 (`Household.Tontine`) donne le pot |
| 9.3 Fin du versement, charges du propriétaire | **calcul fait** (`TaxiPurchase.OwnerDayNet`) : plus de versement, mais entretien au km, prêt et autres charges ; reste à brancher dans `DriverCareer` (mode propriétaire) et à relier aux papiers du jalon 4 (renouvellement à son nom) |
| 9.4 Personnalisation légère | **non fait** : demande des assets (couleurs, autocollants) et les choix de Kevin |

Tests : `Assets/Tests/EditMode/TaxiMarketTests.cs` (4 tests) ; `DataInvariantsTests.TaxiMarket_MatchesDefaults_NoOfferInvented`.

## Ce que dit déjà l'équilibrage (jalon 3)

Avec les valeurs actuelles, une journée rapporte 1 500 à 2 600 FCFA et le versement n'est jamais atteint : l'épargne ne monte pas, l'achat est impossible. La durée visée (question 1) ne peut se régler qu'après la piste A, B ou C de l'équilibrage. `TaxiPurchase.DaysToSave` donne la durée pour un prix et une épargne quotidienne.

## Questions pour Kevin (jalon 9)

1. **Durée visée pour acheter son premier taxi** (question 1) : **A.** 2 semaines de jeu ; **B.** 1 à 2 mois ; **C.** 3 mois ou plus (achat rare et marquant).
2. **Modèles** (question 2), fictifs et inspirés des taxis de Yaoundé : **A.** un seul modèle, neuf ou d'occasion ; **B.** trois modèles (petite citadine, berline courante, berline plus grande) ; **C.** occasions seulement au début, état variable (lien avec les pannes du jalon 6).
3. **Prêt** : existe-t-il, auprès de qui (banque, microfinance, le propriétaire actuel), et à quelles conditions (apport, intérêts, durée : 30 %, 20 %, 60 jours par défaut) ? Que se passe-t-il si on ne rembourse pas ?
4. **Charges du propriétaire** : entretien au km, assurance, papiers, place de parking ; montants.
5. **Personnalisation** : couleurs (le jaune est obligatoire pour un taxi), autocollants, nom sur la vitre arrière ?
