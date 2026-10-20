# DropSorter

A small always-on-top widget for Windows. Drag files onto it and they're moved into folders based on
rules (file type and/or words in the file name). Every move shows up in a history list with an Undo button.

## Using it
- Drag the widget by its background; it snaps to screen edges.
- **History ▾** shows recent moves; **Undo** puts a file back (renamed if the name is taken).
- **⚙** edits rules. First match wins; extensions and keywords are comma-separated; if both are set, both must match.
- The yellow button hides the widget to the tray; the red button quits. Use the tray icon (double-click or right-click) to bring it back, open settings, or exit.
- While you drag it, the shadow lifts and the window wobbles slightly.
- Settings and history live in `%AppData%\DropSorter\` (`config.json`, `history.json`).

Unsigned builds show a Windows SmartScreen warning: *More info → Run anyway*. You can safely ignore this warning; it simply means I haven't paid for a signed certificate.
