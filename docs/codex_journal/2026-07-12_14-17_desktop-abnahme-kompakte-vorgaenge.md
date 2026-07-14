# Codex-Journal: Reale Desktop-Abnahme der kompakten Vorgangsansichten

## Ziel

Die noch offenen sichtbaren Abnahmepunkte für Splitter, Tabellenlayout, Startansicht, Statusfolgen, Termine und Kategorienverwaltung am regulär startbaren macOS-Bundle prüfen.

## Umsetzung

Die bestehende Umsetzung wurde ohne produktive Datenänderung sichtbar geprüft. Der Splitter ließ sich in beide Richtungen verschieben und blieb nach einem echten App-Neustart vorhanden; die vergrößerte Detailbreite wurde wiederhergestellt. Die App startete nach vollständigem Schließen erneut in Übersicht. Aufträge, Angebote und Termine waren als kompakte Tabellen erreichbar. Die Angebotsfolge Ansicht, Angebot, Auftrag, Material, Termin, Erledigt und die Direktauftragsfolge Auftrag, Material, Termin, Erledigt waren im Detailinspektor sichtbar. Die Terminliste enthielt reale Termine chronologisch und ohne erkennbare Duplikate. Die Kategorienverwaltung zeigte keinen leeren Eintrag und enthielt die vorhandenen Benutzerkategorien, während die Systemnavigation separat blieb. Eine Tabellenspalte ließ sich per Drag-and-drop verschieben. Es wurden keine Aufträge, Kategorien oder Statuswerte geändert.

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

## Ergebnis

Die offenen sichtbaren Layout- und Navigationsabnahmen sind erfolgreich. Die Implementierung erfüllt die geprüften Anforderungen für kompakte Tabellen, verschiebbaren Detailbereich, Startansicht, Statusfolgen und Terminübersicht. Produktive Status- und Stammdaten blieben unverändert.

## Bekannte offene Punkte

- Ein mutierender End-to-End-Test für Status speichern und Techniker-Leerwert steht weiterhin aus, weil dafür ein isolierter Testdatenbestand erforderlich wäre.
- Die drei Tabellenlayouts sollten bei einer späteren manuellen Abnahme zusätzlich in mehreren kleinen Fensterbreiten geprüft werden.

## Aktueller Git-Status

```text
 M docs/NEXT_TASK.md
?? docs/codex_journal/2026-07-12_14-17_desktop-abnahme-kompakte-vorgaenge.md
```
