# Jalon 1 — Une journée de taxi (premier jalon jouable)

**But** : prouver que la boucle de base est amusante sur téléphone. On conduit un taxi sur un bout du tronçon, on prend et dépose des clients en course partagée, et la journée se termine par le versement et le bilan.

**Hors périmètre** : police, incidents, famille et charges du foyer, dépôt et course via l'appli, achat de taxi, rendu réaliste final. Les visuels sont **provisoires** (blocs gris et textures libres) : on valide le fonctionnel.

## Décisions déjà prises (vision.md)

Unity 6 LTS (URP), C# ; Android avec réglages bas / moyen / haut ; monde libre ; course partagée (plusieurs clients, chacun sa destination).

## Valeurs par défaut (modifiables, toutes dans les fichiers de données)

| Sujet | Défaut |
|---|---|
| Portion du tronçon | ≈ 1,5 km de l'axe Messassi → Bastos, tracé issu d'OpenStreetMap |
| Places passagers | 4 (taxi type berline) |
| Durée d'une journée de jeu | 12 minutes réelles (6 h → 20 h) |
| Prix d'une course partagée | 300 FCFA (jusqu'à 500 FCFA pour les longs trajets) |
| Versement journalier | 10 000 FCFA |
| Carburant | jauge simple ; plein payé en fin de journée |
| Caméra | derrière le taxi, réglable ; vue intérieure plus tard |

Les montants sont des **valeurs provisoires** : Kevin les ajustera pour qu'elles collent à la réalité de Yaoundé.

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
Modèle de données : client (point de prise, destination, prix), taxi (places, passagers à bord), journée (heure, recettes). Règles : accepter ou refuser, prise en charge, dépose, paiement.
- **Accepté si** : les tests EditMode couvrent l'acceptation, la capacité pleine, la dépose au bon endroit et le paiement.

### 1.5 Clients dans le monde
Clients au bord de la route qui annoncent leur destination et leur prix ; on klaxonne pour accepter ; arrêt pour qu'ils montent ; marqueurs de destination ; dépose.
- **Accepté si** : on peut enchaîner une dizaine de courses avec plusieurs passagers à bord.

### 1.6 HUD et fin de journée
Heure, argent du jour, passagers à bord et leurs destinations. Fin de journée : écran de bilan (recettes, versement, carburant, reste).
- **Accepté si** : le bilan est juste (vérifié par test) et lisible sur le téléphone.

### 1.7 Réglages de qualité
Sélecteur bas / moyen / haut, avec un premier écart réel (ombres, distance d'affichage, résolution de rendu).
- **Accepté si** : le réglage bas tient 30 images par seconde sur un téléphone d'entrée de gamme et le réglage haut 60 sur le S24.

## Validation

Une seule session sur téléphone avec Kevin à la fin du jalon : jouer une journée complète.

## Questions pour Kevin (ne bloquent pas le début)

1. Quelle portion précise du tronçon pour ce jalon (point de départ et d'arrivée) ?
2. Contrôles de conduite : volant à l'écran, flèches gauche / droite, ou inclinaison du téléphone ?
3. As-tu un téléphone d'entrée de gamme pour les tests, en plus du S24 ?
