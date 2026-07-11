# Codex-Journal: Lokale fachliche Desktop-, iPad- und macOS-Änderungen vollständig auf codex/work veröffentlichen

## Ziel

Den tatsächlichen lokalen Entwicklungsstand prüfen, dokumentieren und alle fachlich zugehörigen Änderungen im bestehenden Draft-PR #1 sichtbar machen.

## Umsetzung

Desktop-Kategorien, aggregierte Zähler, Detailformular, Schreibtisch-Notizzettel, Autospeicher- und Designänderungen sowie die iPad-Navigation und das macOS-Bundle wurden geprüft. Die bestehende Dokumentation wurde aktualisiert; PROJEKTSTATUS.md wurde wegen des geänderten fachlichen Entwicklungsstands ergänzt. Alle zu diesem Auftrag gehörenden Dateien werden anschließend explizit mit --include auf codex/work veröffentlicht.

## Geänderte Dateien

- App.axaml
- App.axaml.cs
- MainWindow.axaml
- MainWindow.axaml.cs
- Services/AppSettingsService.cs
- docs/DESIGN_RICHTLINIEN.md
- docs/PROJEKTSTATUS.md
- iPad/BueroCockpitSnapshotReader/BueroCockpitSnapshotReader/SnapshotRootView.swift
- macOS/Info.plist
- scripts/run-macos-bundle.sh
- docs/codex_journal/2026-07-11_20-33_fachliche-aenderungen.md
- docs/codex_last_run.md
- docs/NEXT_TASK.md

## Tests

- `git status --short` und Branchprüfung erfolgreich.
- `bash -n scripts/run-macos-bundle.sh` erfolgreich.
- `bash -n scripts/update-codex-documentation.sh scripts/publish-codex-work.sh` erfolgreich.
- `dotnet build` erfolgreich; nur vorhandene NuGet-Netzwerkwarnungen.
- `xcodebuild -quiet ... CODE_SIGNING_ALLOWED=NO build` erfolgreich.
- `dotnet run` als realer Desktop-Starttest erfolgreich gestartet und kontrolliert beendet.
- Keine neuen offensichtlichen Debugreste oder ungenutzten Projektdateien festgestellt.

## Ergebnis

Der fachliche lokale Entwicklungsstand ist geprüft, dokumentiert und wird vollständig im bestehenden Draft-PR #1 auf codex/work veröffentlicht.

## Bekannte offene Punkte

- Der echte lokale Netzwerk-Sync bleibt deaktiviert.
- Nicht ausgewählte lokale Änderungen außerhalb der fachlich geprüften Pfade bleiben bewusst uncommitted.
- Die Semantik bestehender Diagnose-Logs wurde nicht verändert.

## Aktueller Git-Status

```text
 M App.axaml
 M App.axaml.cs
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M Services/AppSettingsService.cs
 M docs/DESIGN_RICHTLINIEN.md
 M docs/NEXT_TASK.md
 M iPad/BueroCockpitSnapshotReader/BueroCockpitSnapshotReader/SnapshotRootView.swift
?? docs/PROJEKTSTATUS.md
?? docs/codex_journal/2026-07-11_20-33_fachliche-aenderungen.md
?? macOS/
?? scripts/run-macos-bundle.sh
```
