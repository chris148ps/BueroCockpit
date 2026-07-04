# Lokaler Netzwerk-Sync

Stand: 2026-07-05.

Dieses Dokument beschreibt die Zielarchitektur fuer einen spaeteren lokalen Netzwerk-Sync zwischen BueroCockpit Desktop und der iPad-App. In diesem Stand ist auf der Desktop-Seite ein ausschliesslich manuell startbarer lokaler HTTP-Testdienst fuer Statusabfragen, lokale Geraete-Vormerkungen und harmlose Aenderungsmetadaten vorbereitet. Solange dieser Testdienst manuell laeuft, kann er sich optional per Bonjour/mDNS als `_buerocockpit._tcp` ankuendigen, damit das iPad geaenderte IP-Adressen auffinden kann. Wenn Bonjour nicht verfuegbar ist, bleibt die manuelle Desktop-Adresse/IP der Fallback und der Testdienst laeuft trotzdem weiter. Die iPad-App startet kuenftig direkt in die Hauptansicht und prueft die Desktop-Verbindung im sichtbaren Betrieb automatisch. Es wird keine produktive Synchronisation implementiert.

## 1. Ziel

Der lokale Netzwerk-Sync soll die bisherige iCloud-/Live-Aktualisierung langfristig abloesen. BueroCockpit Desktop bleibt das fuehrende System und nutzt weiterhin `OneDrive/BueroCockpit_Daten` als zentrale Datenquelle fuer Windows und Mac. Pairing-Code, Live-Datei, OneDrive-Live-Datei und `IpadLiveFileTargetPath` sind nicht der aktuelle Kopplungsweg fuer den lokalen Netzwerk-Sync. Das iPad wird als mobiler Erfassungsclient angebunden:

- Desktop stellt bei Bedarf einen aktuellen, iPad-lesbaren Snapshot oder Lesestand bereit.
- iPad uebertraegt mobile Eingaenge, Fotos, Skizzen, Notizen und sonstige Dateien manuell an den Desktop.
- Desktop speichert angenommene Uploads zunaechst kontrolliert im zentralen Datenordner, bevor sie fachlich importiert werden.
- Das iPad loescht lokale Originale erst nach bestaetigter Uebernahme.
- Jeder Sync-Lauf wird vom Benutzer bewusst gestartet und zeigt Status, Fehler und Protokoll.

## 2. Nicht-Ziele

Nicht Bestandteil dieses Konzeptschritts:

- kein ASP.NET-/Kestrel-Einbau
- kein Bonjour-/mDNS-Paket; die aktuelle Testdienstphase nutzt nur systemische APIs und behandelt Bonjour als optional
- kein automatisch gestarteter HTTP-Listener
- kein Serverstart im App-Lifecycle
- kein echter iPad-Sync und keine Produktivdatenuebertragung
- keine Datenuebertragung
- kein Umbau des Snapshot-Exports
- keine Datenmigration
- kein Verschieben von iCloud-, OneDrive- oder Produktivdateien
- kein FileSystemWatcher, kein Polling, kein LiveReload
- keine stille Hintergrundsynchronisation

## Phase 2: Desktop-Service-Geruest

Phase 2 legt nur ein Desktop-Service-Geruest an. Der Dienst ist standardmaessig deaktiviert und wird im App-Lifecycle nicht automatisch gestartet. Auch wenn lokale Einstellungen vorhanden sind, oeffnet BueroCockpit keinen Port und startet keinen HTTP-Server.

Vorbereitet sind:

- `LocalSyncService` als In-Memory-Service mit den Zustaenden `Disabled`, `Stopped`, `Starting`, `Running` und `Error`
- `LocalSyncOptions` fuer lokale, geraetespezifische Konfiguration
- lokale AppSettings fuer `LocalNetworkSyncEnabled`, `LocalNetworkSyncPort` und `LocalNetworkSyncDeviceName`
- Platzhaltermethoden fuer Status, Snapshot-Manifest und Mobile-Inbox-Manifestpruefung
- Platzhaltermethoden fuer Aenderungsmetadaten ohne Produktivdaten
- ein Desktop-Einstellungsabschnitt, in dem Geraetename und Port lokal bearbeitet werden koennen

Fuer Phase 2 galt:

- kein automatischer Start
- keine geoeffneten Ports
- kein `HttpListener`
- kein Kestrel oder ASP.NET
- keine neuen NuGet-Abhaengigkeiten
- keine echte Netzwerkkommunikation
- keine Ablage von Geraetename oder Port in `Sync/live/settings.json`
- keine iPad-Codeaenderung
- keine Produktivdatenausgabe im Platzhaltermanifest

Eine spaetere Aktivierung des lokalen Netzwerk-Syncs darf erst in einem separaten Auftrag erfolgen. Dann muessen Start/Stop-UI, Portwahl, Pairing-Sicherheit, Firewall-/macOS-Hinweise und die konkrete Transportimplementierung erneut bewusst entschieden werden.

## Phase 3: Manueller lokaler Testdienst

Phase 3 ergaenzt einen ersten echten Desktop-Testdienst. Er ist nur fuer einen technischen Verbindungstest gedacht und darf ausschliesslich durch den Button `Lokalen Testdienst starten` im Bereich `Lokaler Netzwerk-Sync` gestartet werden. Das Oeffnen der App, das Oeffnen der Einstellungen und vorhandene lokale Einstellungen starten keinen Dienst.

Der Dienst lauscht nach manuellem Start im lokalen Netzwerk auf dem lokal gespeicherten Port. Wenn die native DNS-SD-/Bonjour-Bibliothek verfuegbar ist, kuendigt er sich fuer die Dauer des manuell gestarteten Testdienstes per Bonjour/mDNS als `_buerocockpit._tcp` an. Wenn Bonjour nicht verfuegbar ist, zeigt die Desktop-App einen Hinweis und der HTTP-Testdienst bleibt trotzdem aktiv. Fuer iPad-Tests kann die aktuelle LAN-IP verwendet werden, zum Beispiel `http://192.168.x.x:53941/local-sync/status`; `127.0.0.1` ist nur fuer Tests direkt auf dem Desktop geeignet. Ohne gueltigen gespeicherten Port startet der Dienst nicht. Er stellt nur Statusendpunkte bereit:

```text
GET /health
GET /local-sync/status
GET /local-sync/changes/status
GET /local-sync/state
POST /local-sync/devices/remember
```

`/pairing/status` bleibt nur als tolerierter Alt-Endpunkt erhalten. Der aktuelle Bedienweg nutzt `/local-sync/status`.

Die Antwort enthaelt keine Produktivdaten:

```json
{
  "app": "BueroCockpit",
  "status": "ok",
  "mode": "local-network-test",
  "version": "0.4.14"
}
```

Weiterhin gilt:

- keine iPad-Kopplung und kein Abschluss einer Vertrauensbeziehung
- keine Aufgaben, Kategorien, Anhaenge oder Einstellungen in der Antwort
- Bonjour/mDNS optional, nur waehrend des manuell gestarteten Testdienstes und nur fuer `_buerocockpit._tcp`
- manueller IP-/Adresse-Fallback, wenn Bonjour nicht verfuegbar ist
- keine automatische Hintergrund-Geraetesuche
- kein UDP-Broadcast, keine Subnetzsuche und kein Portscan
- kein dauerhaftes Polling
- kein FileSystemWatcher
- keine Datenuebertragung

`/local-sync/changes/status` und `/local-sync/state` sind nur vorbereitete Metadaten-Endpunkte. Sie liefern keine Aufgaben, Kategorien, Anhaenge, Einstellungen oder sonstigen Produktivdaten:

```json
{
  "app": "BueroCockpit",
  "status": "ok",
  "mode": "local-network-test",
  "changeVersion": "placeholder-20260705120000000",
  "lastChangedUtc": "2026-07-05T12:00:00Z",
  "syncActive": false
}
```

Die lokale `changeVersion` kann beim Speichern im laufenden Desktop-Prozess aktualisiert werden. Das ist nur ein Platzhalter fuer spaetere automatische Aenderungsbereitstellung; daraus entsteht noch kein echter Sync und keine Datenuebertragung.

### iPad-Hauptansicht und Vormerkung in Phase 3

Das iPad startet direkt in die Hauptansicht. Ein vorgeschalteter Assistent, ein Startzwang ueber Live-Datei, OneDrive-/iCloud-Datei oder Pairing-Code gehoeren nicht mehr zum aktuellen Zielweg. Oben in der Hauptansicht zeigt ein kleiner Verbindungsindikator den lokalen Desktop-Status:

- gruener Punkt: `Desktop verbunden`
- roter Punkt: `Desktop nicht verbunden`
- gelb nur waehrend einer laufenden Pruefung

Wenn noch kein Desktop vorgemerkt ist, bleibt die Hauptansicht trotzdem sichtbar und weist dezent darauf hin, dass der Desktop-Testdienst in BueroCockpit manuell gestartet werden muss. Die App sucht den Desktop per Bonjour/mDNS, sofern verfuegbar. Die manuelle Desktop-Adresse/IP bleibt in den Sync-Einstellungen als Fallback erreichbar.

Das iPad kann einen erfolgreich geprueften Desktop lokal als kuenftigen Sync-Partner vormerken. Gespeichert werden nur Desktop-Adresse/IP, Port, Zeitstempel der letzten erfolgreichen Pruefung, lokaler Status sowie vorbereitend die letzte bekannte `changeVersion` und die letzte erfolgreiche Aenderungsstatus-Pruefung. Diese Vormerkung ist der aktuelle iPad-Bedienweg fuer den lokalen Netzwerk-Sync.

Beim Benutzerbefehl `Diesen Desktop verwenden` bleibt diese lokale iPad-Vormerkung die fuehrende lokale Quelle. Danach sendet das iPad einmalig `POST /local-sync/devices/remember` an den Desktop-Testdienst. Der Desktop speichert das iPad nur lokal in `BueroCockpitLocal/local-network-devices.json` und aktualisiert vorhandene Eintraege mit gleicher `deviceId`. Gespeichert werden `deviceId`, `deviceName`, `platform`, optional `appVersion`, `firstSeenUtc`, `lastSeenUtc`, optional `lastRemoteAddress` und `status = remembered`. Diese Geraetewerte werden nicht in die zentrale `settings.json` geschrieben und nicht in Produktivdaten uebernommen. Die Desktop-UI zeigt die vorgemerkte iPads / lokalen Geraete mit Status `vorgemerkt, Sync noch nicht aktiv`.

Die automatische Bonjour-/Netzwerksuche ist davon getrennt. Sie darf die gespeicherte Vormerkung nicht loeschen, nicht herabstufen und nicht durch eine widerspruechliche Hauptmeldung ueberschreiben. Wenn die Suche gerade keinen Desktop findet, bleibt der vorgemerkte Desktop sichtbar und die Suchmeldung lautet sinngemaess `Automatische Suche hat aktuell keinen Desktop gefunden.`. Wenn dieselbe Maschine wiedergefunden wird, duerfen Adresse/IP und Port aktualisiert werden. Ein anderer gefundener Desktop ersetzt die Vormerkung nur nach Benutzeraktion.

Die automatische Pruefung laeuft begrenzt: beim Start, bei Rueckkehr in den aktiven App-Zustand und im sichtbaren Betrieb in einem ruhigen Intervall. Sie stoppt oder pausiert, wenn die Hauptansicht nicht aktiv ist. Es gibt keinen UDP-Broadcast, keinen Portscan und keine aggressive Dauerschleife.

Live-Datei, OneDrive-/iCloud-Datei, `IpadLiveFileTargetPath` und Pairing-Code sind nicht mehr der aktuelle Kopplungsweg im iPad-Bereich `Lokaler Netzwerk-Sync`. Sie bleiben hoechstens Legacy/Fallback fuer bestehende Lesedaten oder tolerantes Lesen alter Einstellungen. Der manuelle IP-Fallback bleibt der verlaessliche Weg, wenn Bonjour/mDNS nicht verfuegbar ist.

## 3. Rollen

### Desktop als fuehrendes System

BueroCockpit Desktop fuehrt Datenbank, Aufgaben, Kategorien, Anhaenge, Schreibtischdaten und Importentscheidungen. Der Desktop entscheidet, ob ein mobiler Eingang angenommen, abgelehnt, als Konflikt markiert oder spaeter importiert wird.

OneDrive/BueroCockpit_Daten bleibt zentrale Datenquelle fuer Desktop-Geraete. Der lokale Netzwerk-Sync ist eine Transport- und Uebergabeschicht zwischen iPad und einem laufenden Desktop im Firmennetz, keine neue Hauptdatenquelle.

### iPad als mobiler Client

Das iPad liest nur freigegebene Snapshot-/Lesedaten und erfasst unterwegs neue Informationen. Es darf nicht direkt in die Desktop-Datenbank schreiben. Uploads sind mobile Eingangspakete, die der Desktop erst nach erfolgreicher Dateiablage, Pruefung und optionaler Benutzerfreigabe uebernimmt.

## 4. Datenfluss

### Desktop -> iPad: Snapshot/Lesestand

Der Desktop liefert auf Anforderung einen kompakten Lesestand. Grundlage ist die bestehende iPad-lesbare Exportstruktur:

```text
BueroCockpit_Daten/Sync/live/metadata.json
BueroCockpit_Daten/Sync/live/categories.json
BueroCockpit_Daten/Sync/live/tasks.json
BueroCockpit_Daten/Sync/live/settings.json
BueroCockpit_Daten/Sync/live/attachments-index.json
BueroCockpit_Daten/Sync/live/previews/
BueroCockpit_Daten/Sync/live.bclive
BueroCockpit_Daten/Sync/snapshots/latest.bcsnapshot
```

Fuer den Netzwerk-Snapshot gilt:

- nur lesbare, optimierte Daten
- keine Originalanhaenge ungefragt
- Aufgaben, Kategorien, Metadaten und zentrale Lesesettings
- Vorschauen nur dort, wo sie bereits fuer die iPad-Leseschicht vorgesehen sind
- keine Desktop-Datenbankdatei
- keine internen lokalen AppSettings
- keine Backups oder produktiven Rohordner

### iPad -> Desktop: mobile Eingaenge/Fotos/Skizzen

Das iPad sendet mobile Eingangspakete. Ein Paket besteht aus einem Manifest und referenzierten Dateien:

```text
manifest.json oder aufgabe.json
originals/
previews/
annotated/
sketches/
files/
```

Die bestehende mobile Inbox kennt bereits `mobile-*`-Eintraege mit `aufgabe.json`, Status `new`, Fotos, Vorschauen, markierten Versionen, Skizzen und Dateien. Der lokale Netzwerk-Sync soll dieses fachliche Format wiederverwenden oder explizit versioniert daraus ableiten.

## 5. Lokale Vertrauensbasis fuer spaeter

Der aktuelle Bedienweg in `docs/LOCAL_NETWORK_PAIRING.md` ist ein lokaler Netzwerk-Testweg ohne Einmal-Code. Dieses Sync-Konzept beschreibt nur den geplanten Ablauf rund um den spaeteren Transport.

Vor einem spaeteren echten Upload oder Abruf muss eine lokale Vertrauensbasis bewusst implementiert werden. Sie ist in diesem Stand nicht aktiv.

Geplanter Ablauf, noch nicht implementiert:

1. Benutzer startet auf dem Desktop bewusst eine neue Vertrauensfreigabe.
2. Desktop zeigt Geraetename, Zeitpunkt und angeforderte Rechte.
3. Benutzer bestaetigt die Gegenstelle am Desktop.
4. iPad und Desktop speichern lokale Geraetekenndaten fuer spaetere manuelle Sync-Laeufe.

Regeln:

- Spaetere Wiedererkennung erfolgt ueber lokal gespeicherte Geraetekenndaten und einen widerrufbaren Vertrauenswert.
- Der Vertrauenswert wird nicht dauerhaft offen angezeigt.
- Der Vertrauenswert kann am Desktop widerrufen werden.
- Unbekannte Geraete werden nie automatisch angenommen.
- Eine Vertrauensfreigabe berechtigt nicht zum direkten Schreiben in Produktivdaten.

## 6. Sicherheitsregeln

- Dienst nur im lokalen LAN nutzen.
- Keine externe Freigabe oder Cloud-Weiterleitung.
- Upload nur mit bewusst bestaetigter lokaler Vertrauensbasis.
- Status- und Logdaten nur fuer freigegebene Geraete, soweit sie nicht fuer den technischen Verbindungstest noetig sind.
- Keine automatische Annahme unbekannter Geraete.
- Keine stille Loeschung mobiler Originale.
- Desktop bestaetigt Uebernahme erst nach vollstaendiger Dateiablage und Pruefsumme.
- Uploads werden zuerst in Staging/Inbox abgelegt, nicht direkt fachlich importiert.
- Dateinamen und Pfade werden normalisiert; absolute iPad-Pfade werden nicht als Zielpfade verwendet.
- Upload-Groessen, Dateitypen und Paketanzahl werden begrenzt.
- ZIP-/Archiv-Inhalte duerfen keine Pfad traversal Eintraege enthalten.
- Fehler und Uebernahmen werden protokolliert.
- Konflikte werden sichtbar gemacht, nicht still ueberschrieben.

## 7. Beispiel-Endpunkte

Dies ist der aktuelle technische Testumfang plus spaetere Schnittstellenentwuerfe. Nur die ausdruecklich als aktuell implementiert markierten Endpunkte gehoeren zum jetzigen Testdienst.

### `GET /local-sync/status`

Aktuell implementierter technischer Verbindungstest. Liefert keine Produktivdaten.

Beispielantwort:

```json
{
  "app": "BueroCockpit",
  "status": "ok",
  "mode": "local-network-test",
  "version": "0.4.14"
}
```

### `GET /local-sync/changes/status`

Aktuell implementierter Platzhalter fuer spaetere automatische Aenderungspruefung. Liefert nur Metadaten und keine Produktivdaten. `syncActive` bleibt in diesem Schritt `false`.

Beispielantwort:

```json
{
  "app": "BueroCockpit",
  "status": "ok",
  "mode": "local-network-test",
  "changeVersion": "placeholder-20260705120000000",
  "lastChangedUtc": "2026-07-05T12:00:00Z",
  "syncActive": false
}
```

### `GET /local-sync/state`

Alias fuer den gleichen harmlosen Metadatenstatus wie `/local-sync/changes/status`.

### `POST /local-sync/devices/remember`

Aktuell implementierte lokale iPad-Geraetevormerkung. Der Endpunkt speichert nur lokale Geraete-Metadaten auf dem Desktop und uebertraegt keine Produktivdaten.

Beispielrequest:

```json
{
  "deviceId": "ipad-...",
  "deviceName": "iPad",
  "platform": "iPadOS",
  "appVersion": "1.0",
  "lastSeenUtc": "2026-07-05T12:00:00Z"
}
```

Beispielantwort:

```json
{
  "app": "BueroCockpit",
  "status": "ok",
  "mode": "local-network-test",
  "message": "Gerät vorgemerkt"
}
```

### `GET /status`

Spaeterer Entwurf fuer freigegebene Geraete. Nicht implementiert.

### `GET /snapshot`

Liefert den aktuellen iPad-Lesestand. Der Inhalt kann spaeter ein kompaktes Paket sein, beispielsweise eine optimierte Variante von `live.bclive` oder ein neues Snapshot-Paketformat.

Regeln:

- nur gekoppelte Geraete
- keine Originaldateien ungefragt
- kleine/optimierte Daten bevorzugen
- ETag, Exportzeit oder Checksumme fuer Wiederholungsabrufe vorsehen

### `POST /mobile-inbox`

Nimmt ein mobiles Eingangspaket entgegen. Der Desktop legt es zuerst unter einer definierten Inbox ab und bestaetigt erst danach die erfolgreiche Uebernahme.

Mindestanforderungen:

- Manifest mit `uploadId`, `deviceId`, `createdAt`, Dateiliste und Pruefsummen
- Dateien vollstaendig empfangen
- Pruefsummen stimmen
- Paket liegt in Staging/Inbox
- Sync-Protokoll wurde geschrieben

### `GET /sync-log`

Optionaler Endpunkt fuer die letzten Sync-Ereignisse. Er darf keine Tokens, lokalen Geheimnisse oder unnoetigen Produktivdaten ausgeben.

## 8. Datenablage im zentralen Ordner

Bestehende zentrale Struktur:

```text
BueroCockpit_Daten/
  buerocockpit.db
  Tasks/
  Backups/
  Sync/
    live/
      metadata.json
      categories.json
      tasks.json
      settings.json
      attachments-index.json
      previews/
    live.bclive
    inbox/
      changes/
      files/
    processed/
    conflicts/
    snapshots/
      latest.bcsnapshot
```

Empfohlenes Ziel fuer spaetere Netzwerk-Uploads:

```text
BueroCockpit_Daten/Sync/inbox/mobile-<yyyyMMdd-HHmmss>-<kurzid>/
  aufgabe.json
  manifest.json
  originals/
  previews/
  annotated/
  sketches/
  files/
```

Verarbeitung:

- Upload startet in einem temporaren Staging-Ordner unter `Sync/inbox`.
- Nach vollstaendiger Pruefung wird der Ordner atomar als `mobile-*` sichtbar gemacht.
- Nach fachlicher Uebernahme verschiebt der Desktop den Eintrag nach `Sync/processed` oder markiert ihn in der bestehenden verarbeiteten Struktur.
- Fehlerhafte oder widerspruechliche Pakete landen in `Sync/conflicts` oder bleiben mit Fehlerstatus sichtbar.

Kompatibilitaet:

- Die bestehende manuelle Mobile-Inbox-Struktur `mobile-inbox/mobile-*` bleibt Legacy/Kompatibilitaet.
- Neue Netzwerk-Uploads sollen bevorzugt unter `BueroCockpit_Daten/Sync/inbox` landen, weil diese Struktur bereits vom Sync-Root vorbereitet wird.
- Ein spaeterer Import darf beide Quellen bewusst lesen, aber nicht unkontrolliert mischen.

## 9. Fehlerfaelle

- Desktop nicht erreichbar: iPad zeigt Fehler und behaelt lokale Originale.
- Lokale Vertrauensbasis fehlt oder ist widerrufen: kein Upload, keine Snapshot-Ausgabe.
- Datenordner fehlt oder ist gesperrt: `/status` meldet Fehler, Upload wird abgelehnt.
- Upload unvollstaendig: Desktop verwirft Staging oder markiert Fehler; iPad behaelt Originale.
- Pruefsumme falsch: keine Uebernahmebestaetigung.
- Datei zu gross: Upload wird kontrolliert abgelehnt.
- Nicht erlaubter Dateityp: Datei oder Paket wird abgelehnt.
- Konflikt oder Dublette: Desktop legt Paket in Inbox/Konfliktbereich ab und verlangt Benutzerentscheidung.
- Import fachlich fehlgeschlagen: Dateiablage kann bestaetigt sein, fachliche Uebernahme aber nicht; iPad darf lokale Originale nur nach klarer Desktop-Bestaetigung loeschen.
- Protokoll kann nicht geschrieben werden: Sync gilt nicht als vollstaendig bestaetigt.

## 10. Offene technische Entscheidungen

- bewusster Vertrauensaufbau nach Bonjour-Fund oder manueller IP-Pruefung
- HTTP lokal oder andere Transportart
- TLS im lokalen Netz ja/nein
- Windows-Firewall-Freigabe und Benutzerfuehrung
- macOS-Netzwerkberechtigungen
- iPad-Dateigroessen und Chunking
- Upload-Wiederaufnahme nach Abbruch
- Pruefsummenalgorithmus, voraussichtlich SHA-256
- Manifestformat und Versionsnummern
- Konfliktbehandlung und Dublettenpruefung
- Speicherort und Verschluesselung lokaler Vertrauenswerte
- Sichtbarkeit und Rotation des Sync-Logs
- Begrenzungen fuer Upload-Groesse, Dateianzahl und erlaubte Dateitypen

Neue Abhaengigkeiten waeren erst bei einer echten Implementierung zu pruefen. Denkbar waeren spaeter TLS-/Zertifikatsunterstuetzung und weitere Transporthaertung. In diesem Schritt werden keine Abhaengigkeiten hinzugefuegt.

## 11. Stufenplan

### Phase 1: Konzept/DTOs

- Architektur und Datenregeln dokumentieren.
- Neutrale DTOs/Records fuer Status und Upload-Manifest vorbereiten.
- Keine Laufzeitlogik aktivieren.

### Phase 2: Desktop-Dienst optional hinter Schalter

- Lokalen Dienst nur manuell oder hinter klarer Einstellung starten.
- Statusanzeige, Firewall-Hinweise und Log vorbereiten.
- Kein automatischer Hintergrundbetrieb ohne Benutzerentscheidung.

### Phase 3: iPad als lokalen Sync-Partner vormerken

- Erfolgreich geprueften Desktop auf dem iPad vormerken.
- Bonjour-Suche und manuelle IP sauber getrennt anzeigen.
- Noch keine Produktivdaten uebertragen.

### Phase 4: Upload mobile Inbox

- iPad sendet mobile Eingangspakete.
- Desktop legt Pakete zuerst in `Sync/inbox` ab.
- Desktop bestaetigt erst nach Dateiablage und Pruefsumme.

### Phase 5: Snapshot abrufen

- iPad ruft manuell aktuellen Lesestand ab.
- Kleine/optimierte Daten bevorzugen.
- Originale nur ausdruecklich und einzeln nachladen, falls spaeter freigegeben.

### Phase 6: Bereinigung mobiler Originale nach Bestaetigung

- iPad loescht lokale Originale erst nach bestaetigter Desktop-Uebernahme.
- Loeschung bleibt sichtbar und darf nicht still nach einem Teilfehler erfolgen.
