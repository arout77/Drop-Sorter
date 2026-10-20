# DropSorter

A small always-on-top widget for Windows. Drag files onto it and they're moved into folders based on
rules (file type and/or words in the file name). Every move shows up in a history list with an Undo button.

## Build the .exe

**On your PC** (needs the .NET 8 SDK):

    dotnet publish -c Release

Output: `bin\Release\net8.0-windows\win-x64\publish\DropSorter.exe` (self-contained, no .NET needed to run it).

**With GitHub** (no SDK needed):
1. Push this folder to a GitHub repo (GitHub Desktop works).
2. Open the repo's **Actions** tab. Each push builds the exe; download it from the run's *Artifacts*.
3. For a shareable download link, tag a release: `git tag v1.0.0 && git push origin v1.0.0`.
   The exe appears under the repo's **Releases** page.

## Using it
- Drag the widget by its background; it snaps to screen edges.
- **History ▾** shows recent moves; **Undo** puts a file back (renamed if the name is taken).
- **⚙** edits rules. First match wins; extensions and keywords are comma-separated; if both are set, both must match.
- **–** hides the widget. Use the tray icon (double-click or right-click) to bring it back, open settings, or exit.
- Settings and history live in `%AppData%\DropSorter\` (`config.json`, `history.json`).

Unsigned builds show a Windows SmartScreen warning: *More info → Run anyway*.
