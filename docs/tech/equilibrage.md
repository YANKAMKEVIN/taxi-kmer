# Équilibrage : étude par simulation (2026-11-21)

Demande de Kevin : « Teste plusieurs réglages et propose-moi le meilleur ». **Aucune valeur de jeu n'a été changée** :
les réglages sont appliqués à des copies des données, en mémoire, par l'outil de simulation. C'est Kevin qui choisit.

Point de départ (données de `main`) : journée de 35 min, versement de 7 000 FCFA, carburant ×8 (`consumptionScale`),
prix de 300 à 800 FCFA ; effets de la réputation, contrôles (jalon 4) et incidents de la route (jalon 6) actifs.

## En bref

| | Réglage | Versement atteint (sans trafic / avec trafic) | Renvoyé en 14 jours (sans / avec) | Épargne après 7 jours (moyenne, sans / avec) |
|---|---|---|---|---|
| Aujourd'hui | carburant ×8, prix ×1, versement 7 000 | 3 % / 2 % | 100 % (le 4ᵉ jour) / 100 % | -35 936 / -38 097 |
| | carburant ×1, prix ×1, versement 7 000 | 15 % / – | 100 % (le 4ᵉ jour) | -14 405 |
| | carburant ×1, prix ×1, versement 5 000 | 30 % / – | 96 % (le 6ᵉ jour) | -13 822 |
| **Recommandé** | **carburant ×1, prix ×2, versement 6 000** | **65 % / 54 %** | **10 % / 40 %** | **18 440 / 8 030** |
| Plus facile | carburant ×1, prix ×2, versement 5 000 | 68 % / 57 % | 6 % / 20 % | 23 780 / 13 487 |
| Plus dur | carburant ×1, prix ×2, versement 7 000 (celui de Kevin) | 62 % / 49 % | 16 % / 53 % | 13 693 / 4 130 |

« Sans trafic » : 200 carrières de 14 jours par réglage (2 800 journées) ; « avec trafic » : les taxis concurrents et
les mototaxis du trafic simulé prennent aussi des clients (15 carrières de 14 jours, 210 journées par réglage, calcul
50 fois plus lent). Le jeu réel est plus proche de « avec trafic » (≈ 18 % de recette en moins).

**Recommandation : carburant ×1, prix ×2, versement 6 000 FCFA.** Le versement est atteint dans 54 à 65 % des journées
(entre les deux simulations, autour de 60 %), le renvoi arrive dans 10 à 40 % des carrières en deux semaines (possible,
pas systématique), et le chauffeur qui tient garde de l'épargne (8 000 à 18 000 FCFA après une semaine). Pourquoi :

1. **Carburant ×1** : avec la journée de 35 min, la consommation réelle (8 L/100 km, 840 FCFA/L, soit ≈ 67 FCFA/km)
   est déjà à l'échelle des kilomètres parcourus (≈ 18 km par journée, ≈ 1 400 FCFA). Le ×8 avait été choisi pour une
   journée de 10 min ; aujourd'hui il fait payer ≈ 540 FCFA par km pour ≈ 440 de recette. Entre ×1 et ×2, l'écart est
   faible (2 à 7 points) ; au-delà de ×3, plus aucun réglage n'atteint 60 %.
2. **Prix ×2** : c'est le seul levier de la grille qui permet d'atteindre 60 %. **Avec les prix actuels (×1), aucun
   réglage ne dépasse 30 %**, même carburant ×1 et versement 5 000. Les prix affichés passent de 300-800 à
   **600-1 600 FCFA** : ce ne sont plus les prix d'une course partagée à Yaoundé (voir la piste hors grille ci-dessous).
3. **Versement 6 000** : il place la difficulté au milieu de la cible (60-75 %) ; 5 000 est confortable, 7 000 garde la
   décision de Kevin mais fait renvoyer la moitié des joueurs en deux semaines avec la concurrence.

## Ce qui bloque vraiment : les frais imprévus (≈ 7 000 FCFA par journée)

Avec tous les systèmes actifs, un joueur moyen paie en moyenne **≈ 7 000 FCFA de frais imprévus par journée**, autant
que sa recette aux prix actuels (≈ 7 900 FCFA sans trafic, ≈ 6 700 avec) :

| Joueur | Journées | Courses | Recette | km | Contrôles | Payé à la police | Crevaisons | Pannes | Accrochages | Négociations | Refus de payer | Clients pris par d'autres | Frais imprévus |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| sans trafic | 560 | 20,6 | 15 352 | 17,7 | 2,04 | 3 640 | 0,13 | 0,04 | 0,3 | 1,94 | 0,59 | 0 | 7 048 |
| avec trafic | 56 | 16,6 | 12 591 | 18 | 2,3 | 3 330 | 0,14 | 0,02 | 0,38 | 1,54 | 0,46 | 40,7 | 6 714 |

Frais imprévus par journée (sans trafic) : Contrôle de police 3 640 ; Dégâts de l'autre véhicule 2 151 ; Garage 1 182 ; Mécanicien de quartier 75.
Frais imprévus par journée (avec trafic) : Contrôle de police 3 330 ; Dégâts de l'autre véhicule 2 616 ; Garage 732 ; Mécanicien de quartier 36.

- **Contrôles de police : ≈ 3 300 à 3 600 FCFA par jour** (≈ 2 contrôles par jour : 2 postes fixes à 50 % de chance
  d'arrêt, 2 contrôles volants à 70 %). Le joueur moyen remplit son taxi (5 places, 2 devant si les carrures le
  permettent) : au-delà de 4 passagers ou de 1 devant (`legalMaxPassengers`, `legalMaxFront`), le problème est réel et
  coûte 3 000 ; sinon, un problème est inventé une fois sur deux (1 000 à 3 000).
- **Accrochages : ≈ 2 200 à 2 600 FCFA par jour** pour 1 accrochage tous les 60 km (hypothèse, voir la méthode) : fautif,
  on paie 1 000 FCFA par km/h de choc (`otherDamagePerKmh`), soit 8 000 à 20 000.
- **Garage : ≈ 700 à 1 200 FCFA par jour** : les nids-de-poule abîment le taxi, et la panne fait tout réparer (500 par point).

Piste **hors grille**, pour garder les vrais prix (calcul pour comprendre, rien n'est changé) : carburant ×1 et prix ×1,
mais sans accrochage ni contrôle, le versement de 7 000 serait atteint dans **78 %** des journées, 6 000 dans 83 % :

| Cas | Conso | Prix | Versement | Versement atteint | Reste moyen / jour | Recette | Carburant acheté | Frais imprévus | Renvoyé en 7 j | Renvoyé en 14 j | Jour du renvoi (médiane) | Épargne à 7 j (moy. / méd.) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| joueur moyen | ×1 | ×1 | 5 000 | 29 % | -5 762 | 7 916 | 1 429 | 7 249 | 82 % | 98 % | 6 | -13 518 / -11 750 |
| joueur moyen | ×1 | ×1 | 6 000 | 21 % | -6 762 | 7 916 | 1 429 | 7 249 | 100 % | 100 % | 4 | -14 072 / -11 100 |
| joueur moyen | ×1 | ×1 | 7 000 | 16 % | -7 762 | 7 916 | 1 429 | 7 249 | 100 % | 100 % | 4 | -13 961 / -11 050 |
| sans accrochage | ×1 | ×1 | 5 000 | 32 % | -3 218 | 7 511 | 1 429 | 4 301 | 80 % | 98 % | 6 | -4 121 / -3 250 |
| sans accrochage | ×1 | ×1 | 6 000 | 21 % | -4 218 | 7 511 | 1 429 | 4 301 | 98 % | 100 % | 5 | -5 491 / -5 250 |
| sans accrochage | ×1 | ×1 | 7 000 | 15 % | -5 218 | 7 511 | 1 429 | 4 301 | 100 % | 100 % | 4 | -5 832 / -5 250 |
| sans accrochage ni contrôle | ×1 | ×1 | 5 000 | 84 % | 1 352 | 8 290 | 1 429 | 510 | 0 % | 0 % | > 14 | 14 585 / 15 950 |
| sans accrochage ni contrôle | ×1 | ×1 | 6 000 | 83 % | 352 | 8 290 | 1 429 | 510 | 0 % | 0 % | > 14 | 9 490 / 10 550 |
| sans accrochage ni contrôle | ×1 | ×1 | 7 000 | 78 % | -648 | 8 290 | 1 429 | 510 | 0 % | 0 % | > 14 | 4 154 / 5 200 |

Autrement dit, **avec les prix de Yaoundé, le versement de 7 000 et le carburant ×1 sont tenables si les contrôles de
police coûtent nettement moins** (moins fréquents, sommes plus basses, ou un joueur qui ne surcharge pas son taxi). Ces
valeurs sont toutes des « valeurs par défaut » encore ouvertes (`police.json`, `road.json`) : c'est probablement le
meilleur levier, mais il demande une nouvelle décision de Kevin et une nouvelle étude (l'outil le permet).

## Sensibilité

Avec le trafic et la concurrence (15 carrières de 14 jours = 210 journées par réglage) :

| Conso | Prix | Versement | Versement atteint | Reste moyen / jour | Recette | Carburant acheté | Frais imprévus | Renvoyé en 7 j | Renvoyé en 14 j | Jour du renvoi (médiane) | Épargne à 7 j (moy. / méd.) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| ×1 | ×2 | 5 000 | 57 % | -1 143 | 12 760 | 1 429 | 7 474 | 7 % | 20 % | > 14 | 13 487 / 17 400 |
| ×1 | ×2 | 6 000 | 54 % | -2 143 | 12 760 | 1 429 | 7 474 | 27 % | 40 % | > 14 | 8 030 / 11 550 |
| ×1 | ×2 | 7 000 | 49 % | -3 143 | 12 760 | 1 429 | 7 474 | 33 % | 53 % | 14 | 4 130 / 6 050 |
| ×1,5 | ×2 | 5 000 | 56 % | -1 877 | 12 742 | 2 143 | 7 476 | 13 % | 13 % | > 14 | 5 367 / 7 200 |
| ×1,5 | ×2 | 6 000 | 53 % | -2 877 | 12 742 | 2 143 | 7 476 | 27 % | 60 % | 14 | 697 / 1 500 |
| ×1,5 | ×2 | 7 000 | 48 % | -3 877 | 12 742 | 2 143 | 7 476 | 27 % | 80 % | 13 | -2 213 / 0 |
| ×1 | ×1,5 | 5 000 | 46 % | -4 156 | 9 746 | 1 429 | 7 474 | 40 % | 73 % | 11 | -3 433 / 0 |
| ×1 | ×1,5 | 6 000 | 40 % | -5 156 | 9 746 | 1 429 | 7 474 | 53 % | 87 % | 7 | -6 487 / -3 200 |
| ×1 | ×1,5 | 7 000 | 32 % | -6 156 | 9 746 | 1 429 | 7 474 | 73 % | 93 % | 6 | -9 937 / -8 150 |
| ×8 | ×1 | 5 000 | 5 % | -15 568 | 6 694 | 9 733 | 7 529 | 100 % | 100 % | 4 | -38 437 / -35 600 |
| ×8 | ×1 | 6 000 | 4 % | -16 568 | 6 694 | 9 733 | 7 529 | 100 % | 100 % | 4 | -38 197 / -32 400 |
| ×8 | ×1 | 7 000 | 2 % | -17 568 | 6 694 | 9 733 | 7 529 | 100 % | 100 % | 4 | -38 097 / -32 400 |

Sensibilité au joueur, sans trafic (40 carrières de 14 jours = 560 journées par ligne) :

| Réglage | Joueur | Versement atteint (7 000 / 6 000 / 5 000) | Reste moyen (7 000) | Renvoyé en 14 j (7 000) | Épargne à 7 j (7 000) |
|---|---|---|---|---|---|
| ×1, prix ×2 | joueur moyen (35 km/h, 1 accrochage / 60 km) | 61 % / 65 % / 68 % | -349 | 18 % | 14 419 |
| ×1, prix ×2 | prudent : 30 km/h | 63 % / 66 % / 69 % | -195 | 20 % | 13 081 |
| ×1, prix ×2 | pressé : 45 km/h | 66 % / 68 % / 70 % | 976 | 15 % | 24 322 |
| ×1, prix ×2 | aucun accrochage | 73 % / 77 % / 81 % | 2 289 | 0 % | 23 922 |
| ×1, prix ×2 | 2 fois plus d'accrochages | 56 % / 58 % / 62 % | -2 138 | 32 % | 7 808 |
| ×1, prix ×2 | refuse les négociations | 62 % / 65 % / 68 % | -70 | 12 % | 16 140 |
| ×1,5, prix ×2 | joueur moyen (35 km/h, 1 accrochage / 60 km) | 59 % / 62 % / 66 % | -1 053 | 22 % | 5 805 |
| ×1,5, prix ×2 | prudent : 30 km/h | 63 % / 66 % / 69 % | -379 | 20 % | 13 081 |
| ×1,5, prix ×2 | pressé : 45 km/h | 62 % / 65 % / 68 % | 259 | 15 % | 15 361 |
| ×1,5, prix ×2 | aucun accrochage | 70 % / 74 % / 77 % | 1 570 | 0 % | 13 899 |
| ×1,5, prix ×2 | 2 fois plus d'accrochages | 54 % / 56 % / 60 % | -2 840 | 40 % | 288 |
| ×1,5, prix ×2 | refuse les négociations | 59 % / 63 % / 66 % | -774 | 18 % | 7 298 |

À retenir : le réglage recommandé tient pour un joueur prudent (30 km/h) ou pressé (45 km/h) ; les accrochages pèsent
lourd (de 73 % sans accrochage à 56 % avec deux fois plus) ; la concurrence du trafic retire 10 à 13 points.

## Grille complète (sans trafic)

200 carrières de 14 jours par réglage (2 800 journées, graines fixes), joueur moyen par défaut. Le versement ne change
rien à la journée elle-même : les mêmes journées sont closes par le propriétaire avec 5 000, 6 000 et 7 000.

| Conso | Prix | Versement | Versement atteint | Reste moyen / jour | Recette | Carburant acheté | Frais imprévus | Renvoyé en 7 j | Renvoyé en 14 j | Jour du renvoi (médiane) | Épargne à 7 j (moy. / méd.) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| ×1 | ×1 | 5 000 | 30 % | -5 427 | 7 931 | 1 429 | 6 930 | 80 % | 96 % | 6 | -13 822 / -12 350 |
| ×1 | ×1 | 6 000 | 22 % | -6 427 | 7 931 | 1 429 | 6 930 | 96 % | 100 % | 5 | -14 480 / -12 900 |
| ×1 | ×1 | 7 000 | 15 % | -7 427 | 7 931 | 1 429 | 6 930 | 98 % | 100 % | 4 | -14 405 / -12 050 |
| ×1 | ×1,25 | 5 000 | 48 % | -3 240 | 10 119 | 1 429 | 6 930 | 34 % | 64 % | 13 | -4 309 / -2 150 |
| ×1 | ×1,25 | 6 000 | 41 % | -4 240 | 10 119 | 1 429 | 6 930 | 52 % | 84 % | 7 | -7 440 / -4 950 |
| ×1 | ×1,25 | 7 000 | 32 % | -5 240 | 10 119 | 1 429 | 6 930 | 74 % | 95 % | 6 | -9 526 / -7 450 |
| ×1 | ×1,5 | 5 000 | 56 % | -1 723 | 11 635 | 1 429 | 6 930 | 20 % | 34 % | > 14 | 3 118 / 4 800 |
| ×1 | ×1,5 | 6 000 | 51 % | -2 723 | 11 635 | 1 429 | 6 930 | 30 % | 50 % | 14 | -450 / 0 |
| ×1 | ×1,5 | 7 000 | 45 % | -3 723 | 11 635 | 1 429 | 6 930 | 42 % | 73 % | 11 | -3 602 / -800 |
| ×1 | ×2 | 5 000 | 68 % | 1 983 | 15 342 | 1 429 | 6 930 | 3 % | 6 % | > 14 | 23 780 / 25 900 |
| ×1 | ×2 | 6 000 | 65 % | 983 | 15 342 | 1 429 | 6 930 | 6 % | 10 % | > 14 | 18 440 / 19 750 |
| ×1 | ×2 | 7 000 | 62 % | -17 | 15 342 | 1 429 | 6 930 | 9 % | 16 % | > 14 | 13 693 / 14 550 |
| ×1,5 | ×1 | 5 000 | 30 % | -6 132 | 7 930 | 2 143 | 6 919 | 90 % | 98 % | 6 | -15 940 / -13 800 |
| ×1,5 | ×1 | 6 000 | 21 % | -7 132 | 7 930 | 2 143 | 6 919 | 98 % | 100 % | 5 | -15 445 / -13 350 |
| ×1,5 | ×1 | 7 000 | 15 % | -8 132 | 7 930 | 2 143 | 6 919 | 99 % | 100 % | 4 | -14 632 / -12 200 |
| ×1,5 | ×1,25 | 5 000 | 47 % | -3 945 | 10 117 | 2 143 | 6 919 | 55 % | 72 % | 7 | -9 890 / -7 000 |
| ×1,5 | ×1,25 | 6 000 | 40 % | -4 945 | 10 117 | 2 143 | 6 919 | 68 % | 88 % | 7 | -11 517 / -8 350 |
| ×1,5 | ×1,25 | 7 000 | 32 % | -5 945 | 10 117 | 2 143 | 6 919 | 86 % | 97 % | 6 | -11 696 / -9 000 |
| ×1,5 | ×1,5 | 5 000 | 55 % | -2 428 | 11 634 | 2 143 | 6 919 | 30 % | 46 % | > 14 | -3 948 / -350 |
| ×1,5 | ×1,5 | 6 000 | 50 % | -3 428 | 11 634 | 2 143 | 6 919 | 43 % | 60 % | 12 | -6 355 / -3 400 |
| ×1,5 | ×1,5 | 7 000 | 44 % | -4 428 | 11 634 | 2 143 | 6 919 | 61 % | 78 % | 7 | -8 086 / -4 650 |
| ×1,5 | ×2 | 5 000 | 66 % | 1 279 | 15 341 | 2 143 | 6 919 | 6 % | 10 % | > 14 | 14 612 / 15 900 |
| ×1,5 | ×2 | 6 000 | 63 % | 279 | 15 341 | 2 143 | 6 919 | 10 % | 15 % | > 14 | 9 600 / 9 750 |
| ×1,5 | ×2 | 7 000 | 61 % | -721 | 15 341 | 2 143 | 6 919 | 15 % | 20 % | > 14 | 5 230 / 4 550 |
| ×2 | ×1 | 5 000 | 27 % | -6 866 | 7 924 | 2 857 | 6 932 | 90 % | 98 % | 5 | -16 917 / -15 100 |
| ×2 | ×1 | 6 000 | 19 % | -7 866 | 7 924 | 2 857 | 6 932 | 97 % | 100 % | 5 | -16 526 / -14 650 |
| ×2 | ×1 | 7 000 | 13 % | -8 866 | 7 924 | 2 857 | 6 932 | 100 % | 100 % | 4 | -15 776 / -12 900 |
| ×2 | ×1,25 | 5 000 | 42 % | -4 680 | 10 110 | 2 857 | 6 932 | 58 % | 84 % | 7 | -10 291 / -8 250 |
| ×2 | ×1,25 | 6 000 | 36 % | -5 680 | 10 110 | 2 857 | 6 932 | 74 % | 93 % | 6 | -12 423 / -10 400 |
| ×2 | ×1,25 | 7 000 | 28 % | -6 680 | 10 110 | 2 857 | 6 932 | 88 % | 98 % | 6 | -12 277 / -9 600 |
| ×2 | ×1,5 | 5 000 | 48 % | -3 165 | 11 625 | 2 857 | 6 932 | 36 % | 54 % | 14 | -3 606 / -1 350 |
| ×2 | ×1,5 | 6 000 | 44 % | -4 165 | 11 625 | 2 857 | 6 932 | 48 % | 68 % | 10 | -6 390 / -4 250 |
| ×2 | ×1,5 | 7 000 | 39 % | -5 165 | 11 625 | 2 857 | 6 932 | 62 % | 87 % | 6 | -8 436 / -6 150 |
| ×2 | ×2 | 5 000 | 60 % | 539 | 15 329 | 2 857 | 6 932 | 10 % | 16 % | > 14 | 15 855 / 17 800 |
| ×2 | ×2 | 6 000 | 57 % | -461 | 15 329 | 2 857 | 6 932 | 12 % | 19 % | > 14 | 10 925 / 12 350 |
| ×2 | ×2 | 7 000 | 55 % | -1 461 | 15 329 | 2 857 | 6 932 | 18 % | 26 % | > 14 | 6 660 / 8 100 |
| ×3 | ×1 | 5 000 | 23 % | -7 580 | 7 927 | 3 582 | 6 925 | 98 % | 100 % | 5 | -21 110 / -19 350 |
| ×3 | ×1 | 6 000 | 16 % | -8 580 | 7 927 | 3 582 | 6 925 | 100 % | 100 % | 4 | -20 960 / -18 550 |
| ×3 | ×1 | 7 000 | 12 % | -9 580 | 7 927 | 3 582 | 6 925 | 100 % | 100 % | 4 | -20 390 / -17 400 |
| ×3 | ×1,25 | 5 000 | 35 % | -5 393 | 10 114 | 3 582 | 6 925 | 79 % | 96 % | 7 | -14 924 / -12 900 |
| ×3 | ×1,25 | 6 000 | 30 % | -6 393 | 10 114 | 3 582 | 6 925 | 90 % | 98 % | 6 | -16 209 / -14 250 |
| ×3 | ×1,25 | 7 000 | 24 % | -7 393 | 10 114 | 3 582 | 6 925 | 97 % | 100 % | 5 | -15 654 / -13 550 |
| ×3 | ×1,5 | 5 000 | 42 % | -3 877 | 11 630 | 3 582 | 6 925 | 56 % | 80 % | 7 | -9 496 / -6 450 |
| ×3 | ×1,5 | 6 000 | 38 % | -4 877 | 11 630 | 3 582 | 6 925 | 69 % | 92 % | 7 | -11 018 / -8 850 |
| ×3 | ×1,5 | 7 000 | 33 % | -5 877 | 11 630 | 3 582 | 6 925 | 84 % | 96 % | 6 | -12 298 / -9 750 |
| ×3 | ×2 | 5 000 | 54 % | -171 | 15 335 | 3 582 | 6 925 | 16 % | 24 % | > 14 | 7 433 / 9 650 |
| ×3 | ×2 | 6 000 | 51 % | -1 171 | 15 335 | 3 582 | 6 925 | 18 % | 30 % | > 14 | 3 237 / 4 700 |
| ×3 | ×2 | 7 000 | 48 % | -2 171 | 15 335 | 3 582 | 6 925 | 26 % | 46 % | > 14 | -109 / 0 |
| ×4 | ×1 | 5 000 | 18 % | -9 013 | 7 921 | 5 017 | 6 917 | 100 % | 100 % | 5 | -23 911 / -21 300 |
| ×4 | ×1 | 6 000 | 12 % | -10 013 | 7 921 | 5 017 | 6 917 | 100 % | 100 % | 4 | -22 914 / -20 600 |
| ×4 | ×1 | 7 000 | 9 % | -11 013 | 7 921 | 5 017 | 6 917 | 100 % | 100 % | 4 | -22 345 / -20 250 |
| ×4 | ×1,25 | 5 000 | 29 % | -6 827 | 10 107 | 5 017 | 6 917 | 98 % | 100 % | 5 | -17 933 / -14 800 |
| ×4 | ×1,25 | 6 000 | 24 % | -7 827 | 10 107 | 5 017 | 6 917 | 99 % | 100 % | 5 | -18 365 / -15 350 |
| ×4 | ×1,25 | 7 000 | 19 % | -8 827 | 10 107 | 5 017 | 6 917 | 100 % | 100 % | 5 | -17 612 / -14 550 |
| ×4 | ×1,5 | 5 000 | 34 % | -5 313 | 11 621 | 5 017 | 6 917 | 87 % | 94 % | 6 | -12 944 / -9 800 |
| ×4 | ×1,5 | 6 000 | 31 % | -6 313 | 11 621 | 5 017 | 6 917 | 92 % | 96 % | 5 | -13 733 / -10 550 |
| ×4 | ×1,5 | 7 000 | 27 % | -7 313 | 11 621 | 5 017 | 6 917 | 98 % | 100 % | 5 | -14 168 / -11 100 |
| ×4 | ×2 | 5 000 | 47 % | -1 610 | 15 324 | 5 017 | 6 917 | 28 % | 42 % | > 14 | 174 / 300 |
| ×4 | ×2 | 6 000 | 44 % | -2 610 | 15 324 | 5 017 | 6 917 | 42 % | 58 % | 13 | -2 960 / 0 |
| ×4 | ×2 | 7 000 | 40 % | -3 610 | 15 324 | 5 017 | 6 917 | 62 % | 76 % | 7 | -4 943 / -1 300 |
| ×8 | ×1 | 5 000 | 5 % | -13 675 | 7 915 | 9 674 | 6 916 | 100 % | 100 % | 4 | -36 097 / -34 150 |
| ×8 | ×1 | 6 000 | 4 % | -14 675 | 7 915 | 9 674 | 6 916 | 100 % | 100 % | 4 | -36 028 / -34 150 |
| ×8 | ×1 | 7 000 | 3 % | -15 675 | 7 915 | 9 674 | 6 916 | 100 % | 100 % | 4 | -35 936 / -33 900 |
| ×8 | ×1,25 | 5 000 | 9 % | -11 491 | 10 099 | 9 674 | 6 916 | 100 % | 100 % | 4 | -28 516 / -25 950 |
| ×8 | ×1,25 | 6 000 | 7 % | -12 491 | 10 099 | 9 674 | 6 916 | 100 % | 100 % | 4 | -28 721 / -26 350 |
| ×8 | ×1,25 | 7 000 | 6 % | -13 491 | 10 099 | 9 674 | 6 916 | 100 % | 100 % | 4 | -28 212 / -25 950 |
| ×8 | ×1,5 | 5 000 | 11 % | -9 978 | 11 612 | 9 674 | 6 916 | 100 % | 100 % | 4 | -23 614 / -21 500 |
| ×8 | ×1,5 | 6 000 | 10 % | -10 978 | 11 612 | 9 674 | 6 916 | 100 % | 100 % | 4 | -23 418 / -21 200 |
| ×8 | ×1,5 | 7 000 | 8 % | -11 978 | 11 612 | 9 674 | 6 916 | 100 % | 100 % | 4 | -23 341 / -21 500 |
| ×8 | ×2 | 5 000 | 20 % | -6 279 | 15 311 | 9 674 | 6 916 | 96 % | 99 % | 5 | -13 149 / -11 800 |
| ×8 | ×2 | 6 000 | 17 % | -7 279 | 15 311 | 9 674 | 6 916 | 98 % | 100 % | 4 | -13 441 / -11 850 |
| ×8 | ×2 | 7 000 | 14 % | -8 279 | 15 311 | 9 674 | 6 916 | 99 % | 100 % | 4 | -13 430 / -11 100 |

Colonnes : **versement atteint** = part des journées où la journée seule le couvre (recette − carburant acheté − frais
imprévus ≥ versement, comme le tampon du bilan) ; **reste moyen** = recette − carburant acheté − frais − versement,
moyenne par journée (tiré vers le bas par les journées à gros frais : il peut être négatif alors que la majorité des
journées couvre le versement) ; **renvoyé** = part des carrières renvoyées par le propriétaire (`owner.json` : 3 manques
sur 7 jours, avec la tolérance selon la réputation ; 2ᵉ fourrière) ; **jour du renvoi** = médiane (« > 14 » si moins de
la moitié est renvoyée) ; **épargne à 7 j** = épargne de la carrière après le 7ᵉ jour (ou au renvoi), le versement
manquant étant complété par l'épargne comme dans le jeu.

## Méthode

Outil : `tools/dotnet/BalanceStudy.cs` (joueur moyen, carrières, grille) et `tools/dotnet/BalanceStudyTests.cs`
(6 tests rapides lancés par la CI ; grilles et rapports à la demande) :

```bash
tools/test_core.sh "FullyQualifiedName~BalanceStudyTests"     # tests de l'outil (≈ 1 s)
tools/test_core.sh "Name=GridWithoutTraffic_Report"           # grille sans trafic (≈ 10 min sur 4 cœurs libres)
tools/test_core.sh "Name=Sensitivity_Report"                  # candidats avec trafic + sensibilité au joueur (≈ 30 min)
tools/test_core.sh "Name=WhatIf_Report|Name=DayContents_Report"   # hors grille, contenu d'une journée type
tools/test_core.sh "Name=Grid_Report"                         # grille entière avec trafic (plusieurs heures)
# rapports : tools/dotnet/bin/Debug/net8.0/equilibrage/*.md
```

**Branché comme dans le jeu** (mêmes objets de `Core`, même ordre que `StreetView`, `PoliceView` et `RoadView`) :
clients au bord de la route et places (corpulence), station-service et achat par appuis de 2 000 FCFA, panne sèche
(technicien), réputation qui passe d'un jour à l'autre et ses effets (patience, négociation, chance d'être contrôlé,
tolérance du propriétaire), négociations et refus de payer (`dialogues.json`), contrôles de police (postes fixes et
volants, papiers, surcharge), nids-de-poule, crevaisons, pannes, dégâts qui passent d'un jour à l'autre, pluie selon le
mois (clients et patience), accrochages (`TrafficCrash`), calendrier (dimanche calme, jours de marché), propriétaire
(`DriverCareer`). Avec le trafic : `CityLife` (trafic, piétons, taxis concurrents et mototaxis qui prennent des clients).

**Le joueur moyen** (hypothèses de l'étude, réglables dans `AveragePlayer`, pas des valeurs de jeu) : aller-retour
dispensaire ↔ Étoudi à **35 km/h** de croisière, accepte tout client (y compris en faisant changer un passager de
place), arrêt de 4 s pour faire monter ou descendre, 8 s pour faire demi-tour, station sous 25 % du réservoir jusqu'à
50 % ; accepte les contre-offres, laisse partir un passager qui refuse de payer ; au contrôle, négocie une fois puis paie
(ne conteste pas, ne fuit pas) ; change la roue lui-même, appelle le mécanicien en cas de panne, le technicien en cas de
panne sèche ; **1 accrochage tous les 60 km** (≈ 1 tous les 3 jours), choc de 8 à 20 km/h, fautif une fois sur deux,
s'arrête et paie ou laisse filer le chauffard (le choc vient de la physique d'Unity : il est tiré au hasard). Carrière
de départ : réservoir de 12 L, réputation 50, taxi neuf.

**Graines fixes** : carrière k, jour d → graine 1 000 003 + 1 000 × k + d (clients, disputes, route, police,
accrochages) ; calcul parallèle mais déterministe (test `SameSeed_SameCareer`).

**Pas simulé** : piétons renversés (le joueur moyen n'en renverse pas), répliques des passagers (pourboires et
réputation des réponses), feux et ralentissements (inclus dans la vitesse moyenne), conduite gênée par le trafic (le
trafic évite le taxi, pas l'inverse), contacts de police (jalon 7, coupé), foyer et charges (jalon 8).

## Points relevés pour Kevin (rien n'est corrigé)

- **Les papiers ne se renouvellent jamais dans le jeu** : `PoliceView` crée chaque jour un `PaperSet` neuf à partir du
  jour 1, et aucun écran n'appelle `PaperSet.Renew`. L'assurance (`startDaysLeft` 12) est donc expirée **à partir du
  13ᵉ jour, pour toujours** : chaque contrôle demande alors 5 000 FCFA (`demandExpiredPaper`). La simulation reproduit le
  jeu tel qu'il est (les jours 13 et 14 de l'horizon en sont plus chers).
- Le joueur qui remplit son taxi (5 places, 2 devant) est en surcharge au sens de `police.json` (4 passagers, 1 devant) :
  c'est la majorité des problèmes réels trouvés aux contrôles. Si c'est voulu, le tutoriel devrait le dire.
- « Recherché » après une fuite ne passe pas d'un jour à l'autre (`PoliceDay` est recréé chaque jour) ; le nombre de fourrières, lui, est gardé par la carrière.
