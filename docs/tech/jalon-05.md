# Jalon 5 — Les passagers parlent (plan technique)

> Ouvert le 2026-11-21 en session cloud, **mécanisme seulement** : les répliques sont le contenu de Kevin (question 3), les sons et la radio demandent des assets. Contenu et critères : `docs/design/roadmap.md` ; vision §4.1 ter (interactions entre clients, dialogues écrits pour le jeu, jamais recopiés). Rien n'est actif : `enabled: false` dans `dialogues.json`.

**But** : l'ambiance camerounaise par les dialogues.

**Accepté si** (feuille de route) : sur une journée, les dialogues ne se répètent pas trop et restent lisibles en conduisant.

## Tâches

| Tâche | État |
|---|---|
| 5.1 Dialogues dans les données | **faite** (`Core/Talk/DialogueConfig.cs`, `dialogues.json`) : personnages types, répliques par situation (montée, discussion, changement de place, détour, paiement, négociation, refus de payer), réponses rapides, **langue de chaque réplique** (français, camfranglais, pidgin, 2026-11-21) ; **5 lignes d'exemple à remplacer**, aucun personnage |
| 5.2 Qui parle, quand | **faite** (`DialogueDirector`) : une bulle à la fois, 6 s de silence entre deux, temps d'affichage selon la longueur (2,5 s au moins, 14 caractères par seconde), une réplique ne revient pas avant 12 autres (on préfère se taire que se répéter ; les répliques pas encore dites dans la journée d'abord), discussions entre passagers quand ils sont au moins deux (toutes les 45 s en moyenne), compteur de répétitions de la journée |
| 5.3 Réponses du chauffeur | **faite** : boutons rapides ouverts 4 s ; sans réponse, la bulle part seule (rien ne bloque la conduite) ; la réponse choisie part avec la bulle pour en appliquer l'effet (réputation, 0 par défaut) |
| 5.4 Plaintes sur le détour | **faite** (`DetourWatch`) : le passager se plaint quand le taxi s'est éloigné de 150 m de sa destination depuis le point le plus proche atteint, puis de nouveau tous les 150 m |
| 5.5 Disputes de place, négociation, refus de payer | **déclencheurs prêts** (`TalkTriggers`) ; la négociation et le refus de payer n'ont **aucune conséquence** et une chance de 0 (`haggleChance`, `refusesToPayChance`) tant que Kevin n'a pas décidé |
| 5.6 Affichage dans Unity | **à faire avec les vraies répliques** : bulle au-dessus du taxi (ou du plan des places), 2 ou 3 boutons de réponse près du klaxon ; branchement dans `StreetView` (montée, changement de place, détour, paiement) |
| 5.7 Radio et ambiance sonore | **radio : mécanisme fait, coupé** (2026-11-21, priorité de Kevin ; `docs/tech/radio.md`) : stations dans `radio.json`, changement de station, silence hors du taxi et pendant les dialogues, bouton minimal ; aucun son fourni (pistes vides, à fournir par Kevin). **Ambiance sonore : non faite** (sons à produire) |

Tests : `Assets/Tests/EditMode/DialogueTests.cs` (9 tests, dont une journée de la durée de `day.json` : moins d'une réplique sur quatre répétée avec 24 répliques par situation pour 35 min ; 8 suffisaient pour 10 min) ; `DataInvariantsTests.Dialogues_Readable_KnownTriggers_ExamplesMarked_Off` (situations connues, longueur lisible, personnages connus, lignes d'exemple marquées).

## Combien de répliques écrire

Pour qu'une journée de 10 minutes ne se répète presque pas avec les réglages par défaut (une bulle toutes les 15 à 30 s), il fallait environ **8 répliques par situation** (≈ 60 au total). **Avec la journée de 35 min** (2026-11-21), il en faut environ **24 par situation** (≈ 170 au total) ; avec moins, le passager se tait plus souvent plutôt que de se répéter.

## Langues : français, camfranglais et pidgin (décision de Kevin, 2026-11-21)

Mécanisme prêt (`Core/Talk`, testé), **aucune vraie réplique écrite** :
- Chaque réplique a un champ **`register`** : `fr`, `camfranglais` ou `pidgin` (`TalkRegisters`) ; vide = convient à tout passager.
- **Choix par passager** : chaque passager reçoit un registre selon les parts de `registers` (poids ; par défaut **français 0,5, camfranglais 0,35, pidgin 0,15**, Yaoundé étant francophone : choix de Claude, à régler par Kevin), tiré à partir de son identifiant : il parle toujours de la même façon pendant sa course, sans toucher au hasard du jeu (`DialogueDirector.RegisterFor`). Un **personnage type** peut imposer son registre (`characters[].register`) ; la présentation ou une sauvegarde peut aussi l'imposer (`AssignRegister`).
- Un passager ne dit que les répliques de son registre ou sans registre ; s'il n'en existe aucune pour la situation, il dit une réplique du **registre de secours** (`fallbackRegister`, `fr` par défaut ; vide = il se tait). Les discussions sans passager désigné prennent tous les registres. La bulle connaît le registre de celui qui parle (`Bubble.Register`, pour un éventuel sous-titre).
- `dialogues.json` : les 3 exemples existants sont marqués `fr` ou neutre, et deux exemples de montée en camfranglais et en pidgin montrent le format, tous **« exemple à remplacer »**.
- Tests : `DialogueTests` (+5 : langues par défaut, registre stable par passager et réparti selon les parts, répliques qui suivent le registre, secours en français ou silence, registre imposé par un personnage) ; `DataInvariantsTests` (registres connus, les trois langues présentes avec un poids, au moins un exemple par langue).
- **Toujours coupé** (`enabled: false`) : il manque les répliques (question 3 : qui écrit), les personnages types (question 4) ; les questions 2, 5, 6, 7 restent ouvertes. Pour 35 min, prévoir ≈ 24 répliques par situation **et par langue** si chaque langue doit éviter les répétitions (sinon le registre de secours prend le relais).

## Questions pour Kevin (jalon 5)

Question 1 tranchée (français, camfranglais, pidgin) ; **les autres restent ouvertes** (la question 8, radio, a son propre document : `docs/tech/radio.md`).


1. ~~Langues à l'écran~~ → **Tranché par Kevin (2026-11-21) : français, camfranglais et pidgin.** Mécanisme fait (voir « Langues »). Reste à régler : la part de chaque langue parmi les passagers (0,5 / 0,35 / 0,15 par défaut).
2. **Voix ou texte** (question 2) : **A.** texte seul au début ; **B.** texte + petits sons de voix (« hmm », « eh ! ») ; **C.** voix enregistrées.
3. **Qui écrit les répliques** (question 3) : **A.** Kevin ; **B.** Claude propose à partir de situations données par Kevin, Kevin valide ; **C.** les deux. Format prêt : `dialogues.json` (`lines` : id, situation, personnage, texte ≤ 90 caractères, réponses).
4. **Personnages types** : lesquels (par ex. la commerçante du marché, l'étudiant, le fonctionnaire, la maman avec bébé…) ? Choisissent-ils leurs destinations ou leurs heures ?
5. **Négociation du prix** : **A.** le client propose moins, le joueur accepte ou refuse ; **B.** marchandage en 2 tours ; **C.** pas de négociation (le prix annoncé fait foi).
6. **Client qui refuse de payer** : **A.** il part sans payer (perte sèche) ; **B.** le joueur peut insister (temps perdu, réputation) ; **C.** appeler la police (lien avec le jalon 4).
7. **Effet des réponses** du chauffeur : sur la réputation, sur le pourboire, ou rien (ambiance seulement) ?
8. **Radio** : questions détaillées dans `docs/tech/radio.md` (noms des stations, musique sous licence ou libre, animateurs, publicités locales).
