# Lokaler Netzwerk-Sync

Stand: 2026-07-04.

Dieses Dokument beschreibt den aktuellen Zielweg fuer den lokalen Netzwerk-Sync zwischen BueroCockpit Desktop und iPad-App. Der aktuelle Stand ist ein technischer Verbindungstest: Desktop-Testdienst manuell starten, Desktop-Adresse auf dem iPad pruefen oder optional per lokalem Bonjour/mDNS-Dienst finden, keine Datenuebertragung, kein Sync und kein Pairing-Abschluss. Die lokale Dienstsuche laeuft nur, solange der Bereich `Lokaler Netzwerk-Sync` auf dem iPad sichtbar ist. Es gibt keine Hintergrundsuche ausserhalb dieser Ansicht und keine Produktivdaten.

## Aktueller Stand

Der aktuelle iPad-Bedienweg im Bereich `Lokaler Netzwerk-Sync` bleibt manuell pruefbar und wird um lokale Dienstfindung ergaenzt:

1. BueroCockpit am Desktop oeffnen.
2. Lokalen Testdienst am Desktop manuell starten.
3. iPad sucht den Desktop optional per Bonjour/mDNS im lokalen Netzwerk oder die Desktop-Adresse/IP wird manuell eingetragen.
4. iPad prueft den Desktop im lokalen Netzwerk.
5. Spaeter diesen Desktop als lokalen Sync-Partner verwenden.

Die iPad-App zeigt dafuer die Desktop-Adresse, den festen Test-Port `53941`, gefundene BueroCockpit-Desktops, den Button `Desktop-Testdienst pruefen` und einen klaren Status. Wenn keine Adresse eingetragen ist, wird `Bitte Desktop-Adresse oder IP eintragen.` bzw. fuer die Automatik `Desktop-Adresse fehlt` angezeigt. Die manuelle Adresseingabe bleibt als Fallback erhalten.

Die manuelle iPad-Pruefung fuehrt ausschliesslich einen HTTP-GET auf `http://<Desktop-Adresse>:53941/pairing/status` aus. Die iPad-App wertet nur die harmlose Statusantwort mit `app = BueroCockpit`, `status = ok` und `mode = pairing-test` aus und zeigt danach `Desktop-Testdienst erreichbar` oder `Desktop-Testdienst nicht erreichbar: <kurzer Fehler>`.

Sobald der Bereich `Lokaler Netzwerk-Sync` sichtbar ist, wird eine kontrollierte automatische Pruefung der eingetragenen Desktop-Adresse vorbereitet. Die erste automatische Pruefung startet erst nach kurzer Verzoegerung von etwa 3 Sekunden. Danach wird der gleiche Statusendpunkt hoechstens alle 30 Sekunden erneut geprueft. Parallel browsed die iPad-App per systemischem Bonjour/mDNS nach Diensten vom Typ `_buerocockpit._tcp`. Automatische Pruefung und Dienstsuche laufen nur, solange diese Ansicht sichtbar ist, und werden beim Verlassen der Ansicht oder beim Freigeben des ViewModels gestoppt. Die manuelle Pruefung funktioniert unabhaengig davon.

Die Desktop-App zeigt im Bereich `Lokaler Netzwerk-Sync` weiterhin die lokalen Desktop-Informationen und kann den lokalen Testdienst manuell starten und stoppen. Nach manuellem Start lauscht der Testdienst im lokalen Netzwerk auf dem gespeicherten Port. Bonjour/mDNS ist nur eine optionale Komfortfunktion: Wenn die native DNS-SD-/Bonjour-Bibliothek verfuegbar ist, kuendigt sich der Dienst lokal als `_buerocockpit._tcp` an. Wenn Bonjour nicht verfuegbar ist, darf die App nicht abstuerzen; der Testdienst bleibt aktiv und das iPad kann den Desktop weiterhin per manueller IP-/Adresseingabe pruefen. Die Desktop-App zeigt dann einen Hinweis wie `Bonjour nicht verfuegbar; manuelle IP-Eingabe verwenden.` Beim Stoppen des Testdienstes wird eine laufende Bonjour-Ankuendigung beendet. Es gibt keine Ankuendigung beim App-Start und keine Ankuendigung, solange der Testdienst nicht manuell laeuft. Fuer das iPad kann die aktuelle Mac-LAN-IP verwendet werden, zum Beispiel `http://192.168.x.x:53941/pairing/status`; `127.0.0.1` funktioniert nur auf dem Mac selbst. Der Dienst liefert unter `/health` bzw. `/pairing/status` eine ungefaehrliche Statusantwort:

Der lokale Testdienst verwendet als sicheren Default-Port `53941`, wenn lokal noch kein gueltiger Port gespeichert ist. Dieser Port wird nur in `BueroCockpitLocal/settings.local.json` gespeichert und kann lokal auf einen anderen gueltigen Port geaendert werden.

Wenn das iPad den Desktop trotz gleicher WLAN-/LAN-Umgebung nicht erreicht, kann die macOS-Firewall den Zugriff auf den manuell gestarteten Testdienst blockieren. IP-Adressen koennen sich im lokalen Netzwerk aendern. Deshalb wird die IP nur als aktuelle Adresse gespeichert und kann durch einen Bonjour/mDNS-Treffer automatisch aktualisiert werden. Die Dienstfindung ersetzt keinen Pairing-Abschluss; sie dient nur dazu, den manuell gestarteten Desktop-Testdienst wiederzufinden.

```json
{
  "app": "BueroCockpit",
  "status": "ok",
  "mode": "pairing-test",
  "version": "0.4.14"
}
```

Der Testdienst ist ein technischer Verbindungstest. Er startet nicht automatisch beim App-Start, startet nicht durch Oeffnen der Einstellungen, fuehrt keine iPad-Kopplung aus und gibt keine Aufgaben, Kategorien, Anhaenge, Einstellungen oder sonstige Produktivdaten aus. Die iPad-Pruefung ruft ausschliesslich `/pairing/status` ab und prueft nur `app`, `status` und `mode`. Die optionale Dienstsuche nutzt ausschliesslich Bonjour/mDNS fuer `_buerocockpit._tcp` und uebertraegt keine Produktivdaten. Ist Bonjour nicht verfuegbar, bleibt die manuelle IP-Eingabe der Fallback. Es findet weiterhin kein Sync statt.

Die Bonjour/mDNS-TXT-Daten bleiben harmlos:

- `app=BueroCockpit`
- `mode=pairing-test`
- `port=53941`
- optional `deviceId`

Live-Datei, iCloud-Import und OneDrive/Microsoft Graph bleiben als alter Lesemodus, Fallback oder spaetere Provider-Option erhalten. Sie sind nicht der aktuelle Kopplungsweg fuer den lokalen Netzwerk-Sync.

Diese Vorbereitung ist noch keine echte Kopplung. Es gibt weiterhin keinen TrustKey-Austausch, keinen Pairing-Abschluss und keine Datenuebertragung. Netzwerkverkehr entsteht nur, wenn der Benutzer den lokalen Testdienst manuell startet und die iPad-Sync-Ansicht sichtbar ist oder der Benutzer auf dem iPad den Statusendpunkt manuell abruft. Es gibt keine Subnetzsuche, keinen UDP-Broadcast-Scan und keinen Portscan.

## Ziel

Der spaetere lokale Sync verbindet genau einen Desktop mit genau einem iPad fuer manuelle Sync-Laeufe im lokalen Firmennetz. Ein Pairing-Code kann spaeter wieder Teil einer Erstkopplung werden, ist aber nicht der aktuelle Bedienweg im iPad-Bereich `Lokaler Netzwerk-Sync`. Danach sollen sich Desktop und iPad ueber gespeicherte `DeviceId` und `TrustKey` wiedererkennen.

Ein neuer Pairing-Code ist nur vorgesehen, wenn ein Geraet neu gekoppelt wird, die Kopplung zurueckgesetzt wurde oder ein TrustKey widerrufen wurde.

## Gemeinsames Datenmodell

Jedes Geraet besitzt lokal eine stabile Identitaet:

```json
{
  "deviceId": "desktop-...",
  "deviceName": "Mac mini Buero",
  "devicePlatform": "macOS"
}
```

Fuer eine gespeicherte Kopplung wird lokal pro Gegenstelle dieser Datensatz gehalten:

```json
{
  "deviceId": "ipad-...",
  "deviceName": "iPad Werkstatt",
  "devicePlatform": "iPadOS",
  "pairedAt": "2026-07-03T10:00:00Z",
  "lastSeenAt": null,
  "trustKey": "...",
  "sharedSecret": ""
}
```

Pflichtfelder fuer spaetere Wiedererkennung:

- `deviceId`: stabile Kennung der Gegenstelle.
- `deviceName`: lokaler Anzeigename der Gegenstelle.
- `devicePlatform`: Plattformhinweis, zum Beispiel `macOS`, `Windows` oder `iPadOS`.
- `pairedAt`: Zeitpunkt der erfolgreichen Kopplung.
- `trustKey`: geheime lokale Vertrauenskennung fuer spaetere Sync-Laeufe.

Optionale Felder:

- `lastSeenAt`: letzter erfolgreicher Kontakt.
- `sharedSecret`: reserviert, falls spaeter ein getrenntes gemeinsames Geheimnis benoetigt wird.

## Pairing-Anfrage

Das iPad sendet bei der geplanten Erstkopplung fachlich diese Anfrage an den Desktop:

```json
{
  "deviceId": "ipad-...",
  "deviceName": "iPad Werkstatt",
  "devicePlatform": "iPadOS",
  "appVersion": "1.0",
  "requestedAtUtc": "2026-07-03T10:00:00Z"
}
```

Der Pairing-Code ist nicht Teil der dauerhaft gespeicherten Geraeteidentitaet. Er wird nur fuer die einmalige Bestaetigung der Erstkopplung verwendet.

## Pairing-Bestaetigung

Nach gueltigem Einmal-Code und manueller Bestaetigung am Desktop ist fachlich diese Antwort vorgesehen:

```json
{
  "desktopDeviceId": "desktop-...",
  "desktopName": "Mac mini Buero",
  "pairedDeviceId": "ipad-...",
  "pairedDeviceName": "iPad Werkstatt",
  "pairedAtUtc": "2026-07-03T10:00:05Z",
  "trustKey": "..."
}
```

Der `trustKey` ersetzt fuer spaetere Wiedererkennung den Pairing-Code. Er darf nicht zentral synchronisiert, nicht im Sync-Live-Export abgelegt und nicht offen angezeigt werden.

## Geplanter Ablauf Desktop zu iPad

1. Desktop erzeugt lokal einen kurzlebigen Pairing-Code.
2. Benutzer gibt diesen Code auf dem iPad ein oder scannt spaeter einen QR-Code.
3. iPad bereitet eine lokale `ipadDeviceId` vor und sendet seine Geraetedaten zusammen mit dem Code.
4. Desktop prueft den Code und zeigt die unbekannte Gegenstelle sichtbar zur manuellen Annahme.
5. Desktop speichert das iPad lokal in `LocalNetworkSyncPairedDevices`.
6. iPad speichert Desktop-Identitaet und `trustKey` lokal in seinen Sync-Einstellungen.
7. Der Pairing-Code wird verworfen und ist fuer normale Sync-Laeufe nicht mehr noetig.

Dieser Ablauf ist geplant, aber in diesem Stand nicht als Netzwerkfunktion implementiert.

## Lokale Speicherung je Geraet

Desktop speichert Pairing-Daten ausschliesslich lokal:

```text
BueroCockpitLocal/settings.local.json
```

Relevante Desktop-Felder:

- `LocalNetworkSyncDeviceId`
- `LocalNetworkSyncDeviceName`
- `LocalNetworkSyncPairingCode`
- `LocalNetworkSyncPairedDevices`

iPad speichert Pairing-Daten ausschliesslich lokal in den Sync-Einstellungen der App. Relevante iPad-Felder:

- `ipadDeviceId`
- `ipadDeviceName`
- `ipadPlatform`
- `desktopAddress`
- `desktopPort`
- `desktopDeviceId`
- `desktopName`
- `desktopPlatform`
- `pairingCode`
- `pairedAt`
- `lastSeenAt`
- `trustKey`
- `sharedSecret`

## Grenzen dieses Stands

- keine unkontrollierte Netzwerksuche, keine Subnetzsuche, kein UDP-Broadcast-Scan und kein Portscan
- kein automatisch gestarteter Netzwerkdienst
- kein automatisch geoeffneter TCP-/UDP-Port
- Bonjour/mDNS optional, nur fuer den manuell gestarteten Testdienst und nur als `_buerocockpit._tcp`
- manueller IP-/Adresse-Fallback, wenn Bonjour nicht verfuegbar ist
- keine Hintergrund-Geraetesuche ausserhalb der sichtbaren lokalen Netzwerk-Sync-Ansicht
- kein dauerhaftes Hintergrund-Polling
- kein FileSystemWatcher
- keine Datenuebertragung
- keine echte Pairing-Validierung ueber Netzwerk
- kein echter Pairing-Abschluss
- keine Speicherung von Pairing-Geheimnissen in zentralen Daten oder Exportdateien
