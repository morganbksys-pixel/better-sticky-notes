# Repository Guidelines

## Project Structure & Module Organization

Better Sticky Notes is a Windows 10/11 application using C#/.NET 10 and WPF. Source files live at the repository root:

- `App.xaml.cs`, `App.Protection.cs`, and `App.Trash.cs` coordinate windows, tray actions, protection, and retention.
- `NoteWindow.*`, `NotesWindow.*`, and `ImportWindow.*` pair XAML views with code-behind.
- `Storage.cs` handles persistence; `NoteProtection.cs` handles encryption; `Markdown*` and `StickyNotesImporter.cs` handle rendering, editing, and import.
- `Assets/` contains the application icon. `tests/` contains the executable regression suite.
- `bin/`, `obj/`, and `dist/` are generated output; do not commit them.

## Build, Test, and Development Commands

Use Windows with the .NET 10 SDK, from the repository root:

```powershell
dotnet build BetterStickyNotes.csproj
dotnet run --project BetterStickyNotes.csproj
dotnet run --project tests/SmokeTests.csproj
dotnet run --project tests/SmokeTests.csproj -- --benchmark 38
./publish.ps1
```

These commands build, launch, run regression checks, benchmark 38 notes, and publish a self-contained Windows x64 distribution. Quit any running application before launching another build. Publishing uses the project version and retains only the three newest distributions in `dist/`.

## Coding Style & Naming Conventions

Follow existing C# conventions: four-space indentation, file-scoped namespaces, PascalCase types and public members, and camelCase locals and private fields. Keep nullable annotations enabled. Match nearby XAML formatting and French user-facing wording. No dedicated formatter or linter is configured. Respect `.gitattributes`: CRLF for C#, XAML, and PowerShell; LF for Markdown. Run `git diff --check` before committing.

## Testing Guidelines

Tests use a custom STA console runner with assertions, not xUnit/NUnit; use `dotnet run`, not `dotnet test`. Add focused checks in `tests/*Tests.cs` and register new suites in `tests/Program.cs`. No coverage percentage is enforced. Test persistence, recovery, protection, and affected UI behavior using temporary data. Manually check tray interactions, Windows session locking, and multi-monitor placement when relevant.

## Commit & Pull Request Guidelines

History uses concise English summaries, including `Release v0.9.0: ...` and `Prepare v0.1.0 portable release`. Follow that descriptive style; no strict prefix scheme is established. PRs should explain behavior changes, validation, and relevant issues. Include screenshots for UI changes and update `README.md` when usage changes.

## Security & Configuration

Personal data resides in `%LocalAppData%\BetterStickyNotes`, including `notes.json`, its backup, and `settings.json`. Never commit personal notes or credentials. Preserve encrypted storage and backup sanitization, keep automatic purge opt-in, and ensure protected titles and content remain anonymous in management views.
