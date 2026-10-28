# Better Sticky Notes — MVP

Application Windows 10/11 (x64), en C# / .NET 10 et WPF. Notes colorées indépendantes, épinglage au premier plan, Markdown, sauvegarde automatique et icône dans la zone de notification.

## Lancer la version portable

[Télécharger la dernière version pour Windows x64](https://github.com/morganbksys-pixel/better-sticky-notes/releases/latest).

Extraire entièrement `BetterStickyNotes-win-x64.zip`, puis lancer `BetterStickyNotes.exe`. Aucun installateur ni installation de .NET nécessaire pour cette distribution autonome.

- Déplacer une note par sa barre supérieure ; redimensionner par les bords.
- Fenêtres aux coins arrondis et au fond extérieur transparent. Glisser le titre ou la poignée à gauche pour déplacer la note.
- Double-cliquer sur le titre (ou utiliser `F2` / **Renommer la note**) pour le modifier. Entrée ou un clic ailleurs termine l’édition ; la poignée reste disponible pendant la saisie.
- **Punaise** : activer ou désactiver le premier plan. Elle est droite et surlignée lorsque la note est épinglée, inclinée et atténuée sinon.
- Les notes s’ouvrent en aperçu. Double-cliquer sur leur contenu passe en édition ; déplacer réellement la note par le bandeau revient à l’aperçu. Un simple clic sur le bandeau ou un changement de fenêtre conserve l’édition. `Ctrl+Entrée` bascule aussi entre les deux modes.
- Le bandeau présente **[…] / Punaise / Masquer**. L’œil barré masque directement la note.
- En édition, une barre en bas à gauche propose gras (`Ctrl+B`), italique (`Ctrl+I`), liste, checklist, code et lien (`Ctrl+K`). Sélectionner plusieurs lignes puis **Code** crée un bloc de code. Sans sélection, un exemple est inséré. Les actions sont annulables avec `Ctrl+Z`.
- Le bouton **?** ouvre un aide-mémoire Markdown, refermable avec Échap ou un clic ailleurs. Les outils supplémentaires passent dans **…** lorsque la note est étroite.
- **⋯** regroupe les autres actions : nouvelle note, renommer, couleur et corbeille avec confirmation.
- Les nouvelles notes apparaissent en bas à gauche de l’écran où se trouve la souris, au-dessus de la barre des tâches.
- Les cases à cocher sont interactives dans l’aperçu. Masquer une note laisse l’application dans la zone de notification.
- Icône jaune : clic ou double-clic pour afficher/masquer (une seule bascule), clic droit pour toutes les actions. Créer une note via **Nouvelle note** dans un menu.
- L’icône système devient grise lorsque toutes les notes sont masquées et redevient jaune dès qu’une note est affichée. L’infobulle indique aussi cet état.
- Le menu système présente les essentiels : **Nouvelle note**, **Afficher / Masquer les notes**, **Mes notes**, **Options**, **Quitter**. **Options** regroupe la corbeille, le démarrage Windows et le dossier des données.
- **Mes notes** ouvre une fenêtre de gestion : recherche dans les titres et contenus sans distinction de casse ou d’accents, visibilité individuelle, sélection multiple et corbeille avec confirmation. Changer la recherche désélectionne les notes ; **Tout sélectionner** vise uniquement les résultats affichés. Un double-clic sur une ligne affiche ou masque la note.
- Après un import, cette fenêtre s’ouvre avec les nouvelles notes sélectionnées et masquées. Leur premier affichage se fait en cascade en bas à gauche de l’écran utilisé. Les positions déjà personnalisées sont conservées. **Réorganiser en cascade** affiche et replace explicitement la sélection. Si le nombre de notes dépasse la place disponible, de nouvelles piles décalées réutilisent l’écran ; la recherche permet alors de choisir un groupe plus petit.
- Raccourcis dans une note : `Ctrl+N`, `Ctrl+Entrée`, `Ctrl+S`.
- Relancer le programme déjà ouvert affiche les notes existantes.

Pour le démarrage automatique, cocher **Options → Démarrer avec Windows** dans le menu système. L’option est désactivée initialement. Elle utilise la clé Run du compte Windows, sans droits administrateur. Après déplacement de l’exécutable, décocher puis recocher l’option pour mettre à jour son chemin. Décocher avant de supprimer l’application.

## Importer les Sticky Notes de Windows

Dans le menu de l’icône système, choisir **Options → Importer depuis Sticky Notes…**. L’application détecte le format classique `plum.sqlite` et les post-it de la nouvelle expérience OneNote dans `%LocalAppData%\Microsoft\OneNote\16.0\Memory\notes.sdk_*.db`. La base locale la plus récemment modifiée est proposée ; on peut en sélectionner une autre, notamment en présence de plusieurs comptes. Les blocs-notes OneNote `.one` ne sont pas pris en charge.

L’aperçu permet de choisir les notes avant de confirmer. Le texte, les listes et les couleurs sont récupérés ; la première ligne devient le titre. Les notes importées sont accessibles dans **Mes notes**, sans ouvrir toutes leurs fenêtres d’un coup. Les images et la mise en forme enrichie ne sont pas importées. Les anciennes bases `.snt` et les notes uniquement présentes dans le cloud ne sont pas prises en charge.

La base source est ouverte en lecture seule. Les notes supprimées ou vides sont ignorées. Le réimport ignore les identifiants déjà importés, même si leurs copies locales ont été modifiées ou mises à la corbeille : il ne synchronise pas les modifications ultérieures de Windows.

En cas de fichier verrouillé, fermer Sticky Notes puis réessayer. Pour utiliser une copie, copier la base après fermeture de Sticky Notes, en conservant à côté ses fichiers `plum.sqlite-wal` et `plum.sqlite-shm` s’ils existent.

## Stockage local

Les notes sont stockées dans `%LocalAppData%\BetterStickyNotes\notes.json`, avec une sauvegarde `.bak`. Le programme est portable ; les données restent attachées au compte Windows. Le menu système ouvre ce dossier. Pour transférer les notes, quitter l’application sur les deux postes et copier ce dossier. Pas de compte ni de synchronisation réseau.

L’enregistrement est automatique après une courte pause de saisie. Une copie précédente est conservée et les écritures remplacent le fichier de façon atomique. Si le fichier principal est illisible, l’application tente la sauvegarde et conserve le fichier endommagé.

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
dotnet run --project tests/SmokeTests.csproj -- --benchmark 38
dotnet run --project tests/SmokeTests.csproj -- --benchmark 100
./publish.ps1
```

Les tests couvrent la sauvegarde, la récupération, les positions des tâches Markdown, le filtrage des liens et la construction WPF. Vérifier manuellement le menu système, le premier plan entre applications, le démarrage Windows et les configurations multi-écrans avant diffusion plus large.
