# Jalon 5 — Les passagers parlent (plan technique)

> Ouvert le 2026-11-21 en session cloud, **mécanisme seulement** : les répliques sont le contenu de Kevin (question 3), les sons et la radio demandent des assets. Contenu et critères : `docs/design/roadmap.md` ; vision §4.1 ter (interactions entre clients, dialogues écrits pour le jeu, jamais recopiés). Rien n'est actif : `enabled: false` dans `dialogues.json`.

**But** : l'ambiance camerounaise par les dialogues.

**Accepté si** (feuille de route) : sur une journée, les dialogues ne se répètent pas trop et restent lisibles en conduisant.

## Tâches

| Tâche | État |
|---|---|
| 5.1 Dialogues dans les données | **faite** (`Core/Talk/DialogueConfig.cs`, `dialogues.json`) : personnages types, répliques par situation (montée, discussion, changement de place, détour, paiement, négociation, refus de payer), réponses rapides ; **3 lignes d'exemple à remplacer**, aucun personnage |
| 5.2 Qui parle, quand | **faite** (`DialogueDirector`) : une bulle à la fois, 6 s de silence entre deux, temps d'affichage selon la longueur (2,5 s au moins, 14 caractères par seconde), une réplique ne revient pas avant 12 autres (on préfère se taire que se répéter ; les répliques pas encore dites dans la journée d'abord), discussions entre passagers quand ils sont au moins deux (toutes les 45 s en moyenne), compteur de répétitions de la journée |
| 5.3 Réponses du chauffeur | **faite** : boutons rapides ouverts 4 s ; sans réponse, la bulle part seule (rien ne bloque la conduite) ; la réponse choisie part avec la bulle pour en appliquer l'effet (réputation, 0 par défaut) |
| 5.4 Plaintes sur le détour | **faite** (`DetourWatch`) : le passager se plaint quand le taxi s'est éloigné de 150 m de sa destination depuis le point le plus proche atteint, puis de nouveau tous les 150 m |
| 5.5 Disputes de place, négociation, refus de payer | **déclencheurs prêts** (`TalkTriggers`) ; la négociation et le refus de payer n'ont **aucune conséquence** et une chance de 0 (`haggleChance`, `refusesToPayChance`) tant que Kevin n'a pas décidé |
| 5.6 Affichage dans Unity | **à faire avec les vraies répliques** : bulle au-dessus du taxi (ou du plan des places), 2 ou 3 boutons de réponse près du klaxon ; branchement dans `StreetView` (montée, changement de place, détour, paiement) |
| 5.7 Radio et ambiance sonore | **non fait** : demande des sons et des musiques (chantier audio, pas en session cloud) |

Tests : `Assets/Tests/EditMode/DialogueTests.cs` (9 tests, dont une journée de 600 s : moins d'une réplique sur quatre répétée avec 8 répliques par situation) ; `DataInvariantsTests.Dialogues_Readable_KnownTriggers_ExamplesMarked_Off` (situations connues, longueur lisible, personnages connus, lignes d'exemple marquées).

## Combien de répliques écrire

Pour qu'une journée de 10 minutes ne se répète presque pas avec les réglages par défaut (une bulle toutes les 15 à 30 s), il faut environ **8 répliques par situation** (≈ 60 au total) ; avec 3, le passager se tait souvent plutôt que de se répéter.

## Questions pour Kevin (jalon 5)

1. **Langues à l'écran** (question 1) : **A.** français camerounais seulement ; **B.** français + camfranglais (vision §2 pilier 4) ; **C.** français, camfranglais et pidgin, langues locales sous-titrées.
2. **Voix ou texte** (question 2) : **A.** texte seul au début ; **B.** texte + petits sons de voix (« hmm », « eh ! ») ; **C.** voix enregistrées.
3. **Qui écrit les répliques** (question 3) : **A.** Kevin ; **B.** Claude propose à partir de situations données par Kevin, Kevin valide ; **C.** les deux. Format prêt : `dialogues.json` (`lines` : id, situation, personnage, texte ≤ 90 caractères, réponses).
4. **Personnages types** : lesquels (par ex. la commerçante du marché, l'étudiant, le fonctionnaire, la maman avec bébé…) ? Choisissent-ils leurs destinations ou leurs heures ?
5. **Négociation du prix** : **A.** le client propose moins, le joueur accepte ou refuse ; **B.** marchandage en 2 tours ; **C.** pas de négociation (le prix annoncé fait foi).
6. **Client qui refuse de payer** : **A.** il part sans payer (perte sèche) ; **B.** le joueur peut insister (temps perdu, réputation) ; **C.** appeler la police (lien avec le jalon 4).
7. **Effet des réponses** du chauffeur : sur la réputation, sur le pourboire, ou rien (ambiance seulement) ?
8. **Radio** : stations fictives ? musique libre de droits ou commandée ?
