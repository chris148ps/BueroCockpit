# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-13 21:18 +0200

## Letzter Auftrag

Auftragsdetail: Kategorienamen, Hierarchie und Legacyfilter korrigieren.

## Zusammenfassung

Die Auftragsdetailauswahl verwendet jetzt den enthaltenen Kategorie-Wrapper und kann vollständige Hierarchiepfade anzeigen. Legacy-Workflowkategorien werden nicht mehr als Checkbox, Chip oder linke Benutzerkategorie dargestellt. Persistenz und bestehende Zuordnungen bleiben unangetastet.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml`
- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`
- `/Users/christian/AppProjekte/BueroCockpit/docs/codex_journal/2026-07-13_21-17_category-detail-filter.md`
- `/Users/christian/AppProjekte/BueroCockpit/docs/codex_last_run.md`
- `/Users/christian/AppProjekte/BueroCockpit/docs/NEXT_TASK.md`

## Tests

- `git pull --ff-only origin main`: erfolgreich, bereits aktuell.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `git diff --check`: erfolgreich.
- Reale Bundle-Prüfung `./scripts/run-macos-bundle.sh Debug`: erfolgreich; die Desktop-App wurde geöffnet.
- Quellprüfung: Checkbox bindet an `Category.SelectionName`; Sidebar, Auswahl und Chips verwenden `IsUserCategory`; `bestellt`, `erstellen`, `gesendet`, `terminieren`, alte `Termine` und `Erinnerung` sind in der gemeinsamen Legacy-Fallbackliste.
- Die produktive Datenbank im aktuell konfigurierten OneDrive-Pfad war während dieses Laufs nicht verfügbar. Eine konkrete Sichtprüfung mit aktuellem produktivem Datenbestand und ein Speichern-/Neustarttest konnten deshalb nicht ehrlich bestätigt werden.

## Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
?? docs/codex_journal/2026-07-13_21-18_category-detail-filter.md
```

## Branch

Wird nach dem Dokumentationslauf durch den Git-Helfer ergänzt.

## Commit

Wird nach dem Dokumentationslauf durch den Git-Helfer ergänzt.

## Push erfolgreich

Nein – der reine Dokumentationslauf führt keinen Push aus.

## Offene Punkte

- Nach Verfügbarkeit des konfigurierten Datenordners muss eine kurze manuelle Abnahme mit realen Kategorien erfolgen: `Firma / Retouren`, keine leeren Checkboxen, kein `bestellt`, kein doppeltes `Termine`, keine alte `Erinnerung`, Speichern und Neustart.

## Empfohlener nächster Schritt

Die korrigierte Kategorienanzeige nach Wiederverfügbarkeit des OneDrive-Datenordners einmal mit dem aktuellen produktiven Datenbestand abnehmen.

1. Desktop mit dem konfigurierten Datenordner starten und die Sidebar sowie das Auftragsdetail öffnen.
2. Eine echte Unterkategorie prüfen, zuordnen, speichern und nach Neustart erneut kontrollieren.
