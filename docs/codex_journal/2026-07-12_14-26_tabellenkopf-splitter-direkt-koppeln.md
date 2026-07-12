# Codex-Journal: Kopf-Splitter direkt mit Tabellenzeilen koppeln

## Ziel

Sicherstellen, dass ein Ziehen des sichtbaren Tabellenkopf-Splitters nicht nur den Titelbereich, sondern auch die tatsächlichen Datenzeilen verbreitert oder verschmälert.

## Umsetzung

Die Breitenpersistenz wird nun direkt am jeweiligen `GridSplitter` behandelt. Dadurch wird der Handler auch ausgeführt, wenn Avalonia das Pointer-Ereignis am Splitter abfängt und nicht bis zum äußeren Header-Grid weiterleitet. Die bestehende zentrale Speicherung und anschließende `RefreshTableProjection()` aktualisieren damit Kopf und Datenzeilen gemeinsam.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`

## Tests

- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `./scripts/run-macos-bundle.sh Debug`: erfolgreich.
- Realer Desktopstart: erfolgreich.
- Auftragsliste geöffnet und den sichtbaren Splitter zwischen Kunde und Ort gezogen.
- Sichtprüfung: Kopfgrenze und Datenzeilengrenze bewegen sich gemeinsam; die Datenzeilen übernehmen die neue Breite unmittelbar.

## Ergebnis

Die tatsächlichen Tabellenzellen folgen jetzt zuverlässig der Breitenänderung des Kopf-Splitters.

## Bekannte offene Punkte

- Keine offenen Punkte zu diesem Fehler.

## Aktueller Git-Status

```text
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
?? docs/codex_journal/2026-07-12_14-26_tabellenkopf-splitter-direkt-koppeln.md
```
