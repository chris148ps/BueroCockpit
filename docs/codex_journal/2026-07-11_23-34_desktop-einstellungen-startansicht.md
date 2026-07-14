# Codex-Journal: Desktop-Einstellungen, Kategorienverwaltung und Startansicht korrigieren

## Ziel

Den Backup-Bereich nach Daten & Pfade verschieben, den leeren Eintrag in der Kategorienverwaltung ursachengerecht ausblenden und jeden vollständigen Desktop-Start in der Übersicht beginnen lassen.

## Umsetzung

- Den vorhandenen Backup-XAML-Block vollständig und ohne Änderungen an Events, Bindings oder Fachlogik von Darstellung nach Daten & Pfade verschoben.
- Ursache des leeren Kategorieeintrags read-only untersucht: Der technische Systembereich `__desk` wurde über die gemeinsam verwendete Collection `TaskCategories` in die Verwaltung übernommen; bei deaktivierter Schreibtisch-Navigation blieb sein `SelectionName` leer.
- Eine separate defensive Collection für die Kategorienverwaltung ergänzt, die Sonderbereiche sowie leere Namen nur in dieser UI ausfiltert. Es wurden keine Datenbankdatensätze oder Zuordnungen verändert.
- Den zentralen Repository-Speichereinstieg gegen leere oder nur aus Leerzeichen bestehende Kategorienamen abgesichert.
- Die explizite Startauswahl von Offene Aufgaben auf den Systembereich Übersicht umgestellt.

## Geänderte Dateien

- `MainWindow.axaml`
- `MainWindow.axaml.cs`
- `Data/BueroRepository.cs`
- `docs/PROJEKTSTATUS.md`
- automatisch erzeugte Laufdokumentation unter `docs/codex_journal/`, `docs/codex_last_run.md` und `docs/NEXT_TASK.md`

## Tests

- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- Read-only-SQLite-Prüfung der Kategorie-IDs, Namen, Eltern-IDs, Sichtbarkeit und Auftragsreferenzen: kein leerer gespeicherter Name; `__overview` ist Übersicht, `__desk` ist Schreibtisch.
- Reale Publish-/Startprüfung: aktuelle Publish-Ausgabe wurde erzeugt und der ausführbare Prozess gestartet. Das Öffnen des `.app`-Bundles über macOS schlug in der Testumgebung mit `RBSRequestErrorDomain Code=5` / `NSPOSIXErrorDomain Code=162` fehl; deshalb konnten die sichtbaren Klick- und Neustartvarianten sowie die Backup-Bedienung nicht vollständig automatisiert bestätigt werden.
- Statische XAML-Prüfung: Backup-Block nur noch im Tab Daten & Pfade; bestehende Handler und Bindings unverändert.

## Ergebnis

Die drei begrenzten Codekorrekturen sind umgesetzt. Produktive Kategorien, Aufträge und Zuordnungen wurden weder gelöscht noch verändert. Build und Diff-Prüfung sind sauber; die sichtbaren macOS-Neustarttests bleiben wegen des dokumentierten Bundle-Launch-Fehlers offen.

## Bekannte offene Punkte

- Sichtbaren manuellen App-Test auf einem regulär startbaren macOS-Desktop nachholen: Kategorie und Einstellungen vor dem Schließen öffnen, jeweils neu starten und Übersicht bestätigen; Backup-Anzeige und Kategorienverwaltung visuell prüfen.

## Aktueller Git-Status

```text
 M Data/BueroRepository.cs
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? docs/codex_journal/2026-07-11_23-34_desktop-einstellungen-startansicht.md
```
