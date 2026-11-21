# Radio du taxi (mécanisme)

> Priorité cochée par Kevin le 2026-11-21 (« radio camerounaise »). Mécanisme écrit en session cloud le même jour : stations dans les données, changement de station dans le taxi, silence hors du taxi et pendant les dialogues, logique testée, bouton minimal. **Aucun son ni aucune musique fournis** : chaque piste est un emplacement vide, **à fournir par Kevin**. **Coupée** (`enabled: false` dans `radio.json`) tant que les questions ci-dessous n'ont pas de réponse.

## Ce qui est fait

| Élément | État |
|---|---|
| Stations dans les données | **fait** : `Assets/Resources/Data/radio.json` (`RadioConfig`) : pour chaque station, `id`, `name`, `frequencyMHz` (0 = à fournir, affichée « FM ? »), `programmes` (pistes ou émissions : `id`, `title`, `kind` = musique / emission / publicite / flash, `clip` = chemin du son sous `Resources`, sans extension, `seconds`, 0 = `defaultProgramSeconds`). Deux stations d'emplacement (« Station 1 (nom à fournir par Kevin) »…), cinq pistes vides |
| Changement de station | **fait** (`Core/Radio/CarRadio.cs`) : un bouton fait défiler coupée → station 1 → station 2 → … → coupée ; `startsOn` allume la première station au début de la journée |
| Stations en direct | **fait** : la grille de chaque station tourne en boucle et avance même quand on ne l'écoute pas ; en changeant de station, on retombe au milieu d'une piste (`ProgrammeOffset`) |
| Silence | **fait** : hors du taxi (pause, bilan), pendant qu'un passager parle (`DialogueActive`, branché quand les bulles du jalon 5 seront affichées), radio coupée ou `enabled: false` ; une piste sans son (emplacement vide) reste muette |
| Bouton dans Unity | **écrit, non exécuté** (`Driving/RadioView.cs`, créé par `StreetView` seulement si `enabled`) : bouton en haut à droite sous les jauges (« Station 1 · FM ? » et le titre en cours ; touche N au clavier), source audio sur le taxi qui charge le son de la piste en cours (`Resources.Load<AudioClip>`) et le reprend au bon endroit |
| Tests | `Assets/Tests/EditMode/RadioTests.cs` (5 tests) ; `DataInvariantsTests.Radio_Off_EmptySlots_ToBeProvidedByKevin` (coupée, noms et titres « à fournir par Kevin », aucun son dans les données ni dans le dépôt) |

Pour l'essayer sur le Mac : `"enabled": true` dans `radio.json` ; pour un son, déposer un fichier sous `Assets/Resources/Radio/` et mettre son chemin dans `clip` (ex. `"Radio/jingle"`), seulement s'il est sous licence compatible (question 2).

## Questions pour Kevin (radio)

1. **Noms des stations** : stations fictives inspirées de Yaoundé (lesquelles, combien ?) ou vraies stations (CRTV, radios privées) ? Une vraie station demande son accord (marque, logo, contenu) ; des noms fictifs évitent ce problème. Et leurs **fréquences** (fictives ou réelles) ?
2. **Musique : sous licence ou libre ?** **A.** musiques libres de droits (Creative Commons compatibles avec un jeu vendu, à créditer) ; **B.** musiques d'artistes camerounais sous licence (accord écrit, coût, durée, plateformes) ; **C.** musiques commandées pour le jeu (makossa, bikutsi, afrobeat…). Aucune musique n'est mise dans le dépôt avant ce choix.
3. **Animateurs** : voix d'animateurs entre les pistes (enregistrées par qui, en quelle langue : français, camfranglais, pidgin comme les dialogues ?) ou seulement de la musique ?
4. **Publicités locales** : fausses publicités humoristiques ou réalistes (produits fictifs, commerces du quartier) ? Des flashs d'information qui parlent du jeu (embouteillages, contrôles, pluie) ?
5. **Place dans le jeu** : radio allumée au départ (`startsOn`) ? Le volume baisse-t-il quand un passager parle (aujourd'hui : silence complet) ? Les passagers réagissent-ils à la musique (lien avec le jalon 5) ?
