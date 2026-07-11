# Codex-Journal: Dokumentationsautomatisierung für größere Codex-Aufträge

## Ziel

Einen kleinen lokalen Runner mit Vorlage und Dry-Run schaffen, der Journal, letzten Lauf und genau eine nächste Aufgabe einheitlich erzeugt.

## Umsetzung

Ein Bash-Skript mit Pflichtabschnitten, Journal-Kollisionsschutz, optionalem Projektstatus-Update und ausschließlich dokumentationsbezogenen Schreibvorgängen ergänzt. Eine Markdown-Vorlage und der genaue Aufruf wurden in AGENTS.md dokumentiert.

## Geänderte Dateien

- scripts/update-codex-documentation.sh
- docs/codex_run_template.md
- AGENTS.md
- docs/codex_journal/2026-07-11_13-27_dokumentationsautomatisierung.md
- docs/codex_last_run.md
- docs/NEXT_TASK.md

## Tests

- Dry-Run mit docs/codex_run_template.md erfolgreich.
- Kontrollierter Lauf mit dieser Beispieldatei erfolgreich.
- Journal-Kollisionsschutz und NEXT_TASK-Struktur werden anschließend geprüft.
- Keine Git-Schreibbefehle ausgeführt.

## Ergebnis

Die vorgeschriebene Dokumentation kann jetzt mit einem nachvollziehbaren lokalen Befehl aktualisiert werden.

## Bekannte offene Punkte

- Der Runner bewertet fachliche Änderungen nicht automatisch; PROJEKTSTATUS.md wird nur mit ausdrücklich übergebener Statusdatei aktualisiert.

## Aktueller Git-Status

```text
 M AGENTS.md
 M App.axaml
 M App.axaml.cs
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M Services/AppSettingsService.cs
 M docs/DESIGN_RICHTLINIEN.md
 M iPad/BueroCockpitSnapshotReader/BueroCockpitSnapshotReader/SnapshotRootView.swift
?? docs/NEXT_TASK.md
?? docs/codex_journal/
?? docs/codex_last_run.md
?? docs/codex_run_template.md
?? macOS/
?? scripts/run-macos-bundle.sh
?? scripts/update-codex-documentation.sh
```
