# Jalon 1 — Une journée de taxi (premier jalon jouable)

**But** : prouver que la boucle de base est amusante sur téléphone. On conduit un taxi sur un bout du tronçon, on prend et dépose des clients en course partagée, et la journée se termine par le versement et le bilan.

**Hors périmètre** : police, incidents, famille et charges du foyer, dépôt et course via l'appli, achat de taxi, rendu réaliste final. Les visuels sont **provisoires** (blocs gris et textures libres) : on valide le fonctionnel.

## Décisions déjà prises (vision.md)

Unity 6 LTS (URP), C# ; Android avec réglages bas / moyen / haut ; monde libre ; course partagée (plusieurs clients, chacun sa destination).

## Valeurs par défaut (modifiables, toutes dans les fichiers de données)

| Sujet | Défaut |
|---|---|
| Portion du tronçon | **Dispensaire de Messassi → Étoudi** (décision de Kevin, 2026-11-11), tracé issu d'OpenStreetMap |
| Places passagers | **5** (décision de Kevin) : 3 derrière, 2 devant **selon la corpulence** (voir ci-dessous) |
| Corpulence (décision de Kevin : elle compte) | chaque client a une largeur : mince = 2, normal = 3, costaud = 4. Banquette avant = 6, arrière = 9. Exemples : costaud + mince devant ✓, costaud + normal devant ✗, trois normaux derrière ✓, deux costauds + un mince derrière ✗ |
| Durée d'une journée de jeu | **10 minutes** réelles (6 h → 20 h) (décision de Kevin) |
| Prix d'une course partagée | **proposé par le client selon la distance : 300 à 800 FCFA** (décision de Kevin) |
| Versement journalier | **10 000 FCFA** (validé par Kevin) |
| Carburant | jauge simple ; plein payé en fin de journée |
| Caméra | derrière le taxi, réglable en glissant le doigt ; vue intérieure plus tard |
| Commandes (décision de Kevin : flèches, pas d'inclinaison) | **à gauche** : flèches ← → pour tourner ; **à droite** : pédale d'accélérateur (grande) et pédale de frein / marche arrière (maintenue à l'arrêt) ; **en haut à droite** : klaxon (grand, c'est le geste clé pour accepter un client) ; **bouton contextuel** « Arrêt / faire monter / faire descendre » qui apparaît près d'un client ou d'une destination. Inclinaison du téléphone : éventuel second mode, plus tard |

Les montants ont été validés par Kevin le 2026-11-11 ; le carburant reste une valeur par défaut.

## Avancement

| Tâche | État |
|---|---|
| 1.1 Socle Unity | **faite** : Unity 6.3 LTS (6000.3.28f1) + Android, URP 17.3, qualité Bas / Moyen / Haut, scène Boot, 29 tests EditMode verts dans Unity, APK de 29 Mo construit (`build/android/TaxiKmer.apk`) ; lancé sur le Galaxy S24 de Kevin (2026-11-11), sans erreur |
| 1.2 Route OSM | **faite sauf essai sur téléphone** : données OSM téléchargées, itinéraire de 6,1 km calculé (`tools/osm/osm_route.py`), monde découpé dans un couloir de 250 m (289 routes, 4 542 bâtiments, `world_messassi_etoudi.json`), construit au lancement par `WorldBuilder` (scène `Monde`, 165 blocs de maillage) ; caméra provisoire qui parcourt l'itinéraire ; crédit OSM à l'écran ; captures de contrôle (`WorldSnapshots`) ; APK construit, pas encore installé (téléphone hors ligne) |
| 1.3 Conduite | **faite, vérifiée sur émulateur Android (2026-11-11), essai sur le S24 à faire** : taxi provisoire en blocs (berline jaune), physique WheelCollider en traction avant, départ au dispensaire de Messassi sur la voie de droite ; commandes tactiles de Kevin (← → à gauche, grande pédale d'accélérateur et frein / marche arrière à droite, klaxon en haut à droite) et clavier ; caméra derrière le taxi qu'on tourne en glissant le doigt ; compteur km/h ; logique pure (`Core/Driving` : commandes → consignes, disposition tactile, caméra) testée, plus 3 tests PlayMode de la physique ; réglages dans `driving.json`. Émulateur `TaxiKmer_Test` (Android 16, ARM64, 4 cœurs, 4 Go) : le monde se construit, aucune erreur Unity dans logcat, flèches, accélérateur, frein / marche arrière (« R » à 15 km/h) et rotation de la caméra au doigt fonctionnent ; au départ, tout droit, on bute sur le bâtiment au bout de la rue (le T où l'itinéraire tourne) |
| 1.4 Logique des courses | **faite** : places et corpulence, changement de place, prix, horloge, bilan, montée et descente, générateur de clients ; 33 tests |
| 1.5 Clients dans le monde | logique faite (générateur) ; lieux de clients dans `stops_messassi_etoudi.json`, distances mesurées sur l'itinéraire OSM : dispensaire de Messassi 0 m, carrefour / marché de Messassi 631 m, pont d'Emana 1 567 m, Emana 2 154 m, Borne Fontaine 2 948 m, Étoudi devant le palais 6 132 m ; présentation Unity à faire |
| 1.6 HUD et bilan | logique du bilan faite ; écrans à faire (Unity) |
| 1.7 Réglages de qualité | à faire (Unity) |

Valeurs par défaut ajoutées : prix 300 (≤ 2 km), 400 (≤ 4 km), 500 (≤ 7 km), 600 (≤ 10 km), 800 au-delà ; carburant 840 FCFA/L, 8 L/100 km ; carrures 30 % minces, 50 % normaux, 20 % costauds ; le taxi remplit l'arrière avant l'avant ; un client déposé au mauvais arrêt reste à bord. Tâche 1.1 : qualité Bas (rendu 70 %, sans ombres, sans HDR), Moyen (85 %, ombres à 35 m), Haut (100 %, ombres douces à 70 m, MSAA 4×, HDR) ; Moyen par défaut ; paysage uniquement ; Android 8 minimum ; ARM64 seulement. Tâche 1.2 : monde **plat** (pas de relief) ; couloir de 250 m de part et d'autre de l'itinéraire ; itinéraire calculé en préférant les grands axes, lieux raccrochés au grand axe le plus proche ; largeurs OSM gardées seulement entre 2 et 25 m (sinon voies × 3,25 m) ; bâtiments de 3 m par étage (1 étage si OSM ne dit rien) ; couleurs provisoires (asphalte gris, bâtiments gris clair, sol latérite) ; brouillard de 150 à 600 m ; caméra provisoire à 40 km/h en allers-retours.

Tâche 1.3 (valeurs par défaut, toutes dans `Assets/Resources/Data/driving.json`, **à relire par Kevin après essai**) : vitesse maximale 80 km/h, marche arrière 15 km/h ; frein maintenu 0,35 s à l'arrêt (< 2 km/h) avant de reculer ; frein à main léger à l'arrêt sans pédale ; volant progressif (braquage complet en 0,4 s, retour au centre en 0,25 s), angle des roues 35° à l'arrêt réduit à 10° à 70 km/h ; berline de 1 150 kg, traction avant ; mesuré en test : 0 → 57 km/h en 6 s, arrêt depuis 57 km/h en 1,8 s, virage très serré possible (145° en 2 s à ~40 km/h, adhérence généreuse, style arcade) ; taxi posé 8 m après le dispensaire, à 1,8 m à droite de l'axe ; remis sur la route s'il reste 3 s sur le toit (ou touche R) ; caméra à 7 m, 12° au-dessus, revient derrière le taxi 2 s après qu'on a lâché, se rapproche si un bâtiment la cache ; boutons : flèches de 0,27 × 0,32 hauteur d'écran, accélérateur 0,22 × 0,48, frein 0,22 × 0,28, klaxon 0,24 × 0,24 ; son de klaxon provisoire synthétisé (deux tons 400 et 500 Hz) ; crédit OSM déplacé en bas au centre ; clavier : flèches / ZQSD / WASD, espace = frein, H = klaxon, R = remettre sur la route.

## Tâches

### 1.1 Socle du projet
Projet Unity 6 (URP), arborescence, assets de qualité bas / moyen / haut, Unity Test Framework, build Android de base.
- **Accepté si** : le projet s'ouvre sans erreur, un test EditMode passe, un APK vide se lance sur le téléphone.

### 1.2 Route de test depuis OpenStreetMap
Script d'import qui transforme un extrait OSM (routes de la portion choisie) en maillage de route et en emprises de bâtiments (blocs gris).
- **Accepté si** : la portion est conduisible et ses carrefours correspondent à la carte réelle.

### 1.3 Conduite du taxi
Physique de voiture arcade-réaliste, contrôles tactiles (volant ou flèches, accélérateur, frein), klaxon.
- **Accepté si** : on conduit confortablement d'un bout à l'autre de la portion sur le S24.

### 1.4 Logique des courses (sans scène, testée)
Modèle de données : client (point de prise, destination, prix, corpulence), taxi (banquettes avant et arrière, passagers à bord), journée (heure, recettes). Règles : accepter ou refuser, placement selon la corpulence, prise en charge, dépose, paiement.
- **Accepté si** : les tests EditMode couvrent l'acceptation, le placement selon la corpulence (avant et arrière), la dépose au bon endroit et le paiement.

### 1.5 Clients dans le monde
Clients au bord de la route qui annoncent leur destination et leur prix ; on klaxonne pour accepter ; arrêt pour qu'ils montent ; marqueurs de destination ; dépose.
- **Accepté si** : on peut enchaîner une dizaine de courses avec plusieurs passagers à bord.

### 1.6 HUD et fin de journée
Heure, argent du jour, passagers à bord et leurs destinations. Fin de journée : écran de bilan (recettes, versement, carburant, reste).
- **Accepté si** : le bilan est juste (vérifié par test) et lisible sur le téléphone.

### 1.7 Réglages de qualité
Sélecteur bas / moyen / haut, avec un premier écart réel (ombres, distance d'affichage, résolution de rendu).
- **Accepté si** : le réglage moyen tient 30 images par seconde sur un téléphone de milieu de gamme récent, et le réglage haut 60 sur le S24.

## Validation

Une seule session sur téléphone avec Kevin à la fin du jalon : jouer une journée complète.

## Questions pour Kevin (ne bloquent pas le début)

1. ~~Portion du tronçon ?~~ → **Tranché** : dispensaire de Messassi → Étoudi.
2. ~~Contrôles de conduite ?~~ → **Tranché** : flèches (schéma proposé ci-dessus) ; inclinaison en second mode éventuel.
3. ~~Corpulence ?~~ → **Tranché** : elle compte (largeurs ci-dessus, valeurs par défaut).
4. ~~Client qui ne rentre pas ?~~ → **Tranché** : au choix du joueur, il refuse le client ou demande à un passager de changer de banquette (`TaxiCabin.FindMoveToFit`, `Shift.MovePassenger`).
5. ~~Noms des arrêts ?~~ → **Tranché** : pas d'arrêts comme pour un bus, mais des lieux où l'on trouve beaucoup de clients : dispensaire de Messassi, carrefour / marché de Messassi, Borne Fontaine, pont d'Emana, Emana, Étoudi (devant le palais de la présidence).

## Extension décidée par Kevin — 2026-11-11

Le premier jalon se termine désormais à **Étoudi, devant le palais de la présidence (Palais de l’Unité)**, au lieu d’Émana. Les repères à documenter sont Carrefour / Marché Messassi, Borne Fontaine, Pont Émana, Émana et l’arrivée à Étoudi. L’ordre géographique et le tracé exact doivent être recoupés avec la carte et Kevin ; les positions des photos d’une fiche Maps ne sont pas automatiquement celles du repère.

Réalisé le 2026-11-11 (tâche 1.2) : lieux et couverture OSM étendus jusqu'à Étoudi ; `stops_messassi_emana.json` remplacé par `stops_messassi_etoudi.json`. Coordonnées des lieux dans `tools/osm/landmarks_messassi_etoudi.json`. Bastos reste une extension ultérieure.

**À relire par Kevin** (choix faits par défaut) :
- « Borne Fontaine » = le **Carrefour Bonne Fontaine** d'OSM. Il est **au sud d'Emana** : l'ordre sur la route est donc dispensaire → carrefour Messassi → pont d'Emana → Emana → Bonne Fontaine → Étoudi (et non Borne Fontaine avant le pont).
- Dispensaire de Messassi = le **Centre Médical d'Arrondissement Yaoundé I Olembe** (seul centre de santé au nord du carrefour sur OSM), à 630 m au nord-est du carrefour.
- Arrivée : l'itinéraire quitte la N1 au carrefour d'Étoudi et prend la rue Charles Bindzi vers l'ouest jusqu'au palais (3,2 km depuis Bonne Fontaine).
