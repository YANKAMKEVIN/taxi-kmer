# Feuille de route — Taxi Kmer

> Version 0.1 (2026-11-11). Proposition de Claude, **à valider par Kevin** : l'ordre, le contenu et le découpage des jalons. Chaque jalon reçoit un plan technique détaillé (`docs/tech/jalon-XX.md`) quand il s'ouvre ; ici, on fixe le but, le contenu, les critères et les questions de design à trancher avant de le commencer.

## Principes

- **Un jalon = une version jouable**, validée par Kevin sur téléphone en une seule session à la fin.
- **Au plus 2 jalons actifs à la fois** : un en code, un en conception (Kevin prépare les réponses du suivant pendant le code du courant).
- **Systèmes risqués validés seuls** : la sauvegarde (tout ce qui peut perdre des données de partie) a son jalon.
- **Visuels provisoires jusqu'au jalon 11** : chaque jalon peut améliorer le monde, mais le passage au rendu réaliste des lieux est un chantier dédié, nourri par les photos de Kevin.
- La logique (courses, argent, places, police…) est en C# pur, testée sans Unity ; Unity ne fait que la présentation.

## Vue d'ensemble

| # | Jalon | En une phrase |
|---|---|---|
| 1 | Une journée de taxi | Conduire de Messassi à Emana, courses partagées, corpulence, versement, bilan. *(plan : `docs/tech/jalon-01.md`)* |
| 2 | La ville vit | Trafic, mototaxis, piétons, embouteillages, clients qui réagissent. |
| 3 | Les jours s'enchaînent | Sauvegarde, journée suivante, carburant et station-service, réputation. |
| 4 | Le contrôle | Police, papiers, corruption, négociation. |
| 5 | Les passagers parlent | Dialogues et interactions entre clients, radio, ambiance sonore. |
| 6 | La route est dure | Accrochages, délits de fuite, pannes, crevaisons, nids-de-poule, pluie. |
| 7 | La vie à la maison | Foyer, situation familiale, charges, épargne, événements de vie. |
| 8 | Dépôt et appli | Courses « dépôt » et commandes par l'appli fictive. |
| 9 | Mon propre taxi | Acheter un taxi, entretien, papiers à son nom, fin du versement. |
| 10 | Le patron | Deuxième taxi, embaucher et gérer des chauffeurs. |
| 11 | Messassi → Bastos | Tronçon complet et premiers lieux réalistes (photos de Kevin). |

Après le jalon 11 : l'entreprise de taxis, le reste de Yaoundé, puis Douala, la démo web.

---

## Jalon 2 — La ville vit

**But** : que la route de Messassi à Emana ressemble à une vraie route de Yaoundé, pleine de monde.

**Contenu**
- Voitures et taxis concurrents sur des voies suivant le tracé OSM, avec priorités aux carrefours.
- Mototaxis (« bendskins ») qui se faufilent et dépassent.
- Piétons sur les bas-côtés, qui traversent.
- Embouteillages aux heures de pointe (densité de trafic selon l'heure, dans les données).
- Les taxis concurrents prennent aussi des clients : un client peut partir avec un autre taxi.
- Les clients refusent si on arrive trop tard, ou si on ne s'arrête pas proprement.

**Accepté si** : 30 images/s au réglage moyen avec le trafic ; une journée paraît vivante ; aucune voiture bloquée en permanence.

**Questions pour Kevin**
1. Feux tricolores sur cette portion, ou seulement des carrefours sans feux ?
2. Les mototaxis peuvent-ils « voler » des clients au joueur ?
3. Peut-on renverser un piéton (et quelles conséquences), ou le jeu l'empêche-t-il ?

## Jalon 3 — Les jours s'enchaînent

**But** : passer d'une journée isolée à une vie de taximan sur plusieurs jours. **Système risqué** : sauvegarde validée seule.

**Contenu**
- Sauvegarde automatique en fin de journée (et à la mise en arrière-plan de l'appli), reprise au lendemain.
- Calendrier (jours de la semaine, dimanche plus calme, jours de marché).
- Carburant : jauge réelle, station-service sur la portion, prix au litre dans les données.
- Relation avec le propriétaire du taxi : versement payé, en retard, ou manqué.
- Réputation du chauffeur (clients satisfaits, conduite).

**Accepté si** : on joue 3 jours de suite, on quitte l'appli à n'importe quel moment et on reprend sans perte ; tests de la sauvegarde (y compris fichier corrompu ou version ancienne).

**Questions pour Kevin**
1. Que se passe-t-il si le versement n'est pas atteint ? (dette, avertissement, renvoi après N fois ?)
2. Le joueur choisit-il ses heures (rentrer plus tôt, travailler de nuit) ?
3. Réputation : visible pour le joueur, ou cachée ?

## Jalon 4 — Le contrôle

**But** : le contrôle de police, cœur du propos sur l'injustice.

**Contenu**
- Postes de contrôle fixes et contrôles volants sur la portion.
- Papiers du taxi et du chauffeur (permis, carte grise, assurance, visite technique, licence de taxi…) avec dates d'expiration.
- Scène de contrôle : le policier vérifie, trouve (ou invente) un problème, demande de l'argent.
- Choix : payer, négocier, contester, appeler quelqu'un ; conséquences (temps perdu, argent, mise en fourrière, réputation).
- Surcharge (trop de passagers, deux devant) comme prétexte.

**Accepté si** : une dizaine de contrôles variés dans une journée de test ; les règles sont dans les données et testées.

**Questions pour Kevin**
1. Le ton exact : satirique, dramatique, ou les deux selon les situations ?
2. Liste des papiers à gérer et leur coût de renouvellement.
3. Contester peut-il parfois gagner ? Si oui, à quelles conditions ?
4. Le joueur peut-il fuir un contrôle ?

## Jalon 5 — Les passagers parlent

**But** : l'ambiance camerounaise par les dialogues.

**Contenu**
- Système de dialogues dans les données (personnages types, sujets, réactions).
- Interactions entre passagers : discussions, disputes de place, plaintes sur le détour, négociation du prix, clients qui ne veulent pas payer.
- Réponses du chauffeur (choix rapides qui ne bloquent pas la conduite).
- Radio du taxi.
- Ambiance sonore : klaxons, appels des clients (« Emana, 300 ! »), marché.

**Accepté si** : sur une journée, les dialogues ne se répètent pas trop et restent lisibles en conduisant.

**Questions pour Kevin**
1. Langues à l'écran : français, camfranglais, pidgin, langues locales (sous-titrées) ?
2. Voix enregistrées ou texte seul (au moins au début) ?
3. Qui écrit les dialogues : Kevin, Claude à partir de situations données par Kevin, ou les deux ?

## Jalon 6 — La route est dure

**But** : les imprévus du quotidien.

**Contenu**
- Dégâts du véhicule et état mécanique.
- Accrochages : le fautif s'arrête, négocie, ou **fuit** (délit de fuite).
- Pannes, crevaisons, nids-de-poule, routes dégradées.
- Pluie : route glissante, embouteillages, clients pressés.
- Garage / mécanicien de quartier : réparations et coûts.

**Accepté si** : chaque incident arrive au moins une fois sur une semaine de jeu et peut être réglé.

**Questions pour Kevin**
1. Fréquence souhaitée des incidents (rare et marquant, ou fréquent) ?
2. Le joueur peut-il causer un accident et fuir lui-même ?
3. Qui paie les réparations tant que le taxi appartient au propriétaire ?

## Jalon 7 — La vie à la maison

**But** : le bilan réel d'un taximan, famille comprise.

**Contenu**
- Situation familiale (célibataire, en couple, enfants) et charges : loyer, nourriture, scolarité, santé, aide à la famille.
- Écran du soir : ce qui reste après les charges, épargne.
- Événements de vie : maladie d'un enfant, rentrée scolaire, deuil, mariage, tontine.
- Épargne et tontine comme moyen d'atteindre l'achat du taxi.

**Accepté si** : on lit clairement, chaque soir, combien on a gagné, dépensé et mis de côté ; les montants sont dans les données.

**Questions pour Kevin**
1. Situation familiale choisie au départ, ou qui évolue ?
2. Peut-on « perdre » (dettes, expulsion) ?
3. Montants des charges types à Yaoundé (loyer, scolarité…).

## Jalon 8 — Dépôt et appli

**But** : les deux autres types de course.

**Contenu**
- **Dépôt** : un client privatise le taxi ; prix négocié, plus élevé.
- **Appli fictive** : commandes reçues sur le téléphone du chauffeur, prise en charge à une adresse, note du client, commission de l'appli.
- Choix stratégique entre les trois types de course selon l'heure et l'endroit.

**Accepté si** : les trois types de course sont jouables dans la même journée, avec leurs règles de passagers respectées.

**Questions pour Kevin**
1. Nom de l'appli fictive.
2. Commission de l'appli et prix types d'un dépôt.

## Jalon 9 — Mon propre taxi

**But** : premier palier de progression : devenir propriétaire.

**Contenu**
- Marché des véhicules (neuf, occasion, état, prix).
- Achat : épargne, prêt, tontine.
- Fin du versement, mais toutes les charges du véhicule pour soi (entretien, papiers, assurance).
- Personnalisation légère du taxi.

**Accepté si** : on peut atteindre l'achat en jouant normalement, et la vie de propriétaire change vraiment l'économie.

**Questions pour Kevin**
1. Durée visée (en jours de jeu) pour acheter son premier taxi.
2. Modèles de voitures (fictifs, inspirés des taxis courants à Yaoundé).

## Jalon 10 — Le patron

**But** : deuxième palier : faire travailler d'autres chauffeurs.

**Contenu**
- Acheter un deuxième taxi, recruter un chauffeur (fiabilité, honnêteté).
- Le chauffeur paie **son** versement au joueur (ou pas : excuses, retards, accidents).
- Écran de gestion de la flotte.
- Le joueur continue de conduire lui-même, ou devient gestionnaire.

**Accepté si** : avec deux taxis, on gère son chauffeur tout en conduisant.

**Questions pour Kevin**
1. Le joueur peut-il arrêter de conduire et ne faire que gérer ?
2. Événements entre patron et chauffeur à montrer.

## Jalon 11 — Messassi → Bastos

**But** : tout le tronçon prévu, et le premier passage au rendu réaliste.

**Contenu**
- Prolongement de la carte d'Emana à Bastos (OSM).
- Lieux emblématiques modélisés à partir des photos de Kevin.
- Passage réaliste de la portion Messassi → Emana (textures, éclairage, végétation, enseignes).
- Profilage des trois réglages de qualité sur la carte complète.

**Accepté si** : on reconnaît les lieux ; 30 images/s au réglage moyen sur un téléphone courant.

**Questions pour Kevin**
1. Liste des lieux emblématiques à prioriser sur le tronçon.
2. Calendrier des photos sur place.

---

## Chantiers parallèles (sans jalon dédié)

- **Art** : catalogue de textures libres, modèles de véhicules, personnages.
- **Audio** : sons de klaxon, moteur, ambiance de rue.
- **Interface** : style des écrans (à définir avec Kevin).
