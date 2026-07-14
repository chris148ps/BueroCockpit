# Codex-Journal: Tabellenbreiten von Kopf und Datenzeilen synchronisieren

## Ziel

Beim Ändern einer Spaltenbreite sollen Tabellenkopf und sämtliche Datenzeilen unmittelbar dieselbe Breite verwenden.

## Umsetzung

Die Kopf-Splitter speichern die Breite bereits in den lokalen Tabellenlayout-Einstellungen. Anschließend wird nun zusätzlich `RefreshTableProjection()` ausgeführt, damit die separat erzeugten `TableCellItem`-Zeilenbreiten sofort aus denselben Layoutwerten neu aufgebaut werden. Datenmodell, Persistenz der Fachdaten, Bindings und Fachlogik wurden nicht verändert.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`

## Tests

- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `./scripts/run-macos-bundle.sh Debug`: erfolgreich.
- Realer Desktopstart: erfolgreich.
- Auftragsliste geöffnet und einen Kopf-Splitter sichtbar verschoben.
- Sichtprüfung: die Spaltengrenze und der Inhalt der Datenzeilen bewegen sich gemeinsam; keine abgeschnittene oder überlappende Zeile sichtbar.

## Ergebnis

Spaltenbreiten wirken jetzt unmittelbar auf Tabellenkopf und Datenzeilen. Die lokale Layoutspeicherung bleibt erhalten.

## Bekannte offene Punkte

- Keine weiteren offenen Punkte zu diesem Fehler.

## Aktueller Git-Status

```text
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
?? docs/codex_journal/2026-07-12_14-21_tabellenbreiten-zeilen-synchronisieren.md
```
