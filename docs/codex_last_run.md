# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-15 22:31 +0200

## Letzter Auftrag

Konfigurierbare Statuszuordnungen über stabile Kategorie-IDs mit genau einer aktuellen Kategorie und Variant A implementieren.

## Zusammenfassung

Die neue Fachlogik ist implementiert und mit isolierten Daten sowohl automatisiert als auch sichtbar im realen macOS-Bundle geprüft. Zwei dabei gefundene UI-Fehler wurden behoben und erneut erfolgreich bedient: initiale ComboBox-Ereignisse verändern keine Statuszuordnungen mehr, und ein Statuswechsel behält einen noch neuen Vorgang bei der programmatischen Kategorienavigation. Produktivdaten, Cloud-Dateien, Netzwerkdienste, Version, Release und Tags blieben unverändert.

## Geänderte Dateien

- AGENTS.md
- BueroCockpit.csproj
- Data/BueroRepository.cs
- MainWindow.axaml
- MainWindow.axaml.cs
- Models/WorkflowCategoryMapping.cs
- Services/WorkflowCategoryService.cs
- Services/IpadSnapshotExportService.cs
- tests/BueroCockpit.WorkflowTests/BueroCockpit.WorkflowTests.csproj
- tests/BueroCockpit.WorkflowTests/Program.cs
- docs/ARBEITSKATEGORIEN.md
- docs/CODEX_AUFTRAGSPRUEFUNG.md
- docs/CODEX_PROJEKTREGELN.md
- docs/DESIGN_RICHTLINIEN.md
- docs/PROJEKTSTATUS.md
- docs/SETTINGS_KONZEPT.md
- docs/TESTRICHTLINIEN.md
- docs/codex_journal/README.md
- docs/ipad-readonly-preparation.md
- docs/NEXT_TASK.md
- docs/codex_last_run.md
- neuer Eintrag unter docs/codex_journal/

## Tests

- Baseline dotnet build vor der Implementierung: erfolgreich, 0 Warnungen, 0 Fehler.
- dotnet build nach der Implementierung: erfolgreich, 0 Warnungen, 0 Fehler.
- dotnet build -r win-x64: erfolgreich, 0 Warnungen, 0 Fehler; keine reale Windows-Bedienung.
- dotnet run --project tests/BueroCockpit.WorkflowTests/BueroCockpit.WorkflowTests.csproj: erfolgreich. Geprüft wurden stabile ID-Zuordnung, Umbenennen/Verschieben, Ersetzen/Entfernen, fehlende und ausgeblendete Ziele, Variant A mit unverändertem Legacy-Mehrfachdatensatz, genau eine Kategorie nach bewusster Änderung, Typkompatibilität sowie die neuen Snapshot-Felder.
- git diff --check: erfolgreich.
- ./scripts/run-macos-bundle.sh Debug mit explizit temporärem Daten- und lokalem Konfigurationsordner: Bundle erfolgreich gebaut, signiert und Prozess real gestartet.
- Sichtbarer macOS-Bedienrundgang mit isolierten Daten: erfolgreich. Geprüft wurden Statuszuordnungen einschließlich fehlender Zuordnung, Auswahl von Haupt- und Unterkategorien, Neuanlage beider Vorgangstypen, Statuswechsel, kompatibler Typwechsel, manuelle Kategorieauswahl, Vorgangs-Drag-and-drop, Umbenennen und Unterordnen einer gemappten Kategorie, Löschschutz mit Abbruch, Zähler sowie Neustartpersistenz.
- Während des sichtbaren Rundgangs gefundene Fehler wurden behoben und erneut geprüft: Statuszuordnungs-ComboBoxen lösen bei der Initialisierung keine Löschung mehr aus; ein ungespeicherter neuer Vorgang bleibt beim statusbedingten Kategorienwechsel erhalten.
- Isolierte SQLite-Prüfung nach geordnetem Beenden und Neustart: `integrity_check = ok`, ein Testvorgang mit genau einer Kategorie und vier unverändert vorhandene Statuszuordnungen.
- Kein xcodebuild, weil kein iPad-Code geändert wurde und additive JSON-Felder von bestehenden Decodern tolerant ignoriert werden.

## Git-Status

```text
 M AGENTS.md
 M BueroCockpit.csproj
 M Data/BueroRepository.cs
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M Services/IpadSnapshotExportService.cs
 M docs/ARBEITSKATEGORIEN.md
 M docs/CODEX_AUFTRAGSPRUEFUNG.md
 M docs/CODEX_PROJEKTREGELN.md
 M docs/DESIGN_RICHTLINIEN.md
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
 M docs/SETTINGS_KONZEPT.md
 M docs/TESTRICHTLINIEN.md
 M docs/codex_journal/README.md
 M docs/codex_last_run.md
 M docs/ipad-readonly-preparation.md
?? Models/WorkflowCategoryMapping.cs
?? Services/WorkflowCategoryService.cs
?? docs/codex_journal/2026-07-15_20-28_statuskategorien-stabile-ids.md
?? tests/
```

## Branch
codex/work

## Commit
3660afb0f699308372c62375edca5d326fc85241

## Push erfolgreich
Ja

## Offene Punkte

- Windows-spezifische Bedienwege sind real unter Windows zu prüfen; der win-x64-Build allein ersetzt dies nicht.

## Empfohlener nächster Schritt

Die Status-/Kategorie-Fachlogik mit isolierten Daten real unter Windows bedienen und die plattformspezifischen Ergebnisse dokumentieren.
