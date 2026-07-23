# Codex-Journal: OneDrive-Produktivlogik bereinigt

## Zeitpunkt

2026-07-20 01:24 +0200

## Auftrag

BC-0031 – Eindeutig veraltete OneDrive- und Desktop-Dateitransportlogik
entfernen. Lokale Produktivdaten, Backup-/Import-ZIP, Pfadsicherung,
iPad-Netzwerk-Sync und Mobile Inbox mussten erhalten bleiben.

## Ausgangslage und Grenzen

- Der Arbeitsbranch `codex/work` war bereits umfangreich geändert; alle
  vorhandenen Änderungen wurden erhalten.
- Produktive Datenbanken, OneDrive-Dateien und Cloudordner durften nicht
  geöffnet, kopiert, migriert oder verändert werden.
- Kein Commit, Push, Merge, Tag, Release oder Versionswechsel.
- Die sichtbare Backup-/Import-Prüfung auf realen Geräten blieb ausdrücklich
  verschoben.

## Umsetzung

- `OneDriveEditDirectory` und `IpadLiveFileTargetPath` aus dem aktiven
  Desktop-Einstellungsmodell entfernt.
- Alte SaveNow-, Repository- und UI-Trigger für dateibasierte iPad-Snapshots
  entfernt.
- Diagnosezeilen und Statuszustände des alten Desktop-Dateiexports entfernt.
- Den Netzwerk-Snapshotgenerator auf ein direktes temporäres
  `current.bcsnapshot` umgestellt; `live.bclive` wird auf dem Desktop nicht
  mehr erzeugt oder als Transportziel verwendet.
- Vollständige Monteurprofile bleiben Bestandteil des Netzwerkpakets.
- Die automatische Kürzung alter absoluter Windows-, macOS- und Cloudpfade auf
  den aktuellen lokalen Datenordner entfernt.
- Mobile-Inbox-Suche und Bereinigung auf lokale Appdaten und lokale
  Sync-Unterordner begrenzt.
- Alte OneDrive-/Live-Einstellungsfelder in den aktuellen Konzeptdokumenten als
  entfernt statt als aktiv tolerierte Eigenschaften dokumentiert.
- Den verbindlichen Standard-Lesestapel in `AGENTS.md` auf
  `AGENTS.md`, Projektregister, Projektstatus, letzten Lauf, nächste Aufgabe
  sowie die beiden aktuellen Auftragszeiger begrenzt.
- Fachdateien werden nur thematisch, `INDEX.md` nur bei unklarem Zeiger oder
  historischem Bezug und Archive/Journale nur bei konkreter Verweisung gelesen.
- `PROJEKTREGISTER.md` als maßgebliche Quelle für dauerhafte
  Architekturentscheidungen festgelegt; Status- und Verlaufsdateien bleiben
  Nachweise des erreichten oder historischen Stands.

## Bewusst erhalten

- `StorageLocationService` und Startblockade bei Symlink/Junction.
- Kontrollierter Sicherheitsdialog und SQLite-Pfaddiagnose.
- `BackupExchangeService`, ZIP-Manifest, Größen-/SHA-256-Prüfung,
  SQLite-Integritätsprüfung und lokales Rückfall-Backup.
- Lokale Backups, lokaler Netzwerkdienst, Delta-Sync, Kopplung,
  Geräteentkopplung und Mobile Inbox.
- Alle Dateien unter `Services/LocalSync/`; die Referenzprüfung ergab aktive
  Verwendungen.
- Historische iPad-Lesekompatibilität wurde nicht geändert, weil BC-0031 den
  Desktop-Produktivweg betrifft.

## Prüfungen

- `git diff --check`: erfolgreich.
- Backup-Austauschtests: erfolgreich, alle 13 Prüfgruppen grün.
- Workflow-/Kategorie-/Netzwerk-Integrationstests: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r osx-arm64`: erfolgreich, 0 Warnungen, 0 Fehler.
- Kein `xcodebuild`, da keine iPad-Datei geändert wurde.

## Offener manueller Nachweis

Die sichtbare Backup-/Import-Geräteprüfung bleibt offen. Dafür muss der
macOS-Sollpfad erst nach eigener Freigabe bewusst lokal eingerichtet werden.
Der OneDrive-Altordner und der vorhandene Symlink blieben unverändert.
