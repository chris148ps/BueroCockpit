# Codex-Journal: Navigation und Kategorien trennen sowie die Auftragsdetailansicht modernisieren

## Ziel

Systemnavigation dauerhaft von Benutzerkategorien trennen und die vollständige normale Auftragsdetailansicht im vorhandenen Windows-11-Design modernisieren, ohne Fachlogik, Bindings, Commands, Events, Persistenz oder Produktivdaten zu verändern.

## Umsetzung

`MainWindow.axaml.cs` führt getrennte `SystemNavigationCategories`- und `UserCategories`-Mengen. Systemseiten werden über bekannte IDs/Namen und reservierte `__`-IDs erkannt; leere Einträge, Archiv und legacy-only Sonderbereiche werden nicht als Benutzerkategorien angeboten. Kategorienverwaltung, Auftragsauswahl, Sortierung, Verschieben und Drag-&-Drop verwenden die Benutzerkategorien. Bestehende Systemseiten bleiben für die Navigation verfügbar und bestehende Benutzerkategorien/Aufträge wurden nicht gelöscht oder migriert.

Die normale Auftragsdetailansicht in `MainWindow.axaml` verwendet neue lokale Designklassen für große Karten, ruhige semantische Flächen, Überschriften, Untertitel, Feldlabels, Chips, Gruppenabstände und den Aktionskopf. Alle bestehenden Bindings, Commands und Events blieben erhalten.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml`
- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`
- `/Users/christian/AppProjekte/BueroCockpit/docs/PROJEKTSTATUS.md`
- automatisch erzeugte Laufdokumentation unter `/Users/christian/AppProjekte/BueroCockpit/docs/codex_journal/`, `/Users/christian/AppProjekte/BueroCockpit/docs/codex_last_run.md` und `/Users/christian/AppProjekte/BueroCockpit/docs/NEXT_TASK.md`

## Tests

- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- Realer Desktop-Start über `./scripts/run-macos-bundle.sh Debug`: erfolgreich.
- Übersicht geöffnet und Übersichtsinhalt sichtbar.
- Kategorie `terminiert` geöffnet und bestehender Auftrag geöffnet.
- Auftragsdetailansicht vollständig über Accessibility-Baum und sichtbaren Screenshot geprüft: Aufgabe, Organisation, Kategorien, Termine, Material und Anhänge vorhanden; neue Karten-/Flächen-/Abstandsoptik sichtbar.
- Speichern ohne Inhaltsänderung ausgelöst: Status `Gespeichert` sichtbar; keine neue Aufgabe, Kategorie oder Datenmigration.
- Navigation zwischen Übersicht, Kategorie/Auftrag, Einstellungen und Kategorienverwaltung geprüft.
- Kategorienverwaltung enthält keine Systemseiten; vorhandene Benutzerkategorien und Unterkategorien sind sichtbar.
- Task-Kategorieauswahl enthält keine Systemseiten wie Übersicht, Schreibtisch, Archiv oder Einstellungen.

## Ergebnis

Die Systemnavigation ist dauerhaft von Benutzerkategorien getrennt. Die Auftragsdetailansicht ist im vorhandenen Windows-11-Design modernisiert; die bestehende Bedienung bleibt erhalten.

## Bekannte offene Punkte

- Keine fachlichen offenen Punkte aus diesem Auftrag.
- Draft-PR #1 bleibt der einzige PR und wird über den vorgeschriebenen Helper aktualisiert.

## Aktueller Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? docs/codex_journal/2026-07-12_00-04_navigation-detail-modernisierung.md
```
