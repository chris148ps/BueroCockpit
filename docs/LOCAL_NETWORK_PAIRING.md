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

Wenn Bonjour keinen weiteren Desktop findet, aber bereits eine manuelle Adresse erfolgreich vorgemerkt ist, zeigt die App keine widerspruechliche Hauptmeldung. Stattdessen gilt sinngemaess:

- `Automatische Suche hat keinen weiteren Desktop gefunden`
- oder bei fehlendem Bonjour: `Bonjour-Suche nicht verfuegbar; manuelle Adresse wird verwendet`

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
