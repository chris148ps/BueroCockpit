# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-17 16:01 +0200

## Auftrag

Den reproduzierbaren macOS-Absturz beim Öffnen von `Alle Vorgänge` untersuchen und beheben.

## Diagnose und Sicherheit

- Regel-, Fach-, Design- und Testrichtlinien wurden vor der Änderung geprüft.
- Drei macOS-Absturzberichte von 15:52 Uhr zeigten denselben unbehandelten
  .NET-Fehler aus einem Skia-Dateistream.
- Die Absturzberichte gehörten über die Mach-O-UUID eindeutig zum aktuellen
  lokalen Debug-Bundle.
- Produktive Daten, OneDrive-Dateien und die produktive Sperrdatei wurden nicht
  verändert. Die Datenbank und lokalen Einstellungen wurden ausschließlich
  lesend in ein Verzeichnis unter `/private/tmp` kopiert.
- Mit einer nur lesend verknüpften, lokal nicht verfügbaren OneDrive-Miniatur
  wurde der Fehler exakt reproduziert:
  `System.IO.IOException: Operation timed out` im
  `SkiaSharp.SKManagedStream.OnReadManagedStream`.

## Änderung

- `ThumbnailBitmapCache` liest Miniaturdateien nun vollständig innerhalb des
  geschützten .NET-Blocks in den Speicher.
- Erst danach erhält Skia einen Memory-Stream. Ein Timeout, eine nicht
  verfügbare Cloud-Datei oder eine beschädigte Miniatur führt damit nur zu
  einer ausgelassenen Vorschau und nicht mehr zum Prozessabbruch.
- Auch die Metadatenabfrage liegt im Fehlerfang.
- Datenmodell, produktive Dateien, Navigation, Kategorien und Sync-Verhalten
  wurden nicht verändert.

## Reale Prüfung

- Das aktuelle macOS-Bundle wurde mit der kopierten Datenbank und isolierter
  lokaler Konfiguration gestartet.
- Vor der Änderung beendete genau die betroffene Miniatur die App beim Öffnen
  von `Alle Vorgänge`.
- Nach der Änderung öffnete sich `Alle Vorgänge` mit 41 Aufgaben und
  Vorgangsdetail vollständig. Die problematische Vorschau wurde übersprungen,
  der Prozess blieb stabil.

## Builds und Tests

- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet run --project
  tests/BueroCockpit.WorkflowTests/BueroCockpit.WorkflowTests.csproj`:
  erfolgreich.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r osx-arm64`: erfolgreich, 0 Warnungen, 0 Fehler.

## Git und Grenzen

- Arbeitsbranch: `codex/work`, Ausgangsstand
  `4209cbb6f9a0db076086434a1f7b76d5d444d601`.
- Kein Commit, Push, Tag, Versionswechsel oder Release.
- `docs/NEXT_TASK.md` bleibt bei genau einer nächsten Aufgabe: der bereits
  geplanten Zielgeräte-Abnahme auf Windows und dem physischen iPad.
