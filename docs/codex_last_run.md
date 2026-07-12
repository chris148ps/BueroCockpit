# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-12 14:06 +0200

## Letzter Auftrag

Dynamische Spaltenprojektion für kompakte Vorgangs- und Terminlisten

## Zusammenfassung

Spaltenreihenfolge und Sichtbarkeit werden nun tatsächlich aus dem lokalen Layoutmodell projiziert; Breiten werden von Kopf und Zeilen gemeinsam verwendet. Die Oberfläche bleibt kompatibel mit vorhandenen Daten und Kategorien.

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

## Git-Status

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

## Branch

Wird nach dem Dokumentationslauf durch den Git-Helfer ergänzt.

## Commit

Wird nach dem Dokumentationslauf durch den Git-Helfer ergänzt.

## Push erfolgreich

Nein – der reine Dokumentationslauf führt keinen Push aus.

## Offene Punkte

- Drag-and-drop-Reihenfolge per Maus ist noch nicht als direkte Pointer-Geste implementiert; gespeicherte Reihenfolgen werden aber bereits projiziert.
- Visuelle Neustartprüfung und tatsächliche Mausabnahme müssen nach Entsperren der macOS-Sitzung erfolgen.

## Empfohlener nächster Schritt

Drag-and-drop-Reihenfolge und vollständige visuelle Abnahme der drei dynamischen Tabellen prüfen und abschließen.

1. Kopfspalten per Pointer verschieben und Reihenfolge unmittelbar projizieren.
2. Layout nach Neustart in Aufträge, Angebote und Termine prüfen.
3. Übersicht, Splitter, Status, Kategorien und leere Monteurzuordnung sichtbar abnehmen.
