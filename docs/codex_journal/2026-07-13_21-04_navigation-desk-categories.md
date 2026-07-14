# Codex-Journal: Navigation, Schreibtisch und Benutzerkategorien konsolidieren.

## Ziel

Zähler der linken Navigation korrekt aus nicht gelöschten Aufträgen ableiten, den optionalen Schreibtisch wieder sichtbar schalten, Diagnose unter Daten & Pfade bündeln und Benutzerkategorien in Navigation, Verwaltung und Auftragsdetail konsistent verwenden.

## Umsetzung

- Die Sidebar zählt Aufträge, Angebote, Material und Termine mit denselben Filtern wie die jeweilige Ansicht.
- Benutzerkategorien werden hierarchisch in die Sidebar aufgenommen; Hauptkategorien zählen eigene sowie untergeordnete Aufträge einmalig.
- Die bestehende lokale Schreibtisch-Einstellung baut die Sidebar sofort neu auf; beim Ausblenden bleibt der Wechsel zur Übersicht erhalten.
- Workflow-, System- und Legacy-Kategorien bleiben in der Persistenz erhalten, werden aber nicht mehr als Benutzerkategorien oder Auftragsziele angeboten.
- Die Auftragsauswahl zeigt den Hierarchiepfad und erlaubt ausschließlich Endkategorien.
- Die unveränderte Diagnosekarte ist nur noch im Tab Daten & Pfade sichtbar.

## Geänderte Dateien

- `MainWindow.axaml`
- `MainWindow.axaml.cs`
- `docs/codex_journal/<Zeitstempel>_navigation-desk-categories.md`
- `docs/codex_last_run.md`
- `docs/NEXT_TASK.md`

## Tests

- `dotnet build` erfolgreich, 0 Warnungen, 0 Fehler.
- Isolierter Repository-Test in einem temporären Datenordner: Kategorie erstellen, Unterkategorie mit ParentId und SortOrder speichern, Testauftrag der Unterkategorie plus Hauptkategorie zuordnen und nach erneutem Repository-Start unverändert laden: erfolgreich.
- Quellprüfung der Sidebar-/Auftragsfilter, der Diagnose-Sichtbarkeit und des lokalen Schreibtisch-Settings: erfolgreich.
- `./scripts/run-macos-bundle.sh Debug` erfolgreich; `/Users/christian/AppProjekte/BueroCockpit/bin/Debug/BueroCockpit.app` wurde real geöffnet.
- `git diff --check` erfolgreich.
- Ein isolierter automatischer Neustartstest der lokalen Einstellung ist unter macOS nicht vollständig ausführbar, weil der App-Support-Pfad im Sandboxprozess auf den echten Benutzerpfad aufgelöst wird und dort nicht geschrieben werden darf.

## Ergebnis

Navigation, Kategorienverwaltung und Auftragsauswahl verwenden nun dieselbe gefilterte Benutzerkategoriequelle. IDs, ParentId, Reihenfolge und bestehende Auftragszuordnungen werden weder migriert noch gelöscht. Der Schreibtisch bleibt als vorhandene lokale Einstellung erhalten und wird beim Umschalten sofort in der Sidebar berücksichtigt.

## Bekannte offene Punkte

- Die Klickprüfung für Schreibtisch ein/aus über einen vollständigen echten App-Neustart muss außerhalb der Sandbox einmal in der normalen Benutzerumgebung erfolgen; der Build, der Desktopstart und der lokale Speicherpfad im Code sind geprüft.

## Aktueller Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
?? docs/codex_journal/2026-07-13_21-04_navigation-desk-categories.md
```
