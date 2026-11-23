# À faire sur le Mac de Kevin

Série d'idées du 23/11 (branche `claude/idees-50`). Une session cloud ne peut ni ouvrir Unity ni lancer le jeu : tout le
code d'affichage ci-dessous est **compilé hors Unity** (`tools/check_unity_compile.sh`, références UnityEngine 2021.3)
mais **jamais exécuté**. Cocher au fur et à mesure.

## 0. Avant tout

- [ ] Fermer Unity, récupérer la branche, ouvrir le projet dans Unity 6000.3.28f1 : aucune erreur de compilation (en
      particulier `Assets/Editor/BuildAndroid.cs`, que le script hors Unity ne compile pas, et les fichiers partiels
      `StreetView.*.cs`).
- [ ] Unity crée seul les `.meta` manquants ? Il ne doit pas en manquer (ils sont dans le dépôt) : `git status` propre
      après l'ouverture.
- [ ] Lancer les tests EditMode puis PlayMode (`CLAUDE.md`, « Commandes ») : tout vert. Les PlayMode attendent la fin du
      chargement progressif (idée 21).

## 1. Idées qui ne se font que sur le Mac, le téléphone ou avec Kevin

- [ ] **Idée 1 — recette Unity du code jamais exécuté** : une séance avec la liste de la section 3 ci-dessous, plus les
      panneaux plus anciens jamais vus en vrai : `PoliceView` (contrôle, 5 choix, touches 1 à 5), `TalkView` (bulles,
      réponses F1 à F3, disputes), `RoadView` (roue de secours, mécanicien, pluie), `RadioView` (bouton, texte des
      annonces, baisse quand un passager parle), `AmbienceView` (rumeur, marché, klaxons), accrochages
      (`TaxiHitDetector` : emboutir une voiture → s'arrêter / partir ; se faire emboutir à l'arrêt → poursuivre /
      laisser ; un choc ne compte qu'une fois), sélecteur de qualité (Bas / Moyen / Haut, choix gardé). Résultat
      attendu : une ligne cochée ou un bug noté par panneau.
- [ ] **Idée 4 — vrais passants animés** : choisir des personnages low-poly CC0 (marche, attente, hèlement), les habiller
      comme les 6 personnages, les importer, faire un Animator ; remplacer capsule + sphère dans `TrafficView` (piétons)
      et `StreetView.People.cs` (clients). Résultat : passants qui marchent, clients qui hèlent.
- [ ] **Idée 5 — véhicules modélisés** : Corolla jaune (plaque orange, baguette ; photos de Kevin), moto, bus, deux
      voitures, CC0 ou Blender ; remplacer les blocs de `TaxiFactory` et `TrafficView.BuildCar` en gardant les noms des
      pièces utilisées par le code (« Phare », « Pare-chocs avant » : dégâts visibles et jour / nuit).
- [ ] **Idée 18 — musiques de la radio** : présélectionner 15 à 20 morceaux d'Afrique centrale CC0 / CC-BY (méthode de
      `docs/tech/radio.md`), Kevin écoute et choisit ; remplir `clip`, `licence`, `credit`, `source` dans `radio.json` ;
      l'écran Crédits (idée 42) les affiche tout seul.
- [ ] **Idée 19 — moteur enregistré** : trois boucles CC0 (ralenti, moyen, haut régime) en fondu selon la vitesse dans
      `TaxiSounds`, plus un passage de vitesse simulé ; régler à l'écoute.
- [ ] **Idée 22 — lieux emblématiques** : palais d'Étoudi, pont d'Emana, marché de Messassi, centre médical d'Olembe,
      d'après `docs/references/photos` ; en commencer un tôt.
- [ ] **Idée 28 — vue depuis la cabine** : modèle d'intérieur (tableau de bord, compteur, radio), caméra au poste du
      chauffeur, passagers sur la banquette (`CabinPlan`).
- [ ] **Idée 39 — tests Unity dans la CI** : mettre la licence Unity de Kevin en secret GitHub, ajouter GameCI
      (EditMode et PlayMode) à `.github/workflows/core-tests.yml`.
- [ ] **Idée 40 — APK par la CI** : GameCI build Android sur `main`, APK en artefact ; clé de signature en secret.
- [ ] **Idée 44 — téléphone d'entrée de gamme** : mesurer les i/s (bouton Qualité) sur un Android à ≈ 150 € en Moyen,
      heure de pointe ; critère 30 i/s. Comparer avec le budget du test `Budget_200Vehicles_150Pedestrians_10000Steps`.
- [ ] **Idée 45 — vraie station-service et garage** : Kevin donne les positions ; les mettre dans `fuel.json` (et le
      garage quand il existera).
- [ ] **Idée 46 — vraies voix** : courtes prises de comédiens camerounais (français, camfranglais, pidgin), accord des
      comédiens ; remplacer `SoundSynth.Voice` (`dialogues.json` → `voiceSounds`).
- [ ] **Idée 48 — Play Store** : test interne, fiche en français, politique de confidentialité, classification d'âge
      (corruption suggérée, accidents) ; utiliser l'AAB de l'idée 41.
- [ ] **Idée 49 — démo web** : build WebGL sur un niveau Bas dédié ; vérifier Resources/Data, l'audio synthétisé et URP.

## 2. Ce qui reste des idées faites en partie

- [ ] **Idée 7** : passer la logique des courses (`Street`) des distances le long de l'itinéraire aux positions 2D
      (`RoadNetwork`, `StopPlacement`), élargir le couloir (`osm_route.py --corridor`), et décider des nouveaux lieux.
- [x] **Idée 8** : trafic, piétons, clients, postes, repères, vendeurs, station, barrières et taxi posés sur le relief,
      `applyTerrain` activé (décision de Kevin du 23/11) ; reste la vérification en jeu (section 3).
- [ ] **Idée 11** : messages de la famille (jalon 7), appli de courses (jalon 8), contacts à appeler au contrôle.
- [ ] **Idée 17** : pièces détachables sur le vrai modèle.
- [ ] **Idée 23** : atlas de façades CC0 (ambientCG), UV des murs, matériaux URP texturés.
- [ ] **Idée 33** : transformer les parties de `StreetView` en composants séparés.
- [ ] **Idée 37** : rejouer aussi la physique dans Unity (mode rejeu avec la graine et les commandes enregistrées) ;
      enregistrer les choix de police et les réponses aux passagers.
- [ ] **Idée 41** : signature de l'AAB avec la clé de Kevin.
- [ ] **Idée 50** : brancher endurance (patience) et raccourcis (détour) si Kevin garde la proposition.

## 3. Vérifications de ce qui a été codé sans être lancé

- [ ] Idée 3 (jour / nuit) : lancer une journée en accéléré (dayLengthRealSeconds bas) ; soleil qui monte à l'est et se couche à l'ouest, teinte orangée vers 18:00, nuit bleutée après 18:45, têtes de lampadaires allumées de 18:00 à 06:20, phares émissifs, faisceau des phares au niveau Haut seulement. Régler daylight.json à l'œil.
- [ ] Idée 6 (mini-carte) : en conduisant, carte ronde en haut à droite sous le klaxon ; routes grises, itinéraire jaune, taxi au centre qui pointe vers le haut, la carte tourne avec le taxi ; pastilles des destinations des passagers (couleurs de la frise), postes de police en bleu, station en vert ; un repère lointain reste collé au bord. Vérifier que le masque rond coupe bien l'image et que rien ne chevauche le klaxon / la pause.
- [ ] Idée 8 (relief, **activé** le 23/11 au soir, décision de Kevin) : `WorldBuilder.applyTerrain` est maintenant vrai par
      défaut (la scène Monde ne l'enregistre pas : la valeur du code s'applique ; vérifier la case « Apply Terrain »).
      Tout ce qui est posé au sol prend la hauteur de `WorldBuilder.HeightAt` / `Core.World.Ground` (interpolée sur les
      mêmes triangles que le sol, testée hors Unity) : taxi au départ et au repositionnement, voitures, taxis
      concurrents et mototaxis du trafic (inclinés dans la pente), piétons, clients au bord de la route, clients
      déposés, vendeurs aux feux, poteaux des feux, postes de police et voiture de poursuite, station-service,
      barrières des cortèges, repères des arrêts. Routes et murets redécoupés tous les 5 m pour suivre le sol. À vérifier :
  - [ ] **Pentes** : parcourir tout l'itinéraire (Messassi → Étoudi, pentes jusqu'à ~15 %) : le taxi monte les côtes
        sans patiner ni reculer à l'arrêt (frein, frein à main), les WheelCollider ne traversent pas le sol, la vitesse
        en montée reste jouable ; noter les endroits durs (distance affichée par la frise).
  - [ ] **Véhicules qui flottent ou s'enfoncent** : à l'arrêt derrière une file au feu dans une montée, puis dans un
        creux et sur une crête : roues des voitures du trafic et des mototaxis au ras de la route (tolérance testée :
        < 25 cm sous chaque essieu), tangage cohérent ; piétons, clients, vendeurs, policiers les pieds au sol ; la
        voiture de poursuite (idée 9) suit les côtes. Route : ne doit pas disparaître sous le sol (sinon baisser
        `Ground.RoadStepMeters`, 5 m) ; la zone d'arrêt jaune devant un client accepté (8 m, à plat) peut s'enfoncer
        dans une pente : la noter si elle se voit. Station-service et postes (9 m et 5 m de large) posés à plat à la
        hauteur de leur centre : un coin peut flotter dans une pente.
  - [ ] **Accrochages et contacts** : emboutir une voiture du trafic dans une côte → le choc est détecté
        (`TaxiHitDetector`) ; renverser un piéton (zone de contact) en pente ; le taxi qui tombe sous le sol est remis
        sur la route (seuil maintenant relatif au relief : 5 m sous le sol, au lieu de y < −5, sinon le taxi aurait été
        remis en place sans arrêt dans les creux, le sol descendant jusqu'à −115 m).
  - [ ] **Caméra** : la caméra de poursuite et le mode photo ne passent pas sous le sol dans les descentes ; la
        mini-carte reste juste (elle est en 2D).
  - [ ] **Perf sur le S24** : i/s (bouton Qualité) au niveau Moyen en heure de pointe, relief activé puis coupé
        (case « Apply Terrain ») : le sol est une grille de 161 × 254 points avec MeshCollider, les routes ont plus de
        sommets (pas de 5 m), et chaque véhicule fait deux lectures de hauteur par image. Noter les deux chiffres et la
        durée du chargement (écran « Chargement de Yaoundé »). Si c'est trop lourd : niveau Bas avec relief coupé.
  - [ ] Bâtiments : ils suivent la pente (toits inclinés), à corriger si gênant. Le MNT est un modèle de surface
        (arbres et toits compris) : bosses possibles sur la route ; les noter avec leur distance.
  - [ ] `WorldSnapshots` (éditeur) et `RouteFlyCamera` placent maintenant la caméra au-dessus du relief : relancer les
        captures.
- [ ] Idée 9 (poursuite) : à un contrôle, choisir « Fuir » ; le panneau disparaît, on peut rouler, une voiture de police bleue et blanche démarre 35 m derrière et suit le taxi sur sa voie ; bandeau « Poursuite ! Police à N m » qui clignote. S'arrêter → rattrapé (fourrière). Rouler vite (> 60 km/h) → échappé, bandeau « Recherché » jusqu'au prochain poste qui arrête le taxi. Régler police.json → chase (vitesse, écarts, durée) avec Kevin.
- [ ] Idée 10 (écran titre) : premier lancement → « Commencer la journée » + « Réglages » ; après une journée terminée et l'appli relancée → « Continuer », ligne « Jour N · épargne … · réputation … », bouton rouge « Nouvelle partie » qui demande « Confirmer » puis repart au jour 1. Décider avec Kevin d'activer save.json → resumeOnStart (reprise de la journée interrompue).
- [ ] Idée 11 (téléphone) : bouton « Téléphone » à gauche sous les jauges (clignote en vert s'il y a un message) ; après une journée au versement manqué, le matin suivant : message du propriétaire avec la somme et le nombre de manques restants ; un appui ouvre l'écran des messages, un second le ferme. Vérifier qu'il ne chevauche ni les jauges, ni le bouton Qualité, ni le bouton d'action. Kevin : textes du propriétaire, nom de l'appli (phone.json → appName), messages de la famille (jalon 7), contacts à appeler au contrôle.
- [ ] Idée 12 (pluie) : forcer une averse (road.json → rainChanceByMonth à 1) ; gouttes étirées autour de la caméra en Moyen / Haut, aucune en Bas ; chaussée qui brille puis sèche lentement après l'averse ; deux essuie-glaces noirs qui balaient le pare-brise puis se rangent ; bruit de pluie sur la tôle (provisoire, synthétisé). Vérifier que les gouttes tombent bien vers le bas (rotation du système) et le coût en i/s.
- [ ] Idée 13 (histoires) : sur plusieurs journées, quand l'étudiant monte le matin, il dit les répliques « histoire-etudiant-1 » à « -4 » dans l'ordre (une par jour au plus), la dernière avec un pourboire de 200 ; idem la commerçante (3 rencontres, pourboire de 150). Relancer l'appli entre deux jours : l'histoire reprend où elle en était. Kevin valide les textes (page tools/texts).
- [ ] Idée 14 (événements de ville) : forcer un cortège (city_events.json → chancePerDay 1 pour « cortege ») un jour de semaine : annonce le matin, à 08:00 deux barrières rouges et blanches en travers de la route devant le palais (le taxi ne passe pas), trafic plus dense, puis « Le cortège est passé » à 09:30. Forcer « match » un mercredi / samedi / dimanche : plus de clients après 16:30 et répliques de supporters. Vérifier que le trafic simulé ne traverse pas les barrières de façon gênante.
- [ ] Idée 15 (klaxon) : derrière une voiture lente, klaxonner : 7 fois sur 10 elle se range à droite et ralentit 4 s, puis reprend sa voie ; rien si on est à contresens ou si elle est arrêtée au feu. Les mototaxis se faufilaient déjà par la droite (test existant).
- [ ] Idée 16 (caméra vivante) : sur le S24, le champ de vision passe en douceur de 60° à l'arrêt à 70° vers 80 km/h ; petite secousse sur un nid-de-poule, forte à un accrochage, éteinte en moins d'une seconde. Régler driving.json → camera (fovFastDeg, potholeShake, crashShake) ; vérifier que la secousse ne donne pas mal au cœur.
- [ ] Idée 17 (dégâts visibles) : avec des dégâts sauvegardés à 30 / 50 / 80 (career.vehicleDamage), le pare-chocs avant pend de plus en plus, le phare gauche est noir à partir de 45, de la fumée grise sort du capot à partir de 60. Le soir, vérifier que le cycle jour / nuit (idée 3) ne rallume pas l'émission du phare cassé. Faire de vraies pièces détachables avec le modèle de la Corolla.
- [ ] Idée 20 (trafic moins coûteux) : mesurer les i/s (bouton Qualité) en heure de pointe au niveau Moyen avant / après ; en Bas et Moyen plus aucune ombre de voiture ni de piéton, en Haut seulement les caisses et les corps. Dans le Frame Debugger, vérifier que les matériaux du trafic passent par le SRP Batcher (l'instanciation GPU n'est prise que s'il est coupé).
- [ ] Idée 21 (chargement progressif) : au lancement, écran « Chargement de Yaoundé… N % » avec une barre jaune, l'image ne gèle plus ; le décor apparaît du départ du taxi vers le loin ; le taxi ne tombe pas à travers le sol ; à 100 % l'écran disparaît et le jeu (écran titre) commence. Mesurer la durée sur le S24 et un téléphone d'entrée de gamme (WorldBuilder.frameBudgetMs = 12 ms). Relancer les tests PlayMode (ils attendent la fin du chargement avec WaitForSeconds). WorldSnapshots (éditeur) construit toujours d'un coup.
- [ ] Idée 23 (façades et toits) : les bâtiments ne sont plus gris : murs ocre, beiges, blancs, terre cuite, vert pâle, sable ; toits de tôle (neuve, grise, rouillée) et dalles de béton pour 3 niveaux et plus. Régler buildings.json à l'œil ; compter les appels de rendu (6 couleurs de murs + 4 de toits par case). Reste à faire : télécharger un atlas de façades CC0 (ambientCG), UV des murs, matériaux URP avec texture.
- [ ] Idée 24 (vendeurs aux feux) : s'arrêter au feu rouge du marché de Messassi ou de Borne Fontaine ; après ~2,5 s un vendeur (capsule orange, plateau sur la tête) se tient à la fenêtre gauche, message « Un vendeur propose … » ; avec des passagers, parfois l'un d'eux achète et le dit (répliques « vendeur »). Il repart quand le taxi démarre. Kevin : valider l'idée, les articles et les prix (vendors.json).
- [ ] Idée 25 (objectifs du jour) : dans la pause, sous l'état des papiers, « Objectifs du jour (0/2) » et les lignes ; message « Objectif réussi : … » quand c'est fait ; au bilan, la ligne des objectifs sous la réputation. Vérifier la place à l'écran (texte long). **Primes activées** (décision de Kevin du 23/11 au soir ; montants d'après l'étude : matinal 400, honnête 500, recette 100, prudent 200, plein 100 FCFA) : au bilan, l'épargne augmente de la prime des objectifs réussis (versée avant la clôture : elle peut couvrir un versement manqué) ; vérifier que le bilan le montre clairement (sinon ajouter une ligne « Prime des objectifs »).
- [ ] Idée 26 (frein à main) : bouton « Frein à main » au-dessus de la pédale de frein (touche C au clavier) ; à 30-40 km/h, braquer et serrer : l'arrière glisse, le taxi pivote (demi-tour sur la double voie) ; à l'arrêt il tient le taxi. Régler avec Kevin sur le S24 : driving.json → car.handbrakeTorqueNm, handbrakeRearGrip, et sidewaysStiffness (1,6) pour le glissement général ; vérifier que le bouton ne gêne pas le pouce sur la pédale.
- [ ] Idée 27 (mode photo) : dans la pause, bouton « Mode photo » en haut à droite ; le HUD disparaît, l'heure est figée, glisser pour regarder, ▲ / ▼ pour avancer / reculer, « Monter » / « Descendre », la caméra reste à moins de 80 m du taxi ; « Photo » enregistre une image sans les boutons dans persistentDataPath/photos (à vérifier : la capture se fait en fin d'image, les boutons peuvent apparaître) ; « Retour » ou Échap revient à la pause, la caméra de conduite reprend.
- [ ] Idée 29 (texte et couleurs) : Réglages → lignes sur deux colonnes ; « Texte : normal / grand / très grand » agrandit les bulles des passagers, leurs réponses et les messages ; « Couleurs daltoniens : oui » passe la jauge du versement et les bordures des bulles en bleu / jaune / orange, le carburant bas en orange. Vérifier que les textes très grands ne débordent pas des bulles. Réglages gardés après relance (mêmes clés pour volume et vibrations).
- [ ] Idée 30 (gaucher, taille, opacité) : Réglages → « Commandes : gaucher » met les flèches à droite (◄ à gauche de ►), pédales et frein à main à gauche, le bouton Qualité à droite ; « Boutons : 115 % / 85 % » élargit ou réduit les commandes du bas ; « Opacité : 75 % / 50 % » rend les boutons plus transparents. Vérifier en gaucher que la mini-carte (en haut à droite) ne touche pas le bouton Qualité, et que le plan des places / le téléphone à gauche ne gênent pas les pédales.
- [ ] Idée 31 (volumes séparés) : Réglages → « Radio / Voix / Effets / Rue : 100 % → 75 → 50 → 25 → coupé » ; couper la radio laisse le moteur, les voix et la rue ; « Rue » baisse rumeur, marché, klaxons lointains et appels des clients ; « Effets » le moteur, le klaxon, les freins, les portes, les pièces et la pluie sur la tôle. 10 lignes de réglages : vérifier que l'écran des réglages tient en hauteur sur le S24 (2 colonnes de 5).
- [ ] Idée 33 (StreetView découpé) : ouvrir le projet, vérifier qu'Unity compile les fichiers partiels StreetView.*.cs et que le composant StreetView de la scène garde ses références (le script principal reste StreetView.cs, même GUID) ; relancer les tests PlayMode. Étape suivante possible : transformer ces parties en composants séparés (FuelStationView, StopMarkersView, PeopleView, HudView).
- [ ] Idée 37 (rejeu) : mettre save.json → recordReplay à true, jouer une journée : au bilan, fichier persistentDataPath/rejeux/rejeu-jour-N.json (vérifier la taille, quelques Mo). Pour reproduire un bug signalé : envoyer ce fichier ; DayReplayer.RunStreet rejoue la rue (clients, courses, recette) dans un test dotnet. À faire dans Unity : un mode qui rejoue aussi la physique à partir des commandes enregistrées (graine fixée dans StreetView.seed), et l'enregistrement des choix de police et des réponses (événements prévus, pas encore branchés).
- [ ] Idée 38 (journal) : après quelques journées, ouvrir persistentDataPath/journal.json (sur le S24 : Android/data/com.yankamkevin.taxikmer/files/journal.json) : une ligne par journée (recette, carburant, contrôles et sommes payées, accrochages, pourboires…) ; la console Unity affiche « Versement atteint N % des journées : dans la cible / trop dur / trop facile ». Comparer à docs/tech/equilibrage.md (cible 60-80 %).
- [ ] Idée 41 (AAB et ASTC) : menu « Taxi Kmer > Build Android (AAB, Play Store) » (ou RunBundle en ligne de commande) → build/android/TaxiKmer.aab ; vérifier que BuildAndroid.cs compile dans Unity 6 (non compilé en session cloud : dossier Editor), noter la taille de l'APK avant / après ASTC (log « [TaxiKmer] Build Android … Mo »), régler la signature dans Player Settings avec la clé de Kevin, essayer l'AAB avec bundletool ou un test interne Play Console.
- [ ] Idée 42 (crédits) : bouton « Crédits » en haut à gauche de l'écran titre et de la pause ; l'écran liste OpenStreetMap (ODbL), le relief Copernicus, Unity, puis chaque morceau de radio crédité (quand il y en aura) ; Retour / Échap revient d'où l'on vient. Vérifier que le texte tient à l'écran. Kevin : la ligne d'introduction (nom affiché).
- [ ] Idée 43 (crédit OSM en uGUI) : « © contributeurs OpenStreetMap » toujours visible en bas au centre, même taille qu'avant, sous les boutons et les menus ; vérifier dans le Profiler que plus aucun appel OnGUI ne tourne par image. L'assembly TaxiKmer.World référence maintenant UnityEngine.UI.
- [ ] Idée 50 (compétences) : proposition coupée (skills.json → enabled false). Si Kevin l'active : après quelques journées, la pause affiche « Négociation niv. N · Endurance niv. N · Raccourcis niv. N », les clients marchandent moins fort et les négociations avec la police échouent moins. Endurance (patience) et raccourcis (détour) sont calculés mais pas encore branchés dans la rue.
- [ ] Idée 2 (page de validation) : `python3 tools/texts/textes_validation.py page`, ouvrir `build/validation/textes.html`, cocher quelques textes, « Exporter decisions.json », puis `python3 tools/texts/textes_validation.py apply ~/Downloads/decisions.json` ; `git diff` ne montre que les textes touchés.
- [ ] Idée 32 (textes de l'interface) : les messages de la rue (début de journée, klaxon, montée, piéton renversé…) s'affichent comme avant, sans « {lieu} » ni « {prix} » visibles.
- [ ] Idée 34 / 35 / 36 / 38 : rien à voir à l'écran ; les tests dotnet les couvrent (sauvegardes abîmées, sauvegardes de référence v1 à v6, budget de temps du trafic, journal de fin de journée).
