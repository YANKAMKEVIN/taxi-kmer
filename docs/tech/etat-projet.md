# État du projet — Taxi Kmer

**Phase** : jalon 1 en cours.

**Décidé** (Kevin, 2026-11-11) : nom « Taxi Kmer » ; dépôt séparé de Pokemake ; trois types de course (partagée, dépôt, via une appli fictive) ; monde libre ; style réaliste ; Unity 6 LTS (URP) avec réglages bas / moyen / haut ; Street View et internet = référence seulement, textures libres puis photos sur place fournies par Kevin.

**Jalon 1** (validé par Kevin) : dispensaire de Messassi → Étoudi, devant le palais de la présidence ; 5 places ; journée de 10 min ; course partagée de 300 à 800 FCFA selon la distance ; versement de 10 000 FCFA ; corpulence des clients (2 places devant selon la carrure) ; commandes à flèches (pas d'inclinaison) ; client qui ne rentre pas : le joueur le refuse ou fait changer un passager de place ; lieux de clients : dispensaire de Messassi, carrefour / marché de Messassi, Borne Fontaine, pont d'Emana, Emana, Étoudi (devant le palais de la présidence).

**Feuille de route** : `docs/design/roadmap.md` (jalons 2 à 11, proposition à valider par Kevin).

**Jalon 1 en cours** : logique pure faite et testée (33 tests, `tools/test_core.sh`) ; outil d'import OSM écrit. Détail : `docs/tech/jalon-01.md` (Avancement).

**Prochaine étape** : tâche 1.1 sur le Mac de Kevin (Unity 6 LTS + module Android), puis téléchargement OSM de Messassi → Étoudi, devant le palais de la présidence.

**Questions ouvertes** : `vision.md` §9, `roadmap.md` (par jalon).

**Mise à jour Kevin (2026-11-11)** : arrivée du jalon 1 déplacée d’Émana à Étoudi devant le palais ; recherche photo ciblée sur les repères du trajet. Extension du JSON de lieux et du tracé OSM encore à réaliser ; aucune distance supplémentaire validée.
