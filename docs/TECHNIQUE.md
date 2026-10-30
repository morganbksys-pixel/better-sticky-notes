# Documentation technique

[Retour au README](../README.md)

Ce document décrit les choix techniques et les limites de l’application publiée en version **0.21.4**, dont le repli, la recherche dans un post-it et les captures d’écran.

## Architecture

Application Windows 10/11 en C# / .NET 10 et WPF, publiée pour Windows x64. Markdig analyse le Markdown ; Microsoft.Data.Sqlite lit les bases à importer. Les sources se trouvent principalement à la racine.

| Sources | Responsabilité |
| --- | --- |
| [App.xaml.cs](../App.xaml.cs) et `App.*.cs` | Cycle de vie, sauvegarde, navigation, groupes, protection, corbeille et captures |
| [NoteWindow.xaml](../NoteWindow.xaml) et `NoteWindow.*.cs` | Post-it, édition, aperçu, recherche, repli et images |
| [NotesWindow.xaml](../NotesWindow.xaml) et `NotesWindow.*.cs` | Gestion des notes, recherche, sélection multiple et galerie |
| [Storage.cs](../Storage.cs) | Modèles, formats JSON, chargement et sauvegarde |
| [NoteProtection.cs](../NoteProtection.cs) | Dérivation de clé et chiffrement |
| [MarkdownView.cs](../MarkdownView.cs), [MarkdownEditing.cs](../MarkdownEditing.cs) | Rendu WPF et outils d’édition Markdown |
| [StickyNotesImporter.cs](../StickyNotesImporter.cs) | Lecture des bases Sticky Notes et OneNote |
| [ScreenCapture.cs](../ScreenCapture.cs), [NoteStore.Captures.cs](../NoteStore.Captures.cs) | Capture d’écran et stockage des PNG |
| [tests/](../tests/) | Suite de régression avec exécution STA |

## Stockage et compatibilité

Les données de l’utilisateur sont dans `%LocalAppData%\BetterStickyNotes`. Le programme est portable, mais les données restent liées au compte Windows, indépendamment du dossier de l’exécutable.

| Élément | Contenu |
| --- | --- |
| `notes.json` | Notes, groupes, états des fenêtres et références aux captures |
| `notes.json.bak` | Copie de secours des notes et groupes |
| `settings.json` | Délai facultatif de purge de la corbeille |
| `captures/` | Images PNG, nommées à partir des identifiants de note et de capture |
| `notes.json.damaged-*` | Fichier illisible conservé lors d’une récupération |

Le chargement accepte l’ancien tableau JSON de notes et les documents versionnés. Le format 1 rassemble les notes et les groupes. Une sauvegarde contenant des captures utilise le format 2 ; les PNG restent dans des fichiers séparés. Une sauvegarde sans capture utilise le format 1.

**Ne pas rouvrir les données avec une version antérieure qui ne connaît pas leur format.** Pour sauvegarder ou transférer les données, quitter l’application et copier le dossier entier. La copie `.bak` référence les mêmes PNG que le fichier principal : elle ne constitue pas une seconde copie des images.

### Enregistrement et récupération

Les modifications déclenchent normalement une sauvegarde après 450 ms sans nouveau changement. Les valeurs des notes sont copiées depuis le fil de l’interface ; la sérialisation et les écritures sont ensuite exécutées en arrière-plan, dans l’ordre.

L’écriture passe par un fichier temporaire puis remplace le fichier principal. En fonctionnement courant, `.bak` conserve la version précédente. Avec des notes protégées ou après une opération sensible, elle reçoit la même version que le fichier principal pour ne pas conserver les anciens contenus confidentiels ou supprimés.

Si le fichier principal est illisible, l’application tente de charger `.bak`, conserve le fichier endommagé puis restaure le fichier principal. Si la récupération échoue, le chargement s’arrête pour préserver les données.

Quitter attend la dernière sauvegarde ; en cas d’échec, l’application reste ouverte et signale le problème. Les modifications non enregistrées restent en mémoire et une nouvelle tentative est programmée.

## Protection des notes

Le titre et le contenu sont chiffrés avec AES-256-GCM. La clé de 256 bits est dérivée du mot de passe par PBKDF2-SHA256, avec 600 000 itérations et un sel aléatoire de 16 octets. Chaque chiffrement utilise un nouveau nonce de 12 octets et un tag de 16 octets ; l’identifiant de la note sert de donnée authentifiée associée.

La première protection définit un mot de passe commun d’au moins huit caractères. Les protections suivantes utilisent ce mot de passe tant qu’une note protégée subsiste, y compris dans la corbeille. Après retrait de toutes les protections ou suppression définitive de toutes les notes protégées, un nouveau mot de passe peut être choisi.

Le mot de passe et les clés ne sont pas enregistrés sur disque. Une note déverrouillée conserve son contenu accessible en mémoire, mais ses sauvegardes restent chiffrées. Le verrouillage efface le titre et le texte du modèle, vide les contrôles et réinitialise l’historique d’annulation.

- Masquer ou fermer une note protégée la reverrouille. Le verrouillage de session, la déconnexion et la déconnexion d’une session distante verrouillent aussi les notes protégées.
- Une note verrouillée apparaît comme **Note protégée** et ne participe pas aux recherches non vides. Déverrouillée, elle affiche son titre et son extrait et participe aux recherches. Le reverrouillage actualise immédiatement les listes.
- Les identifiants, couleurs, positions, dimensions, groupes et autres métadonnées restent lisibles. Les noms des groupes ne doivent donc pas contenir d’informations confidentielles.
- Les captures ne sont pas chiffrées. Leur ajout aux notes protégées est interdit ; les captures existantes doivent être retirées avant de protéger une note.

**Un mot de passe oublié ne peut pas être récupéré.** La protection et la suppression définitive assainissent la copie `.bak` après sauvegarde réussie, mais ne modifient ni les copies externes, ni les anciens fichiers `.damaged-*`, ni la base d’import d’origine. Elles ne réalisent pas un effacement sécurisé du disque ou du presse-papiers.

## Corbeille et purge

La mise à la corbeille est immédiate. La restauration conserve le groupe et les captures et laisse le post-it masqué. Supprimer un groupe réaffecte ses notes à **Sans groupe**, y compris celles de la corbeille, sans les supprimer.

La suppression définitive manuelle demande confirmation. **Vider la corbeille** cible toute la corbeille, même lorsqu’une recherche est active. Une sauvegarde réussie retire également ces notes de `.bak`, puis les PNG concernés sont supprimés.

La purge automatique est désactivée par défaut. Le réglage accepte de 1 à 36 500 jours et s’applique à partir de la date de mise à la corbeille, indépendamment de la recherche et de la sélection. Une fois activée, la purge s’exécute sans confirmation à l’application du réglage, au démarrage puis toutes les heures tant que l’application tourne.

Restaurer puis remettre une note à la corbeille réinitialise le délai. Une ancienne note supprimée sans date reçoit une date lors de son chargement, ensuite enregistrée. Un réglage absent ou illisible laisse la purge désactivée.

## Captures et images

La capture choisit l’écran contenant la plus grande partie du post-it, sans dépendre de la souris. Elle prend l’écran entier, barre des tâches comprise, à sa résolution native. Les fenêtres de l’application qui recouvrent cet écran deviennent brièvement transparentes ; leur opacité et leur capacité à recevoir les interactions sont restaurées même en cas d’erreur.

Les images sont enregistrées en PNG. La suppression d’une capture actualise d’abord les références dans les notes et la copie de secours, puis supprime son fichier après écriture réussie. La corbeille conserve les images jusqu’à la suppression définitive de leur note.

Le lecteur propose un ajustement à la fenêtre, un affichage à 100 %, un zoom manuel de 1 % à 800 % autour du pointeur et un déplacement par glissement. La navigation précédente/suivante boucle sur les captures de la note ou sur la liste de la galerie au moment de l’ouverture. Chaque nouvelle image repart en affichage ajusté.

La galerie référence les images existantes sans les dupliquer. Dans la corbeille, leur consultation et leur export restent possibles ; il faut restaurer la note pour modifier ses captures. Le repli de la section des captures est enregistré séparément du repli du post-it.

Les boutons **Notes actives / Corbeille** filtrent la galerie. La sélection multiple utilise les conventions Windows : Ctrl+clic, Maj+clic, flèches, Ctrl+A et Échap. Entrée ouvre une capture ; Ctrl+C copie une image ou les fichiers sélectionnés ; Ctrl+S enregistre une image ou exporte la sélection dans un dossier sans écraser les fichiers existants. Suppr demande confirmation du nombre de captures, puis retire uniquement les images, en conservant les notes. Une actualisation conserve la sélection ; changer de filtre l’efface.

## Import Sticky Notes / OneNote

La détection recherche ces bases et propose la plus récemment modifiée :

```text
%LocalAppData%\Packages\Microsoft.MicrosoftStickyNotes_8wekyb3d8bbwe\LocalState\plum.sqlite
%LocalAppData%\Microsoft\OneNote\16.0\Memory\notes.sdk_*.db
```

La base est ouverte en lecture seule. Les notes supprimées ou vides sont ignorées. Les identifiants déjà présents sont exclus, même si la copie locale a été modifiée ou mise à la corbeille. L’import ne synchronise pas les modifications ultérieures de la source.

Le texte, les listes et les couleurs sont récupérés. Le titre provient du titre OneNote lorsqu’il existe, sinon de la première ligne non vide ; les titres trop longs sont tronqués. Les images, la mise en forme enrichie, les bases `.snt`, les blocs-notes `.one` et les notes uniquement dans le cloud ne sont pas pris en charge.

Fermer Sticky Notes / OneNote avant l’import. Pour travailler sur une copie de base, conserver à côté ses fichiers associés `-wal` et `-shm` s’ils existent. Les notes importées sont initialement masquées et sélectionnées dans **Mes notes**.

## Fenêtres, Markdown et performances

Le lancement manuel et la relance ouvrent **Mes notes**, sans restaurer les post-it. Une seconde instance réveille la fenêtre de gestion de l’instance active. Le démarrage Windows utilise `--background` via l’entrée `BetterStickyNotes` de `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, sans droits administrateur. Une entrée existante est actualisée vers le chemin du nouvel exécutable lors du lancement ; l’option reste facultative.

Les ouvertures de groupes sont progressives pour laisser l’interface répondre. Le double-clic dans la liste révèle et déplie uniquement la note ciblée, sans changer son épinglage. Les déplacements ne reconstruisent pas la liste.

Mes notes affiche les notes datées de la plus récente à la plus ancienne. Les anciennes notes sans date restent ensuite dans leur ordre enregistré. Les nouvelles notes enregistrent leur date de création ; les imports conservent celle de la source lorsqu’elle est disponible. L’œil ouvert indique une note visible, l’œil barré une note masquée ; un cadenas unique remplace cet indicateur pour les notes protégées. Le bouton de masquage du post-it et les menus utilisent la même paire d’yeux.

Les aperçus Markdown sont libérés au masquage. Le cache retient au maximum cinq fenêtres masquées, pendant environ une minute ; le nettoyage périodique ferme ensuite leurs contrôles. Le texte, la position, le curseur et la sélection sont conservés pour la réouverture. **L’historique Ctrl+Z est perdu lorsqu’une fenêtre sort du cache.** Les notes protégées sont fermées et nettoyées dès leur verrouillage.

Le rendu prend en charge titres, gras, italique, barré, listes, tâches interactives, citations et code. Seuls les liens explicites HTTP, HTTPS et mailto sont ouverts. Le HTML reste du texte ; les images Markdown et les tableaux ne sont pas rendus. Les captures jointes utilisent un affichage distinct du Markdown. Le correcteur orthographique est actif dans les notes visibles en édition et désactivé en aperçu ou au masquage.

## Développement et validation

Prérequis : Windows et SDK .NET 10. Quitter l’application avant de tester une autre compilation.

```powershell
dotnet build BetterStickyNotes.csproj
dotnet run --project BetterStickyNotes.csproj
dotnet run --project tests/SmokeTests.csproj
dotnet run --project tests/SmokeTests.csproj -- --quiet-ui
dotnet run --project tests/SmokeTests.csproj -- --note-tools
dotnet run --project tests/SmokeTests.csproj -- --captures
dotnet run --project tests/SmokeTests.csproj -- --benchmark 38
dotnet run --project tests/SmokeTests.csproj -- --benchmark 100
git diff --check
```

Les tests utilisent un exécutable STA avec assertions et des données temporaires, sans xUnit ni NUnit. Ajouter les suites dans `tests/Program.cs`.

| Mode | Portée et effets visibles |
| --- | --- |
| Suite complète | Persistance, récupération, protection, groupes, import, Markdown et interface ; peut afficher et activer des fenêtres ou dialogues |
| `--quiet-ui` | Vérifications de menus, disposition et modèles, sans afficher ni activer de fenêtre |
| `--note-tools` | Recherche interne et repli, avec fenêtres de test transparentes ; peut activer une fenêtre |
| `--captures` | Stockage PNG, gestion, lecteur, galerie et restauration des fenêtres ; pixels synthétiques uniquement, peut activer une fenêtre |
| `--benchmark N` | Mesures de recherche, affichage, masquage et mémoire sur N notes synthétiques |

La suite complète est à lancer avec accord pour les fenêtres qu’elle peut afficher. Les tests automatisés ne remplacent pas les vérifications Windows de l’épinglage à la barre des tâches, des clics sur l’icône système, du démarrage automatique, du verrouillage de session ou du placement multi-écrans. Le rendu des captures réelles, les écrans à échelles différentes et le confort du zoom et du glissement se vérifient manuellement.

Avant d’attribuer un comportement à une version, vérifier le chemin de l’exécutable actif et la cible du raccourci. La version affichée dans **Mes notes** et l’infobulle système provient des métadonnées de l’assembly compilé, via [AppInfo.cs](../AppInfo.cs).

## Publication

```powershell
./publish.ps1
```

Le script publie une application Windows x64 autonome et produit :

- `dist/BetterStickyNotes-<version>/BetterStickyNotes.exe` ;
- `dist/BetterStickyNotes-<version>-win-x64.zip` ;
- `dist/SHA256SUMS.txt`, avec les empreintes SHA-256 des archives.

Après publication réussie, il conserve au maximum trois versions locales et nettoie les anciennes distributions. Il ne modifie pas les données personnelles dans `%LocalAppData%`. Les dossiers `bin/`, `obj/` et `dist/` ne doivent pas être commités.

Les versions suivent `0.mineure.correctif` : lot de fonctionnalités → version mineure ; correction ou petit ajustement → correctif. La version `1.0.0` reste réservée à une validation explicite de la maturité de l’application.
