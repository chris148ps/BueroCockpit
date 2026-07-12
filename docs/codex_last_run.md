# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-12 14:09 +0200

## Letzter Auftrag

Spaltenreihenfolge per Drag-and-drop ergänzen

## Zusammenfassung

Spaltenreihenfolge kann jetzt über die Header-Pointer-Geste geändert und im jeweiligen lokalen Ansichtslayout gespeichert werden.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`

## Tests

- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `git diff --check`: erfolgreich.
- macOS-Bundle-Build und Startpfad über `./scripts/run-macos-bundle.sh Debug`: erfolgreich.
- Statische Prüfung: Pointer-Capture, sichtbare Spaltenreihenfolge, Persistenz und anschließende Projektion sind verbunden.
- Sichtbare Mausabnahme war wegen der gesperrten macOS-Sitzung nicht möglich.

## Git-Status

```text
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? docs/codex_journal/2026-07-12_14-09_spalten-drag.md
```

## Branch
codex/work

## Commit
becae67566fb22fa762f4550054f2c24bc031b2d

## Push erfolgreich
Ja

## Offene Punkte

- Visuelle Endabnahme inklusive Neustartwiederherstellung wartet auf eine entsperrte macOS-Sitzung.
- Mutierende Status-/Monteur-Neustarttests bleiben aus Sicherheitsgründen außerhalb produktiver Daten.

## Empfohlener nächster Schritt

Nach Entsperren des Macs die drei Tabellenansichten und gespeicherten Layouts sichtbar abnehmen.

1. Aufträge, Angebote und Termine öffnen.
2. Breite, Reihenfolge, Sichtbarkeit und horizontales Scrollen prüfen.
3. App neu starten und Layoutwiederherstellung kontrollieren.
