# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-13 21:04 +0200

## Letzter Auftrag

Navigation, Schreibtisch und Benutzerkategorien konsolidieren.

## Zusammenfassung

Navigation, Kategorienverwaltung und Auftragsauswahl verwenden nun dieselbe gefilterte Benutzerkategoriequelle. IDs, ParentId, Reihenfolge und bestehende Auftragszuordnungen werden weder migriert noch gelöscht. Der Schreibtisch bleibt als vorhandene lokale Einstellung erhalten und wird beim Umschalten sofort in der Sidebar berücksichtigt.

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

## Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
?? docs/codex_journal/2026-07-13_21-04_navigation-desk-categories.md
```

## Branch
codex/work

## Commit
550bf5906e0fb2234532f57039591684b135ad16

## Push erfolgreich
Ja

## Offene Punkte

- Die Klickprüfung für Schreibtisch ein/aus über einen vollständigen echten App-Neustart muss außerhalb der Sandbox einmal in der normalen Benutzerumgebung erfolgen; der Build, der Desktopstart und der lokale Speicherpfad im Code sind geprüft.

## Empfohlener nächster Schritt

Die Kategorie- und Schreibtischabläufe in der normalen Desktop-Benutzerumgebung kurz manuell gegen reale Arbeitsdaten abnehmen.

1. Schreibtisch ein- und ausschalten, neu starten und die Rückkehr zur Übersicht beim Ausblenden prüfen.
2. Eine vorhandene Benutzerkategorie verschachteln und die Darstellung in Navigation, Einstellungen und Auftragsdetail vergleichen.
