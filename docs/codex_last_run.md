# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-12 21:08 +0200

## Letzter Auftrag

Kopfclick-Sortierung für die kompakten Tabellen

## Zusammenfassung

Die drei kompakten Tabellen unterstützen jetzt zuverlässige Kopfklick-Sortierung. Sortieren, Verschieben und Resize sind getrennte Bedienhandlungen; Sortierwerte bleiben je Ansicht persistent.

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

## Git-Status

```text
 M MainWindow.axaml.cs
 M Services/AppSettingsService.cs
 M docs/DESIGN_RICHTLINIEN.md
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? docs/codex_journal/2026-07-12_21-08_spaltenkopf-sortierung.md
```

## Branch
codex/work

## Commit
01645215bb595beb49a0eb9c75d6390019b5a192

## Push erfolgreich
Ja

## Offene Punkte

- Produktive mutierende Status-/Monteurtests wurden weiterhin nicht ausgeführt; sie gehören nicht zu diesem Layout- und Sortierauftrag.

## Empfohlener nächster Schritt

Keine weitere Sortier- oder Tabellenänderung; als nächstes kann ein isolierter Testdatenbestand für mutierende Workflow-Speichertests vorbereitet werden.

1. Temporären lokalen Testdatenordner bereitstellen.
2. Status- und Technikeränderungen ohne Produktivdaten testen.
