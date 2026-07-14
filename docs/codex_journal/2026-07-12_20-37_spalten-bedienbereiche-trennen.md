# Codex-Journal: Spaltenverschieben und Spaltenbreitenänderung in den kompakten Tabellen trennen

## Ziel

Die Bedienkonflikte zwischen Spaltenverschieben und Spaltenbreitenänderung dauerhaft beseitigen und alle drei Tabellenansichten sichtbar abnehmen.

## Umsetzung

Die bisherigen GridSplitter-Ereignisse wurden durch eigene, 12 px breite Resize-Griffe an jeder sichtbaren Spalte ersetzt. Der normale Kopfbereich verschiebt eine Spalte; der rechte Randbereich startet ausschließlich die Breitenänderung. Die Breite wird während der Pointer-Bewegung live in der Kopf-Griddefinition und in allen `TableCellItem`-Zeilen aktualisiert. Mindestbreiten gelten je Spaltentyp, auch für die letzte sichtbare Spalte und die zusätzliche Titelspalte. Die Breite wird nach dem Loslassen getrennt je Ansicht gespeichert. Kopf und Zeilen liegen in einem gemeinsamen horizontalen ScrollViewer. Für die sichtbare Abnahme wurde zusätzlich eine kompakte Toolbar-Aktion zum Ein-/Ausblenden der Titelspalte ergänzt.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml`
- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`
- `/Users/christian/AppProjekte/BueroCockpit/docs/PROJEKTSTATUS.md`
- Dokumentationsdateien werden durch den vorgeschriebenen Runner aktualisiert.

## Tests

- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `./scripts/run-macos-bundle.sh Debug`: erfolgreich.
- Realer Desktopstart des aktuellen Bundles: erfolgreich; Start in Übersicht.
- Aufträge: zwei Spalten verbreitert/verkleinert, Spalte verschoben, danach erneut Breite geändert, Titel eingeblendet und Titelbreite geändert, horizontal gescrollt, Neustartpersistenz von Reihenfolge/Breite geprüft.
- Angebote: zwei Spalten verbreitert/verkleinert, Spalte verschoben, danach erneut Breite geändert, horizontaler Tabellenbereich geprüft, Neustartpersistenz von Reihenfolge/Breite geprüft.
- Termine: zwei Spalten verbreitert/verkleinert, Spalte verschoben, danach erneut Breite geändert, horizontal gescrollt, Neustartpersistenz von Reihenfolge/Breite geprüft.
- Kopf- und Datenzeilen: sichtbare Spaltengrenzen blieben nach den Änderungen gemeinsam ausgerichtet.
- Produktive Aufträge, Kategorien, Statuswerte und sonstige Fachdaten wurden nicht verändert.

## Ergebnis

Spaltenverschieben und Spaltenbreitenänderung sind in Aufträgen, Angeboten und Terminen getrennt bedienbar. Breitenänderungen wirken live auf Kopf und Datenzellen, bleiben je Ansicht getrennt gespeichert und werden nach Neustart wiederhergestellt.

## Bekannte offene Punkte

- Produktive mutierende Status-/Monteurtests wurden weiterhin nicht ausgeführt; sie gehören nicht zu diesem Layoutauftrag.

## Aktueller Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? docs/codex_journal/2026-07-12_20-37_spalten-bedienbereiche-trennen.md
```
