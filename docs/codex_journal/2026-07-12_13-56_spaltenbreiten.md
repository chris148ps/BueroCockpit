# Codex-Journal: Spaltenbreiten und getrennte Tabellenlayouts für Vorgangsansichten

## Ziel

Die offene Tabellenkonfiguration fortsetzen und Spaltenbreiten über sichtbare Kopf-Splitter mit den Datenzeilen und lokalen, nach Ansicht getrennten Einstellungen verbinden.

## Umsetzung

Auftrags-, Angebots- und Terminlayouts besitzen getrennte lokale `TableLayoutSettings` mit Spaltenbreiten, Reihenfolge, Sichtbarkeit und Sortierwerten. Die sichtbaren Kopfzeilen verwenden nun `GridSplitter`-Handles; die Datenzeilen binden an dieselben Breitenwerte. Änderungen werden je nach Ansicht in `OrdersTableLayout`, `OffersTableLayout` oder `AppointmentsTableLayout` gespeichert. Standardwerte können über das Kopf-Kontextmenü wiederhergestellt werden. Horizontales und vertikales Scrollen bleiben aktiviert. Der Splitter zwischen Liste und Detailbereich bleibt in der Übersicht ausgeblendet.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml`
- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`
- `/Users/christian/AppProjekte/BueroCockpit/Services/AppSettingsService.cs`
- `/Users/christian/AppProjekte/BueroCockpit/docs/DESIGN_RICHTLINIEN.md`

## Tests

- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `git diff --check`: erfolgreich.
- macOS-Bundle-Startpfad erneut gebaut über `./scripts/run-macos-bundle.sh Debug`.
- Statische Prüfung: getrennte Layoutobjekte, Kopf-Splitter und Zeilenbindungen sind vorhanden; Kunde bleibt Pflichtspalte.
- Sichtbare Computer-Use-Prüfung war wegen der gesperrten macOS-Sitzung nicht möglich.

## Ergebnis

Spaltenbreiten sind jetzt als lokale, ansichtsspezifische Werte modelliert und über sichtbare Kopf-Splitter mit den Tabellenzeilen verbunden. Standardlayouts lassen sich zurücksetzen.

## Bekannte offene Punkte

- Drag-and-drop-Reihenfolge der Spalten ist noch nicht vollständig umgesetzt.
- Das Kontextmenü bietet derzeit die optionale Titelinformation, aber noch keine vollständig dynamische Liste aller Spalten mit eigener Sichtbarkeitsverwaltung je Ansicht.
- Neustart- und visuelle Splittertests müssen nach Entsperren der macOS-Sitzung wiederholt werden.

## Aktueller Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M Services/AppSettingsService.cs
 M docs/DESIGN_RICHTLINIEN.md
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? docs/codex_journal/2026-07-12_13-56_spaltenbreiten.md
```
