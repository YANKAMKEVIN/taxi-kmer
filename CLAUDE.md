# Taxi Kmer

Jeu 3D sur la vie d'un taximan à Yaoundé (tronçon Messassi → Bastos au départ, puis extension, puis Douala). Cible principale : Android (y compris entrée de gamme) ; web en démo.

Kevin est le **directeur du jeu** : il conçoit, Claude implémente. Toute décision de game design non documentée → la lui poser, ne pas l'inventer.

## À lire avant de travailler

- `docs/design/vision.md` — **source de vérité du design** (piliers, boucles, mécaniques, questions ouvertes).
- `docs/tech/etat-projet.md` — où en est le projet, prochaine étape (court, mis à jour à chaque tâche terminée).

## Règles de travail

1. Données (types de course, clients, prix, charges, dialogues) = fichiers de données, jamais codées en dur dans la logique.
2. Logique de jeu séparée de la présentation (testable sans scène).
3. Petites questions (durées, tailles, couleurs…) → valeur par défaut notée dans le commit et `etat-projet.md` ; seules les vraies décisions de design vont à Kevin.
4. Monde : Yaoundé **reconnaissable** (routes OpenStreetMap, ODbL à créditer ; lieux emblématiques faits main). Jamais de données 3D Google ou d'autres sources sous licence incompatible.

## Décisions techniques

À fixer au démarrage du premier jalon (moteur proposé : Godot 4, voir `vision.md` §7).
