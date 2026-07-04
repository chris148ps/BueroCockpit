# Lokaler Netzwerk-Sync

Stand: 2026-07-05.

Dieses Dokument beschreibt den aktuellen Bedien- und Zielweg fuer den lokalen Netzwerk-Sync zwischen BueroCockpit Desktop und iPad-App. Der Dateiname ist historisch; der aktuelle Bedienweg verwendet keinen Pairing-Code.

## Aktueller Zielweg

1. BueroCockpit Desktop bleibt das fuehrende System.
2. Der lokale Testdienst startet ausschliesslich manuell im Desktop-Bereich `Lokaler Netzwerk-Sync`.
3. Der Testdienst verwendet standardmaessig Port `53941`.
4. Das iPad sucht den Desktop per Bonjour/mDNS, falls verfuegbar.
5. Das iPad kann den Desktop jederzeit manuell ueber Desktop-Adresse/IP pruefen.
6. Ein erfolgreich gepruefter Desktop kann auf dem iPad als lokaler Sync-Partner vorgemerkt werden.
7. Das iPad registriert sich danach beim Desktop-Testdienst als vorgemerktes lokales Geraet.
8. Der Desktop zeigt vorgemerkte iPads im Bereich `Lokaler Netzwerk-Sync`.

In diesem Stand gibt es noch keinen echten Sync, keinen Kopplungsabschluss und keine Produktivdatenuebertragung.

## Desktop

Der Desktop-Bereich `Lokaler Netzwerk-Sync` zeigt:

- Status des lokalen Netzwerk-Syncs.
- Bonjour-Status.
- lokalen Testdienst manuell starten/stoppen.
- gespeicherten Port, standardmaessig `53941`.
- LAN-Adresse(n) fuer die manuelle iPad-Eingabe.
- Hinweis, dass Bonjour nur fuer die automatische Desktop-Suche benoetigt wird.

Der Testdienst stellt nur harmlose Statusendpunkte bereit:

```text
GET /health
GET /local-sync/status
POST /local-sync/devices/remember
```

`/pairing/status` bleibt nur als tolerierter Alt-Endpunkt erhalten, damit vorhandene Testclients nicht hart brechen. Der aktuelle iPad-Bedienweg nutzt `/local-sync/status`.

Beispielantwort:

```json
{
  "app": "BueroCockpit",
  "status": "ok",
  "mode": "local-network-test",
  "version": "0.4.14"
}
```

Die Antwort enthaelt keine Aufgaben, Kategorien, Anhaenge, Einstellungen oder sonstige Produktivdaten.

`POST /local-sync/devices/remember` nimmt nur lokale iPad-Metadaten entgegen:

```json
{
  "deviceId": "...",
  "deviceName": "...",
  "platform": "iPadOS",
  "appVersion": "...",
  "lastSeenUtc": "..."
}
```

Die Antwort lautet:

```json
{
  "app": "BueroCockpit",
  "status": "ok",
  "mode": "local-network-test",
  "message": "Gerät vorgemerkt"
}
```

Die Speicherung erfolgt ausschliesslich lokal unter `BueroCockpitLocal/local-network-devices.json`. Eintraege mit gleicher `deviceId` werden aktualisiert statt dupliziert. Die zentrale `settings.json` und Produktivdaten bleiben unangetastet.

Der Desktop-Bereich zeigt `vorgemerkte iPads / lokale Geraete`, den Geraetenamen, die Plattform, den letzten Kontakt und den Status `vorgemerkt, Sync noch nicht aktiv`. Wenn noch kein Geraet vorhanden ist, steht dort `Noch kein iPad vorgemerkt.`.

## iPad

Der iPad-Bereich `Lokaler Netzwerk-Sync` zeigt:

- automatische Desktop-Suche per Bonjour/mDNS.
- manuelle Desktop-Adresse/IP.
- Port `53941`.
- Button zum Pruefen des Desktop-Testdienstes.
- Button zum Vormerken eines erfolgreich geprueften Desktop.
- Status `Lokaler Desktop vorgemerkt`.
- letzte erfolgreiche Pruefung.
- getrennte Meldungen fuer Bonjour-Suche und manuelle Verbindung.
- Registrierungsstatus nach `Diesen Desktop verwenden`.

Ein manuell oder ueber die sichtbare Desktop-Liste vorgemerkter Desktop wird lokal in den iPad-Settings gespeichert:

- Desktop-Adresse/IP.
- Port.
- Zeitstempel der letzten erfolgreichen Pruefung.
- Status `Lokaler Desktop vorgemerkt` oder `Desktop im lokalen Netzwerk gefunden`.

Die automatische Suche darf diesen gespeicherten Desktop nicht loeschen, nicht herabstufen und nicht visuell durch eine Meldung ersetzen, die wie eine fehlende Einrichtung wirkt. Wenn die automatische Suche aktuell keinen Desktop findet, bleibt der Hauptstatus `Lokaler Desktop vorgemerkt`; nur die Suchmeldung darf auf `Automatische Suche hat aktuell keinen Desktop gefunden.` wechseln. Findet die Suche denselben Desktop wieder, duerfen Adresse/IP und Port aktualisiert werden. Findet die Suche einen anderen Desktop, wird er nur als weiterer gefundener Desktop angezeigt und erst nach Benutzeraktion vorgemerkt.

Wenn Bonjour keinen weiteren Desktop findet, aber bereits eine manuelle Adresse erfolgreich vorgemerkt ist, zeigt die App keine widerspruechliche Hauptmeldung. Stattdessen gilt sinngemaess:

- `Automatische Suche hat aktuell keinen Desktop gefunden.`
- oder bei fehlendem Bonjour: `Bonjour-Suche nicht verfuegbar; manuelle Adresse wird verwendet`

Beim Tippen auf `Diesen Desktop verwenden` speichert das iPad zuerst lokal den vorgemerkten Desktop. Danach sendet es einmalig `POST /local-sync/devices/remember` an den Desktop-Testdienst. Bei Erfolg zeigt die App `Desktop vorgemerkt, iPad am Desktop registriert.`. Wenn der POST fehlschlaegt, bleibt der lokale Desktop vorgemerkt und die App zeigt `Desktop lokal vorgemerkt. Registrierung am Desktop noch nicht möglich.`. Es gibt keinen Hintergrund-Retry und keine Endlosschleife; ein neuer Versuch passiert nur durch erneute Benutzeraktion.

## Bonjour/mDNS

Bonjour/mDNS ist ein optionaler Komfortweg fuer die automatische Desktop-Suche. Die manuelle IP-Eingabe bleibt immer der Fallback.

Auf Windows muss Bonjour/mDNS vorhanden sein, damit das iPad den Desktop automatisch finden kann. BueroCockpit prueft dafuer vorbereitend:

- Dienst `mDNSResponder` vorhanden.
- Dienst `mDNSResponder` laeuft.
- `dns_sd.dll` ist verfuegbar.

Fehlt Bonjour, darf die App nicht abstuerzen. Der lokale Testdienst darf weiterhin laufen und das iPad kann den Desktop per manueller Adresse pruefen.

Der Windows-Installer soll Bonjour kuenftig erkennen und optional installieren oder Benutzer zur Installation fuehren. Es werden keine fremden Bonjour-MSI/EXE-Dateien im Repository gebuendelt.

## Legacy/Fallback

Pairing-Code, Live-Datei, iCloud-Datei, OneDrive-Live-Datei und `IpadLiveFileTargetPath` sind nicht der aktuelle Kopplungsweg fuer den lokalen Netzwerk-Sync.

Bestehende lokale Settings duerfen alte Felder wie `LocalNetworkSyncPairingCode` oder `LocalNetworkSyncPairedDevices` weiterhin enthalten. Diese Felder werden tolerant gelesen, im aktuellen Netzwerk-Sync-Bedienweg aber ignoriert.

Snapshot-/Lesemodus, bestehende Mobile-Eingaenge und vorhandene Live-Datei-Importe bleiben als Legacy/Fallback erhalten, solange sie fuer vorhandene Datenanzeige oder manuelle Lesedaten noetig sind. Sie werden nicht automatisch migriert, geloescht oder als neuer lokaler Netzwerk-Sync beschrieben.

## Grenzen

- kein Release, kein Tag, keine Versionserhoehung.
- kein echter Sync in diesem Schritt.
- keine Aufgaben, Kategorien, Anhaenge oder sonstigen Produktivdaten uebertragen.
- kein Desktop-Autostart fuer den Testdienst.
- kein UDP-Broadcast.
- kein Portscan.
- keine unkontrollierte Hintergrundsuche.
- keine automatische Datei-/Cloud-Migration.
- keine Aenderung an produktiven OneDrive-/iCloud-Daten.
