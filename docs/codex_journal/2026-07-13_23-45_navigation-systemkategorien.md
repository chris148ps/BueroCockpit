# Codex-Journal: Navigation und persistierte Bearbeitung der Systembereiche und Benutzerkategorien.

## Ziel

Die linke Navigation in getrennte Bereiche und echte Kategorien aufteilen und die sieben Bereiche als persistierte, bearbeitbare Kategorien führen.

## Umsetzung

Die Navigation verwendet jetzt getrennte Collections für `Bereiche` und `Kategorien`. Die virtuellen Bereiche werden über reservierte IDs geladen, mit vorhandenen persistierten Werten zusammengeführt und über denselben Speicherpfad wie Benutzerkategorien umbenannt, verschoben, verschachtelt und ausgeblendet. Firma, Netzbetreiber und andere fachliche Einträge bleiben ausschließlich echte Benutzerkategorien. Legacy- und Workflowkategorien bleiben aus den Benutzeransichten und der Auftragsauswahl herausgefiltert. Das Löschen mit Zuordnungen fragt nach und blendet nur aus; Aufträge, IDs, ParentId und Zuordnungen werden nicht gelöscht.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml`
- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`

## Tests

- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `./scripts/run-macos-bundle.sh Debug`: erfolgreich; Bundle gebaut und mit `open` gestartet.
- Eine Sichtprüfung mit dem produktiven aktuellen Datenbestand war nicht möglich, weil der konfigurierte OneDrive-Datenpfad nicht verfügbar war.

## Ergebnis

Bereiche und Benutzerkategorien sind in der Sidebar getrennt sichtbar. Die sieben Bereiche werden anhand reservierter IDs persistenzfähig geladen und können über die vorhandene Kategorienverwaltung bearbeitet werden. Löschungen bleiben datenbewusst und reversibel auf UI-Ebene.

## Bekannte offene Punkte

- Die manuelle Sichtprüfung mit dem produktiven OneDrive-Datenbestand sowie Speichern und Neustart mit einem echten Testauftrag müssen nach Verfügbarkeit des Datenordners erfolgen.

## Aktueller Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
?? docs/codex_journal/2026-07-13_23-45_navigation-systemkategorien.md
```
