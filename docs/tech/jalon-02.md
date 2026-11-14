# Jalon 2 — La ville vit (plan technique)

> Ouvert le 2026-11-14, pendant que le jalon 1 attend sa validation sur téléphone. Contenu et critères : `docs/design/roadmap.md`. Les questions de Kevin ci-dessous ne bloquent pas le début : chaque réponse provisoire est un réglage dans les données, facile à changer.

**But** : que la route de Messassi à Étoudi ressemble à une vraie route de Yaoundé, pleine de monde.

**Accepté si** : 30 images/s au réglage moyen avec le trafic ; une journée paraît vivante ; aucune voiture bloquée en permanence.

## Choix techniques

- **Trafic sur l'axe principal d'abord** : le joueur fait l'aller-retour Messassi ↔ Étoudi ; c'est là que le trafic se voit. Les rues latérales (véhicules qui entrent et sortent aux carrefours) viennent ensuite (tâche 2.6).
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
| 2.6 Carrefours et rues latérales | à faire : véhicules qui entrent sur l'axe et en sortent, priorité au grand axe |
| 2.7 Performance et qualité | à faire sur téléphone : mesurer les 30 images/s en Moyen avec le trafic de pointe ; le réglage Bas réduit la densité |

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
- Mesuré hors Unity (essai sur le vrai monde, heure de pointe) : jusqu'à 38 véhicules et 37 piétons autour du joueur, 0,2 ms de calcul par image sur PC ; joueur garé 3 min sur la voie : 47 voitures le contournent, aucune bloquée plus de 4 s.

## Questions pour Kevin (réponses provisoires en place)

1. **Feux tricolores** sur cette portion, ou seulement des carrefours sans feux ? → *provisoire* : pas de feux.
2. **Les mototaxis peuvent-ils « voler » des clients au joueur ?** → *provisoire* : non (`motoTakesClients: false`), seuls les taxis concurrents prennent des clients.
3. **Peut-on renverser un piéton** (et avec quelles conséquences), ou le jeu l'empêche-t-il ? → *provisoire* : les piétons ne traversent jamais juste devant le joueur ; s'il les heurte quand même, rien ne se passe encore (pas de conséquence avant la réponse).
4. *(nouvelle)* Les **taxis concurrents** s'arrêtent pour un client sur deux qui attend dans leur sens : est-ce le bon niveau de concurrence ?
