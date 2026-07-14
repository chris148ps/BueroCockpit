# Codex-Journal: Status-, Termin- und Monteurkorrekturen für die kompakte Vorgangsansicht

## Ziel

Die kompakte Vorgangsansicht um die verbindliche Angebotsstatusfolge, eine robuste Terminprojektion, leere Monteurzuordnung und lokale Grundlagen für getrennte Tabellenlayouts ergänzen.

## Umsetzung

Der Angebotsablauf enthält Ansicht, Angebot, Angebot gesendet, Auftrag, Material, Termin und Erledigt; der Direktauftrag enthält Auftrag, Material, Termin und Erledigt. Persistierte WorkflowStep-Werte bleiben maßgeblich, nur leere Bestandswerte werden defensiv abgeleitet. Die Terminansicht filtert reale Termine nach Alle, Vergangen, Heute und Zukünftig, dedupliziert nach Vorgangs-ID und sortiert chronologisch. Die Monteurauswahl beginnt mit einem vollständig leeren Eintrag und speichert bei Auswahl eine leere Zuordnung; fehlende Monteure bleiben in den Listen leer. Der Splitter ist in der Übersicht nicht mehr sichtbar. AppSettings enthalten getrennte lokale Tabellenlayoutobjekte für Aufträge, Angebote und Termine sowie lokale Sortierwerte. Horizontales und vertikales Scrollen sind für die Vorgangsliste aktiviert.

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

## Ergebnis

Statusfolge, Terminfilter, deduplizierte Sortierung, leere Monteurzuordnung, Splitterverhalten und lokale Layoutdaten sind kompatibel erweitert. Keine destruktive Migration oder Änderung bestehender Kategorien und Zuordnungen wurde vorgenommen.

## Bekannte offene Punkte

- Echte per-Maus-Spaltenbreiten-Handles und per Drag-and-drop verschiebbare Spaltenreihenfolge sind noch nicht vollständig umgesetzt.
- Spaltenlayouts werden strukturell getrennt gespeichert; die vollständige visuelle Anwendung von Breiten, Reihenfolge und Sichtbarkeit auf jede einzelne Tabellenzeile steht noch aus.
- Mutierende End-to-End-Neustarttests für Status und Monteur wurden nicht auf Produktivdaten ausgeführt.
- Sichtprüfung nach der letzten Änderung war wegen der gesperrten macOS-Sitzung nicht möglich.

## Aktueller Git-Status

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
