# Codex-Journal: Windows-11-Dark-Umstellung der Desktop-Oberfläche konsolidieren

## Ziel

Verbliebene inkonsistente Desktop-Farben, Rahmen, Radien und Interaktionszustände bereinigen, ohne Fachlogik oder Architektur zu verändern.

## Umsetzung

Die Desktop-XAML systematisch geprüft und die verbliebenen festen Karten-/Kategorienradien, 1.2px-Rahmen und den pillenförmigen Preview-Schließenbutton auf die vorhandenen Windows-11-Designressourcen vereinheitlicht. Ausgewählte Auftragskarten verwenden nun 1px-Rahmen; Status- und Schreibtisch-Dokumentflächen bleiben als fachliche Sonderfarben bestehen. Projektstatus und Laufdokumentation aktualisiert.

## Geänderte Dateien

- MainWindow.axaml
- docs/PROJEKTSTATUS.md
- docs/codex_journal/2026-07-11_20-54_dark-konsolidierung.md
- docs/codex_last_run.md
- docs/NEXT_TASK.md

## Tests

- `git status --short` und Branchprüfung erfolgreich.
- Direkte Farb-/Brush-/Radius-Suche über Desktop-XAML und C# durchgeführt.
- `git diff --check` erfolgreich.
- `dotnet build` erfolgreich; nur vorhandene NuGet-Netzwerkwarnungen.
- `dotnet run` als realer Desktop-Starttest erfolgreich gestartet und kontrolliert beendet.
- Keine Datenmodell-, Persistenz-, Netzwerk-, Installer- oder Releaseänderungen.
- Commit 65814f9 auf codex/work erstellt, gepusht und Draft-PR #1 aktualisiert.

## Ergebnis

Die verbliebenen klaren Desktop-Rahmenausreißer sind auf zentrale Windows-11-Ressourcen zurückgeführt; der bestehende Draft-PR #1 wurde mit der visuellen Nachbesserung aktualisiert.

## Bekannte offene Punkte

- Fachliche Sonderflächen für Schreibtisch-Dokumente und mobile Statusbadges verwenden weiterhin eigene Kontrastfarben.
- Die visuelle Prüfung erfolgte per Ressourcen-/XAML-Inspektion und Starttest; eine pixelgenaue Screenshot-Abnahme ist nicht Bestandteil dieses Laufs.

## Aktueller Git-Status

```text
Arbeitsbaum sauber nach dem Push von codex/work.
```
