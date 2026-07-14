# Codex-Journal: Kopfclick-Sortierung für die kompakten Tabellen

## Ziel

Aufträge, Angebote und Termine per Klick auf die jeweilige Spalte typgerecht, stabil und getrennt persistiert sortieren können, ohne Verschieben oder Resize zu beeinträchtigen.

## Umsetzung

Die Tabellenköpfe zeigen für die aktive Sortierspalte einen dezenten Auf-/Abwärtspfeil. Ein kurzer Klick sortiert aufsteigend; ein weiterer Klick auf dieselbe Spalte dreht die Richtung um. Ein Klick auf eine andere Spalte beginnt aufsteigend. Die Sortierung verwendet fachliche Statusrangfolgen, case-insensitive Textvergleiche, chronologische Datum-/Uhrzeitwerte, leere Werte am Ende und stabile ID-Tiebreaker. Sortierfeld und Richtung werden in den drei bestehenden TableLayoutSettings getrennt gespeichert. Die vorhandene Bewegungsprüfung verhindert, dass ein Spalten-Drag oder Resize zusätzlich sortiert. Die Designrichtlinien wurden um diese dauerhaften Tabellenregeln ergänzt.

## Geänderte Dateien

- /Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs
- /Users/christian/AppProjekte/BueroCockpit/Services/AppSettingsService.cs
- /Users/christian/AppProjekte/BueroCockpit/docs/DESIGN_RICHTLINIEN.md
- /Users/christian/AppProjekte/BueroCockpit/docs/PROJEKTSTATUS.md
- Dokumentationsdateien werden durch den vorgeschriebenen Runner aktualisiert.

## Tests

- git diff --check: erfolgreich.
- dotnet build: erfolgreich, 0 Warnungen, 0 Fehler.
- ./scripts/run-macos-bundle.sh Debug: erfolgreich.
- Realer Desktopstart des aktuellen Bundles: erfolgreich; Start in Übersicht.
- Aufträge: Status, Kunde, Titel, Termin, Ort und Techniker jeweils angeklickt; Auf-/Abwärtspfeile geprüft; Status in fachlicher Reihenfolge geprüft; Drag ohne Sortiernebenwirkung; Resize ohne Sortiernebenwirkung; erneuter normaler Sortierklick geprüft.
- Angebote: Status, Kunde, Titel, Termin, Ort und Techniker jeweils angeklickt; Angebotsstatusfolge mit Ansicht vor Angebot und Angebot gesendet geprüft.
- Termine: Status, Kunde, Ort, Techniker, Datum und Uhrzeit über beide horizontalen Ansichten angeklickt; chronologische Datums-/Uhrzeitsortierung geprüft; horizontales Scrollen geprüft.
- Titelspalte: in Aufträgen und Angeboten sortiert.
- Neustart: aktive Sortierspalte, Richtungspfeil, Spaltenreihenfolge und getrennte Ansichten geprüft.
- Leere Werte werden durch die Sortierlogik einheitlich zuletzt behandelt.
- Produktive Aufträge, Kategorien, Statuswerte und sonstige Fachdaten wurden nicht verändert.

## Ergebnis

Die drei kompakten Tabellen unterstützen jetzt zuverlässige Kopfklick-Sortierung. Sortieren, Verschieben und Resize sind getrennte Bedienhandlungen; Sortierwerte bleiben je Ansicht persistent.

## Bekannte offene Punkte

- Produktive mutierende Status-/Monteurtests wurden weiterhin nicht ausgeführt; sie gehören nicht zu diesem Layout- und Sortierauftrag.

## Aktueller Git-Status

```text
 M MainWindow.axaml.cs
 M Services/AppSettingsService.cs
 M docs/DESIGN_RICHTLINIEN.md
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? docs/codex_journal/2026-07-12_21-08_spaltenkopf-sortierung.md
```
