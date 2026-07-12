# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-12 13:56 +0200

## Letzter Auftrag

Spaltenbreiten und getrennte Tabellenlayouts für Vorgangsansichten

## Zusammenfassung

Spaltenbreiten sind jetzt als lokale, ansichtsspezifische Werte modelliert und über sichtbare Kopf-Splitter mit den Tabellenzeilen verbunden. Standardlayouts lassen sich zurücksetzen.

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

## Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M Services/AppSettingsService.cs
 M docs/DESIGN_RICHTLINIEN.md
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? docs/codex_journal/2026-07-12_13-56_spaltenbreiten.md
```

## Branch

Wird nach dem Dokumentationslauf durch den Git-Helfer ergänzt.

## Commit

Wird nach dem Dokumentationslauf durch den Git-Helfer ergänzt.

## Push erfolgreich

Nein – der reine Dokumentationslauf führt keinen Push aus.

## Offene Punkte

- Drag-and-drop-Reihenfolge der Spalten ist noch nicht vollständig umgesetzt.
- Das Kontextmenü bietet derzeit die optionale Titelinformation, aber noch keine vollständig dynamische Liste aller Spalten mit eigener Sichtbarkeitsverwaltung je Ansicht.
- Neustart- und visuelle Splittertests müssen nach Entsperren der macOS-Sitzung wiederholt werden.

## Empfohlener nächster Schritt

Die drei Tabellen auf eine gemeinsame dynamische Spaltenprojektion mit echter Reihenfolge- und Sichtbarkeitsverwaltung umstellen.

1. Gemeinsame Spaltendefinitionen für Vorgänge und Termine einführen.
2. Header-Kontextmenü, Drag-and-drop und Zeilenprojektion an diese Definition binden.
3. Alle Layoutwerte getrennt speichern und nach Neustart visuell prüfen.
