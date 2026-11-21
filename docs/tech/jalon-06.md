# Jalon 6 — La route est dure (plan technique)

> Ouvert le 2026-11-21 en session cloud, **logique pure seulement**. Contenu et critères : `docs/design/roadmap.md` ; vision §4.5. Rien n'est actif : `enabled: false` dans `road.json` tant que Kevin n'a pas fixé la fréquence des incidents et qui paie.

**But** : les imprévus du quotidien.

**Accepté si** (feuille de route) : chaque incident arrive au moins une fois sur une semaine de jeu et peut être réglé.

## Tâches

| Tâche | État |
|---|---|
| 6.1 Dégâts et état mécanique | **faite** (`Core/Road/RoadIncidents.cs`, `VehicleCondition`) : dégâts de 0 à 100, pneu crevé, panne ; vitesse limitée (crevé 15 km/h, très abîmé 40 km/h, en panne 0) |
| 6.2 Accrochages | **faite** (`RoadIncidents.Collision`) : dégâts selon la vitesse au choc ; joueur fautif → il paie les dégâts de l'autre et perd du temps ; autre fautif → il paie, négocie (la moitié) ou **fuit** (délit de fuite : rien), tiré selon les poids des données |
| 6.3 Pannes, crevaisons, nids-de-poule | **faite** : nids-de-poule placés au hasard le long de l'itinéraire (même graine pour la carrière), sans danger au pas, dégâts et crevaison au-dessus de 20 km/h ; roue de secours ; panne au kilomètre, plus probable quand le taxi est abîmé |
| 6.4 Pluie | **faite** (`WeatherDay`) : une averse possible dans la journée ; pendant l'averse, adhérence, trafic, arrivée et patience des clients changent (facteurs des données) |
| 6.5 Garage / mécanicien de quartier | **faite** (`VehicleCondition.Quote`, `RepairAtGarage`) : devis (dégâts, pneu, panne), dépense « Garage » du bilan, temps perdu, part payée par le chauffeur réglable |
| 6.6 Branchement dans le jeu | **à faire après les décisions de Kevin** : collisions du taxi avec le trafic (détecteur existant des piétons), nids-de-poule visibles, pluie (ciel, essuie-glaces), garage sur la portion, adhérence dans `TaxiCar` ; état du véhicule dans la sauvegarde |

Tests : `Assets/Tests/EditMode/RoadTests.cs` (7 tests, dont 20 semaines simulées : par semaine en moyenne 2,5 crevaisons, 2,5 pannes, 2 jours de pluie, 2,5 délits de fuite sur 14 accrochages d'essai, tous réglés au garage) ; `DataInvariantsTests.Road_MatchesDefaults_Off_Sane`.

## Valeurs par défaut (`road.json`, provisoires)

- Coupé (`enabled: false`).
- Accrochage : 0,8 point de dégâts par km/h ; fautif : 1 000 FCFA par km/h à l'autre et 30 min ; autre fautif : paie 5 fois sur 10, négocie 3 fois (moitié), fuit 2 fois.
- Nids-de-poule : 3 par km, sans danger sous 20 km/h, 0,05 point par km/h au-delà, crevaison 3 fois sur 100.
- Panne : 3 % par km, plus 0,03 % par point de dégâts ; crevé : 15 km/h ; roue de secours : 20 min ; très abîmé (60 points) : 40 km/h.
- Pluie : 3 jours sur 10, averse de 1 à 3 h ; adhérence ×0,7, trafic ×1,3, clients ×1,3, patience ×0,7.
- Garage : 500 FCFA et 2 min par point ; pneu 1 500 FCFA et 20 min ; panne 10 000 FCFA et 2 h ; le chauffeur paie tout (`driverShareOfRepairs: 1`).

## Questions pour Kevin (jalon 6)

1. **Fréquence des incidents** (question 1) : **A.** rare et marquant (une panne par mois de jeu, une crevaison par semaine) ; **B.** fréquent (valeurs par défaut : 2 à 3 par semaine de chaque) ; **C.** selon l'état du taxi et de la route (peu sur route neuve, beaucoup après la pluie).
2. **Le joueur peut-il causer un accident et fuir lui-même** (question 2) ? Non codé. **A.** non ; **B.** oui, avec risque d'être retrouvé (police, jalon 4) et perte de réputation ; **C.** oui, sans suite si personne n'a vu.
3. **Qui paie les réparations** tant que le taxi est au propriétaire (question 3) ? **A.** le chauffeur (par défaut) ; **B.** le propriétaire, sauf accident fautif ; **C.** moitié-moitié (`driverShareOfRepairs: 0.5`).
4. **Pluie** : saisons des pluies (mars-juin, septembre-novembre à Yaoundé) avec le calendrier, ou au hasard toute l'année ?
5. **Garage** : où sur la portion, et le mécanicien de quartier vient-il sur place (dépannage) ?
6. **Délit de fuite subi** : peut-on poursuivre le fautif, porter plainte (police), ou rien ?
