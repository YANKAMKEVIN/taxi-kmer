# Jalon 1 — Une journée de taxi (premier jalon jouable)

**But** : prouver que la boucle de base est amusante sur téléphone. On conduit un taxi sur un bout du tronçon, on prend et dépose des clients en course partagée, et la journée se termine par le versement et le bilan.

**Hors périmètre** : police, incidents, famille et charges du foyer, dépôt et course via l'appli, achat de taxi, rendu réaliste final. Les visuels sont **provisoires** (blocs gris et textures libres) : on valide le fonctionnel.

## Décisions déjà prises (vision.md)

Unity 6 LTS (URP), C# ; Android avec réglages bas / moyen / haut ; monde libre ; course partagée (plusieurs clients, chacun sa destination).

## Valeurs par défaut (modifiables, toutes dans les fichiers de données)

| Sujet | Défaut |
|---|---|
| Portion du tronçon | **Dispensaire de Messassi → Emana** (décision de Kevin, 2026-11-11), tracé issu d'OpenStreetMap |
| Places passagers | **5** (décision de Kevin) : 3 derrière, 2 devant **selon la corpulence** (voir ci-dessous) |
| Corpulence (décision de Kevin : elle compte) | chaque client a une largeur : mince = 2, normal = 3, costaud = 4. Banquette avant = 6, arrière = 9. Exemples : costaud + mince devant ✓, costaud + normal devant ✗, trois normaux derrière ✓, deux costauds + un mince derrière ✗ |
| Durée d'une journée de jeu | **10 minutes** réelles (6 h → 20 h) (décision de Kevin) |
| Prix d'une course partagée | **proposé par le client selon la distance : 300 à 800 FCFA** (décision de Kevin) |
| Versement journalier | **10 000 FCFA** (validé par Kevin) |
| Carburant | jauge simple ; plein payé en fin de journée |
| Caméra | derrière le taxi, réglable en glissant le doigt ; vue intérieure plus tard |
| Commandes (décision de Kevin : flèches, pas d'inclinaison) | **à gauche** : flèches ← → pour tourner ; **à droite** : pédale d'accélérateur (grande) et pédale de frein / marche arrière (maintenue à l'arrêt) ; **en haut à droite** : klaxon (grand, c'est le geste clé pour accepter un client) ; **bouton contextuel** « Arrêt / faire monter / faire descendre » qui apparaît près d'un client ou d'une destination. Inclinaison du téléphone : éventuel second mode, plus tard |

Les montants ont été validés par Kevin le 2026-11-11 ; le carburant reste une valeur par défaut.

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

1. ~~Portion du tronçon ?~~ → **Tranché** : dispensaire de Messassi → Emana.
2. ~~Contrôles de conduite ?~~ → **Tranché** : flèches (schéma proposé ci-dessus) ; inclinaison en second mode éventuel.
3. ~~Corpulence ?~~ → **Tranché** : elle compte (largeurs ci-dessus, valeurs par défaut).
4. Quand le client ne rentre pas (banquette trop pleine) : il refuse de monter, ou le joueur peut demander à un passager de changer de place ?
