# Codex-Journal: Desktop-Einstellungen und Technikerverwaltung an Windows-11-Dark-Referenz angleichen

## Ziel

Die Desktop-Einstellungen erhalten horizontale Tabs und eine zweispaltige Technikerverwaltung nach dem Referenzbild. Technikerprofile sollen Name, Kürzel, E-Mail und Telefon rückwärtskompatibel speichern.

## Umsetzung

- Horizontalen Einstellungstabstreifen mit Allgemein, Aufträge, Kategorien, Techniker, Darstellung, Daten & Pfade und Sync ergänzt.
- Technikerbereich als zwei flache Karten umgesetzt: Liste mit Standardmarkierung und transparentem Lösch-X links, Bearbeitungsformular rechts.
- Technikerprofile in den bestehenden Live-Settings ergänzt; alte technicianNames werden beim Laden tolerant übernommen und weiterhin für bestehende Auftragsauswahlen bereitgestellt.
- Standard-Techniker bleibt gegen Löschen geschützt.
- Whitespace-Korrektur im Laufdokument und im Dokumentationsgenerator beibehalten.

## Geänderte Dateien

- MainWindow.axaml
- MainWindow.axaml.cs
- Services/LiveSettingsService.cs
- docs/PROJEKTSTATUS.md
- docs/codex_last_run.md
- docs/NEXT_TASK.md
- docs/codex_journal/
- scripts/update-codex-documentation.sh

## Tests

- git diff --check erfolgreich.
- dotnet build erfolgreich mit 0 Fehlern.
- scripts/run-macos-bundle.sh Debug erfolgreich.
- Reale Start- und Sichtprüfung: Einstellungen, alle sieben Tabs, Technikerliste, Techniker-Auswahl, Formular und Lösch-X geprüft.
- Keine vorhandenen Techniker gelöscht oder Profildaten verändert; der Standard-Techniker war ohne Löschaktion sichtbar.

## Ergebnis

Die Desktop-Einstellungen und die Technikerverwaltung folgen der Referenzansicht. Bestehende Namenslisten bleiben kompatibel, neue Profildaten werden im vorhandenen zentralen Live-Settings-Format gespeichert.

## Bekannte offene Punkte

- Ein umfassender Funktionslauf für Speichern und Löschen mit produktiven Technikerdaten wurde bewusst nicht ausgeführt, damit keine vorhandenen lokalen Daten verändert oder gelöscht werden.
- Der echte lokale Netzwerk-Sync bleibt unverändert vorbereitet und deaktiviert.

## Aktueller Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M Services/LiveSettingsService.cs
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
 M docs/codex_last_run.md
 M scripts/update-codex-documentation.sh
?? docs/codex_journal/2026-07-11_21-23_techniker-einstellungen.md
```
