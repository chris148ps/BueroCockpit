# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-12 14:17 +0200

## Letzter Auftrag

Reale Desktop-Abnahme der kompakten Vorgangsansichten

## Zusammenfassung

Die offenen sichtbaren Layout- und Navigationsabnahmen sind erfolgreich. Die Implementierung erfüllt die geprüften Anforderungen für kompakte Tabellen, verschiebbaren Detailbereich, Startansicht, Statusfolgen und Terminübersicht. Produktive Status- und Stammdaten blieben unverändert.

## Geänderte Dateien

- Keine Produktiv- oder Quellcodedatei in diesem Abnahmelauf.
- Dokumentationsdateien werden durch den vorgeschriebenen Runner aktualisiert.

## Tests

- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `./scripts/run-macos-bundle.sh Debug`: erfolgreich.
- Echter Desktopstart und vollständiger Neustart: erfolgreich; Start in Übersicht.
- Aufträge: kompakte Tabelle, Kundenname als sichtbare Primärbezeichnung, Detailinspektor und Splitter sichtbar.
- Splitter: nach links verschoben; Liste wurde breiter und Detailbereich schmaler, ohne Überlappung.
- Detailbreite: nach Neustart wiederhergestellt.
- Angebote: Angebotsablauf im Stepper sichtbar.
- Termine: Filter Alle sichtbar; 9 reale Termine chronologisch von 17.06.2026 bis 19.10.2026, keine Duplikate erkennbar.
- Tabellenspalte: Kunde per Drag-and-drop verschoben; neue Reihenfolge wurde unmittelbar angezeigt.
- Kategorienverwaltung: kein leerer Eintrag; Systemnavigation nicht als Verwaltungseintrag dargestellt.
- Große Fenstergröße: Oberfläche blieb erreichbar.
- Statusänderung, Speichern bestehender Aufträge, Technikeränderung und Kategorieanlage/-umbenennung wurden aus Sicherheitsgründen nicht mutiert getestet, damit keine Produktivdaten verändert werden.

## Git-Status

```text
 M docs/NEXT_TASK.md
?? docs/codex_journal/2026-07-12_14-17_desktop-abnahme-kompakte-vorgaenge.md
```

## Branch
codex/work

## Commit
7c97da1670279152e5d8a1169d6a88841c5e2f60

## Push erfolgreich
Ja

## Offene Punkte

- Ein mutierender End-to-End-Test für Status speichern und Techniker-Leerwert steht weiterhin aus, weil dafür ein isolierter Testdatenbestand erforderlich wäre.
- Die drei Tabellenlayouts sollten bei einer späteren manuellen Abnahme zusätzlich in mehreren kleinen Fensterbreiten geprüft werden.

## Empfohlener nächster Schritt

Isolierten, nicht-produktiven UI-Testdatenbestand für mutierende Status- und Techniker-Speichertests bereitstellen.

1. Einen temporären lokalen Datenordner ausschließlich für den Abnahmetest verwenden.
2. Direkten Auftrag und Angebotsvorgang anlegen oder vorhandene Testdaten verwenden.
3. Status und Techniker ändern, speichern, neu starten und die identische Listenanzeige prüfen.
