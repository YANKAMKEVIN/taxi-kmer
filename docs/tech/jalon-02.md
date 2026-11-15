# Jalon 2 — La ville vit (plan technique)

> Ouvert le 2026-11-14, pendant que le jalon 1 attend sa validation sur téléphone. Contenu et critères : `docs/design/roadmap.md`. Les questions de Kevin ci-dessous ne bloquent pas le début : chaque réponse provisoire est un réglage dans les données, facile à changer.

**But** : que la route de Messassi à Étoudi ressemble à une vraie route de Yaoundé, pleine de monde.

**Accepté si** : 30 images/s au réglage moyen avec le trafic ; une journée paraît vivante ; aucune voiture bloquée en permanence.

## Choix techniques

- **Trafic sur l'axe principal** : le joueur fait l'aller-retour Messassi ↔ Étoudi ; c'est là que le trafic se voit. Aux carrefours, des véhicules entrent et sortent par les rues latérales à droite (40 premiers mètres de la rue, au-delà ils disparaissent, cachés par les bâtiments).
- **Une trajectoire par sens** (`world.traffic.forward` / `backward`), calculée par `tools/osm/osm_traffic.py` sur le graphe orienté des routes OSM : sur la double voie (deux chaussées à sens unique à ~11 m), chaque sens roule sur sa chaussée. L'itinéraire du joueur, lui, ignore les sens uniques (calcul du jalon 1). Là où la chaussée retour manque dans les données du couloir (vers Étoudi), le trafic partage la chaussée à sens unique, traitée comme à double sens.
- **Simulation en C# pur** (`Core/Traffic`), testée sans Unity : position = distance le long de la trajectoire + écart latéral (on roule à droite). Suivi IDM (chacun adapte sa vitesse au véhicule de devant, freine pour un obstacle). Le trafic n'existe que dans un rayon de 380 m autour du joueur (piétons : 220 m) ; il apparaît au-delà de 160 m, caché par le brouillard.
- **Unity ne fait qu'afficher** (`TrafficView`, créé par `StreetView`, aucune modification de scène nécessaire) : véhicules en blocs provisoires (voitures de couleurs variées, taxis jaunes, mototaxis avec leur conducteur), piétons en silhouettes ; objets réutilisés (pas de création à chaque apparition) ; colliders cinématiques pour que le taxi du joueur les heurte.

## Tâches

| Tâche | État |
|---|---|
| 2.1 Trajectoires du trafic par sens (outil OSM) | **faite** (2026-11-14) : `osm_traffic.py` + 4 tests ; forward 6 096 m, backward 6 098 m, à moins de 13,5 m de l'itinéraire |
| 2.2 Trafic : voitures et taxis, deux sens, densité selon l'heure, dépassement | **logique faite et testée** (2026-11-14) ; affichage Unity écrit (`TrafficView`, branché dans `StreetView`), compilé hors Unity (`tools/check_unity_compile.sh`), test PlayMode ajouté ; **à lancer dans Unity et à voir sur téléphone** |
| 2.3 Mototaxis qui se faufilent | **logique faite et testée** : ils roulent au bord, longent les voitures par la droite (au plus 15 km/h plus vite qu'elles) |
| 2.4 Piétons sur les bas-côtés, traversées | **logique faite et testée** : ils traversent quand le trafic peut freiner, jamais juste devant le joueur ; le trafic s'arrête pour eux |
| 2.5 Concurrence pour les clients | **logique faite et testée** : les taxis concurrents prennent les clients qui attendent ; un client accepté attend 30 s puis refait signe aux autres ; pour monter, le taxi doit s'arrêter du côté du client |
| 2.6 Carrefours et rues latérales | **logique faite et testée** (2026-11-14) : 31 carrefours à droite vers Étoudi, 34 vers le dispensaire (`osm_traffic.py`) ; des véhicules tournent à droite dans les rues latérales, d'autres en sortent, s'arrêtent à la ligne d'arrêt et entrent sur l'axe quand il y a un trou (priorité au grand axe, jamais devant le joueur) ; pas de tourne-à-gauche (on ne coupe pas le trafic d'en face) |
| 2.8 Feux tricolores | **logique faite et testée** (2026-11-14) : cycle vert 25 s, orange 3 s, rouge 20 s (l'axe, les deux sens ensemble ; feux décalés entre eux) ; le trafic s'arrête au rouge (à l'orange s'il peut freiner sans brusquer), ne double pas devant un feu, ligne d'arrêt 6 m avant le carrefour ; les rues latérales du carrefour entrent quand l'axe est au rouge ; 3 mototaxis sur 10 grillent le rouge ; poteaux affichés dans Unity. Le joueur qui grille un feu : aucune conséquence avant le jalon 4 (police) |
| 2.7 Performance et qualité | à faire sur téléphone : mesurer les 30 images/s en Moyen avec le trafic de pointe ; le réglage Bas réduit la densité |

## Références photo utilisées (2026-11-15)

Lots de Kevin dans `docs/references/photos` (sur `main`) :
- **Départ confirmé** : notre point de départ est à 7 m du « Centre Médical d'Arrondissement Yaoundé I Olembe » et à 26 m de la « Gendarmerie – Brigade de Nkolondom » dans OpenStreetMap ; la note de Kevin dit que le dispensaire est à côté de la gendarmerie. Le dispensaire de Messassi est donc ce centre médical.
- **Muret central et lampadaires** (photo libre RN1 Émana, Pierre Geitner, CC BY-SA 4.0) : muret en béton de 0,8 m de haut et 0,5 m de large au milieu de la double voie, ouvert à 14 m de part et d'autre des carrefours et des feux (`osm_traffic.py` : 31 tronçons, 2,2 km) ; lampadaire double (mât de 10 m, deux bras courbés vers les chaussées) tous les 38 m ; le muret a une collision (on ne le traverse pas).
- **Taxis** (photos libres de Minette Lontsie et Simbanematick, CC BY-SA 4.0, lot 07) : jaune, pas d'enseigne sur le toit (retirée des taxis concurrents), baguette sombre sur les flancs, pastille bleu et blanc sur les portières avant, plaques orange (toutes les voitures). Ajoutés au lancement, sur le taxi du joueur aussi.

## Aperçu sans Unity

`dotnet test tools/dotnet --filter PreviewExport` exporte 2 min 30 de simulation (heure de pointe, autour du carrefour de Messassi) dans `build/preview/replay.json`, rejoué vu du ciel dans une page web (artefact « Messassi à l'heure de pointe »). Sert à juger le comportement du trafic avant l'essai sur téléphone.

## Valeurs par défaut (à régler avec Kevin après essai)

- Réglage de qualité Bas : 60 % du trafic et des piétons (`trafficScale` dans `quality.json`) ; Moyen et Haut : 100 %.
- Messages : « Un autre taxi a pris le client pour … » si c'était à moins de 120 m du joueur ; « Ton client en a eu marre d'attendre » quand le client accepté abandonne.
- Piétons sans collider pour l'instant (le taxi les traverse) en attendant la réponse à la question 3 ; véhicules avec collider cinématique (on les heurte, ils ne bougent pas).

Toutes dans `Assets/Resources/Data/traffic.json` et `street.json`.

- Densité (véhicules par km et par sens) : 12 à 6 h, **20-22 aux pointes 7-8 h et 17-18 h**, 10-11 en journée. Mélange : 45 % voitures, 35 % taxis, 20 % mototaxis.
- Vitesses voulues : voitures 50 ± 8 km/h, taxis 45 ± 6, mototaxis 50 ± 10. Suivi : 1,2 s derrière le véhicule de devant (0,8 s pour les motos), 2 m à l'arrêt.
- Voie à 1,7 m à droite de l'axe de la chaussée ; mototaxis à 3,1 m (au bord) ; voie de gauche à -1,7 m pour doubler.
- Dépassement : après 3 s bloqué derrière un véhicule arrêté ou très lent (< 5 km/h) ; sur un double sens, seulement si personne n'arrive en face sur 90 m ; on ne contourne pas un piéton (on attend).
- Taxis concurrents : 1 chance sur 2 de s'arrêter pour un client qui attend dans leur sens ; arrêt de 6 s ; 5 places (ils apparaissent avec 0 à 4 passagers).
- Clients : un client accepté attend le joueur **30 s**, puis refait signe aux autres taxis ; un taxi concurrent ne prend jamais le client que le joueur a accepté ; pour monter, le taxi du joueur doit être arrêté sur la moitié de chaussée du client.
- Clients placés sur le trottoir de la chaussée de leur sens, à 5 m de son axe (sur la double voie, la bonne chaussée).
- Piétons : 10-14 par km et par côté de jour, 4 à 6 h ; marche de 1 à 1,5 m/s, trottoir à 5 m de l'axe ; chacun a 6 chances sur 10 par minute de vouloir traverser ; il traverse si le trafic peut s'arrêter sans freiner fort (3 m/s², 1 s de réaction) et si le taxi du joueur est assez loin pour qu'il ait fini de traverser avant son arrivée (+ 2 s de marge, et jamais à moins de 20 m).
- Carrefours : chaque véhicule a 12 % de chances de tourner dans une rue à droite qu'il croise (15 km/h pour tourner, 20 km/h dans la rue) ; à la pointe, 2 véhicules par minute sortent de chaque rue latérale (chemins de service : 30 %) ; il faut 4 s d'avance sur le véhicule qui arrive (jamais devant le joueur) ; après 20 s d'attente, il se contente de 1,5 s (le suivant freine) ; 2 véhicules au plus en attente par rue.
- Distance à l'itinéraire pour que les clients voient le taxi : 15 → **20 m** (`street.json`), car sur la double voie le joueur peut rouler sur la bonne chaussée à ~15 m de l'itinéraire.
- Mesuré hors Unity (essai sur le vrai monde, heure de pointe, carrefours compris) : jusqu'à 41 véhicules et 36 piétons autour du joueur, 0,2 ms de calcul par image sur PC, aucun véhicule arrêté plus de 25 s (attente aux carrefours) ; joueur garé 3 min sur la voie : 53 voitures le contournent.

## Questions pour Kevin (réponses provisoires en place)

1. ~~Feux tricolores sur cette portion ?~~ → **Tranché** (Kevin, 2026-11-14 et 15) : oui, « au niveau des carrefours ». OpenStreetMap n'en a aucun sur le secteur : fichier fait main `tools/osm/traffic_lights_messassi_etoudi.json`. Placés aux carrefours nommés dans les références photo de Kevin (`docs/references/photos` sur `main`, vidéo Nkozoa–Étoudi) : **carrefour du marché de Messassi** (631 m) et **carrefour Borne Fontaine Emana** (2 932 m, route tertiaire dans OSM). Les feux eux-mêmes ne sont pas lisibles sur ces images (320 × 180) ; les seules photos de feux (« Feux Tongolo ») sont après Étoudi, hors du tronçon. **À confirmer par Kevin** : les deux carrefours des rues 5.760 (3 545 m) et 5.769 / Albert Ateba Ebe (3 780 m), gardés en provisoire ; Pas de feu au pont d'Emana ni au carrefour d'Étoudi (Kevin, 2026-11-15).
   Autre enseignement des photos : de Messassi à Borne Fontaine, la route est une 2 × 2 voies séparée par un muret en béton (confirme les deux chaussées suivies par le trafic ; le muret n'est pas encore dessiné).
2. ~~Les mototaxis peuvent-ils « voler » des clients au joueur ?~~ → **Tranché** (Kevin, 2026-11-14) : oui tant que le client n'est pas monté dans le taxi du joueur, même s'il l'a accepté d'un coup de klaxon ; non une fois à bord. Un taxi concurrent, lui, ne prend pas le client accepté par le joueur (choix de Claude, à confirmer). Mototaxi : 2 places, montée en 3 s.
3. ~~Peut-on renverser un piéton ?~~ → **Tranché** (Kevin, 2026-11-14) : oui. Pas trop grave → on l'emmène à l'hôpital et on paie les premiers soins (on perd de l'argent) ; sinon la gendarmerie peut interpeller le chauffeur (garde à vue), avec un risque de saisie du véhicule. **Fait** (`Core/Incidents`, `accidents.json`) avec ces valeurs par défaut, à régler : moins de 4 km/h = simple bousculade ; moins de 25 km/h = blessé léger (soins 10 000 à 20 000 FCFA, 1 h perdue) ; au-delà = blessé grave : 6 chances sur 10 d'être interpellé (garde à vue : la journée s'arrête, bilan affiché ; taxi saisi 3 fois sur 10), sinon soins 40 000 à 80 000 FCFA et 2 h 30 perdues. Dans tous les cas les passagers descendent sans payer (choix de Claude, à confirmer). La saisie du taxi n'a pas encore d'effet sur les jours suivants (pas de journée suivante avant le jalon 3). Pas encore de fuite possible (délit de fuite : jalon 6).
4. *(nouvelle)* Les **taxis concurrents** s'arrêtent pour un client sur deux qui attend dans leur sens : est-ce le bon niveau de concurrence ?
