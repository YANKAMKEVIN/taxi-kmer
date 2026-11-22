# Jalon 4 — Le contrôle (plan technique)

> Ouvert le 2026-11-21 en session cloud, **logique pure seulement**, pendant que les jalons 1 à 3 attendent l'essai sur téléphone. Contenu et critères : `docs/design/roadmap.md` ; vision §4.4. **Activé le 21/11** (`enabled: true` dans `police.json`) après la 2ᵉ série de décisions de Kevin : tout ce qui reste ouvert est en valeurs par défaut (sommes, fréquence, positions des postes). Affichage minimal écrit (`Driving/PoliceView.cs`), **non exécuté dans Unity**.

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
| 4.5 Affichage dans Unity | **écrit, non exécuté** (21/11) : `Driving/PoliceView.cs`, créé par `StreetView` si `enabled` ; `PoliceDay` cherche un contrôle sur le trajet du taxi (chance multipliée par l'effet de la réputation), panneau au centre (nom du poste, phrase du policier, un bouton par choix, touches 1 à 5), conduite bloquée pendant le contrôle, effet appliqué à la journée et phrase de fin en message. Postes dans le monde **écrits, non exécutés** (22/11, `Core/Police/PoliceMarkers.cs`, `StreetView.MakePolicePost`) : poste fixe = abri bleu, barrière rouge et blanche, plots ; contrôle volant = pick-up de police et plots, visible seulement pendant qu'il est en place ; policier au bord de la chaussée qui s'avance quand il arrête le taxi ; texte « Contrôle » / « Contrôle volant » au-dessus à 250 m ou moins (primitives provisoires, valeurs par défaut de Claude) |
| 4.6 Papiers dans la sauvegarde et renouvellement | **faite** (décision de Kevin du 21/11 au soir) : renouvellement automatique à l'échéance, payé sur l'épargne (`Core/Police/PaperRenewal.cs`, `autoRenewPapers`) ; dates des papiers dans la sauvegarde (version 6, `CareerData.papers`, migration testée) ; message au début de la journée (`PoliceTexts.Renewal`, `StreetView`, non exécuté dans Unity) ; montants bas fixés le 22/11 (assurance 500, visite technique 1 000, licence 2 000 FCFA, ci-dessous) |

| 4.7 Décisions de Kevin du 21/11 | **faite** : papiers option A, contester option B, fuir option B (`ControlChoice.Flee`, recherché, `PoliceDay`), appeler option B avec le crochet des contacts (`PoliceContacts`, coupé), fourrière renvoyée à la 2ᵉ fois (`OwnerConfig.impoundsBeforeFired`, `DriverCareer.Impounds`, sauvegarde version 5), réputation inchangée ; voir « Décisions de Kevin du 21/11 » |

Tests : `Assets/Tests/EditMode/PoliceTests.cs` (13 tests, dont le ton des textes ; dont la journée de test à 10 contrôles variés) ; données vérifiées par `DataInvariantsTests.Police_MatchesDefaults_StaysOffUntilKevinDecides`.

## Décisions de Kevin du 21/11 (tranché par Kevin le 21/11)

- **Papiers, option A** : assurance **30 jours**, visite technique **180 jours**, licence **365 jours** de jeu (`papers[].validityDays`). Jours restants au départ inchangés (12, 25, 40) pour qu'on voie une expiration tôt dans la carrière ; renouvellement automatique fait le 21/11 au soir (tâche 4.6) ; montants de renouvellement bas fixés le 22/11 (500 / 1 000 / 2 000 FCFA, ci-dessous).
- **Contester, option B** : on gagne parfois, **environ 1 fois sur 3**, seulement si le problème est inventé et que les papiers sont en règle (issue « gagne », `requiresInvented`) ; sinon on perd (paie 1,5 fois, 1 h perdue) ou c'est la **fourrière**. Poids par défaut : gagne 1, perd 1,5, fourrière 0,5 → problème inventé : gagne 1/3, perd 1/2, fourrière 1/6 ; problème réel : perd 3/4, fourrière 1/4. Vérifié sur 3 000 contrôles (`PoliceTests.Contest_WinsAboutOneInThree_OnlyWhenInventedAndPapersInOrder`).
- **Fuir, option B** : nouveau choix « Fuir ». Poursuite : **rattrapé 4 fois sur 10 → fourrière** ; échappé 6 fois sur 10 → **recherché** jusqu'à la fin de la journée : le prochain contrôle en place l'arrête à coup sûr (`wantedStopChance`) et c'est la fourrière (4/10), l'**arrestation** (2/10 : garde à vue, la journée s'arrête) ou un contrôle ordinaire où le policier parle de 10 000 FCFA (4/10). Valeurs par défaut.
- **Appeler quelqu'un, option B** : ça marche si on a un contact (« arrange » : rien à payer, 20 min ; `requiresContact`) ; sans contact, personne ne répond (paie la somme, 20 min). Les contacts viendront des événements de vie du jalon 7 : **crochet prêt** (`PoliceContacts.Add`, `PoliceControl(…, hasContact)`), **coupé** (`contactsEnabled: false`) tant que le jalon 7 n'existe pas.
- **Fourrière** : le propriétaire renvoie le chauffeur **seulement à la 2ᵉ fois** (`owner.json` → `impoundsBeforeFired: 2`) ; la 1ʳᵉ fois il menace. Compte gardé dans la sauvegarde (version 5, migration testée).
- **Réputation** : payer, refuser ou contester **ne la changent pas** (`reputationPaid` et `reputation` des issues à 0, vérifié par `PoliceTests.Reputation_PayRefuseContest_DoNotChangeIt` et `DataInvariantsTests`). Fuir ne la change pas non plus (valeur par défaut). En revanche, la réputation change la **chance d'être arrêté** (effet décidé au jalon 3 : ×1,3 à ×0,7).
- **Activé** : `enabled: true`.

## Décisions de Kevin du 21/11 au soir (après l'étude d'équilibrage)

- **Équilibrage** : carburant ×1, prix des courses ×2 (600 à 1 600 FCFA), versement de 6 000 FCFA (`jalon-03.md`,
  « Équilibrage appliqué »). Rien ne change dans `police.json` ; les contrôles restent le premier poste de frais imprévus
  (≈ 3 600 FCFA par journée pour le joueur moyen, `equilibrage.md`), mais la recette doublée les absorbe : versement
  atteint dans 65 % des journées sans trafic, 54 % avec.
- **Papiers : renouvellement automatique à l'échéance, payé sur l'épargne** (`police.json` → `autoRenewPapers: true`).
  Avant, ils ne se renouvelaient jamais : l'assurance (`startDaysLeft` 12) était expirée à partir du 13ᵉ jour, pour
  toujours. Désormais, au début de chaque journée (`SaveService.StartDayPapers` → `SaveSnapshot.RenewPapersAtDayStart` →
  `PaperRenewal.AtDayStart`), chaque papier expiré ce jour-là est renouvelé pour sa durée (30 / 180 / 365 jours, sans trou :
  la nouvelle période part de l'échéance) et son coût (`renewalCost`) est retiré de l'épargne (`DriverCareer.PayFromSavings`).
  Les dates sont sauvegardées (version 6) : relancer la même journée ne fait pas payer deux fois.
  - **Montants (décision de Kevin du 22/11 : « montants bas, pour rester vers 60 à 70 % »)** : **assurance 500, visite
    technique 1 000, licence 2 000 FCFA** (`renewalCost` dans `police.json` et valeurs par défaut du code ; permis et carte
    grise n'expirent pas). Choisis par l'étude d'équilibrage (`equilibrage.md`, `Papers_Report`, épargne réelle du matin,
    six jeux de montants, 14 et 45 jours) : versement de 6 000 atteint dans **70 % des journées sans concurrence sur
    14 jours, 69 % sur 45 jours** (cible 60-70 % tenue) ; **avec la concurrence, 60 % sur 14 jours et 53 % sur 45 jours**,
    le plus haut des jeux non nuls (51 % pour tous les autres, 67 % avec des papiers gratuits). Le montant compte peu (de
    500 à 5 000 FCFA pour l'assurance, 1 à 2 points) : la baisse vient des chauffeurs à l'épargne nulle ou négative, qui ne
    peuvent rien payer et roulent ensuite avec un papier expiré (5 000 FCFA demandés à chaque contrôle) ; avec la
    concurrence, 36 % des journées sur 45 jours (question 10, tranchée le 22/11 : option A, on garde tel quel).
  - **Épargne insuffisante (validé par Kevin)** : le papier **reste expiré**, rien n'est
    pris, et un message l'annonce au début de la journée (« Assurance expirée : pas assez d'épargne pour la renouveler
    (500 FCFA). Gare aux contrôles. ») ; nouvel essai chaque matin. Un papier gratuit se renouvelle même avec une
    épargne négative. Si plusieurs papiers arrivent à échéance le même jour, l'épargne les paie dans l'ordre des données.
  - Message quand tout va bien : « Assurance renouvelée (500 FCFA pris sur l'épargne). » (ou « Assurance renouvelée. »
    s'il est gratuit), affiché 6 s après les annonces du jour.
  - Effet sur l'équilibrage (`equilibrage.md`) : plus aucun contrôle ne trouve de papier expiré ; ce que le joueur moyen
    laisse aux contrôles passe de ≈ 3 640 à ≈ 2 980 FCFA par journée (sur 14 jours) ; versement atteint dans **70 %** des
    journées sans trafic (65 % avant), **61 %** avec (54 %), avec des papiers gratuits ; montants du 22/11 : voir plus haut.
  - Tests : `PoliceTests.AutoRenewal_*` (4), `SaveTests` (version 6, aller-retour, renouvellement coupé),
    `BalanceStudyTests.Papers_RenewedAtDeadline_NeverExpiredAtControls`, `Renewal_NotAffordable_*`,
    `RunCareers_PaidRenewals_UseRealSavings`, `DataInvariantsTests` (montants bas, entre 1 et 5 000 FCFA).

## Valeurs par défaut encore ouvertes (à confirmer par Kevin)

- **Sommes demandées** : papier expiré 5 000, surcharge 3 000, prétexte 1 000 à 3 000, recherché 10 000 ; fourrière 15 000 FCFA.
- **Fréquence des contrôles** : postes fixes arrêtent 1 fois sur 2 (pas deux fois en 2 h), 2 contrôles volants par jour, en place 1 h, arrêt 7 fois sur 10.
- **Positions des postes fixes** : Messassi (900 m) et Étoudi (5 200 m), provisoires.
- Poids des issues (contester, fuir, recherché). (Montants de renouvellement des papiers et épargne insuffisante : tranchés par Kevin, 21-22/11.)

## Valeurs par défaut (`police.json`, avant le 21/11)

- ~~**Coupé** (`enabled: false`)~~ → activé le 21/11.
- Deux postes fixes **provisoires** : Messassi (900 m) et Étoudi (5 200 m), arrêt une fois sur deux ; 2 contrôles volants par jour, en place 1 h, arrêt 7 fois sur 10.
- Contrôle : 10 min de jeu ; règle de charge : 4 passagers au plus, 1 devant (le taxi en prend 5, dont 2 devant : la surcharge est presque toujours un prétexte possible, comme dans la feuille de route).
- En règle : 1 chance sur 2 que le policier invente un problème (deux prétextes d'**exemple à remplacer**).
- Sommes demandées : papier expiré 5 000, surcharge 3 000, prétexte 1 000 à 3 000 (arrondies à 500).
- Négocier : -30 %, mais 3 fois sur 10 le policier se braque (+20 %) ; 5 min par tentative ; 2 tentatives ; plancher 500.
- ~~Contester : perd toujours~~ → option B (ci-dessus).
- ~~Appeler quelqu'un : personne ne répond~~ → option B, avec un contact (ci-dessus).
- Fourrière : 15 000 FCFA ; réputation : 0 partout.
- Papiers : permis et carte grise n'expirent pas ; ~~visite technique 60, licence 90~~ → option A (ci-dessus) ; renouvellement 500 / 1 000 / 2 000 FCFA (Kevin, 22/11).

## Ton : réaliste (décision de Kevin, 2026-11-21)

**On suggère la corruption sans la montrer crûment.** Appliqué :
- `Core/Police/PoliceTexts.cs` : textes à l'écran du contrôle. Le policier « parle de 3 000 FCFA » sans nommer la somme (ni « amende » ni « pot-de-vin ») ; la phrase est **la même pour un problème réel et pour un prétexte inventé** : au joueur de comprendre ; à la fin, « 2 000 FCFA laissés au contrôle. Bonne route. », sans reçu ni explication ; en règle, « Le policier regarde les papiers, longtemps. ». Formulations provisoires de Claude, à relire par Kevin.
- Au bilan, la dépense reste « Contrôle de police » (réel ou inventé, même ligne).
- **Mots crus interdits** (`PoliceTexts.CrudeWords` : corruption, pot-de-vin, bakchich, racket, dessous-de-table…) dans ces textes et dans `police.json` (prétextes, postes, papiers) : vérifié par `PoliceTests.Texts_RealisticTone_SuggestWithoutShowing` et `DataInvariantsTests.Police_Texts_RealisticTone_NoCrudeWords`.
- Prétextes : doivent rester **plausibles** (ce qu'un vrai policier pourrait reprocher : extincteur, triangle…), pas absurdes ; les deux de `police.json` restent des **exemples à remplacer**.
- Le contrôle est **activé** depuis le 21/11 (questions 2, 3, 4, 5, 7 et 8 tranchées ; 6 et 9 en valeurs par défaut).

## Questions pour Kevin (jalon 4)

Questions 1 à 5, 7, 8 et 10 tranchées ; **6 (sommes, fréquence) et 9 (positions des postes) restent ouvertes**, en valeurs par défaut.


1. ~~Ton : satirique, dramatique, ou les deux ?~~ → **Tranché par Kevin (2026-11-21) : ton réaliste. On suggère la corruption sans la montrer crûment.** Voir « Ton » ci-dessus.
2. ~~Papiers~~ → **option A, tranché par Kevin le 21/11** (30 / 180 / 365 jours de jeu) ; **renouvellement automatique à l'échéance, payé sur l'épargne** (tranché par Kevin le 21/11 au soir, fait). **Montants tranchés le 22/11** : bas (500 / 1 000 / 2 000 FCFA, étude d'équilibrage) ; épargne insuffisante : le papier reste expiré, un message l'annonce, nouvel essai chaque matin (validé). Ancienne question : **Papiers et coûts de renouvellement** (question 2) : la liste codée est celle de la feuille de route. Propositions de durées et de coûts : **A.** proches du réel, en jours de jeu (assurance 30 j, visite technique 180 j, licence 365 j) ; **B.** raccourcis pour qu'on les voie expirer (assurance 7 j, visite 14 j, licence 30 j) ; **C.** pas d'expiration avant le jalon 9 (taxi à soi), seulement des prétextes. Et : qui paie tant que le taxi est au propriétaire (le chauffeur, le propriétaire, ou partage) ?
3. ~~Contester~~ → **option B, tranché par Kevin le 21/11** (1 chance sur 3 si inventé et papiers en règle, sinon fourrière possible). Ancienne question : **Contester peut-il gagner** (question 3) ? Propositions : **A.** jamais (perd, paie plus) ; **B.** parfois, seulement si le problème est inventé et les papiers en règle (par ex. 1 chance sur 3), sinon fourrière possible ; **C.** selon la réputation (quand Kevin aura décidé de ses effets). Réglable : poids de `gagne` / `fourriere` dans `contestOutcomes`.
4. ~~Fuir~~ → **option B, tranché par Kevin le 21/11** (poursuite, fourrière ou arrestation au prochain poste). Ancienne question : **Fuir un contrôle** (question 4) ? Non codé. Propositions : **A.** impossible (le policier se met devant) ; **B.** possible, avec poursuite et risque de fourrière ou d'arrestation au prochain poste ; **C.** possible seulement sur un contrôle volant.
5. ~~Appeler quelqu'un~~ → **option B, tranché par Kevin le 21/11** (marche avec un contact ; crochet du jalon 7, coupé). Ancienne question : **Appeler quelqu'un** : qui (un « grand frère », le propriétaire, un ami policier) et ce que ça change ? Propositions : **A.** coûte du temps, marche rarement ; **B.** marche si on a un contact (événement de vie, jalon 7) ; **C.** le propriétaire paie et le retient sur l'épargne.
6. **Sommes demandées** et **fréquence** : valeurs ci-dessus à confirmer (combien de contrôles par jour, combien demandé) ; à mettre en face de l'équilibrage (depuis le 21/11 au soir : carburant ×1, prix ×2, versement 6 000 ; les contrôles coûtent ≈ 3 600 FCFA par journée au joueur moyen, `equilibrage.md`).
7. ~~Fourrière~~ → **tranché par Kevin le 21/11 : renvoyé seulement à la 2ᵉ fois.** Ancienne question : **Fourrière** : la journée s'arrête et coûte 15 000 FCFA (provisoire) ; le propriétaire renvoie-t-il le chauffeur, comme pour une saisie ? (non par défaut).
8. ~~Réputation~~ → **tranché par Kevin le 21/11 : payer, refuser ou contester ne la changent pas.** Ancienne question : **Réputation** : payer, refuser ou contester changent-ils la réputation ? Dans quel sens (0 par défaut) ?
9. **Positions des postes fixes** sur la portion (comme les feux et la station).
10. ~~Papier impayé faute d'épargne, avec la concurrence~~ → **tranché par Kevin le 22/11 : option A, on garde tel quel** (chauffeur sans épargne = papier expiré, somme demandée au contrôle ; rien à coder, la spirale fait partie du jeu). Ancienne question (relevé par l'étude du 22/11, `equilibrage.md`) : avec les
    montants bas, le versement reste dans la cible sans concurrence (69-70 %), mais avec la concurrence il tombe à 53 % sur
    45 jours (67 % avec des papiers gratuits), quel que soit le montant : le chauffeur à l'épargne nulle ou négative ne peut
    rien payer et roule avec un papier expiré (5 000 FCFA à chaque contrôle). La règle validée reste en place. Pistes :
    **A.** garder tel quel (la spirale fait partie du jeu) ; **B.** le propriétaire avance le renouvellement et le retient
    sur l'épargne (le papier n'expire jamais) ; **C.** papier expiré moins cher au contrôle (`demandExpiredPaper` 5 000 →
    2 000 par exemple).
