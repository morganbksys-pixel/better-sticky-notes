# Better Sticky Notes — MVP

Application Windows 10/11 (x64), en C# / .NET 10 et WPF. Notes colorées indépendantes, épinglage au premier plan, Markdown, sauvegarde automatique et icône dans la zone de notification.

## Lancer la version portable

Extraire entièrement `BetterStickyNotes-win-x64.zip`, puis lancer `BetterStickyNotes.exe`. Aucun installateur ni installation de .NET nécessaire pour cette distribution autonome.

- Déplacer une note par sa barre supérieure ; redimensionner par les bords.
- Fenêtres aux coins arrondis et au fond extérieur transparent. Glisser le titre ou la poignée à gauche pour déplacer la note.
- Double-cliquer sur le titre (ou utiliser `F2` / **Renommer la note**) pour le modifier. Entrée ou un clic ailleurs termine l’édition ; la poignée reste disponible pendant la saisie.
- **Punaise** : activer ou désactiver le premier plan. Elle est droite et surlignée lorsque la note est épinglée, inclinée et atténuée sinon.
- **Œil / stylo**, dans le bandeau à gauche de la punaise : aperçu / modification, avec une infobulle au survol.
- **⋯** regroupe les autres actions : nouvelle note, renommer, couleur, masquer (œil barré) et corbeille avec confirmation.
- Les nouvelles notes apparaissent en bas à gauche de l’écran où se trouve la souris, au-dessus de la barre des tâches.
- Les cases à cocher sont interactives dans l’aperçu. Masquer une note laisse l’application dans la zone de notification.
- Icône jaune : clic ou double-clic pour afficher/masquer (une seule bascule), clic droit pour toutes les actions. Créer une note via **Nouvelle note** dans un menu.
- Le menu système présente les essentiels : **Nouvelle note**, **Afficher / Masquer les notes**, **Mes notes**, **Options**, **Quitter**. **Options** regroupe la corbeille, le démarrage Windows et le dossier des données.
- Raccourcis dans une note : `Ctrl+N`, `Ctrl+Entrée`, `Ctrl+S`.
- Relancer le programme déjà ouvert affiche les notes existantes.

Pour le démarrage automatique, cocher **Options → Démarrer avec Windows** dans le menu système. L’option est désactivée initialement. Elle utilise la clé Run du compte Windows, sans droits administrateur. Après déplacement de l’exécutable, décocher puis recocher l’option pour mettre à jour son chemin. Décocher avant de supprimer l’application.

## Données

Les notes sont stockées dans `%LocalAppData%\BetterStickyNotes\notes.json`, avec une sauvegarde `.bak`. Le programme est portable ; les données restent attachées au compte Windows. Le menu système ouvre ce dossier. Pour transférer les notes, quitter l’application sur les deux postes et copier ce dossier. Pas de compte ni de synchronisation réseau.

L’enregistrement est automatique après une courte pause de saisie. Une copie précédente est conservée et les écritures remplacent le fichier de façon atomique. Si le fichier principal est illisible, l’application tente la sauvegarde et conserve le fichier endommagé.

## Markdown pris en charge

Titres, gras, italique, barré, listes, tâches, liens HTTP/HTTPS/mailto, citations et code. Le HTML est affiché comme texte ; les images distantes et les tableaux ne sont pas rendus dans ce MVP.

## Développement

Prérequis : Windows et SDK .NET 10.

```powershell
dotnet build BetterStickyNotes.csproj
dotnet run --project tests/SmokeTests.csproj
./publish.ps1
```

Les tests couvrent la sauvegarde, la récupération, les positions des tâches Markdown, le filtrage des liens et la construction WPF. Vérifier manuellement le menu système, le premier plan entre applications, le démarrage Windows et les configurations multi-écrans avant diffusion plus large.
