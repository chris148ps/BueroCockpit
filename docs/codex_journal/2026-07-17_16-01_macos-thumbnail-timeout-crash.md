# Codex-Journal: macOS-Absturz durch Cloud-Miniatur

Datum: 2026-07-17 16:01 +0200

Ausgangscommit: `4209cbb6f9a0db076086434a1f7b76d5d444d601`

Arbeitsbranch: `codex/work`

## Auftrag

Die App startete auf dem Mac, stürzte aber beim Öffnen von Bereichen wie
`Alle Vorgänge` ab. Der Fehler sollte geprüft und behoben werden.

## Befund

Drei aktuelle macOS-Diagnoseberichte zeigten einen `SIGABRT` nach einer
unbehandelten .NET-Ausnahme im Skia-Lesepfad. Die Mach-O-UUID stimmte mit dem
aktuellen lokalen App-Bundle überein.

Für die Reproduktion wurde die produktive SQLite-Datenbank ausschließlich
lesend nach `/private/tmp` kopiert. Lokale Einstellungen wurden ebenfalls
kopiert. Produktive Daten, OneDrive-Dateien und die produktive Sperrdatei
blieben unverändert.

Eine in OneDrive formal vorhandene, aber lokal nicht abrufbare
Anhang-Miniatur löste den Absturz zuverlässig aus. Der vollständige Fehler war
`System.IO.IOException: Operation timed out` aus
`SkiaSharp.SKManagedStream.OnReadManagedStream`. Der bisherige
`Bitmap(path)`-Aufruf übergab Skia einen Dateistream; ein späterer Fehler aus
dem nativen Read-Callback konnte den umgebenden Fehlerfang nicht sicher
erreichen.

## Korrektur

`ThumbnailBitmapCache.Load` liest Bilddaten nun zuerst mit
`File.ReadAllBytes` innerhalb des vorhandenen Fehlerfangs und erzeugt das
Bitmap anschließend aus einem Memory-Stream. Nicht verfügbare oder beschädigte
Miniaturen liefern weiterhin `null`, können den Prozess aber nicht mehr über
einen nativen Dateistream beenden.

## Prüfung

- Vorher: Öffnen von `Alle Vorgänge` mit der problematischen Miniatur beendet
  die App reproduzierbar.
- Nachher: `Alle Vorgänge` öffnet sich mit 41 Aufgaben und dem ausgewählten
  Vorgangsdetail; die problematische Vorschau wird ausgelassen und die App
  bleibt aktiv.
- Die Prüfung verwendete ausschließlich isolierte Datenpfade.
- `dotnet build`, `dotnet build -r win-x64` und
  `dotnet build -r osx-arm64` waren ohne Warnungen und Fehler erfolgreich.
- Die vorhandenen Workflow-/Kategorie-Integrationstests waren erfolgreich.
- Kein Release, Tag, Versionswechsel oder Eingriff in Produktivdaten.
