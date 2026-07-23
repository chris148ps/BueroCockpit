# Codex-Journal: Inkrementeller Sync und vollständige Monteurliste

## Ausgangslage

- Branch: `codex/work`
- Ausgangscommit: `73b67e9`
- Der Arbeitsbaum enthielt bereits die noch nicht veröffentlichten Änderungen
  des vorherigen iPad-Revisionsauftrags.
- Der bisherige manuelle Lauf lud bei jedem Klick einen vollständigen
  Desktop-Snapshot. iPad-Änderungen wurden zwar idempotent nach `Sync/inbox`
  übertragen, aber erst nach einer bewussten Desktopaktion fachlich
  übernommen.
- Die iPad-Monteurauswahl leitete ihre Namen nur aus bereits exportierten
  Aufgaben ab. In dem beobachteten Stand waren deshalb nur zwei Monteure
  sichtbar.

## Regelprüfung und Freigabe

Die vorhandenen Regeln schlossen Netzwerk-Upserts aus. Die Umsetzung wurde
gestoppt und der Widerspruch gemeldet. Nach der ausdrücklichen Zustimmung des
Benutzers wurden die Regeln eng auf einen manuellen, authentisierten,
gerätebezogenen und konfliktgesicherten Delta-Sync erweitert. Automatischer
Dienststart, Hintergrundsync, Cloudtransport und stille Überschreibungen
bleiben verboten.

## Implementierung

### Desktop-Checkpoint und Delta

- `LocalSyncDeltaStore` liest das vorhandene plattformneutrale Snapshotpaket
  und bildet je stabiler ID einen SHA-256-Fingerprint.
- Eine globale monotone Serversequenz ändert sich nur bei geändertem
  Gesamtmanifest. Jedes gekoppelte Gerät besitzt seinen eigenen bestätigten
  Manifeststand und einen getrennten ausstehenden Ack-Stand.
- `GET /local-sync/changes?since=<revision>` liefert geänderte Aufgaben,
  Kategorien, Monteure, Anhänge, Dateien und unterstützte Tombstones.
- `POST /local-sync/ack` bestätigt nur den passenden Token und die passende
  Revision. Ein Abbruch oder falscher Token lässt den alten Checkpoint
  unverändert.
- `GET /local-sync/snapshot` bleibt für Erstabgleich und alte iPad-Clients
  erhalten und liefert für neue Clients Ack-Token und Sequenz in Headern.

### iPad-Übernahme

- Der iPad-Client ruft zuerst den Delta-Endpunkt auf. Nur bei
  `requiresFullSync` lädt er das Vollpaket.
- Delta-Dateien werden anhand Länge und SHA-256 geprüft.
- Der neue lokale Stand wird in ein Staging-Verzeichnis geschrieben und über
  Verzeichniswechsel atomar installiert. Erst danach folgt die Ack-Anfrage.
- Revision, Sequenzen, API-Version und letzter Status liegen gerätelokal in
  `UserDefaults`.

### iPad zu Desktop

- Mobile Pakete erhalten monotone Clientsequenzen und bleiben bis zur
  Desktopbestätigung ausstehend.
- Der Desktop validiert und staged weiterhin zuerst unter `Sync/inbox`.
- Konfliktfreie Neuanlagen und Änderungen werden innerhalb desselben manuellen
  Requests fachlich gespeichert. Erst danach erhält das iPad
  `accepted` beziehungsweise `skipped`.
- Mobile Aufgaben-IDs bleiben beim Desktop-Upsert erhalten.
- Mobile Anhangs-IDs werden deterministisch aus Paket-ID und relativem Pfad
  gebildet. Dateien werden vor dem Verschieben nach `Sync/processed` in den
  verwalteten Anhangsspeicher kopiert.
- Unabhängige Felder werden automatisch zusammengeführt. Gleichzeitige
  Änderungen desselben Felds ergeben HTTP 409, bleiben im Eingang und sind im
  vorhandenen Prüfdialog manuell entscheidbar.

### Monteure

- Zentrale Monteurprofile erhalten beim Laden fehlender Altprofile einmalig
  stabile IDs.
- Das lokale Netzwerk-Snapshotpaket enthält `technicians.json` mit allen
  zentral konfigurierten Profilen.
- Die iPad-Auswahl verwendet diese Liste als Quelle und ergänzt nur zur
  Legacy-Toleranz Namen aus bestehenden Aufgaben.

## Isolierte Prüfungen

- Der Workflow-Testlauf war vollständig erfolgreich.
- Checkpointfälle: neues Gerät, Erstabgleich, falscher Ack, richtiger Ack,
  App-/Store-Neustart, zweiter Lauf ohne Änderung, Abbruch vor Ack,
  Wiederholung, mehrere Geräte und verlorener Checkpoint.
- Deltafälle: genau ein geänderter Auftrag, geänderte Original-/Vorschaudatei,
  unveränderte Objekte, Kategorie, Monteur, Auftrag-/Anhang-Tombstones.
- Rückweg: neues Paket, versionierte Änderung, stabile ID, Wiederholung,
  Transportkonflikt, falsche Prüfsumme, unvollständiges Paket, mehrere Fotos,
  Skizze, Datei und Wiederaufnahme nach fehlendem Beleg.
- Monteurliste: vier konfigurierte Profile ergaben vier unterschiedliche IDs im
  Paket.
- HTTP: fehlende, falsche und gültige Kopplung, Vollsnapshot, Ack,
  anschließendes leeres Delta und abgebrochenes JSON.

Messung des reproduzierbaren Testbestands:

- 100 Aufgaben vorhanden, 1 geändert, 1 übertragen
- 1 Anhang vorhanden, 1 geändert, 1 übertragen
- Vollpaket 2758 Byte, Deltaantwort 1213 Byte
- HTTP-Loopback Vollabgleich 1357 Byte/1,90 ms
- HTTP-Loopback Leerlaufdelta 481 Byte/2,86 ms

Die Laufzeiten sind lokale Einzelmessungen und kein Ersatz für die reale
WLAN-Messung.

## Builds

- `git diff --check`: erfolgreich
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler
- `dotnet build -r osx-arm64`: erfolgreich, 0 Warnungen, 0 Fehler
- iOS-Simulator-Build mit
  `/private/tmp/BueroCockpitSnapshotReaderDerivedData`: erfolgreich

## Offene Abnahme und Grenzen

- Physisches iPad, reales WLAN und Firmen-Windows-Rechner wurden nicht bedient.
- Eine sichtbare Reparaturschaltfläche ist nicht implementiert.
- iPad-seitige Lösch-/Archivbefehle und Chunk-Wiederaufnahme einer einzelnen
  großen Datei sind nicht implementiert.
- Mobile Originale werden nach Bestätigung nicht automatisch gelöscht.
- Es wurden kein Commit, Push, Merge, Pull Request, Release, Tag oder
  Versionswechsel ausgeführt.
