# Taxi Kmer — Vision (brouillon v0.1)

> Brouillon issu de la discussion du 2026-11-11. Les éléments marqués **[proposition]** sont des suggestions de Claude, à valider ou rejeter par Kevin (directeur du jeu). Les **questions ouvertes** sont en fin de document.

## 1. Pitch

La vie d'un taximan à Yaoundé, en 3D. On commence chauffeur pour le taxi d'un autre, on récupère les clients au bord de la route et on les dépose à bon port. Puis on achète son propre taxi, un deuxième, et on finit par monter son entreprise de taxis.

**Inspiration** : un jeu de mototaxi congolais qui se passe à Kinshasa. Taxi Kmer s'en distingue par la voiture (taxi jaune), Yaoundé et la couche de gestion (versement, charges du foyer, flotte, entreprise).

Le jeu montre la réalité du métier : les gains et les charges de chaque jour, la famille à nourrir, les contrôles de police (même avec tous ses papiers en règle), la corruption, les accidents avec délit de fuite.

## 2. Piliers

1. **Conduire et servir le client** : prendre, transporter et déposer, dans une ville vivante (trafic, mototaxis, piétons).
2. **Faire ses comptes** : chaque journée se termine par un bilan (recettes, versement, carburant, charges, épargne).
3. **Monter en grade** : chauffeur → propriétaire → plusieurs taxis → entreprise.
4. **Le Cameroun tel qu'il est** : lieux reconnaissables, dialogues en français camerounais et camfranglais, injustices montrées sans détour.

## 3. Boucle de jeu

**Boucle courte (une course)**
Repérer un client → l'accepter ou le refuser → le conduire → le déposer → être payé.

**Boucle moyenne (une journée)**
Partir le matin → enchaîner les courses → gérer les imprévus (police, panne, accident, embouteillage) → rentrer → **bilan du jour**.

**Boucle longue (la carrière)**
Épargner → acheter un taxi → embaucher des chauffeurs → gérer la flotte → s'étendre (nouveaux quartiers, puis Douala).

## 4. Mécaniques clés

### 4.1 Les trois types de course (décision de Kevin, 2026-11-11)
| Type | Passagers | Fonctionnement |
|---|---|---|
| **Course partagée** (par défaut) | plusieurs, chacun sa destination | Le client annonce sa destination et son prix au bord de la route (« Bastos, 300 ! »). Le joueur accepte d'un coup de klaxon ou passe son chemin, et organise l'ordre des dépôts. |
| **Dépôt** | une seule personne (ou son groupe) | Course privée, plus chère, sans autre passager. |
| **Course via application** (appli fictive, type Yango) | seulement les passagers de la commande | Commande reçue sur le téléphone du chauffeur, prise en charge à une adresse, pas d'autre passager. |

L'application est **fictive** (décision de Kevin, 2026-11-11) : aucune vraie marque dans le jeu. Son nom reste à choisir ; en attendant, on l'appelle « l'appli ».

### 4.2 Le versement [proposition]
Tant qu'il conduit le taxi d'un autre, le joueur doit reverser chaque jour une somme fixe au propriétaire. S'il ne l'atteint pas, la relation avec le propriétaire se dégrade.

### 4.3 Bilan du jour
- **Recettes** : les courses.
- **Sorties** : versement, carburant, réparations, amendes et « arrangements », repas.
- **Charges du foyer** : selon la situation (célibataire, marié, nombre d'enfants) : loyer, nourriture, scolarité, santé.
- **Reste** : l'épargne du jour.

### 4.4 Contrôles de police
Contrôle des papiers (permis, carte grise, assurance, visite technique…). Même en règle, on peut être sommé de payer. Choix du joueur [proposition] : payer, négocier, contester (temps perdu, risque d'escalade).

### 4.5 Incidents de la route
Accrochages, délits de fuite, pannes, crevaisons, nids-de-poule, embouteillages, pluie.

### 4.6 Progression
Chauffeur salarié → propriétaire d'un taxi → plusieurs taxis avec des chauffeurs embauchés (qui eux aussi doivent un versement) → entreprise.

## 5. Monde

- **Ville de départ** : Yaoundé, le tronçon **Messassi → Bastos**.
- **Objectif** : une ville **reconnaissable**. Exactitude totale non visée [proposition].
  - **Routes** : tracé issu d'OpenStreetMap (licence ODbL, à créditer).
  - **Bâtiments ordinaires** : générés à partir des emprises OSM, puis habillés.
  - **Lieux emblématiques** : modélisés à la main.
  - **Interdit dans le jeu** : données 3D Google, images Maps / Street View et photos d'internet sans licence libre (référence visuelle seulement).
  - **Textures** : sources libres d'abord, puis photos prises sur place, fournies par Kevin.
- **Extensions** : d'autres quartiers de Yaoundé, puis Douala.

## 6. Ton

Réaliste, chaleureux, avec de l'humour. Les injustices sont montrées sans ton moralisateur, de façon plutôt satirique [proposition].

## 7. Technique [proposition]

| Sujet | Valeur proposée |
|---|---|
| Style | **Réaliste** (décision de Kevin, 2026-11-11) : textures photographiques, lumière crédible, lieux reconnaissables. Réglages de qualité adaptables (bas / moyen / haut) selon le téléphone. |
| Moteur | **Unity 6 LTS**, pipeline URP (décision de Kevin, 2026-11-11) |
| Cible principale | Android. La plupart des joueurs camerounais ont des téléphones courants, comparables à ceux d'Europe (précision de Kevin, 2026-11-11). **Trois réglages de qualité : bas / moyen / haut** ; le réglage bas sert la minorité d'appareils anciens ou modestes. |
| Cible secondaire | Web (démo), éventuellement iOS |
| Vue | 3D, caméra derrière le taxi ; contrôles tactiles |
| Données | courses, clients, dialogues, prix et charges dans des fichiers de données |

## 8. Premier jalon jouable [proposition]

Un bout du tronçon en monde libre, un taxi, des courses partagées (prise, refus, dépôt, paiement), le versement et le bilan du jour. Pas encore de police, de famille ni de flotte.

## 9. Questions ouvertes (à trancher par Kevin)

1. ~~Taxi partagé ou une course à la fois ?~~ → **Tranché** : trois types de course (voir 4.1).
2. ~~Monde libre ou trajets guidés ?~~ → **Tranché** : monde libre.
3. ~~Vraie marque ou appli fictive ?~~ → **Tranché** : appli fictive. Reste à choisir son nom (propositions : Allô Taxi, Kmer Ride, Mbolo).
4. ~~Style visuel ?~~ → **Tranché** : réaliste (référence : GTA, Spider-Man ; on reconnaît les lieux).
5. ~~Téléphones visés ?~~ → **Tranché** : surtout des téléphones courants (comme en Europe), réglages bas / moyen / haut pour les autres.
6. ~~Moteur ?~~ → **Tranché** : Unity.
7. ~~Photos des lieux ?~~ → **Tranché** : Street View et internet servent seulement de **référence** pour modéliser ; les textures du jeu viennent de sources libres (Poly Haven, ambientCG, Mapillary, Wikimedia Commons) ; Kevin fournira les photos prises sur place quand il le faudra.
8. Situation familiale : choisie au départ, ou qui évolue pendant la partie (mariage, enfants) ?
9. Peut-on perdre (dettes, taxi saisi, permis retiré) ?
10. Langue : français seulement, ou aussi pidgin et camfranglais à l'écran ?
11. Modèle économique : payant, gratuit avec publicité, ou gratuit ?
12. Calendrier : en parallèle de Pokemake ou après ?
13. ~~Nom du dépôt ?~~ → **Tranché** : `taxi-kmer`.
