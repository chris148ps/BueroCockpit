# Lokaler Netzwerk-Sync

Stand: 2026-07-16.

## Aktueller verbindlicher Stand

BüroCockpit Desktop bleibt das führende System. Der aktuell implementierte Sync ist bewusst gerichtet und manuell:

```text
iPad -> Desktop -> Sync/inbox
```

- Der Desktop-Sync-Dienst startet ausschließlich über `Lokalen Sync-Dienst starten` in den Einstellungen.
- Nur Verbindungssuche und sichtbare Statusprüfung dürfen im vorhandenen begrenzten Umfang laufen.
- Eine Datenübertragung beginnt ausschließlich durch `Jetzt synchronisieren` auf dem iPad.
- Der aktuelle Transport übernimmt mobile Eingänge samt `aufgabe.json`, Originalfotos, Vorschauen, markierten Fassungen, Skizzen und Dateien.
- Der Transport schreibt niemals direkt in die Desktop-Datenbank. Vollständig geprüfte Pakete werden zuerst unter `Sync/inbox/mobile-*` sichtbar.
- Der bestehende Desktop-Mobile-Inbox-Loader liest diese Eingänge zusätzlich zur tolerierten alten Struktur `mobile-inbox/mobile-*`.
- Desktop -> iPad über das lokale Netzwerk und eine bidirektionale Datenbankzusammenführung sind nicht implementiert.
- Bestehende Snapshot-/Lesedatenwege bleiben davon getrennt; Cloud- oder Live-Dateien sind kein Sync-Transport.

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

Bonjour/mDNS kündigt nur den bewusst gestarteten Dienst als `_buerocockpit._tcp` an. Die manuelle Desktop-Adresse/IP auf Port `53941` bleibt der Fallback. Die Suche ersetzt einen vorgemerkten Desktop niemals automatisch durch einen anderen.

## Bedienablauf

1. Benutzer startet den lokalen Sync-Dienst am Desktop.
2. Das iPad findet den Desktop per Bonjour oder verwendet die gespeicherte/manuell eingegebene Adresse.
3. `Diesen Desktop verwenden` erzeugt beziehungsweise verwendet eine stabile iPad-Geräte-ID und einen zufälligen lokalen Vertrauensnachweis.
4. `POST /local-sync/devices/remember` merkt das iPad am Desktop vor. Das ist noch keine Freigabe.
5. Der Desktop zeigt das vorgemerkte iPad unter `Lokaler Netzwerk-Sync`; der Benutzer wählt `Für Uploads freigeben`.
6. Erst `Jetzt synchronisieren` am iPad prüft Dienst und Kopplung und überträgt neue mobile Eingänge.
7. Der Desktop bestätigt ein Paket erst nach Validierung, vollständiger Staging-Ablage, atomarer Sichtbarmachung, Beleg und Sync-Protokoll.
8. Wiederholte manuelle Läufe vergleichen stabile IDs und Prüfsummen und erzeugen keine Duplikate.

Die iPad-Hauptansicht zeigt Ziel, Fortschrittsphase, Abschlusszahlen und den letzten erfolgreichen Zeitpunkt. Während eines Laufs ist die Schaltfläche gegen Mehrfachstart gesperrt.

## Fortschrittsphasen auf dem iPad

- Desktop wird gesucht
- Verbindung wird hergestellt
- Kopplung wird geprüft
- Daten werden verglichen
- Daten werden übertragen
- Fotos werden übertragen
- Übertragung wird bestätigt
- Synchronisation abgeschlossen

Nicht benötigte Übertragungsphasen dürfen übersprungen werden. Ein Lauf ohne neue Daten endet erfolgreich mit Nullwerten; lokale Dateien werden dabei nicht verändert.

## Kopplung und lokale Gerätezustände

Die iPad-Seite speichert Desktop-Zuordnung, stabile iPad-Geräte-ID und Vertrauensnachweis ausschließlich lokal in `UserDefaults`. Der Desktop speichert ausschließlich lokal in `BueroCockpitLocal/local-network-devices.json`:

- `deviceId`, Gerätename, Plattform und optionale App-Version
- erster und letzter Kontakt sowie letzte Remote-Adresse
- SHA-256-Hash des Vertrauensnachweises, niemals den offenen Nachweis
- Zustand `pending`, `trusted` oder `revoked`
- Zeitpunkt und Meldung des letzten bestätigten Uploads

Ein neuer oder geänderter Nachweis setzt das Gerät auf `pending`. Nur die ausdrückliche Desktop-Aktion setzt `trusted`; der Benutzer kann die Freigabe widerrufen. Die Prüfung verwendet einen zeitkonstanten Hashvergleich. Ein unbekanntes, vorgemerktes, widerrufenes oder falsch authentisiertes Gerät darf keine Daten hochladen.

## Implementierte Endpunkte

### Harmlose Statusendpunkte

```text
GET /health
GET /local-sync/status
GET /local-sync/changes/status
GET /local-sync/state
GET /pairing/status              (tolerierter alter Statusalias)
```

`/local-sync/status` liefert App, Status, Kompatibilitätsmodus `local-network-test`, Version, Desktopname, Desktop-Geräte-ID und `manualSyncAvailable`. Er liefert keine Aufgaben, Kategorien oder Anhänge. `changes/status` und `state` bleiben harmlose vorbereitete Metadatenendpunkte; `syncActive` bleibt `false`.

### `POST /local-sync/devices/remember`

Merkt ein iPad mit Geräte-ID, Anzeigename, Plattform, App-Version und lokalem Vertrauensnachweis vor. Die Antwort enthält `pairingStatus`; ein neues Gerät ist `pending` und benötigt die Desktop-Freigabe.

### `GET /local-sync/pairing/status`

Erwartet:

```text
X-BueroCockpit-Device-Id
X-BueroCockpit-Trust-Key
```

Nur `trusted` ergibt HTTP 200. `missing`, `invalid`, `pending` und `revoked` ergeben HTTP 403 mit verständlichem Status.

### `POST /local-sync/mobile-inbox`

Erwartet dieselben Authentisierungsheader und ein JSON-Paket nach Schema `local-sync-inbox-v1`. Binärdateien werden in diesem ersten klar begrenzten Transport Base64-kodiert im JSON übertragen. Begrenzungen:

- höchstens 250 Dateien
- höchstens 100 MiB pro Datei
- höchstens 220 MiB dekodierte Paketdaten
- höchstens 310 MiB HTTP-Request einschließlich Base64/JSON
- nur `aufgabe.json` sowie Pfade unter `originals`, `previews`, `annotated`, `sketches` und `files`

Absolute Pfade, Pfad-Traversal, doppelte Pfade, leere Dateien, Größenabweichungen, falsche SHA-256-Prüfsummen und unpassende Foto-/JSON-Signaturen werden abgelehnt. Chunked-HTTP-Anfragen werden unterstützt, aber beim Lesen tatsächlich begrenzt.

Antwortstatus:

- `accepted`: vollständig neu abgelegt
- `skipped`: identische stabile ID und identischer Inhalt bereits bestätigt
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

Die fachliche Übernahme eines sichtbaren mobilen Eingangs bleibt eine bewusste Desktop-Aktion. Danach verschiebt der vorhandene Ablauf neue Netzwerkeingänge nach `Sync/processed`; alte manuelle Eingänge behalten aus Kompatibilitätsgründen ihre alte Verarbeitungsstruktur.

## Idempotenz und Konflikte

- Mobile Entwürfe besitzen bereits eine stabile ID; diese ist Upload-ID und ID in `aufgabe.json`.
- Der Inhaltsfingerprint wird deterministisch aus normalisiertem Pfad, Größe und SHA-256 aller Dateien gebildet.
- Identische Wiederholungen werden bestätigt, aber nicht erneut angelegt.
- Abweichender Inhalt unter derselben ID überschreibt weder den vorhandenen Inbox-Eingang noch eine Desktopaufgabe. Er wird vollständig unter `Sync/conflicts` erhalten und als Konflikt gemeldet.
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
- Nach jedem Fehler bleibt `Jetzt synchronisieren` erneut verfügbar.

## Noch nicht implementiert

- lokaler Netzwerk-Lesestand Desktop -> iPad
- automatische oder vollständige bidirektionale Zusammenführung
- direkter fachlicher Import ohne Desktop-Benutzeraktion
- Upload-Streaming/Chunk-Wiederaufnahme innerhalb einer einzelnen großen Datei
- TLS für den lokalen HTTP-Transport
- sichtbare Konfliktauflösung über Desktop-UI
- sichtbare, bestätigte Bereinigung mobiler Originale

Der nächste Ausbau muss diese Grenzen respektieren und darf den gerichteten, idempotenten Inbox-Transport nicht durch eine unkontrollierte Datenbank-Synchronisation ersetzen.
