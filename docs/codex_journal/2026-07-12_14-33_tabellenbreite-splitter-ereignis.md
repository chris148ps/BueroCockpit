# Codex-Journal: Behandelte Kopf-Splitter-Ereignisse zuverlässig übernehmen

## Ziel

Spaltenbreiten aus dem Tabellenkopf nach dem Ziehen verlässlich in die echten Datenzeilen übernehmen.

## Umsetzung

Der Avalonia-`GridSplitter` markiert sein Pointer-Release als behandelt. Deshalb erreichte der normale Ereignishandler die Speicherung nicht. Jeder dynamisch erzeugte Splitter registriert die Freigabe nun mit `handledEventsToo: true`. Die Breitenübernahme wird außerdem hinter den Layoutdurchlauf verschoben, damit die tatsächlich vom Splitter berechneten Breiten gespeichert und anschließend in die Zellprojektion übernommen werden.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`

## Tests

- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `./scripts/run-macos-bundle.sh Debug`: erfolgreich.
- Desktopprozess vollständig geschlossen und das neue Bundle gestartet.
- In Aufträge den sichtbaren Splitter zwischen Kunde und Ort gezogen.
- Sichtprüfung: Kunde beginnt nach dem Ziehen im Kopf und in allen Datenzeilen an derselben neuen Position.

## Ergebnis

Die Breite einer Kopfspalte verändert jetzt zuverlässig die zugehörigen Datenzellen in der Tabelle.

## Bekannte offene Punkte

- Keine offenen Punkte zu diesem Fehler.

## Aktueller Git-Status

```text
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
?? docs/codex_journal/2026-07-12_14-33_tabellenbreite-splitter-ereignis.md
```
