# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-12 13:49 +0200

## Letzter Auftrag

Status-, Termin- und Monteurkorrekturen für die kompakte Vorgangsansicht

## Zusammenfassung

Statusfolge, Terminfilter, deduplizierte Sortierung, leere Monteurzuordnung, Splitterverhalten und lokale Layoutdaten sind kompatibel erweitert. Keine destruktive Migration oder Änderung bestehender Kategorien und Zuordnungen wurde vorgenommen.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml`
- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`
- `/Users/christian/AppProjekte/BueroCockpit/Models/TaskItem.cs`
- `/Users/christian/AppProjekte/BueroCockpit/Services/AppSettingsService.cs`
- `/Users/christian/AppProjekte/BueroCockpit/docs/DESIGN_RICHTLINIEN.md`

## Tests

- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `git diff --check`: erfolgreich.
- macOS-Bundle-Erzeugung und Startpfad über `./scripts/run-macos-bundle.sh Debug`: erfolgreich.
- Frühere sichtbare Abnahme bestätigt: grobe Navigation, kompakte Auftragsliste, Kundenname, sichtbarer Splitter und Terminfilter.
- Statische Prüfung: leere Monteurzuordnung bleibt leer; WorkflowStep ist gemeinsame sichtbare Statusquelle; gespeicherte WorkflowStep-Werte werden nicht erneut aus Kategorien überschrieben.
- Sichtbare Computer-Use-Folgeprüfung durch automatische Mac-Sperre verhindert.

## Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M Models/TaskItem.cs
 M Services/AppSettingsService.cs
 M docs/DESIGN_RICHTLINIEN.md
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? docs/codex_journal/2026-07-12_13-49_status-termine-leer-monteur.md
```

## Branch
codex/work

## Commit
dcc94d0655bbef07e7321f477b54cfa11fa23816

## Push erfolgreich
Ja

## Offene Punkte

- Echte per-Maus-Spaltenbreiten-Handles und per Drag-and-drop verschiebbare Spaltenreihenfolge sind noch nicht vollständig umgesetzt.
- Spaltenlayouts werden strukturell getrennt gespeichert; die vollständige visuelle Anwendung von Breiten, Reihenfolge und Sichtbarkeit auf jede einzelne Tabellenzeile steht noch aus.
- Mutierende End-to-End-Neustarttests für Status und Monteur wurden nicht auf Produktivdaten ausgeführt.
- Sichtprüfung nach der letzten Änderung war wegen der gesperrten macOS-Sitzung nicht möglich.

## Empfohlener nächster Schritt

Die drei Tabellenansichten mit echten Spaltenhandles, Drag-and-drop-Reihenfolge und vollständig angewandter Layoutpersistenz fertigstellen.

1. Gemeinsame Spaltendefinitionen für Auftrag, Angebot und Termin anlegen.
2. Breitenhandles, Drag-and-drop-Reihenfolge und Kontextmenüaktionen an dieselbe Definition binden.
3. Layout nach Neustart visuell in allen drei Ansichten prüfen.
