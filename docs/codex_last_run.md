# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-14 21:50 +0200

## Letzter Auftrag

Kategorienauswahl, Papierkorb und Kategorie-Drag nachkorrigieren

## Zusammenfassung

Die Detailauswahl bildet jetzt die vollständige fachliche Kategorienstruktur ab: Hauptkategorien mit Unterkategorien sind sichtbar, aber nicht auswählbar; 13 Endkategorien bleiben auswählbar. Der Papierkorb steht fest direkt über Einstellungen. Ein sichtbarer Drag-Griff macht das Sortieren und Unterordnen des Kategorienbaums eindeutig. Organisatorische Filter wie Angebote bleiben bewusst keine fachlichen Drop-Ziele.

## Geänderte Dateien

- `MainWindow.axaml`
- `MainWindow.axaml.cs`
- `docs/PROJEKTSTATUS.md`
- `docs/codex_last_run.md`
- `docs/NEXT_TASK.md`
- neuer Eintrag unter `docs/codex_journal/`

## Tests

- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler. Keine reale Bedienung unter Windows.
- Isolierter Avalonia-Zustandslauf: vollständige Kategorienstruktur, 13 auswählbare Endkategorien, deaktivierte Hauptkategorien sowie feste Fußreihenfolge `Papierkorb`, `Einstellungen` bestätigt.
- Isolierter Kategorie-Sortier- und Persistenztest: Reihenfolge geändert, gespeichert, aus SQLite neu gelesen und anschließend auf die Ausgangsreihenfolge zurückgesetzt.
- Isolierter Vorgangs-Drop-Pfad: fachliche Kategorie geändert; `WorkflowType` und `WorkflowStep` blieben unverändert.
- Organisatorische Navigation blieb `Übersicht, Alle Vorgänge, Angebote, Aufträge, Material, Termine, Schreibtisch`; der Papierkorb ist nicht mehr Teil dieser scrollenden Liste.
- Die macOS-Sitzung war weiterhin gesperrt. Echter Maus-Drag und sichtbare Abnahme wurden deshalb nicht als real bedient ausgewiesen.
- Eine alte temporäre Testkonfiguration enthielt zunächst noch einen produktiven Sync-Zielpfad. Die read-only Zeitstempelprüfung zeigte dort keine Dateiänderung aus dem Testzeitraum; vor dem nächsten Lauf wurde der Zielpfad auf den temporären Testordner korrigiert.

## Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
 M scripts/run-macos-bundle.sh
?? docs/codex_journal/2026-07-14_21-50_kategorienauswahl-papierkorb-drag.md
```

## Branch
codex/work

## Commit
53614f5a54f1f3605bdeeedffc05d88735aea98f

## Push erfolgreich
Ja

## Offene Punkte

- Echter Maus-Drag, sichtbarer Drag-Griff, vollständige Kategorienauswahl und Papierkorbposition konnten wegen der gesperrten macOS-Sitzung nicht real bedient beziehungsweise sichtbar abgenommen werden; Zustands- und Persistenzpfade waren isoliert erfolgreich.
- Windows-spezifische Bedienwege wurden gebaut, aber nicht real auf Windows getestet.
- Die lokale, nicht zu diesem Auftrag gehörende Änderung an `scripts/run-macos-bundle.sh` blieb unangetastet und wird nicht veröffentlicht.

## Empfohlener nächster Schritt

Die korrigierte Kategorienauswahl, den Kategorien-Drag, Vorgangs-Drop und den festen Navigationsfuß auf einer entsperrten macOS-Sitzung mit einem isolierten Testprofil sichtbar abnehmen.

1. App mit `BUEROCOCKPIT_DATA_DIRECTORY` und `BUEROCOCKPIT_LOCAL_CONFIG_DIRECTORY` auf temporäre Ordner starten.
2. Vorgänge per Maus zwischen zulässigen fachlichen Endkategorien ziehen und Kategorien über den neuen Drag-Griff umsortieren.
3. Haupt-/Endkategorien in der Auswahl und die Reihenfolge `Papierkorb`, `Einstellungen` sichtbar prüfen.
4. Nach Neustart Typ, Bearbeitungsstand, Kategorie, Kategorienreihenfolge, Filter und Zähler sichtbar sowie per SQLite prüfen.
