# Codex-Journal: Sidebar vertikal strukturieren und Systembereiche schützen.

## Ziel

`Bereiche` und `Kategorien` ausschließlich untereinander anzeigen, die Benutzerkategorien scrollbar machen und Systembereiche ohne Bearbeitungsaktionen darstellen.

## Umsetzung

Den Sidebar-Container von `DockPanel` auf ein Grid mit eindeutigen Zeilen umgestellt. Kopfbereich, Überschrift `Bereiche`, vollständige Systemliste, Überschrift `Kategorien` und die verbleibende Benutzerkategorienliste sind dadurch vertikal fest angeordnet. Die Benutzerliste scrollt nur vertikal; horizontales Scrollen und abgeschnittene Namen wurden entfernt. Systemeinträge haben in der Sidebar keine Drag-/Drop-Handler, kein Kontextmenü und keine Umbenennungs-/Löschaktionen. Die Benutzerkategorienliste behält Auswahl, Zähler, Drag-and-drop und Kontextmenü unverändert.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml`

## Tests

- `git pull --ff-only origin main`: erfolgreich, bereits aktuell.
- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `./scripts/run-macos-bundle.sh Debug`: erfolgreich; Bundle gebaut und mit `open` gestartet.
- Keine Datenbank-, Kategorie- oder Auftragszuordnungsänderungen vorgenommen.

## Ergebnis

Die Sidebar ist eindeutig vertikal aufgebaut. Systembereiche stehen vollständig oben, Benutzerkategorien darunter und nutzen den restlichen Platz mit vertikalem Scrollen. Bearbeitungsfunktionen bleiben ausschließlich bei Benutzerkategorien.

## Bekannte offene Punkte

- Eine pixelgenaue Sichtprüfung mit dem produktiven Datenbestand war in dieser Umgebung nicht möglich; der reale Bundle-Start wurde erfolgreich ausgeführt.

## Aktueller Git-Status

```text
 M MainWindow.axaml
 M docs/NEXT_TASK.md
?? docs/codex_journal/2026-07-14_00-40_sidebar-vertikal-systembereiche.md
```
