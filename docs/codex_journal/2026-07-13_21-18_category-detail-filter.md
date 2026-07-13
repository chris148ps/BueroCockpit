# Codex-Journal: Auftragsdetail: Kategorienamen, Hierarchie und Legacyfilter korrigieren.

## Ziel

Checkboxen im Auftragsdetail sollen vollständige Kategorienpfade aus dem enthaltenen `Category`-Objekt anzeigen. Legacy-Workflowkategorien und alte doppelte Navigationseinträge sollen gemeinsam mit Sidebar und zugeordneten Kategorien ausgeblendet werden, ohne Daten zu löschen.

## Umsetzung

- Das Checkbox-Binding verwendet nun `Category.SelectionName` des `TaskCategorySelection`-Wrappers.
- Die gemeinsame `IsUserCategory`-Prüfung wird für Sidebar, Auftragsauswahl und zugeordnete Kategoriepfade verwendet.
- Reine Workflow-/Legacybezeichnungen wie `bestellt`, `erstellen`, `gesendet`, `terminieren`, alte `Termine` und `Erinnerung` werden als Fallback für alte Datensätze ausgefiltert.
- Echte Kategorien wie `Firma` und `Retouren` bleiben erhalten; ihre Hierarchie wird als `Firma / Retouren` angezeigt.
- Bestehende Kategorie-IDs, ParentId, Reihenfolge und Auftragszuordnungen werden nicht verändert.

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

## Ergebnis

Die Auftragsdetailauswahl verwendet jetzt den enthaltenen Kategorie-Wrapper und kann vollständige Hierarchiepfade anzeigen. Legacy-Workflowkategorien werden nicht mehr als Checkbox, Chip oder linke Benutzerkategorie dargestellt. Persistenz und bestehende Zuordnungen bleiben unangetastet.

## Bekannte offene Punkte

- Nach Verfügbarkeit des konfigurierten Datenordners muss eine kurze manuelle Abnahme mit realen Kategorien erfolgen: `Firma / Retouren`, keine leeren Checkboxen, kein `bestellt`, kein doppeltes `Termine`, keine alte `Erinnerung`, Speichern und Neustart.

## Aktueller Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
?? docs/codex_journal/2026-07-13_21-18_category-detail-filter.md
```
