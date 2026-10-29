# Repository Guidelines

## Project Structure

Better Sticky Notes targets Windows 10/11 with C#/.NET 10 and WPF. Sources live at the repository root:

- `App*.cs`: lifecycle, navigation, protection, purge, and version information.
- `*Window.xaml` / `*.xaml.cs`: views and code-behind.
- `Storage.cs`, `NoteProtection.cs`, `Markdown*`, `StickyNotesImporter.cs`: persistence, encryption, Markdown, and import.
- `Assets/`: icons; `tests/`: executable regression suite.
- `bin/`, `obj/`, `dist/`: generated output; never commit these directories.

## Development Commands

Use Windows and the .NET 10 SDK:

```powershell
dotnet build BetterStickyNotes.csproj
dotnet run --project BetterStickyNotes.csproj
dotnet run --project tests/SmokeTests.csproj
dotnet run --project tests/SmokeTests.csproj -- --benchmark 38
./publish.ps1
```

These build, launch, test, benchmark 38 notes, and publish the portable Windows x64 application. Quit the running application before testing another build.

## Coding Conventions

Use four-space C# indentation, file-scoped namespaces, PascalCase types/public members, camelCase locals/private fields, and nullable annotations. Follow nearby XAML formatting. Keep user-facing text French. No dedicated formatter/linter is configured. Respect `.gitattributes`: CRLF for C#, XAML, PowerShell; LF for Markdown. Run `git diff --check`.

## Testing and Diagnosis

Tests use a custom STA assertion runner, not xUnit/NUnit. Add `tests/*Tests.cs` suites to `tests/Program.cs`; use temporary data. No coverage percentage is mandated. Distinguish automated results from actual Windows checks: taskbar pinning, tray clicks, session locking, and multi-monitor placement. Investigate unexpected behavior by checking the active executable's version/path and shortcut target before attributing a cause.

## Product Behavior

Manual launch, relaunch, and tray double-click open **Mes notes**. Windows startup uses `--background`. Tray single-click does nothing; its icon stays yellow. List double-click reveals only the targeted note without changing pinning. Preserve native multi-selection. Protected notes remain anonymous in lists/search even when unlocked. Screenshot capture is deferred pending a separate request.

## Versions and Delivery

Use `0.minor.patch`: feature batches increment minor; fixes/small adjustments increment patch. Reserve `1.0.0` for user-approved readiness. Read displayed versions from compiled assembly metadata. Publishing retains at most three versions in `dist/`; provide the published executable's path.

## Commits and Pull Requests

Use concise English summaries, e.g. `Release v0.9.0: ...`. PRs explain behavior, validation, relevant issues, and include UI screenshots when applicable. Update `README.md` for usage changes.

## Security

Personal data lives in `%LocalAppData%\BetterStickyNotes`. Never commit notes or credentials. Preserve encrypted storage and backup sanitization. Automatic trash purge remains opt-in.
