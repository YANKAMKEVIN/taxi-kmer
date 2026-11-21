# Jalon 6 — La route est dure (plan technique)

> Ouvert le 2026-11-21 en session cloud, **logique pure seulement**. Contenu et critères : `docs/design/roadmap.md` ; vision §4.5. **Activé le 21/11** (`enabled: true` dans `road.json`) après la 2ᵉ série de décisions de Kevin (questions 1, 2, 3, 4 et 6 tranchées) ; affichage minimal `Driving/RoadView.cs`, **non exécuté dans Unity**.
>
> **Priorité cochée par Kevin le 2026-11-21.** Questions 1, 2, 3, 4 et 6 tranchées le 21/11 ; reste la 5 (garage sur la portion). Décidé le même jour et actif : la **panne sèche** (pousser ou appeler un technicien du quartier, `jalon-03.md`). Avec la **journée de 35 min**, les incidents comptés au kilomètre sont 3,5 fois plus nombreux par journée : ≈ 8 crevaisons et 8 pannes par semaine aux valeurs par défaut (contre 2,5 avec 10 min), à prendre en compte pour la question 1. Le « mécanicien de quartier qui vient sur place » (question 5) pourrait reprendre le technicien de la panne sèche.

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
| 6.6 Branchement dans le jeu | **en partie, écrit, non exécuté** (21/11) : `RoadDay` + `RoadView` : nids-de-poule, crevaisons, pannes sur le trajet du taxi, vitesse limitée (avec celle du réservoir), boutons « Changer la roue » (20 min) et « Appeler un mécanicien » (déplacement 2 000 + réparation, payés par le chauffeur), pluie de saison (messages, clients ×1,3, patience ×0,7, trafic ×1,3, bulles « pluie »), dégâts dans la sauvegarde (version 5). **Reste** : collisions du taxi avec le trafic (détecteur à écrire, puis fuite et poursuite déjà prêtes dans la logique), nids-de-poule visibles, ciel et essuie-glaces, adhérence dans `TaxiCar`, garage sur la portion (question 5) |
| 6.7 Décisions de Kevin du 21/11 | **faite** : voir « Décisions de Kevin du 21/11 » |

Tests : `Assets/Tests/EditMode/RoadTests.cs` (11 tests, dont 80 semaines simulées avec la journée de 35 min : **1,16 crevaison par semaine et 0,96 panne par mois de 30 jours**, ≈ 23 km par jour, toutes réglées ; pluie de saison sur 4 ans : 46 % des jours en saison des pluies, 11 % en saison sèche) ; `SaveTests.Version4File_UpgradesTo5_NoImpound_NoDamage` ; `DataInvariantsTests.Road_MatchesDefaults_On_KevinDecisions`.

## Décisions de Kevin du 21/11 (tranché par Kevin le 21/11)

- **Incidents rares et marquants** : ≈ 1 panne par mois de jeu, 1 crevaison par semaine. Probabilités recalculées avec la journée de 35 min (≈ 23 km et ≈ 35 nids-de-poule pris par jour à 40 km/h) : panne **0,13 % par km** (+ 0,001 % par point de dégâts ; avant 3 % + 0,03 %), crevaison **0,44 % par nid-de-poule** pris au-dessus de 20 km/h (avant 3 %), dégâts d'un nid-de-poule **0,005 point par km/h** au-delà de 20 km/h (avant 0,05 : 35 points par jour auraient coûté ≈ 17 500 FCFA de garage chaque soir). Mesuré : 1,16 crevaison par semaine, 0,96 panne par mois.
- **Fuite du joueur, option B** : fautif d'un accrochage, il peut fuir (`RoadIncidents.PlayerFlees`) : **-10 de réputation** tout de suite ; **retrouvé par la police 1 fois sur 2** : il paie alors **1,5 fois** les dégâts de l'autre et perd **90 min** (convocation) (`Street.ApplyAtFaultCollision`). Valeurs par défaut.
- **Réparations : le chauffeur paie** (`driverShareOfRepairs: 1`). Mécanicien de quartier sur place en cas de panne : 2 000 FCFA de déplacement (`mechanicCallFee`, valeur par défaut) + la réparation.
- **Pluie selon les saisons de Yaoundé**, avec le calendrier : `calendar.json` donne la date du jour 1 (**lundi 2 mars 2026** par défaut, qui tombe bien un lundi) ; `road.json` → `rainChanceByMonth` : mars 0,4, avril 0,5, mai 0,5, juin 0,4, septembre 0,5, octobre 0,6, novembre 0,4 ; juillet 0,15, août 0,2 (petite saison sèche) ; décembre 0,05, janvier 0,05, février 0,1 (grande saison sèche). Valeurs par défaut.
- **Délit de fuite subi : on peut poursuivre le fautif** (`RoadIncidents.Chase`, `Street.ApplyChase`) : 20 min de poursuite ; rattrapé **4 fois sur 10**, il paie les réparations (`DayLedger.Compensations`, dans la recette sans compter de course). Première tranche logique, testée ; pas encore branchée (les accrochages avec le trafic ne le sont pas).
- **Dégâts** : ils passent d'un jour à l'autre (sauvegarde version 5, `career.vehicleDamage`).
- **Activé** : `enabled: true`.

## Valeurs par défaut (`road.json`, provisoires)

- ~~Coupé~~ → activé le 21/11 ; ~~nids-de-poule 0,05 point par km/h, crevaison 3 fois sur 100 ; panne 3 % par km~~ → recalculés (ci-dessus).
- Accrochage : 0,8 point de dégâts par km/h ; fautif : 1 000 FCFA par km/h à l'autre et 30 min ; autre fautif : paie 5 fois sur 10, négocie 3 fois (moitié), fuit 2 fois.
- Nids-de-poule : 3 par km, sans danger sous 20 km/h, 0,05 point par km/h au-delà, crevaison 3 fois sur 100.
- Panne : 3 % par km, plus 0,03 % par point de dégâts ; crevé : 15 km/h ; roue de secours : 20 min ; très abîmé (60 points) : 40 km/h.
- Pluie : 3 jours sur 10, averse de 1 à 3 h ; adhérence ×0,7, trafic ×1,3, clients ×1,3, patience ×0,7.
- Garage : 500 FCFA et 2 min par point ; pneu 1 500 FCFA et 20 min ; panne 10 000 FCFA et 2 h ; le chauffeur paie tout (`driverShareOfRepairs: 1`).

## Questions pour Kevin (jalon 6)

1. ~~Fréquence~~ → **option A, tranché par Kevin le 21/11** (1 panne par mois, 1 crevaison par semaine, recalculées). Ancienne question : **Fréquence des incidents** (question 1) : **A.** rare et marquant (une panne par mois de jeu, une crevaison par semaine) ; **B.** fréquent (valeurs par défaut : 2 à 3 par semaine de chaque) ; **C.** selon l'état du taxi et de la route (peu sur route neuve, beaucoup après la pluie).
2. ~~Fuite du joueur~~ → **option B, tranché par Kevin le 21/11.** Ancienne question : **Le joueur peut-il causer un accident et fuir lui-même** (question 2) ? Non codé. **A.** non ; **B.** oui, avec risque d'être retrouvé (police, jalon 4) et perte de réputation ; **C.** oui, sans suite si personne n'a vu.
3. ~~Qui paie~~ → **le chauffeur, tranché par Kevin le 21/11.** Ancienne question : **Qui paie les réparations** tant que le taxi est au propriétaire (question 3) ? **A.** le chauffeur (par défaut) ; **B.** le propriétaire, sauf accident fautif ; **C.** moitié-moitié (`driverShareOfRepairs: 0.5`).
4. ~~Pluie~~ → **saisons de Yaoundé avec le calendrier, tranché par Kevin le 21/11.** Ancienne question : **Pluie** : saisons des pluies (mars-juin, septembre-novembre à Yaoundé) avec le calendrier, ou au hasard toute l'année ?
5. **Garage** : où sur la portion, et le mécanicien de quartier vient-il sur place (dépannage) ?
6. ~~Délit de fuite subi~~ → **on peut poursuivre le fautif, tranché par Kevin le 21/11** (première tranche logique). Ancienne question : **Délit de fuite subi** : peut-on poursuivre le fautif, porter plainte (police), ou rien ?
