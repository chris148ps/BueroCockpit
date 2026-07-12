# Codex-Journal: Dynamische Spaltenprojektion für kompakte Vorgangs- und Terminlisten

## Ziel

Die bislang vorbereiteten Layoutdaten tatsächlich auf Tabellenkopf und Datenzeilen anwenden und die Spaltenkonfiguration über getrennte lokale Ansichtslayouts bedienbar machen.

## Umsetzung

Aufträge, Angebote und Termine verwenden nun dieselbe dynamische Tabellenprojektion. Die sichtbaren Spalten werden aus der jeweiligen `TableLayoutSettings`-Reihenfolge und Sichtbarkeitsliste gebildet; Kunde bleibt unveränderbar sichtbar. Titel und weitere reale Spalten können über das Kopf-Kontextmenü ein- und ausgeblendet werden. Kopf und Datenzeilen verwenden dieselben `TableCellItem`-Werte und Breiten. Kopf-Splitter aktualisieren die gespeicherte Breite der jeweiligen Ansicht. Sortierfeld und Sortierrichtung werden getrennt je Ansicht gespeichert. Die Übersicht bleibt ohne Splitter und ohne Tabellenbereich.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml`
- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`
- `/Users/christian/AppProjekte/BueroCockpit/Models/TaskItem.cs`
- `/Users/christian/AppProjekte/BueroCockpit/Models/TableCellItem.cs`
- `/Users/christian/AppProjekte/BueroCockpit/Services/AppSettingsService.cs`

## Tests

- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `git diff --check`: erfolgreich.
- macOS-Bundle-Build und Startpfad über `./scripts/run-macos-bundle.sh Debug`: erfolgreich.
- Statische Prüfung: gemeinsame dynamische Spaltenprojektion, ansichtsspezifische Layoutdaten, Kopf-Splitter, geschützte Kunde-Spalte und Kontextmenü vorhanden.
- Sichtbare Computer-Use-Prüfung nach der letzten Änderung war wegen der gesperrten macOS-Sitzung nicht möglich.

## Ergebnis

Spaltenreihenfolge und Sichtbarkeit werden nun tatsächlich aus dem lokalen Layoutmodell projiziert; Breiten werden von Kopf und Zeilen gemeinsam verwendet. Die Oberfläche bleibt kompatibel mit vorhandenen Daten und Kategorien.

## Bekannte offene Punkte

- Drag-and-drop-Reihenfolge per Maus ist noch nicht als direkte Pointer-Geste implementiert; gespeicherte Reihenfolgen werden aber bereits projiziert.
- Visuelle Neustartprüfung und tatsächliche Mausabnahme müssen nach Entsperren der macOS-Sitzung erfolgen.

## Aktueller Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M Models/TaskItem.cs
 M Services/AppSettingsService.cs
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? Models/TableCellItem.cs
?? docs/codex_journal/2026-07-12_14-06_dynamische-spalten.md
```
