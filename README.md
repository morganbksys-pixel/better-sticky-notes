# Better Sticky Notes — MVP

Application Windows 10/11 (x64), en C# / .NET 10 et WPF. Notes colorées indépendantes, épinglage au premier plan, Markdown, sauvegarde automatique et icône dans la zone de notification.

La version active est affichée dans le titre de **Mes notes** et dans l’infobulle de l’icône système. La rubrique **Mes notes → Paramètres → À propos** indique également le chemin de l’exécutable actif, utile si plusieurs versions sont présentes.

## Lancer la version portable

[Télécharger la dernière version pour Windows x64](https://github.com/morganbksys-pixel/better-sticky-notes/releases/latest).

Extraire entièrement `BetterStickyNotes-win-x64.zip`, puis lancer `BetterStickyNotes.exe`. Aucun installateur ni installation de .NET nécessaire pour cette distribution autonome.

- Déplacer une note par sa barre supérieure ; redimensionner par les bords élargis (9 unités WPF) ou par la poignée en bas à droite. Son trait fin suit la courbe du coin et s’accentue au survol ; la zone de saisie de 24 × 24 est plus large que le dessin. Le curseur diagonal indique le redimensionnement.
- Fenêtres aux coins arrondis et au fond extérieur transparent. Glisser le titre ou la poignée à gauche pour déplacer la note.
- Double-cliquer sur le titre (ou utiliser `F2` / **Renommer la note**) pour le modifier. Entrée ou un clic ailleurs termine l’édition ; la poignée reste disponible pendant la saisie.
- **Punaise** : activer ou désactiver le premier plan. Elle est pleine, droite et surlignée lorsque la note est épinglée ; en contour, inclinée et atténuée sinon.
- Les notes s’ouvrent en aperçu. Double-cliquer sur leur contenu passe en édition ; déplacer réellement la note par le bandeau revient à l’aperçu. Passer à une autre application ou une autre fenêtre affiche automatiquement le rendu Markdown de la dernière saisie ; revenir sur la note la laisse en aperçu. Un simple clic sur le bandeau et l’utilisation des menus (correction, mise en forme, couleur, aide) conservent l’édition. `Ctrl+Entrée` bascule aussi entre les deux modes.
- Le bandeau présente **[…] / Punaise / ×**. La croix ferme le post-it sans supprimer la note ; elle reste disponible dans **Mes notes**. Une note protégée est également reverrouillée. Pour la réafficher, utiliser la liste, son bouton de visibilité, le double-clic ou son menu contextuel.
- En édition, une barre en bas à gauche propose gras (`Ctrl+B`), italique (`Ctrl+I`), liste, checklist, code et lien (`Ctrl+K`). Sélectionner plusieurs lignes puis **Code** crée un bloc de code. Sans sélection, un exemple est inséré. Les actions sont annulables avec `Ctrl+Z`.
- Le bouton **?** ouvre un aide-mémoire Markdown, refermable avec Échap ou un clic ailleurs. Les outils supplémentaires passent dans **…** lorsque la note est étroite.
- **⋯** regroupe les autres actions : nouvelle note, renommer, couleur et corbeille sans confirmation.
- Les nouvelles notes apparaissent en bas à droite de l’écran où se trouve la souris, au-dessus de la barre des tâches, puis se décalent vers le haut et la gauche.
- Les cases à cocher sont interactives dans l’aperçu. Masquer une note laisse l’application dans la zone de notification.
- Icône jaune : simple clic sans action, double-clic pour ouvrir **Mes notes** ou la ramener au premier plan, clic droit pour créer une note, ouvrir **Mes notes** ou quitter. Créer une note via **Nouvelle note** dans un menu.
- L’icône système reste jaune, indépendamment de la visibilité des notes.
- Le menu système propose uniquement **Nouvelle note**, **Mes notes** et **Quitter**. **Mes notes** devient l’accueil : **Paramètres**, en bas de la barre latérale, rassemble le démarrage Windows, l’import, le dossier des données et les informations de version.
- **Mes notes** ouvre une fenêtre de gestion : recherche dans les titres et contenus sans distinction de casse ou d’accents, visibilité individuelle, sélection multiple et corbeille sans confirmation. Changer la recherche désélectionne les notes ; **Tout sélectionner** vise uniquement les résultats affichés. La sélection utilise les lignes surlignées, sans coches : clic simple, Ctrl+clic, Maj+clic et Ctrl+A quand la liste a le focus. Le double-clic affiche la note ciblée et la ramène au premier plan, même si elle était masquée ou réduite, sans modifier son épinglage. Il ne masque jamais une note ; il est sans effet dans la corbeille. Utiliser le bouton de visibilité ou le menu contextuel pour masquer. Suppr envoie la sélection à la corbeille, ou demande confirmation pour la supprimer définitivement depuis la corbeille. Dans la recherche, les raccourcis restent ceux de la saisie de texte.
- Après un import, cette fenêtre s’ouvre avec les nouvelles notes sélectionnées et masquées. Leur premier affichage se fait en cascade en bas à droite de l’écran utilisé. Les positions déjà personnalisées sont conservées. **Réorganiser en cascade** affiche et replace explicitement la sélection. Si le nombre de notes dépasse la place disponible, de nouvelles piles décalées réutilisent l’écran ; la recherche permet alors de choisir un groupe plus petit.
- Raccourcis dans une note : `Ctrl+N`, `Ctrl+Entrée`, `Ctrl+S`.
- Lancer le programme ou cliquer sur son raccourci épinglé ouvre **Mes notes**. S’il est déjà lancé, sa fenêtre de gestion est ramenée au premier plan, sans afficher ni déplacer les post-it. **Mes notes** possède une entrée dans la barre des tâches : clic droit sur cette entrée pour épingler l’application. Au lancement à froid, les post-it restent masqués jusqu’à une action explicite.

Pour le démarrage automatique, cocher **Mes notes → Paramètres → Démarrer avec Windows**. L’option est désactivée initialement. Elle utilise la clé Run du compte Windows avec l’argument `--background`, sans droits administrateur : aucune fenêtre ne s’ouvre au démarrage de Windows. Une entrée existante créée par l’application est mise à jour vers le nouvel exécutable lors de son lancement. Si nécessaire après déplacement, décocher puis recocher l’option pour actualiser son chemin. Décocher avant de supprimer l’application.

## Accueil, raccourcis et apparence

Les paramètres s’affichent dans la fenêtre **Mes notes**. Cliquer sur une vue ou un groupe revient à la liste.

| Raccourci | Action |
| --- | --- |
| Ctrl+N | Nouvelle note dans le groupe affiché ou celui du post-it actif |
| F2 | Renommer le groupe ayant le focus, la seule note sélectionnée, ou le post-it actif |
| Échap | Annuler le renommage |
| Ctrl+A / Suppr | Sélectionner toutes les lignes / envoyer la sélection à la corbeille lorsque la liste a le focus |
| Ctrl+Entrée | Basculer édition et aperçu dans un post-it |

Les champs de saisie conservent leurs raccourcis de texte. Ces commandes restent locales à Sticky ; un dialogue modal bloque les commandes de sa fenêtre parente.

Les post-it utilisent une barre de défilement fine, arrondie et discrète, identique en édition et en aperçu, avec une zone de prise plus large et un contraste accru au survol. La molette reste disponible. L’accueil, les dialogues internes, les menus et les infobulles partagent des formes arrondies ; le sélecteur de fichier Windows reste natif.

## Groupes de notes

Dans **Mes notes**, la barre latérale rassemble **Toutes les notes**, **Sans groupe**, les groupes personnels et **Corbeille**, avec leurs compteurs. Le bouton **+** à côté de **MES GROUPES** crée un groupe, tout comme le clic droit sur le fond vide de la liste des groupes. Faire un **clic droit sur un dossier** pour afficher/masquer ses notes, changer sa couleur, le renommer ou le supprimer. La touche Menu ou Maj+F10 ouvre ce même menu sur le groupe ayant le focus. Chaque note appartient à un seul groupe, ou à **Sans groupe**. Les noms sont limités à 80 caractères et ne peuvent pas être dupliqués, sans distinction de casse.

- Choisir un groupe filtre les notes actives ; la recherche s’applique dans ce groupe. Changer de vue réinitialise la recherche et la sélection. La corbeille est une vue indépendante regroupant toutes les notes supprimées.
- **Déplacer vers…** affecte les notes sélectionnées à un groupe. Le menu **⋯ → Groupe** d’une note permet également de changer son affectation.
- **Nouvelle note** depuis Mes notes utilise le groupe sélectionné. Depuis un post-it, le menu **Nouvelle note** et **Ctrl+N** reprennent son groupe. Les créations depuis l’icône système ou les paramètres et les imports restent dans **Sans groupe**. Après un import, la liste revient à **Toutes les notes** pour montrer la sélection importée.
- Dans **Mes notes**, le bouton jaune **＋ Nouvelle note** permet de créer une note. Le bouton **⋯** de l’en-tête est supprimé dans toutes les vues ; les actions sont accessibles par clic droit sur la liste ou sur les groupes.
- Le clic droit sur un groupe conserve la vue actuelle et la sélection de notes. Ses commandes ciblent toujours le dossier visé, même si un autre groupe est affiché. **Afficher les notes du groupe / Masquer les notes du groupe** agit sur toutes ses notes actives, même avec une recherche en cours, sans modifier les autres groupes, les positions ou l’épinglage. Les notes protégées suivent le déverrouillage habituel. **Sans groupe** propose uniquement ces deux actions. La gestion des groupes se fait directement sur les dossiers.
- Supprimer un groupe remet toutes ses notes dans **Sans groupe**, y compris dans la corbeille, sans les supprimer. Restaurer une note conserve son groupe si celui-ci existe toujours.
- **Clic droit sur le dossier → Couleur du dossier** propose huit couleurs pour l’icône du groupe : gris, bleu, vert, jaune, orange, rouge, rose et violet. La couleur courante est cochée et reste enregistrée après renommage et redémarrage. Les groupes existants utilisent le gris par défaut ; les couleurs des notes restent indépendantes.

Les notes verrouillées restent anonymes dans les listes et exclues des recherches non vides. Une note protégée déverrouillée affiche son titre et son extrait, avec un cadenas ouvert, et participe à la recherche. Le reverrouillage masque immédiatement ces informations et actualise les résultats. Les noms des groupes et leurs associations sont des métadonnées non chiffrées : ne pas y placer d’informations confidentielles.

Les dossiers des groupes sont pleins et remplis avec leur couleur, dans la navigation et les menus. Dans **Déplacer vers** et **⋯ → Groupe**, les noms des groupes sont surlignés avec une teinte claire de cette couleur. La coche du groupe courant est conservée ; **Sans groupe** reste neutre.

Les menus contextuels utilisent des icônes communes pour les mêmes actions : notamment la corbeille pour supprimer et les yeux pour afficher/masquer. L’entrée **Groupe** du menu des post-it et **Déplacer vers** dans la liste portent un dossier plein.

La version 0.11.0 enregistre les groupes et les notes ensemble dans un document JSON versionné. Les anciens fichiers sont lus automatiquement ; la sauvegarde suivante utilise ce nouveau format. **Ne pas rouvrir ces données avec une ancienne version de l’application**, qui ne connaît pas ce format. La copie de secours conserve également les groupes correspondant à sa version des notes.

## Corbeille et notes confidentielles

**Mes notes** présente une liste épurée sans quadrillage, avec titre, extrait et état de visibilité. Dans **Toutes les notes**, le groupe figure devant l’extrait. Les actions sont accessibles par **clic droit sur la liste** (ou la touche Menu / Maj+F10) : **Afficher**, **Masquer**, **Déplacer vers**, **Réorganiser en cascade**, **Mettre à la corbeille**, **Tout sélectionner** et **Désélectionner**. Un clic droit sur une ligne déjà sélectionnée conserve la sélection multiple ; sur une autre ligne, il sélectionne uniquement celle-ci. Un clic droit sur le fond vide désélectionne les notes. Aucun bouton de sélection n’apparaît en bas. **Tout sélectionner** reste disponible dans le menu contextuel et par Ctrl+A dans la liste. Dans **Corbeille**, le menu contextuel propose **Restaurer** et **Supprimer définitivement…** ; ce même menu permet aussi de vider toute la corbeille, même avec un filtre actif, et de régler la purge automatique. La restauration remet les notes dans Mes notes, masquées, sans ouvrir leurs fenêtres.

**Purge automatique** : dans **Corbeille → clic droit sur la liste → Purge automatique…**, cocher l’option dans la fenêtre dédiée, saisir un délai de 1 à 36 500 jours, puis cliquer sur **Appliquer**. Elle est désactivée par défaut. Une fois activée, les notes arrivées à échéance sont supprimées définitivement sans confirmation, dès l’application du réglage, au démarrage puis toutes les heures tant que l’application tourne, indépendamment de la recherche et de la sélection. Le délai part de la date de mise à la corbeille ; restaurer puis supprimer à nouveau une note le réinitialise. Les anciennes notes sans date reçoivent une date au premier chargement de cette version, enregistrée pour les démarrages suivants. Le réglage est stocké dans `settings.json` à côté des notes ; s’il est illisible, la purge reste désactivée.

La mise à la corbeille est immédiate, pour une note comme pour une sélection. Dans **Mes notes → Corbeille**, chaque note propose **Restaurer** et **Supprimer définitivement…**. **Vider la corbeille…** supprime toutes les notes qui s’y trouvent. Seules les suppressions définitives demandent confirmation ; elles retirent aussi les notes de la sauvegarde `.bak` après un enregistrement réussi.

Dans le menu **⋯** d’une note, choisir **Protéger par mot de passe…**. La première note protégée définit un mot de passe commun (8 caractères minimum, à confirmer) ; les suivantes demandent ce même mot de passe. La note est ensuite verrouillée et masquée. Dans **Mes notes**, elle apparaît comme **Note protégée 🔒**, sans titre ni extrait confidentiel : le bouton de visibilité **Déverrouiller**, ou **Afficher** dans le menu contextuel, demande le mot de passe et ouvre la note. Une fois déverrouillée, son vrai titre et son extrait sont visibles dans la liste, avec un cadenas ouvert, et la recherche retrouve son contenu. Cette règle s’applique à toutes les vues, y compris la corbeille. Une recherche non vide exclut uniquement les notes verrouillées ; effacer la recherche pour les retrouver.

Le menu d’une note déverrouillée propose **Verrouiller** et **Retirer la protection…** (mot de passe obligatoire). Masquer la note, fermer sa fenêtre ou verrouiller la session Windows la reverrouille. Le menu contextuel de la liste permet de **Verrouiller toutes les notes protégées**. Après un redémarrage, elles restent verrouillées. Depuis **Mes notes**, afficher une sélection demande le mot de passe pour chaque note verrouillée.

Les titres et contenus sont chiffrés avec AES-256-GCM et une clé dérivée du mot de passe par PBKDF2-SHA256 (600 000 itérations, sel aléatoire). Ils restent chiffrés sur disque pendant l’édition ; le mot de passe et les clés ne sont pas enregistrés. L’identifiant, la couleur, les dimensions et les autres métadonnées restent lisibles. Le verrouillage vide les contrôles et réinitialise l’historique d’annulation. La corbeille conserve la protection.

**Un mot de passe oublié ne peut pas être récupéré.** Tant qu’il reste une note protégée, y compris dans la corbeille, le même mot de passe s’applique aux nouvelles protections. Si toutes les protections sont retirées ou toutes les notes protégées supprimées définitivement, la prochaine protection permet de choisir un nouveau mot de passe.

La copie `.bak` est actualisée avec les données chiffrées lorsqu’une protection est activée ; en présence de notes protégées, elle contient la même version que le fichier principal après une sauvegarde réussie. Les anciennes copies externes, les fichiers historiques `.damaged-*` et la base Sticky Notes d’origine ne sont pas modifiés : ils peuvent contenir une ancienne version en clair. Cette protection ne réalise pas un effacement sécurisé du disque ni du presse-papiers.

## Importer les Sticky Notes de Windows

Dans **Mes notes → Paramètres**, choisir **Importer depuis Sticky Notes…**. L’application détecte le format classique `plum.sqlite` et les post-it de la nouvelle expérience OneNote dans `%LocalAppData%\Microsoft\OneNote\16.0\Memory\notes.sdk_*.db`. La base locale la plus récemment modifiée est proposée ; on peut en sélectionner une autre, notamment en présence de plusieurs comptes. Les blocs-notes OneNote `.one` ne sont pas pris en charge.

L’aperçu permet de choisir les notes avant de confirmer. Le texte, les listes et les couleurs sont récupérés ; la première ligne devient le titre. Les notes importées sont accessibles dans **Mes notes**, sans ouvrir toutes leurs fenêtres d’un coup. Les images et la mise en forme enrichie ne sont pas importées. Les anciennes bases `.snt` et les notes uniquement présentes dans le cloud ne sont pas prises en charge.

La base source est ouverte en lecture seule. Les notes supprimées ou vides sont ignorées. Le réimport ignore les identifiants déjà importés, même si leurs copies locales ont été modifiées ou mises à la corbeille : il ne synchronise pas les modifications ultérieures de Windows.

En cas de fichier verrouillé, fermer Sticky Notes puis réessayer. Pour utiliser une copie, copier la base après fermeture de Sticky Notes, en conservant à côté ses fichiers `plum.sqlite-wal` et `plum.sqlite-shm` s’ils existent.

## Stockage local

Les notes sont stockées dans `%LocalAppData%\BetterStickyNotes\notes.json`, avec une sauvegarde `.bak`. Le programme est portable ; les données restent attachées au compte Windows. **Mes notes → Paramètres → Ouvrir le dossier des données** ouvre ce dossier. Pour transférer les notes, quitter l’application sur les deux postes et copier ce dossier. Pas de compte ni de synchronisation réseau.

L’enregistrement est automatique après une courte pause de saisie. Une copie de secours est conservée et les écritures remplacent le fichier de façon atomique. Hors protection ou suppression définitive, cette copie conserve la version précédente. Si le fichier principal est illisible, l’application tente la sauvegarde et conserve le fichier endommagé.

## Markdown pris en charge

Titres, gras, italique, barré, listes, tâches, liens HTTP/HTTPS/mailto, citations et code. Le HTML est affiché comme texte ; les images distantes et les tableaux ne sont pas rendus dans ce MVP.

## Performances et sauvegardes

Les écritures automatiques sont regroupées après une pause de saisie, puis sérialisées en arrière-plan à partir d’un instantané des notes. Quitter attend la dernière sauvegarde ; en cas d’échec, l’application reste ouverte. Les déplacements ne reconstruisent plus la liste, et les lignes de « Mes notes » sont conservées entre les mises à jour.

L’ouverture d’un groupe est progressive pour laisser l’interface répondre. Masquer une note annule son éventuelle ouverture encore en attente. La correction orthographique fonctionne dans les notes visibles en édition, y compris pendant l’utilisation du menu de correction. Elle est désactivée en aperçu et au masquage.

Les aperçus Markdown sont libérés au masquage. Au maximum cinq fenêtres masquées restent en cache, pendant une minute. Ensuite, leur fermeture libère les contrôles ; le texte, la position, le curseur et la sélection sont conservés pour la réouverture. **L’historique d’annulation Ctrl+Z est réinitialisé lorsqu’une fenêtre sort du cache.**

## Développement et tests

Prérequis : Windows et SDK .NET 10.

```powershell
dotnet build BetterStickyNotes.csproj
dotnet run --project tests/SmokeTests.csproj
dotnet run --project tests/SmokeTests.csproj -- --quiet-ui
dotnet run --project tests/SmokeTests.csproj -- --benchmark 38
dotnet run --project tests/SmokeTests.csproj -- --benchmark 100
./publish.ps1
```

La publication locale crée `dist/BetterStickyNotes-<version>/BetterStickyNotes.exe` et son ZIP autonome. Après une publication réussie, le script conserve les trois versions les plus récentes (dossiers et ZIP) et actualise `SHA256SUMS.txt`. Les données personnelles dans `%LocalAppData%` ne sont pas touchées.

Les tests couvrent la sauvegarde, la récupération, les positions des tâches Markdown, le filtrage des liens et la construction WPF. Vérifier manuellement le menu système, le premier plan entre applications, le démarrage Windows et les configurations multi-écrans avant diffusion plus large.

Le mode de test `--quiet-ui` vérifie les menus, la disposition et les couleurs sur des données synthétiques, sans afficher ni activer de fenêtre. La suite complète contient des tests de focus et de dialogues qui peuvent apparaître à l’écran ; ne la lancer qu’avec accord.
