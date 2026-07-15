# Codex-Journal: Konfigurierbare Statuszuordnungen über stabile Kategorie-IDs mit genau einer aktuellen Kategorie und Variant A implementieren.

## Ziel

Normale Kategorien vollständig benutzerverwaltet lassen, jeden neuen oder bewusst geänderten Vorgang auf genau eine aktuelle Kategorie begrenzen und diese bei Typ- und Statuswechseln über zentral konfigurierte stabile Kategorie-IDs bestimmen, ohne Produktivdaten zu migrieren.

## Umsetzung

- Widersprüchliche aktuelle Regeldateien vor der Implementierung auf frei benannte Kategorien, stabile ID-Zuordnungen, Variant A und die Release-Konsistenzprüfung vereinheitlicht.
- Additive Tabelle WorkflowCategoryMappings in buerocockpit.db ergänzt; keine bestehenden Vorgänge oder Zuordnungen migriert.
- Statuszuordnungen für sämtliche Angebots- und Direktauftragsstatus unter Einstellungen > Kategorien ergänzt, einschließlich sichtbarer fehlender oder ungültiger Ziele.
- Neuanlage mit verpflichtender Typauswahl, bestätigte Typänderung mit kompatibler beziehungsweise ausdrücklicher Statuswahl und blockierende Hinweise bei fehlender Zuordnung umgesetzt.
- Statuswechsel übernimmt genau die konfigurierte Kategorie; manuelle Auswahl und Drag & Drop ändern nur die Kategorie und erlauben Haupt- sowie Unterkategorien.
- Kategorie-Löschung verlangt bei Verwendungen Ersatz, bewusstes Entfernen oder Abbruch; kein stilles Verschieben.
- Neue, importierte, duplizierte und bewusst geänderte Vorgänge schreiben genau eine Kategorie fort; unveränderte Legacy-Mehrfachzuordnungen bleiben tolerant und ohne Start-/Ladeschreibvorgang erhalten.
- Navigation, Suche, Zähler, Detail und getrennte Status-/Kategorie-Badges verwenden die aktuelle Kategorie-ID beziehungsweise den vollständigen Kategoriepfad.
- Snapshot-Export additiv um currentCategoryId, workflowType und workflowStep ergänzt; kein iPad-Code und keine Netzwerk-/Sync-Architektur geändert.
- Eigenständigen isolierten Workflow-/Repository-/Export-Testlauf ergänzt.
- Im sichtbaren macOS-Rundgang zwei Auswahlfehler gefunden und minimal behoben: programmatisch initialisierte Statuszuordnungs-ComboBoxen werden nicht mehr als Benutzeränderung behandelt; ein noch neuer Vorgang wird bei der programmgesteuerten Navigation in seine neue Statuskategorie nicht mehr verworfen.

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
- Sichtbarer vollständiger macOS-Bedienrundgang mit isolierten Daten: erfolgreich. Geprüft wurden Statuszuordnungen einschließlich fehlender Zuordnung, Auswahl von Haupt- und Unterkategorien, Neuanlage beider Vorgangstypen, Statuswechsel, kompatibler Typwechsel, manuelle Kategorieauswahl, Vorgangs-Drag-and-drop, Umbenennen und Unterordnen einer gemappten Kategorie, Löschschutz mit Abbruch, Zähler und Neustartpersistenz.
- Beide im sichtbaren Rundgang gefundenen Fehler wurden nach der Korrektur erneut erfolgreich bedient.
- Isolierte SQLite-Prüfung nach geordnetem Beenden und Neustart: integrity_check ok, ein Testvorgang mit genau einer Kategorie und vier unverändert vorhandene Statuszuordnungen.
- Kein xcodebuild, weil kein iPad-Code geändert wurde und additive JSON-Felder von bestehenden Decodern tolerant ignoriert werden.

## Ergebnis

Die neue Fachlogik ist implementiert und in isolierten automatisierten Prüfungen sowie im sichtbaren realen macOS-Bundle bestätigt. Produktivdaten, Cloud-Dateien, Netzwerkdienste, Version, Release und Tags blieben unverändert.

## Bekannte offene Punkte

- Windows-spezifische Bedienwege sind real unter Windows zu prüfen; der win-x64-Build allein ersetzt dies nicht.

## Aktueller Git-Status

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
