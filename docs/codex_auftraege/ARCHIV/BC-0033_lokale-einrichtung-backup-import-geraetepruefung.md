# Codex-Auftrag BC-0033

## Status

ERSETZT

## Datum

2026-07-20

## Titel

Sichtbare lokale Einrichtung und Backup-/Import-Geräteprüfung

## Ziel

Den aktuellen Mac bewusst vom noch vorhandenen OneDrive-Symlink auf einen
echten lokalen Produktivdatenordner umstellen und den neuen
Backup-Austauschablauf mit einem isolierten Testarchiv sichtbar abnehmen.

## Sicherheitsgrenzen

- Vor jeder Änderung Sollpfad, Symlinkziel und vorhandene Dateien nur lesend
  dokumentieren.
- Symlink erst nach ausdrücklicher Bestätigung entfernen.
- OneDrive-Altordner weder verschieben noch löschen.
- Für die sichtbare Erstabnahme ausschließlich isolierte Testdaten verwenden.
- Kein Commit, Push, Merge, Tag, Release oder Versionswechsel.

## Ergebnis

Der Auftrag wurde nicht begonnen. Der Nutzer hat am 23.07.2026 ausdrücklich
freigegeben, BC-0033 durch BC-0034 zu ersetzen. Produktive Daten, der
macOS-Sollpfad und der OneDrive-Altbestand wurden nicht verändert.

## Beziehungen

- Ersetzt durch: `BC-0034`
