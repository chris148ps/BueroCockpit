# Lokaler Netzwerk-Sync

Stand: 2026-07-18.

## Aktueller verbindlicher Stand

BüroCockpit Desktop bleibt das führende System. Der aktuell implementierte Sync
ist manuell und gerätebezogen inkrementell:

```text
Desktop --Delta seit bestätigtem Checkpoint--> iPad
iPad --ausstehende Mobile-Pakete--> Desktop-Upsert oder sichtbarer Konflikt
```

- Der Desktop-Sync-Dienst startet ausschließlich über `Lokalen Sync-Dienst starten` in den Einstellungen.
- Nur Verbindungssuche und sichtbare Statusprüfung dürfen im vorhandenen begrenzten Umfang laufen.
- Eine Datenübertragung beginnt ausschließlich durch `Jetzt synchronisieren` auf dem iPad.
- Nach erfolgreicher Kopplungsprüfung ruft das iPad
  `GET /local-sync/changes?since=<revision>` ab. Nur neue oder geänderte
  Aufträge, Kategorien, Monteure, Anhangsmetadaten, Vorschauen und unterstützte
  Tombstones werden übertragen.
- Nur bei fehlendem, falschem oder noch nicht bestätigtem Checkpoint liefert
  der Desktop die Anforderung für einen vollständigen Erstabgleich. Der
  iPad-Client prüft Vollpaket oder Delta vor der atomaren lokalen Übernahme.
- Lokale mobile Eingänge bleiben getrennt erhalten und werden durch einen
  Desktopabruf nicht überschrieben.
- Der Rücktransport übernimmt mobile Eingänge samt `aufgabe.json`,
  Originalfotos, Vorschauen, markierten Fassungen, Skizzen und Dateien.
- Vollständig geprüfte iPad-Pakete werden zuerst atomar unter
  `Sync/inbox/mobile-*` sichtbar. Konfliktfreie Änderungen werden danach
  idempotent in die Desktopdaten übernommen; erst dann antwortet der Desktop
  positiv.
- Der bestehende Desktop-Mobile-Inbox-Loader liest diese Eingänge zusätzlich zur tolerierten alten Struktur `mobile-inbox/mobile-*`.
- Bestehende Desktopaufträge können auf dem iPad für Notiz, stabile
  Kategorie-ID, Vorgangstyp/Status, Termin, Wiedervorlage samt Grund und
  Monteur offline bearbeitet werden. Die Änderung wird als eigenes
  versioniertes Inbox-Paket übertragen.
- Unabhängig geänderte Felder werden automatisch zusammengeführt. Bei
  gleichzeitiger Änderung desselben Felds bleiben Desktop- und iPad-Stand im
  Eingang erhalten; der bestehende Prüfdialog ermöglicht die manuelle
  Entscheidung.
- Die vollständige zentrale Monteurliste wird mit stabilen IDs als
  `technicians.json` übertragen. Damit enthält die Offline-Auswahl nicht mehr
  nur Monteure, die zufällig bereits einem exportierten Auftrag zugewiesen
  waren.
- Cloud, iCloud, OneDrive und bestehende gemeinsam gespeicherte Live-Dateien
  sind kein Transport dieses lokalen Netzwerk-Syncs.

## Dauerhafte Sicherheitsgrenzen

- kein automatischer Start des Desktop-Dienstes
- keine automatische Hintergrund-Datenübertragung
- kein Import allein durch Bonjour-Fund oder Statusprüfung
- kein UDP-Broadcast, Portscan oder Durchprobieren von Portbereichen
- keine Reaktivierung des alten Pairing-Codes
- kein iCloud-, OneDrive- oder `live.bclive`-Transport
- keine Übertragung einer vollständigen Datenbankdatei
- keine stille Datenmigration oder stille Überschreibung von Desktopdaten
- keine Löschung mobiler Originale in dieser Stufe
- keine Secrets in zentralen Einstellungen, Produktivdaten oder Repository

## Implementiertes Change-Tracking

Der normale, weiterhin ausschließlich über `Jetzt synchronisieren` ausgelöste
Lauf verwendet einen gerätebezogenen inkrementellen Austausch:

- Ein gültiger Checkpoint gehört immer genau zu einem freigegebenen Gerät.
- Nach einem bestätigten Erstabgleich werden nur neue, geänderte oder
  ausdrücklich unterstützte gelöschte beziehungsweise archivierte Objekte und
  Dateien übertragen.
- Der Desktop bildet aus stabiler Objekt-ID und SHA-256 des normalisierten
  Snapshotobjekts beziehungsweise der Datei einen Fingerprint. Eine globale
  monotone `serverSequence` ändert sich bei einem neuen Gesamtstand; jeder
  Geräteeintrag hält davon unabhängig seinen bestätigten Manifeststand.
- Prüfsummen und stabile IDs verhindern die erneute Übertragung unveränderter
  Anhänge. Geräteuhren entscheiden nicht darüber, ob ein Objekt übertragen
  wird.
- Ein Checkpoint wird erst nach erfolgreicher Übernahme und ausdrücklicher
  Protokollbestätigung fortgeschrieben. Bei Abbruch bleibt der vorherige
  Checkpoint bestehen.
- Authentisierte und vollständig validierte konfliktfreie Änderungen dürfen im
  manuellen Lauf idempotent per Upsert übernommen werden.
- Haben Desktop und iPad seit dem gemeinsamen bestätigten Stand denselben
  Datensatz oder dasselbe Feld geändert, bleiben beide Stände erhalten. Es
  erfolgt keine stille Überschreibung; der Konflikt wird sichtbar entschieden.
- Ein vollständiger Abgleich wird nur für Erstkopplung, leeren lokalen Stand,
  fehlenden oder inkompatiblen Checkpoint verwendet. Eine separate
  Reparaturschaltfläche ist noch nicht freigegeben und deshalb nicht sichtbar.
- Automatischer Hintergrundsync, automatischer Dienststart und unaufgeforderte
  Datenübertragung bleiben verboten.

Bonjour/mDNS kündigt nur den bewusst gestarteten Dienst als `_buerocockpit._tcp` an. Die manuelle Desktop-Adresse/IP auf Port `53941` bleibt der Fallback. Die Suche ersetzt einen vorgemerkten Desktop niemals automatisch durch einen anderen.

## Bedienablauf

1. Benutzer startet den lokalen Sync-Dienst am Desktop.
2. Das iPad findet den Desktop per Bonjour oder verwendet die gespeicherte/manuell eingegebene Adresse.
3. `Diesen Desktop verwenden` erzeugt beziehungsweise verwendet eine stabile iPad-Geräte-ID und einen zufälligen lokalen Vertrauensnachweis.
4. `POST /local-sync/devices/remember` merkt das iPad am Desktop vor. Das ist noch keine Freigabe.
5. Der Desktop zeigt das vorgemerkte iPad unter `Lokaler Netzwerk-Sync`; der
   Benutzer gibt das Gerät ausdrücklich frei.
6. Erst `Jetzt synchronisieren` am iPad prüft Dienst und Kopplung und fordert
   das Delta seit der letzten bestätigten Serverrevision an.
7. Bei einem fehlenden Checkpoint erfolgt einmalig der vollständige
   Erstabgleich. Ansonsten wird nur das Delta atomar in den lokalen
   Offline-Speicher eingespielt.
8. Das iPad sendet alle noch nicht bestätigten Mobile-Pakete. Der Desktop
   bestätigt sie erst nach Validierung, vollständiger Staging-Ablage,
   konfliktfreiem Upsert und sicherer Dateispeicherung.
9. Erst `POST /local-sync/ack` verschiebt den Desktop-Checkpoint auf die
   gelieferte Revision. Bei Abbruch bleibt der alte Stand bestehen.
10. Wiederholte manuelle Läufe vergleichen stabile IDs und Prüfsummen und
    erzeugen keine Duplikate.

Die iPad-Hauptansicht zeigt Ziel, Fortschrittsphase, Abschlusszahlen und den letzten erfolgreichen Zeitpunkt. Während eines Laufs ist die Schaltfläche gegen Mehrfachstart gesperrt.

## Fortschrittsphasen auf dem iPad

- Desktop wird gesucht
- Verbindung wird hergestellt
- Kopplung wird geprüft
- Daten werden verglichen
- Desktopdaten werden geladen
- Daten werden übertragen
- Fotos werden übertragen
- Lokaler Datenbestand wird aktualisiert
- Übertragung wird bestätigt
- Synchronisation abgeschlossen

Nicht benötigte Übertragungsphasen dürfen übersprungen werden. Ein Lauf ohne neue Daten endet erfolgreich mit Nullwerten; lokale Dateien werden dabei nicht verändert.

## Kopplung und lokale Gerätezustände

Die iPad-Seite speichert Desktop-Zuordnung, stabile iPad-Geräte-ID und Vertrauensnachweis ausschließlich lokal in `UserDefaults`. Der Desktop speichert ausschließlich lokal in `BueroCockpitLocal/local-network-devices.json`:

- `deviceId`, Gerätename, Plattform und optionale App-Version
- erster und letzter Kontakt sowie letzte Remote-Adresse
- SHA-256-Hash des Vertrauensnachweises, niemals den offenen Nachweis
- Zustand `pending`, `trusted` oder `revoked`
- Zeitpunkt und Meldung des letzten bestätigten Syncs
- bestätigte Serverrevision und -sequenz, bestätigte Clientsequenz sowie
  Sync-API-Version

Ein neuer oder geänderter Nachweis setzt das Gerät auf `pending`. Nur die
ausdrückliche Desktop-Aktion setzt `trusted`; der Benutzer kann die Freigabe
widerrufen oder das Gerät nach einer Bestätigung vollständig aus der lokalen
Geräteliste löschen. `Gerät löschen` entfernt ausschließlich den Eintrag aus
`local-network-devices.json` und den gerätespezifischen Stand aus
`local-network-sync-state.json`. Aufträge, Anhänge, mobile Eingänge,
Bestätigungsbelege und Konflikte bleiben erhalten. Das Gerät verliert bei
laufendem Dienst sofort den Zugriff; eine spätere erneute Kopplung beginnt mit
einem neuen Erstabgleich. Die Prüfung verwendet einen zeitkonstanten
Hashvergleich. Ein unbekanntes, vorgemerktes, widerrufenes, gelöschtes oder
falsch authentisiertes Gerät darf keine Daten hochladen.

Der ausführliche Fingerprint- und Checkpointstand liegt ausschließlich lokal in
`BueroCockpitLocal/local-network-sync-state.json`. Ein ausstehender Ack-Token
ist kein bestätigter Stand. Das iPad hält Revisionen, Sequenzen und Status
ausschließlich in `UserDefaults`.

## Implementierte Endpunkte

### Harmlose Statusendpunkte

```text
GET /health
GET /local-sync/status
GET /local-sync/changes/status
GET /local-sync/state
GET /pairing/status              (tolerierter alter Statusalias)
```

`/local-sync/status` liefert App, Status, Kompatibilitätsmodus
`local-network-test`, Version, Desktopname, Desktop-Geräte-ID,
`manualSyncAvailable`, `snapshotDownloadAvailable` und die
Snapshot-Schemaversion. Er liefert selbst keine Aufgaben, Kategorien oder
Anhänge. `changes/status` und `state` bleiben harmlose vorbereitete
Metadatenendpunkte; `syncActive` bleibt `false`.

### `POST /local-sync/devices/remember`

Merkt ein iPad mit Geräte-ID, Anzeigename, Plattform, App-Version und lokalem Vertrauensnachweis vor. Die Antwort enthält `pairingStatus`; ein neues Gerät ist `pending` und benötigt die Desktop-Freigabe.

### `GET /local-sync/pairing/status`

Erwartet:

```text
X-BueroCockpit-Device-Id
X-BueroCockpit-Trust-Key
```

Nur `trusted` ergibt HTTP 200. `missing`, `invalid`, `pending` und `revoked` ergeben HTTP 403 mit verständlichem Status.

### `GET /local-sync/changes?since=<revision>`

Dies ist der normale Desktop-zu-iPad-Pfad nach einem bestätigten Erstabgleich.
Die Antwort nach `local-sync-delta-v1` enthält:

- Ausgangs- und Zielrevision sowie monotone Serversequenz
- letzte am Desktop bestätigte Clientsequenz
- geänderte Aufträge, Kategorien, Monteure und Anhangsmetadaten
- Auftrag-, Kategorie-, Monteur- und Anhangs-Tombstones
- ausschließlich geänderte Paketdateien mit Länge, SHA-256 und Inhalt
- Anzahl der Änderungen und der unverändert übersprungenen Objekte/Dateien
- Ack-Token, sofern ein neuer Serverstand zu bestätigen ist

Fehlt der Geräte-Checkpoint oder stimmt `since` nicht mit ihm überein, enthält
die Antwort `requiresFullSync=true` und keine unkontrollierte Teillieferung.
Auch bei einem Abbruch wird der bestätigte Manifeststand nicht verändert.

### `GET /local-sync/snapshot`

Erwartet dieselben Authentisierungsheader. Nur ein `trusted`-Gerät erhält HTTP
200. Der Desktop erzeugt dafür temporär ein `local-sync-snapshot-v1`-Paket und
streamt es als `application/vnd.buerocockpit.snapshot+zip`. Das Paket enthält
keine SQLite-Datenbank. Es verwendet das vorhandene Snapshotformat mit
Kategorien, Aufgaben und sicheren Anhangsvorschauen beziehungsweise
Anhangsmetadaten.

Dieser Endpunkt ist der Kompatibilitäts- und Erstabgleichspfad für ein altes
iPad oder einen fehlenden Checkpoint. Die Antwort enthält
`X-BueroCockpit-Snapshot-Schema`, `X-BueroCockpit-Change-Version`,
`X-BueroCockpit-Server-Sequence`, `X-BueroCockpit-Ack-Token` und
`X-BueroCockpit-Created-At`. Temporäre Exportdaten werden nach der Antwort
entfernt. Ohne gültige Kopplung wird vor jedem Produktivdatenzugriff HTTP 403
geliefert. Ein alter iPad-Client kann das Paket weiterhin lesen, setzt aber
ohne den neuen Ack-Aufruf keinen Delta-Checkpoint.

### `POST /local-sync/ack`

Bestätigt genau den ausstehenden Ack-Token und die dazugehörige Serverrevision
für das authentisierte Gerät. Erst danach werden Servermanifest,
Serversequenz, Clientsequenz, Zeitpunkt, Status und API-Version als erfolgreich
fortgeschrieben. Ein falscher, alter oder nach einem Neustart nicht mehr
passender Token ergibt HTTP 409 und verändert den bestätigten Stand nicht.

### `POST /local-sync/mobile-inbox`

Erwartet dieselben Authentisierungsheader. Alte Neuanlagen bleiben als
`local-sync-inbox-v1` lesbar. Das gemeinsame Aufgabenmodell verwendet
`local-sync-inbox-v2` mit `operation=create|update`; Änderungen bestehender
Vorgänge tragen zusätzlich eine eigene Paket-ID, `desktopTaskId`,
`baseRevision`, `confirmedRevision` und die erhaltenen `baseValues`.
Binärdateien werden in diesem klar begrenzten Transport Base64-kodiert im JSON
übertragen. Begrenzungen:

- höchstens 250 Dateien
- höchstens 100 MiB pro Datei
- höchstens 220 MiB dekodierte Paketdaten
- höchstens 310 MiB HTTP-Request einschließlich Base64/JSON
- nur `aufgabe.json` sowie Pfade unter `originals`, `previews`, `annotated`, `sketches` und `files`

Absolute Pfade, Pfad-Traversal, doppelte Pfade, leere Dateien, Größenabweichungen, falsche SHA-256-Prüfsummen und unpassende Foto-/JSON-Signaturen werden abgelehnt. Chunked-HTTP-Anfragen werden unterstützt, aber beim Lesen tatsächlich begrenzt.

Antwortstatus:

- `accepted`: vollständig abgelegt, fachlich übernommen und bestätigt
- `skipped`: identische stabile ID und identischer Inhalt bereits fachlich
  bestätigt
- `conflict`: gleiche stabile ID mit abweichendem Inhalt; Desktopbestand bleibt unverändert
- `invalid`: unvollständiges oder ungültiges Paket
- `failed`: sichere Ablage oder Protokollierung fehlgeschlagen

## Paket- und Ablagestruktur

```text
BueroCockpit_Daten/Sync/
  inbox/
    mobile-<stabile-id>/
      aufgabe.json
      manifest.json
      originals/
      previews/
      annotated/
      sketches/
      files/
  receipts/
    <stabile-id>.json
  conflicts/
    mobile-<stabile-id>-conflict-.../
  processed/
  sync-log.jsonl
```

Jeder Upload beginnt in einem `.staging-*`-Ordner unter `Sync/inbox`. Dateien werden mit `FileShare.None` geschrieben und auf den Datenträger gespült. Erst nach vollständiger Prüfung wird der Ordner atomar umbenannt. Der Beleg speichert ID, Inhaltsfingerprint und Ziel. Wenn ein Prozess zwischen atomarer Ablage und Beleg abbricht, erkennt ein Wiederholungsversuch das vollständige Manifest am Ziel, stellt den Beleg wieder her und erzeugt kein Duplikat.

Nach der atomaren Ablage übernimmt der manuelle Sync konfliktfreie Neuanlagen
und Änderungen per Upsert. Mobile Neuanlagen behalten ihre stabile Aufgaben-ID.
Anhangs-IDs werden deterministisch aus Paket-ID und relativem Dateipfad
gebildet; die Datei wird vor dem Verschieben aus `Sync/inbox` in den verwalteten
Anhangsspeicher kopiert. Erst danach wandert das Paket nach `Sync/processed`.
Konfliktpakete bleiben unverändert in `Sync/inbox` und sind über den bestehenden
Desktop-Prüfdialog sichtbar. Alte manuelle Eingänge behalten aus
Kompatibilitätsgründen ihre alte Verarbeitungsstruktur.

## Idempotenz und Konflikte

- Mobile Entwürfe besitzen bereits eine stabile ID; diese ist Upload-ID und ID in `aufgabe.json`.
- Bei `v2` ist die Upload-ID die stabile Identität des einzelnen
  Änderungspakets. `desktopTaskId` bezeichnet davon getrennt den bestehenden
  Desktopvorgang; dadurch können mehrere Änderungen desselben Vorgangs
  idempotent und ohne gegenseitiges Überschreiben transportiert werden.
- Der Inhaltsfingerprint wird deterministisch aus normalisiertem Pfad, Größe und SHA-256 aller Dateien gebildet.
- Identische Wiederholungen werden bestätigt, aber nicht erneut angelegt.
- Abweichender Inhalt unter derselben Paket-ID überschreibt weder den
  vorhandenen Inbox-Eingang noch eine Desktopaufgabe. Er wird vollständig unter
  `Sync/conflicts` erhalten und als Konflikt gemeldet.
- Für bestehende Aufgaben vergleicht `MobileTaskRevisionService` je Feld den
  gemeinsamen Basiswert, den aktuellen Desktopwert und den iPad-Wert.
  Unabhängige Felder werden automatisch übernommen; gleichzeitige abweichende
  Änderungen bleiben zur manuellen Entscheidung erhalten.
- Dateiname oder Anzeigename allein dienen nie als Identität.
- Bereits erfolgreich abgelegte Pakete bleiben auch dann sicher, wenn ein späteres Paket fehlschlägt.

## Fotos, Skizzen und Dateien

Originalfotos werden zusammen mit vorhandenen Vorschauen übertragen. SHA-256, deklarierte Länge, Dateiendung und die grundlegende tatsächliche Signatur für JPEG, PNG, WebP und HEIC/HEIF werden geprüft. Skizzen einschließlich vorhandener `.pkdrawing`-Originale und sonstige Dateien bleiben als Dateien erhalten; sie werden nicht in die Datenbank oder eine Live-Datei eingebettet.

Die iPad-Implementierung löscht oder verschiebt nach einer Bestätigung noch keine lokalen Originale. Das ist absichtlich konservativer als eine automatische Bereinigung: bei Erfolg, Fehler, Konflikt oder Abbruch bleibt ein erneuter manueller Versuch möglich. Eine spätere sichtbare Bereinigungsfunktion ist ein eigener Auftrag.

## Fehlerverhalten

- Nicht erreichbarer oder nicht gefundener Desktop, Zeitüberschreitung und Verbindungsabbruch werden am iPad mit Handlungshinweis angezeigt.
- Fehlende, ungültige, ausstehende oder widerrufene Kopplung blockiert vor der Datenübertragung.
- Fehlende oder unlesbare iPad-Dateien führen zu keiner positiven Bestätigung.
- Ein fehlender/gesperrter Desktop-Datenordner oder Speicherfehler führt zu HTTP 503/Fehlerstatus.
- Ungültiges oder abgebrochenes JSON erzeugt keinen sichtbaren Teilimport.
- Prüfsummenfehler und unpassende Dateisignaturen erzeugen keinen sichtbaren Teilimport.
- Konflikte bleiben separat erhalten; der bestehende Desktopinhalt wird nicht überschrieben.
- Ein abweichender Objektstand wird zusätzlich feldweise im Desktop-Prüfdialog
  sichtbar. Nicht ausgewählte iPad-Werte überschreiben den Desktop nicht.
- Nach jedem Fehler bleibt `Jetzt synchronisieren` erneut verfügbar.

## Noch nicht implementiert

- sichtbare, bestätigte Reparaturschaltfläche für einen absichtlich
  vollständigen Neuaufbau
- iPad-seitige Lösch- oder Archivbefehle; aktuell werden nur
  Desktop-Tombstones zum iPad unterstützt
- Upload-Streaming/Chunk-Wiederaufnahme innerhalb einer einzelnen großen Datei
- TLS für den lokalen HTTP-Transport
- sichtbare, bestätigte Bereinigung mobiler Originale

Der iPad-Client löscht auch nach einer Desktopbestätigung keine Originale aus
der Fotomediathek oder aus seinem App-Speicher. Ein beschädigtes Delta, eine
fehlgeschlagene Dateiprüfung oder ein Konflikt verändert den bestätigten
Checkpoint nicht.
