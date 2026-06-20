# BueroCockpitSnapshotReader

Native SwiftUI iPad-App für die reine Snapshot-Leseansicht.

## Start in Xcode

1. `iPad/BueroCockpitSnapshotReader/BueroCockpitSnapshotReader.xcodeproj` öffnen.
2. Zielgerät oder iPad-Simulator auswählen.
3. App starten und bevorzugt `latest.bcsnapshot` importieren.
4. Falls nötig, weiterhin einen Ordner mit `Sync/snapshots/` auswählen oder `metadata.json` laden.
5. Für ein echtes iPad in `Signing & Capabilities` ein Apple-Entwicklerteam wählen, falls Xcode das Team nicht automatisch setzt.

## Erwartete Dateien

- `metadata.json`
- `categories.json`
- `tasks.json`
- `attachments-index.json` optional
- `latest.bcsnapshot` als empfohlene Einzelfile
