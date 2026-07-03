# Lokaler Netzwerk-Sync

Stand: 2026-07-03.

Dieses Dokument beschreibt die Zielarchitektur fuer einen spaeteren manuellen lokalen Netzwerk-Sync zwischen BueroCockpit Desktop und der iPad-App. In diesem Stand ist auf der Desktop-Seite ein ausschliesslich manuell startbarer lokaler HTTP-Testdienst fuer Statusabfragen vorbereitet. Es wird keine produktive Synchronisation implementiert und keine iPad-Codeaenderung vorgenommen.

## 1. Ziel

Der lokale Netzwerk-Sync soll die bisherige iCloud-/Live-Aktualisierung langfristig abloesen. BueroCockpit Desktop bleibt das fuehrende System und nutzt weiterhin `OneDrive/BueroCockpit_Daten` als zentrale Datenquelle fuer Windows und Mac. Das iPad wird als mobiler Erfassungsclient angebunden:

- Desktop stellt bei Bedarf einen aktuellen, iPad-lesbaren Snapshot oder Lesestand bereit.
- iPad uebertraegt mobile Eingaenge, Fotos, Skizzen, Notizen und sonstige Dateien manuell an den Desktop.
- Desktop speichert angenommene Uploads zunaechst kontrolliert im zentralen Datenordner, bevor sie fachlich importiert werden.
- Das iPad loescht lokale Originale erst nach bestaetigter Uebernahme.
- Jeder Sync-Lauf wird vom Benutzer bewusst gestartet und zeigt Status, Fehler und Protokoll.

## 2. Nicht-Ziele

Nicht Bestandteil dieses Konzeptschritts:

- kein ASP.NET-/Kestrel-Einbau
- kein Bonjour-/mDNS-Paket
- kein automatisch gestarteter HTTP-Listener
- kein Serverstart im App-Lifecycle
- keine iPad-Netzwerkimplementierung
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
- Platzhaltermethoden fuer Status, Pairing-Code, Snapshot-Manifest und Mobile-Inbox-Manifestpruefung
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

Der Dienst bindet restriktiv an die lokale Adresse `127.0.0.1` und den lokal gespeicherten Port. Ohne gueltigen gespeicherten Port startet der Dienst nicht. Er stellt nur Statusendpunkte bereit:

```text
GET /health
GET /pairing/status
```

Die Antwort enthaelt keine Produktivdaten:

```json
{
  "app": "BueroCockpit",
  "status": "ok",
  "mode": "pairing-test",
  "version": "0.4.14"
}
```

Weiterhin gilt:

- keine iPad-Kopplung
- kein Pairing-Abschluss
- keine Aufgaben, Kategorien, Anhaenge oder Einstellungen in der Antwort
- keine automatische Geraetesuche
- kein Bonjour/mDNS
- kein UDP-Broadcast
- kein Polling
- kein FileSystemWatcher
- keine Datenuebertragung

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

## 5. Pairing-Konzept

Das gemeinsame lokale Pairing-Datenformat ist in `docs/LOCAL_NETWORK_PAIRING.md` definiert. Dieses Sync-Konzept beschreibt nur den geplanten Ablauf rund um den spaeteren Transport.

Pairing ist Voraussetzung fuer jeden Upload und fuer sensible Statusinformationen.

Vorgeschlagener Ablauf:

1. Benutzer startet auf dem Desktop "iPad koppeln".
2. Desktop erzeugt einen kurzlebigen Einmal-Code oder QR-Code.
3. iPad liest Code/QR und sendet eine Pairing-Bestaetigung.
4. Desktop zeigt Geraetename, Zeitpunkt und angeforderte Rechte.
5. Benutzer bestaetigt die Kopplung am Desktop.
6. iPad erhaelt einen `TrustKey` fuer spaetere manuelle Sync-Laeufe.

Regeln:

- Einmal-Code laeuft kurzzeitig ab.
- Pairing-Code wird nur einmal zur Erstkopplung verwendet.
- Spaetere Wiedererkennung erfolgt ueber gespeicherte `DeviceId` und `TrustKey`.
- TrustKey wird nicht dauerhaft offen angezeigt.
- TrustKey kann am Desktop widerrufen werden.
- Unbekannte Geraete werden nie automatisch angenommen.
- Pairing berechtigt nicht zum direkten Schreiben in Produktivdaten.

## 6. Sicherheitsregeln

- Dienst nur im lokalen LAN nutzen.
- Keine externe Freigabe oder Cloud-Weiterleitung.
- Upload nur mit gueltigem Pairing.
- Status- und Logdaten nur fuer gekoppelte Geraete, soweit sie nicht fuer die Kopplung selbst noetig sind.
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

Dies ist ein Schnittstellenentwurf, keine Implementierung.

### `GET /status`

Liefert Servername, App-Version, Datenordnerstatus und Pairing-Status.

Beispielantwort:

```json
{
  "serverName": "Mac mini",
  "appName": "BueroCockpit",
  "appVersion": "0.4.13",
  "dataFolderAvailable": true,
  "dataFolderDisplayName": "BueroCockpit_Daten",
  "pairingRequired": true,
  "pairedDeviceCount": 1,
  "serverTimeUtc": "2026-07-02T10:00:00Z"
}
```

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

### `POST /pairing/start`

Startet Kopplung oder gibt Metadaten fuer einen Einmal-Code/QR-Code aus.

### `POST /pairing/confirm`

Bestaetigt Kopplung mit Code und Geraetedaten. Die Desktop-App muss die Annahme unbekannter Geraete sichtbar machen und bestaetigen lassen.

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
- Pairing fehlt oder ist widerrufen: kein Upload, keine Snapshot-Ausgabe.
- Einmal-Code abgelaufen: neue Kopplung starten.
- Datenordner fehlt oder ist gesperrt: `/status` meldet Fehler, Upload wird abgelehnt.
- Upload unvollstaendig: Desktop verwirft Staging oder markiert Fehler; iPad behaelt Originale.
- Pruefsumme falsch: keine Uebernahmebestaetigung.
- Datei zu gross: Upload wird kontrolliert abgelehnt.
- Nicht erlaubter Dateityp: Datei oder Paket wird abgelehnt.
- Konflikt oder Dublette: Desktop legt Paket in Inbox/Konfliktbereich ab und verlangt Benutzerentscheidung.
- Import fachlich fehlgeschlagen: Dateiablage kann bestaetigt sein, fachliche Uebernahme aber nicht; iPad darf lokale Originale nur nach klarer Desktop-Bestaetigung loeschen.
- Protokoll kann nicht geschrieben werden: Sync gilt nicht als vollstaendig bestaetigt.

## 10. Offene technische Entscheidungen

- Bonjour/mDNS oder QR-Code-only fuer Geraetefindung
- HTTP lokal oder andere Transportart
- TLS im lokalen Netz ja/nein
- Windows-Firewall-Freigabe und Benutzerfuehrung
- macOS-Netzwerkberechtigungen
- iPad-Dateigroessen und Chunking
- Upload-Wiederaufnahme nach Abbruch
- Pruefsummenalgorithmus, voraussichtlich SHA-256
- Manifestformat und Versionsnummern
- Konfliktbehandlung und Dublettenpruefung
- Speicherort und Verschluesselung von Pairing-Tokens
- Sichtbarkeit und Rotation des Sync-Logs
- Begrenzungen fuer Upload-Groesse, Dateianzahl und erlaubte Dateitypen

Neue Abhaengigkeiten waeren erst bei einer echten Implementierung zu pruefen. Denkbar waeren spaeter ein lokaler HTTP-Server, Bonjour/mDNS und optional TLS-/Zertifikatsunterstuetzung. In diesem Schritt werden keine Abhaengigkeiten hinzugefuegt.

## 11. Stufenplan

### Phase 1: Konzept/DTOs

- Architektur und Datenregeln dokumentieren.
- Neutrale DTOs/Records fuer Status, Pairing und Upload-Manifest vorbereiten.
- Keine Laufzeitlogik aktivieren.

### Phase 2: Desktop-Dienst optional hinter Schalter

- Lokalen Dienst nur manuell oder hinter klarer Einstellung starten.
- Statusanzeige, Firewall-Hinweise und Log vorbereiten.
- Kein automatischer Hintergrundbetrieb ohne Benutzerentscheidung.

### Phase 3: iPad Pairing

- QR-/Einmal-Code-Ablauf implementieren.
- Token sicher speichern und widerrufbar machen.
- Geraeteverwaltung im Desktop anzeigen.

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
