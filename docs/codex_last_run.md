# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-12 20:37 +0200

## Letzter Auftrag

Spaltenverschieben und Spaltenbreitenänderung in den kompakten Tabellen trennen

## Zusammenfassung

Spaltenverschieben und Spaltenbreitenänderung sind in Aufträgen, Angeboten und Terminen getrennt bedienbar. Breitenänderungen wirken live auf Kopf und Datenzellen, bleiben je Ansicht getrennt gespeichert und werden nach Neustart wiederhergestellt.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml`
- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`
- `/Users/christian/AppProjekte/BueroCockpit/docs/PROJEKTSTATUS.md`
- Dokumentationsdateien werden durch den vorgeschriebenen Runner aktualisiert.

## Tests

- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `./scripts/run-macos-bundle.sh Debug`: erfolgreich.
- Realer Desktopstart des aktuellen Bundles: erfolgreich; Start in Übersicht.
- Aufträge: zwei Spalten verbreitert/verkleinert, Spalte verschoben, danach erneut Breite geändert, Titel eingeblendet und Titelbreite geändert, horizontal gescrollt, Neustartpersistenz von Reihenfolge/Breite geprüft.
- Angebote: zwei Spalten verbreitert/verkleinert, Spalte verschoben, danach erneut Breite geändert, horizontaler Tabellenbereich geprüft, Neustartpersistenz von Reihenfolge/Breite geprüft.
- Termine: zwei Spalten verbreitert/verkleinert, Spalte verschoben, danach erneut Breite geändert, horizontal gescrollt, Neustartpersistenz von Reihenfolge/Breite geprüft.
- Kopf- und Datenzeilen: sichtbare Spaltengrenzen blieben nach den Änderungen gemeinsam ausgerichtet.
- Produktive Aufträge, Kategorien, Statuswerte und sonstige Fachdaten wurden nicht verändert.

## Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? docs/codex_journal/2026-07-12_20-37_spalten-bedienbereiche-trennen.md
```

## Branch
codex/work

## Commit
2750e85e252dcb75a3efc411cdc31495f22d1db4

## Push erfolgreich
Ja

## Offene Punkte

- Produktive mutierende Status-/Monteurtests wurden weiterhin nicht ausgeführt; sie gehören nicht zu diesem Layoutauftrag.

## Empfohlener nächster Schritt

Keine weitere Layoutkorrektur; als nächstes kann ein isolierter Testdatenbestand für mutierende Workflow-Speichertests vorbereitet werden.

1. Temporären lokalen Testdatenordner bereitstellen.
2. Status- und Technikeränderungen ohne Produktivdaten testen.
