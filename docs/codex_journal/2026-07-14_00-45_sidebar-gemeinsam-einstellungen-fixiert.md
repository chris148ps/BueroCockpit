# Codex-Journal: Gemeinsame Sidebar-Navigation mit fixierten Einstellungen.

## Ziel

Nur eine Überschrift `Kategorien` anzeigen, Systembereiche und Benutzerkategorien gemeinsam vertikal führen und `Einstellungen` dauerhaft am unteren Sidebar-Rand fixieren.

## Umsetzung

Die Sidebar verwendet jetzt einen gemeinsamen vertikalen Scrollbereich für Systembereiche und echte Benutzerkategorien unter einer einzigen Überschrift `Kategorien`. `Einstellungen` liegt außerhalb dieses Scrollbereichs in einer eigenen unteren Zeile mit Abstand und dezenter Trennlinie. Die Systemliste enthält keine Bearbeitungs- oder Drag-and-drop-Aktionen; die vorhandenen Bearbeitungsfunktionen der Benutzerkategorien bleiben erhalten. Der gemeinsame Scrollbereich deaktiviert horizontales Scrollen und innere verschachtelte Scrollbars.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml`
- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`

## Tests

- `git pull --ff-only origin main`: erfolgreich, bereits aktuell.
- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `./scripts/run-macos-bundle.sh Debug`: erfolgreich; Bundle gebaut und mit `open` gestartet.
- Keine Datenbank-, Kategorie- oder Auftragszuordnungen verändert.

## Ergebnis

Die Sidebar zeigt nur noch `Kategorien`, alle Navigations- und Benutzerkategorieeinträge stehen darunter in einem gemeinsamen Scrollbereich, und `Einstellungen` bleibt unten fixiert und optisch getrennt.

## Bekannte offene Punkte

- Eine pixelgenaue Sichtprüfung mit dem produktiven Datenbestand war in dieser Umgebung nicht möglich; Build und realer Bundle-Start waren erfolgreich.

## Aktueller Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
?? docs/codex_journal/2026-07-14_00-45_sidebar-gemeinsam-einstellungen-fixiert.md
```
