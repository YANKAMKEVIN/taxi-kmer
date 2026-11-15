# Jalon 3 — Les jours s'enchaînent (plan technique)

> Ouvert le 2026-11-15, **partie technique seulement** (la sauvegarde), à la demande de Kevin, pendant que le jalon 1 attend sa validation et que le jalon 2 attend l'essai sur téléphone. Contenu et critères : `docs/design/roadmap.md`. Le reste du jalon (calendrier, carburant, propriétaire, réputation) attend les réponses de Kevin.

**Accepté si** (feuille de route) : on joue 3 jours de suite, on quitte l'appli à n'importe quel moment et on reprend sans perte ; tests de la sauvegarde, y compris fichier corrompu ou version ancienne.

## Tâches

| Tâche | État |
|---|---|
| 3.1 Sauvegarde sûre | **faite** (2026-11-15, session cloud) : `Core/Save` ; 13 tests (coupure de courant à chaque étape d'une écriture, fichier tronqué, modifié, vide, illisible, sans copie de secours, version plus récente, version ancienne, journée sauvegardée puis reprise, bilan ajouté à la carrière) |
| 3.2 Sauvegarde dans le jeu | **écrite, à essayer dans Unity** : `SaveService` (créé par `StreetView`) ; sauvegarde la journée en cours toutes les 30 s et quand l'appli passe en arrière-plan ou se ferme ; enregistre le bilan en fin de journée ; reprise au lancement **coupée** (`resumeOnStart: false` dans `save.json`) jusqu'à la validation du jalon ; test PlayMode de bout en bout (sauvegarder, relancer, reprendre) dans un dossier temporaire |
| 3.3 Journée suivante | attend Kevin : que se passe-t-il si le versement n'est pas atteint, l'argent passe-t-il d'un jour à l'autre, horaires libres ? |
| 3.4 Calendrier, carburant et station, propriétaire, réputation | attend Kevin (questions de la feuille de route) |

## Choix techniques

- **Ce qui est sauvegardé** (`SaveData`, version 1) : numéro du jour ; bilans des journées terminées ; journée en cours : heure, recettes, courses, kilomètres, dépenses imprévues, passagers à bord (destination, prix, carrure, banquette), passagers partis sans payer, position et sens du taxi, graine des clients. Les clients qui attendent au bord de la route, le trafic et les piétons ne sont pas sauvegardés : ils sont recréés (rien d'argent n'en dépend). Rien de ce qui attend une décision de Kevin (dette, réputation, épargne).
- **Écriture sûre** : fichier temporaire écrit jusqu'au disque, l'ancienne sauvegarde devient la copie de secours, puis le temporaire la remplace. Coupure à n'importe quel moment : on retrouve l'ancienne ou la nouvelle sauvegarde, jamais rien.
- **Lecture** : empreinte FNV-1a 64 bits du contenu ; fichier abîmé → mis de côté (`partie-abimee-N.json`, jamais effacé) et reprise du temporaire complet ou de la copie de secours ; rien de lisible → nouvelle partie ; sauvegarde d'une version plus récente du jeu → rien n'est chargé ni écrasé.
- **Versions** : `SaveMigrations.CurrentVersion` ; un champ ajouté prend sa valeur par défaut tout seul ; une étape de migration n'est écrite que si le sens d'un champ change.
- **Fichiers** : `Application.persistentDataPath/sauvegarde/partie.json` (+ `.bak`, `.tmp`). Les tests n'y touchent jamais (dossier temporaire).
- La logique est en C# pur (`ISaveStorage`, `ISaveSerializer`) : testée hors Unity avec un disque en mémoire qui simule les coupures, et System.Text.Json ; dans Unity, `FileSaveStorage` et JsonUtility (les tests EditMode utilisent JsonUtility dans Unity).

## Valeurs par défaut (`save.json`)

Sauvegarde automatique toutes les 30 s réelles ; aussi à la mise en arrière-plan et à la fermeture ; reprise au lancement coupée.
