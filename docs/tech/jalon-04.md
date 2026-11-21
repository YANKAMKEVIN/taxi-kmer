# Jalon 4 — Le contrôle (plan technique)

> Ouvert le 2026-11-21 en session cloud, **logique pure seulement**, pendant que les jalons 1 à 3 attendent l'essai sur téléphone. Contenu et critères : `docs/design/roadmap.md` ; vision §4.4. Rien n'est actif dans le jeu : `enabled: false` dans `police.json` tant que Kevin n'a pas répondu aux questions ci-dessous.

**But** : le contrôle de police, cœur du propos sur l'injustice.

**Accepté si** (feuille de route) : une dizaine de contrôles variés dans une journée de test ; les règles sont dans les données et testées.

## Ce que disent la feuille de route et la vision (codé)

- Postes de contrôle **fixes** et contrôles **volants** sur la portion.
- **Papiers** du taxi et du chauffeur (permis, carte grise, assurance, visite technique, licence de taxi) avec **dates d'expiration**.
- Le policier vérifie, **trouve (ou invente)** un problème, **demande de l'argent** ; « même en règle, on peut être sommé de payer » (vision §4.4).
- Choix : **payer, négocier, contester, appeler quelqu'un** ; conséquences : **temps perdu, argent, fourrière, réputation**.
- **Surcharge** (trop de passagers, **deux devant**) comme prétexte.
- Bilan : les sommes payées sont une sortie « amendes et arrangements » (vision §4.3).

## Tâches

| Tâche | État |
|---|---|
| 4.1 Papiers et expiration | **faite** (`Core/Police/Papers.cs`, `PaperSet`) : dernier jour de validité par papier, jours restants, papiers expirés, renouvellement (à temps : la période suit l'ancienne ; expiré : elle part du jour même), texte « Assurance : 3 jours » |
| 4.2 Postes fixes et contrôles volants | **faite** (`PoliceSchedule`) : postes des données, contrôles volants tirés au hasard (position, heure, durée) ; le taxi qui passe devant un contrôle en place est arrêté selon une chance ; un poste n'arrête pas deux fois en moins de 2 h de jeu |
| 4.3 Scène de contrôle | **faite** (`PoliceControl`) : problème réel d'abord (papier expiré, surcharge, deux devant), sinon prétexte inventé selon une chance, sinon « en règle » et on repart (le contrôle prend quand même 10 min) ; somme demandée ; payer, négocier (baisse ou le policier se braque, 2 tentatives, plancher), contester et appeler (issues tirées dans les données) ; temps, argent, fourrière, réputation |
| 4.4 Conséquences sur la journée | **faite** (`Street.ApplyPoliceControl`) : somme payée → dépense « Contrôle de police » du bilan, temps perdu sur l'horloge, réputation ; fourrière → frais « Fourrière », passagers descendus sans payer, journée arrêtée (`DaySummary.Impounded`) |
| 4.5 Affichage dans Unity | **à faire après les décisions de Kevin** : panneau du contrôle (problème, somme, 4 boutons qui ne bloquent pas plus que nécessaire), poste en primitives, ligne du bilan ; non écrit en session cloud (Unity n'est pas installé, et rien n'est actif tant que `enabled` est à false) |
| 4.6 Papiers dans la sauvegarde et renouvellement | **à faire après les décisions de Kevin** (qui paie, combien, où renouveler) : `PaperSet.Restore` est prêt pour la sauvegarde |

Tests : `Assets/Tests/EditMode/PoliceTests.cs` (13 tests, dont le ton des textes ; dont la journée de test à 10 contrôles variés) ; données vérifiées par `DataInvariantsTests.Police_MatchesDefaults_StaysOffUntilKevinDecides`.

## Valeurs par défaut (`police.json`, provisoires)

- **Coupé** (`enabled: false`).
- Deux postes fixes **provisoires** : Messassi (900 m) et Étoudi (5 200 m), arrêt une fois sur deux ; 2 contrôles volants par jour, en place 1 h, arrêt 7 fois sur 10.
- Contrôle : 10 min de jeu ; règle de charge : 4 passagers au plus, 1 devant (le taxi en prend 5, dont 2 devant : la surcharge est presque toujours un prétexte possible, comme dans la feuille de route).
- En règle : 1 chance sur 2 que le policier invente un problème (deux prétextes d'**exemple à remplacer**).
- Sommes demandées : papier expiré 5 000, surcharge 3 000, prétexte 1 000 à 3 000 (arrondies à 500).
- Négocier : -30 %, mais 3 fois sur 10 le policier se braque (+20 %) ; 5 min par tentative ; 2 tentatives ; plancher 500.
- Contester : perd toujours (paie 1,5 × la somme, 1 h perdue) ; « gagne » et « fourrière » existent avec un poids 0.
- Appeler quelqu'un : personne ne répond (paie la somme, 20 min) ; « arrange » existe avec un poids 0.
- Fourrière : 15 000 FCFA ; réputation : 0 partout.
- Papiers : permis et carte grise n'expirent pas ; assurance 30 jours (12 restants au départ), visite technique 60 (25), licence 90 (40) ; renouvellement à 0 FCFA en attendant Kevin.

## Ton : réaliste (décision de Kevin, 2026-11-21)

**On suggère la corruption sans la montrer crûment.** Appliqué :
- `Core/Police/PoliceTexts.cs` : textes à l'écran du contrôle. Le policier « parle de 3 000 FCFA » sans nommer la somme (ni « amende » ni « pot-de-vin ») ; la phrase est **la même pour un problème réel et pour un prétexte inventé** : au joueur de comprendre ; à la fin, « 2 000 FCFA laissés au contrôle. Bonne route. », sans reçu ni explication ; en règle, « Le policier regarde les papiers, longtemps. ». Formulations provisoires de Claude, à relire par Kevin.
- Au bilan, la dépense reste « Contrôle de police » (réel ou inventé, même ligne).
- **Mots crus interdits** (`PoliceTexts.CrudeWords` : corruption, pot-de-vin, bakchich, racket, dessous-de-table…) dans ces textes et dans `police.json` (prétextes, postes, papiers) : vérifié par `PoliceTests.Texts_RealisticTone_SuggestWithoutShowing` et `DataInvariantsTests.Police_Texts_RealisticTone_NoCrudeWords`.
- Prétextes : doivent rester **plausibles** (ce qu'un vrai policier pourrait reprocher : extincteur, triangle…), pas absurdes ; les deux de `police.json` restent des **exemples à remplacer**.
- Le contrôle reste **coupé** (`enabled: false`) : il dépend encore des questions 2, 3, 5, 6, 7 et 9 (papiers et coûts, contester, appeler, sommes et fréquence, fourrière, positions des postes) ; 4 (fuir) et 8 (réputation) ne bloquent pas l'activation mais restent ouvertes.

## Questions pour Kevin (jalon 4)

Question 1 tranchée (ton réaliste) ; **les 8 autres restent ouvertes**.


1. ~~Ton : satirique, dramatique, ou les deux ?~~ → **Tranché par Kevin (2026-11-21) : ton réaliste. On suggère la corruption sans la montrer crûment.** Voir « Ton » ci-dessus.
2. **Papiers et coûts de renouvellement** (question 2) : la liste codée est celle de la feuille de route. Propositions de durées et de coûts : **A.** proches du réel, en jours de jeu (assurance 30 j, visite technique 180 j, licence 365 j) ; **B.** raccourcis pour qu'on les voie expirer (assurance 7 j, visite 14 j, licence 30 j) ; **C.** pas d'expiration avant le jalon 9 (taxi à soi), seulement des prétextes. Et : qui paie tant que le taxi est au propriétaire (le chauffeur, le propriétaire, ou partage) ?
3. **Contester peut-il gagner** (question 3) ? Propositions : **A.** jamais (perd, paie plus) ; **B.** parfois, seulement si le problème est inventé et les papiers en règle (par ex. 1 chance sur 3), sinon fourrière possible ; **C.** selon la réputation (quand Kevin aura décidé de ses effets). Réglable : poids de `gagne` / `fourriere` dans `contestOutcomes`.
4. **Fuir un contrôle** (question 4) ? Non codé. Propositions : **A.** impossible (le policier se met devant) ; **B.** possible, avec poursuite et risque de fourrière ou d'arrestation au prochain poste ; **C.** possible seulement sur un contrôle volant.
5. **Appeler quelqu'un** : qui (un « grand frère », le propriétaire, un ami policier) et ce que ça change ? Propositions : **A.** coûte du temps, marche rarement ; **B.** marche si on a un contact (événement de vie, jalon 7) ; **C.** le propriétaire paie et le retient sur l'épargne.
6. **Sommes demandées** et **fréquence** : valeurs ci-dessus à confirmer (combien de contrôles par jour, combien demandé) ; à mettre en face de l'équilibrage du jalon 3 (journée de 35 min : 6 600 à 10 500 FCFA de recette pour ≈ 11 400 de carburant ; le versement n'est déjà pas atteint).
7. **Fourrière** : la journée s'arrête et coûte 15 000 FCFA (provisoire) ; le propriétaire renvoie-t-il le chauffeur, comme pour une saisie ? (non par défaut).
8. **Réputation** : payer, refuser ou contester changent-ils la réputation ? Dans quel sens (0 par défaut) ?
9. **Positions des postes fixes** sur la portion (comme les feux et la station).
