# Codex-Journal: Physischer iPad-Test und gekoppeltes Gerät löschen

## Ausgangslage

- Branch: `codex/work`
- Ausgangscommit: `73b67e9`
- Der Arbeitsbaum enthielt die noch nicht veröffentlichten Änderungen der
  vorherigen Workflow-, iPad- und Delta-Sync-Aufträge.
- Der inkrementelle Sync war automatisiert und im Simulator geprüft, aber noch
  nicht mit dem aktuellen Stand auf einem physischen iPad.
- Vorgemerkte oder freigegebene Geräte konnten in den Desktop-Einstellungen
  nur freigegeben oder widerrufen, aber nicht vollständig entfernt werden.

## Regel- und Sicherheitsprüfung

- Geprüft wurden `AGENTS.md`, `CODEX_PROJEKTREGELN.md`,
  `CODEX_AUFTRAGSPRUEFUNG.md`, `DESIGN_RICHTLINIEN.md`,
  `LOCAL_NETWORK_SYNC.md` und `TESTRICHTLINIEN.md`.
- Das Löschen wurde auf rein lokale Geräte- und Checkpointmetadaten begrenzt.
  Aufträge, Anhänge, Inbox-Pakete, Belege und Konflikte werden nicht gelöscht.
- Der physische Test verwendete einen isolierten Desktop-Testdienst auf Port
  53942 sowie eine getrennte, signierte iPad-Test-App mit eigener Bundle-ID.
  Die reguläre iPad-App und ihr Datencontainer wurden nicht ersetzt.

## Implementierung

- Die Geräteliste der Desktop-Einstellungen besitzt nun pro Eintrag
  `Gerät löschen`.
- Vor der Aktion erläutert ein Bestätigungsdialog, dass nur lokale Freigabe und
  Sync-Checkpoint entfernt werden und eine erneute Kopplung einen Erstabgleich
  benötigt.
- `LocalNetworkDeviceStore.Delete` entfernt den lokalen Geräteeintrag.
- `LocalSyncDeltaStore.DeleteDeviceCheckpoint` entfernt bestätigten und
  ausstehenden Checkpoint genau dieses Geräts.
- Der Checkpoint wird zuerst entfernt. Scheitert dieser Schritt, bleibt die
  Gerätefreigabe bestehen. Nach erfolgreicher Löschung verliert das Gerät auch
  bei laufendem Dienst sofort den Zugriff.

## Isolierte Prüfungen

- Der Workflow-Testlauf war vollständig erfolgreich.
- Das Löschen eines Geräts entfernt Freigabe und Checkpoint idempotent.
- Ein gelöschtes Gerät erhält bei laufendem HTTP-Dienst sofort HTTP 403.
- Der Checkpoint eines zweiten Geräts und bereits empfangene Inbox-Daten
  bleiben unverändert.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler.
- iOS-Simulator-Build auf `iPad Pro 13-inch (M5)` mit isoliertem DerivedData:
  erfolgreich.
- `git diff --check`: erfolgreich.

## Physisches iPad Air 7

- Der aktuelle iPad-Stand wurde für das reale Gerät mit automatischer
  Entwicklungssignatur gebaut.
- Die isolierte Test-App
  `de.buerocockpit.ipad.codexphysicaltest` wurde installiert und gestartet.
- Der echte `LocalNetworkManualSyncClient` erreichte den isolierten
  `LocalSyncService` über WLAN unter `192.168.178.52:53942`.
- Der Erstabgleich wurde geladen, auf dem Gerät mit `SnapshotReader` gelesen
  und bestätigt. Ergebnis: 1 Testauftrag, 4 Monteure (`Monteur Alpha`,
  `Monteur Beta`, `Monteur Gamma`, `Monteur Delta`), Revision `server-1`.
- Der direkt folgende zweite Sync verwendete den bestätigten Checkpoint,
  verlangte keinen Vollabgleich, übertrug 0 Aufträge und 0 Dateien und meldete
  `Keine Änderungen vorhanden`.
- Das Ergebnis wurde aus dem App-Datencontainer zurückgelesen. Danach wurden
  nur die isolierte Test-App und der Testdienst entfernt; Port 53942 war
  geschlossen. Die reguläre App `de.buerocockpit.ipad` blieb installiert.

## Offene Abnahme und Grenzen

- Der sichtbare Desktop-Bestätigungsdialog für `Gerät löschen` wurde noch nicht
  auf dem Firmen-Windows-Rechner bedient.
- Physische iPad-Neuanlage, Rückänderung, Foto-/Dateiübertragung,
  Konfliktentscheidung und echter Verbindungsabbruch bleiben Teil der
  vollständigen Endgeräteabnahme.
- Kein Commit, Push, Merge, Pull Request, Release, Tag oder Versionswechsel.
