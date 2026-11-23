# Radio du taxi (mécanisme)

> Priorité cochée par Kevin le 2026-11-21 (« radio camerounaise »). Mécanisme écrit en session cloud le même jour : stations dans les données, changement de station dans le taxi, silence hors du taxi et pendant les dialogues, logique testée, bouton minimal. **Aucun son ni aucune musique fournis** : chaque piste est un emplacement vide, **à fournir par Kevin**. **Activée le 21/11** (`enabled: true`) après les décisions de Kevin, **avec des pistes vides** : elle diffuse alors seulement les annonces, les publicités et les flashs, en texte à l'écran.

## Ce qui est fait

| Élément | État |
|---|---|
| Stations dans les données | **fait** : `Assets/Resources/Data/radio.json` (`RadioConfig`) : pour chaque station, `id`, `name`, `frequencyMHz` (0 = à fournir, affichée « FM ? »), `programmes` (pistes ou émissions : `id`, `title`, `kind` = musique / emission / publicite / flash, `clip` = chemin du son sous `Resources`, sans extension, `seconds`, 0 = `defaultProgramSeconds`). Deux stations d'emplacement (« Station 1 (nom à fournir par Kevin) »…), cinq pistes vides |
| Changement de station | **fait** (`Core/Radio/CarRadio.cs`) : un bouton fait défiler coupée → station 1 → station 2 → … → coupée ; `startsOn` allume la première station au début de la journée |
| Stations en direct | **fait** : la grille de chaque station tourne en boucle et avance même quand on ne l'écoute pas ; en changeant de station, on retombe au milieu d'une piste (`ProgrammeOffset`) |
| Silence | **fait** : hors du taxi (pause, bilan), pendant qu'un passager parle (`DialogueActive`, branché quand les bulles du jalon 5 seront affichées), radio coupée ou `enabled: false` ; une piste sans son (emplacement vide) reste muette |
| Bouton dans Unity | **écrit, non exécuté** (`Driving/RadioView.cs`, créé par `StreetView` seulement si `enabled`) : bouton en haut à droite sous les jauges (« Station 1 · FM ? » et le titre en cours ; touche B au clavier depuis le 23/11, N avant), source audio sur le taxi qui charge le son de la piste en cours (`Resources.Load<AudioClip>`) et le reprend au bon endroit |
| Tests | `Assets/Tests/EditMode/RadioTests.cs` (5 tests) ; `DataInvariantsTests.Radio_Off_EmptySlots_ToBeProvidedByKevin` (coupée, noms et titres « à fournir par Kevin », aucun son dans les données ni dans le dépôt) |

## Décisions de Kevin du 21/11 (tranché par Kevin le 21/11)

- **Stations fictives inspirées de Yaoundé** : 4 stations **proposées** par Claude (`statut: "proposé"`), noms et fréquences inventés, aucun vrai nom de station ni de marque (liste de vrais noms interdits vérifiée par `DataInvariantsTests`) :

| Station | Fréquence | Couleur | Pourquoi ce nom |
|---|---|---|---|
| **Nkol FM** | 94,1 FM | généraliste, réveil et conseils | *nkol* = colline en ewondo (Nkolbisson, Nkoldongo…) : clin d'œil à la « ville aux sept collines » |
| **Kongossa FM** | 104,3 FM | parole, humour, camfranglais | *kongossa* = les ragots, le bavardage, au Cameroun |
| **Bikutsi Express** | 89,9 FM | musique (bikutsi, makossa…) | le bikutsi, rythme de la région de Yaoundé |
| **Radio Ndamba** | 98,7 FM | sport du quartier | *ndamba* = le ballon, le football (à confirmer par Kevin) |

  **À vérifier par Kevin** : qu'aucune vraie station de Yaoundé ne porte ces noms ou ces fréquences (Claude n'a pas pu le vérifier sur place).
- **Musique libre de droits** (CC0 ou CC-BY) : règle et méthode ci-dessous ; **aucun fichier ajouté**. Les pistes de musique restent des emplacements vides (`clip: ""`, « à fournir : morceau CC0 ou CC-BY ») et la grille les saute tant qu'elles n'ont pas de son (`skipSilentProgrammes: true`).
- **Animateurs en français, camfranglais et pidgin** : 11 textes d'animateur **proposés** (`kind: "emission"`, `text`, `register`, `statut: "proposée"`), mêmes règles que les dialogues : tournures sûres seulement, sinon français marqué `"traduction": "à traduire"` (1 texte : les supporters, en pidgin).
- **Publicités humoristiques et réalistes**, produits et commerces **fictifs** : 6 publicités proposées (beignets-haricots Chez Tantine Rose, garage Bonne Route d'Emana, auto-école Patience, crédit Allô Tonton, huile rouge Soleil du Village, savon Clair Matin).
- **Flashs d'info liés au jeu**, branchés sur les vrais événements (`RadioNewsDesk`, `CarRadio.PushEvent`, 8 flashs proposés dans `flashes`) : **embouteillages** aux heures de pointe (7 h - 9 h et 17 h - 19 h, valeurs par défaut), **contrôle de police volant** qui s'installe (jalon 4, avec le lieu le plus proche), **pluie** qui commence (jalon 6), **jour de marché** (calendrier). Chaque événement n'est annoncé qu'une fois ; le flash passe devant le programme pendant 12 s (`flashSeconds`).
- **Quand un passager parle, le volume baisse** à 30 % (`duckVolume`) au lieu du silence (`CarRadio.Volume`).
- **Activée avec des pistes vides** : `RadioView` affiche le texte de l'animateur, de la publicité ou du flash sous le bouton de la radio (sous-titre), sans son.
- Total : **25 textes radio proposés** (11 animateurs, 6 publicités, 8 flashs) ; langues : français 17, camfranglais 4, pidgin 4.
- Tests : `RadioTests` (+3 : pistes vides et textes à l'écran, flashs, rédaction ; volume baissé pendant les dialogues) ; `DataInvariantsTests.Radio_On_FictionalStations_ProposedTexts_NoSound_FreeLicencesOnly`.

## Musique : règle de licence (tranché par Kevin le 21/11 : musique libre de droits)

Le jeu sera **vendu** : seule une licence qui permet l'usage commercial et la modification convient.

| Licence | Utilisable ? | Pourquoi |
|---|---|---|
| **CC0** (domaine public) | **oui** | aucune condition ; créditer quand même par politesse |
| **CC-BY** (4.0 de préférence) | **oui** | usage commercial permis ; **crédit obligatoire** (auteur, titre, licence, lien) |
| CC-BY-SA | non (par prudence) | le « partage dans les mêmes conditions » pourrait s'étendre au jeu ou à la bande-son modifiée |
| CC-BY-**NC** (et toute licence NC) | **non** | pas d'usage commercial : interdit dans un jeu vendu |
| CC-BY-**ND** (et toute licence ND) | **non** | pas de modification : on ne peut ni couper, ni baisser, ni mixer avec l'animateur |
| licences « maison » (sites de musique gratuite), « libre de droits » sans licence écrite | non | conditions variables, souvent interdites dans un produit revendu ; à traiter au cas par cas avec Kevin |

`radio.json` refuse toute piste avec un son sans `licence` « CC0 » ou « CC-BY », sans `credit` et sans `source` (`DataInvariantsTests`).

## Musique : méthode pour choisir et créditer un morceau

1. **Chercher** sur des sites qui affichent la licence de chaque morceau : Free Music Archive, ccMixter, Wikimedia Commons, Internet Archive (collections « Netlabels »), Jamendo (filtrer « usage commercial » et vérifier la licence CC du morceau). Pour l'ambiance : mots-clés *bikutsi*, *makossa*, *afrobeat*, *highlife*, *Cameroon*.
2. **Vérifier la licence sur la page du morceau** (pas seulement celle de l'album ou de l'artiste) : CC0 ou CC-BY, sans NC ni ND ; l'artiste doit être celui qui publie (pas une réédition d'un morceau d'un autre).
3. **Garder la preuve** : adresse de la page, date, capture d'écran de la licence (ou une copie sur web.archive.org), dans `docs/references/musique/` (à créer au premier morceau).
4. **Préparer le fichier** : format `.ogg`, volume normalisé, sous `Assets/Resources/Radio/` ; noter toute modification (coupé, raccourci, mixé).
5. **Remplir `radio.json`** pour la piste : `clip` (`"Radio/nom-du-fichier"`, sans extension), `title`, `seconds`, `licence` (`"CC0"` ou `"CC-BY"`), `credit` (« Titre — Artiste »), `source` (adresse de la page).
6. **Créditer dans le jeu** : la ligne du morceau dans l'écran des crédits (« Titre — Artiste, licence CC-BY 4.0, lien ; modifié : raccourci »), comme le crédit OpenStreetMap. Les crédits se génèrent à partir des champs `credit`, `licence` et `source`.
7. **Relire avec Kevin** avant de fusionner : c'est lui qui écoute et valide.

Pour l'essayer sur le Mac : `"enabled": true` dans `radio.json` ; pour un son, déposer un fichier sous `Assets/Resources/Radio/` et mettre son chemin dans `clip` (ex. `"Radio/jingle"`), seulement s'il est sous licence compatible (question 2).

## Questions pour Kevin (radio)

Questions 1 à 4 tranchées le 21/11 (stations fictives, musique libre CC0 / CC-BY, animateurs en trois langues, publicités humoristiques et flashs liés au jeu) ; la 5 en partie (le volume baisse quand un passager parle). Restent : valider les noms et fréquences proposés, les textes proposés, choisir les morceaux, `startsOn` (radio allumée au départ ? non par défaut) et la réaction des passagers à la musique.


1. ~~Noms des stations~~ → **stations fictives, tranché par Kevin le 21/11** (4 proposées ci-dessus). Ancienne question : **Noms des stations** : stations fictives inspirées de Yaoundé (lesquelles, combien ?) ou vraies stations (CRTV, radios privées) ? Une vraie station demande son accord (marque, logo, contenu) ; des noms fictifs évitent ce problème. Et leurs **fréquences** (fictives ou réelles) ?
2. ~~Musique~~ → **option A, libre de droits (CC0 ou CC-BY), tranché par Kevin le 21/11.** Ancienne question : **Musique : sous licence ou libre ?** **A.** musiques libres de droits (Creative Commons compatibles avec un jeu vendu, à créditer) ; **B.** musiques d'artistes camerounais sous licence (accord écrit, coût, durée, plateformes) ; **C.** musiques commandées pour le jeu (makossa, bikutsi, afrobeat…). Aucune musique n'est mise dans le dépôt avant ce choix.
3. ~~Animateurs~~ → **en français, camfranglais et pidgin, textes proposés par Claude, tranché par Kevin le 21/11** (voix : non enregistrées, texte à l'écran). Ancienne question : **Animateurs** : voix d'animateurs entre les pistes (enregistrées par qui, en quelle langue : français, camfranglais, pidgin comme les dialogues ?) ou seulement de la musique ?
4. ~~Publicités~~ → **humoristiques et réalistes, produits fictifs, plus des flashs liés au jeu, tranché par Kevin le 21/11.** Ancienne question : **Publicités locales** : fausses publicités humoristiques ou réalistes (produits fictifs, commerces du quartier) ? Des flashs d'information qui parlent du jeu (embouteillages, contrôles, pluie) ?
5. **Place dans le jeu** (le volume baisse quand un passager parle : **tranché par Kevin le 21/11**) : radio allumée au départ (`startsOn`) ? Le volume baisse-t-il quand un passager parle (aujourd'hui : silence complet) ? Les passagers réagissent-ils à la musique (lien avec le jalon 5) ?
