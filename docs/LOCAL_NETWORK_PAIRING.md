# Lokales Netzwerk-Pairing

Stand: 2026-07-04.

Dieses Dokument definiert das gemeinsame lokale Pairing-Datenformat fuer BueroCockpit Desktop und die iPad-App. Es ist nur ein Vertrag fuer eine spaetere lokale Netzwerk-Kopplung. In diesem Stand gibt es keine Netzwerksuche, kein Bonjour/mDNS, keine Hintergrundsuche ausserhalb der sichtbaren Pairing-/Sync-Ansicht und keine Datenuebertragung. Auf der Desktop-Seite gibt es nur einen manuell startbaren lokalen HTTP-Testdienst fuer eine einfache Statusantwort.

## Aktueller Stand

Die iPad-App kann einen Pairing-Code lokal formal pruefen und speichern. Gueltig ist aktuell das Format `ABCD-1234`: vier Buchstaben, Bindestrich, vier Ziffern. Bei gueltigem Code setzt die iPad-App ihren lokalen Status auf `Kopplung vorbereitet` und zeigt den Hinweis, dass die Verbindung erst in einem spaeteren Schritt aktiviert wird.

Zusaetzlich kann die iPad-App im Bereich `Lokaler Netzwerk-Sync` die Desktop-Adresse lokal speichern und den Button `Desktop-Testdienst pruefen` anzeigen. Dieser Button fuehrt weiterhin ausschliesslich nach manueller Betaetigung einen einzelnen HTTP-GET auf `http://<Desktop-Adresse>:53941/pairing/status` aus. Die iPad-App wertet nur die harmlose Statusantwort mit `app = BueroCockpit`, `status = ok` und `mode = pairing-test` aus und zeigt danach `Desktop-Testdienst erreichbar` oder `Desktop-Testdienst nicht erreichbar`.

Aktueller Stand der iPad-Seite: Sobald der Bereich `Lokaler Netzwerk-Sync` sichtbar ist, wird eine kontrollierte automatische Pruefung der eingetragenen Desktop-Adresse vorbereitet. Die erste automatische Pruefung startet erst nach kurzer Verzoegerung von etwa 3 Sekunden. Danach wird der gleiche Statusendpunkt hoechstens alle 30 Sekunden erneut geprueft. Die automatische Pruefung laeuft nur, solange diese Ansicht sichtbar ist, und wird beim Verlassen der Ansicht oder beim Freigeben des ViewModels gestoppt.

Die Desktop-App zeigt im Bereich `Lokaler Netzwerk-Sync` lokal die `DeviceId`, den Pairing-Code und den Status `Wartet auf iPad-Kopplung` bzw. `Pairing vorbereitet`. Zusaetzlich kann der Benutzer dort den lokalen Testdienst manuell starten und stoppen. Der Testdienst bindet nur an die definierte lokale Adresse und liefert unter `/health` bzw. `/pairing/status` eine ungefaehrliche Statusantwort:

Der lokale Testdienst verwendet als sicheren Default-Port `53941`, wenn lokal noch kein gueltiger Port gespeichert ist. Dieser Port wird nur in `BueroCockpitLocal/settings.local.json` gespeichert und kann lokal auf einen anderen gueltigen Port geaendert werden.

```json
{
  "app": "BueroCockpit",
  "status": "ok",
  "mode": "pairing-test",
  "version": "0.4.14"
}
```

Der Desktop-Status bedeutet nur: Die lokale Erstkopplung ist vorbereitet und wartet auf eine spaetere iPad-Kopplung. Der Testdienst ist ein technischer Verbindungstest. Er startet nicht automatisch beim App-Start, startet nicht durch Oeffnen der Einstellungen, fuehrt keine iPad-Kopplung aus und gibt keine Aufgaben, Kategorien, Anhaenge, Einstellungen oder sonstige Produktivdaten aus. Die iPad-Pruefung ruft ausschliesslich `/pairing/status` ab, prueft nur `app`, `status` und `mode`, sucht keine Geraete und uebertraegt keine Produktivdaten.

Desktop und iPad zeigen jetzt jeweils eine kurze Bedienfuehrung fuer die spaetere Kopplung. Diese Checklisten beschreiben nur die notwendigen Benutzerschritte: Desktop offen lassen, Pairing-Code am Desktop ablesen, Code auf dem iPad eingeben und `Kopplung vorbereiten` druecken. Der Stand ist damit: Bedienfuehrung vorbereitet, noch keine Verbindung.

Diese Vorbereitung ist noch keine echte Kopplung. Es gibt weiterhin keine Suche, keinen TrustKey-Austausch, keinen Pairing-Abschluss und keine Datenuebertragung. Netzwerkverkehr entsteht nur, wenn der Benutzer den lokalen Testdienst manuell startet und die iPad-Pairing-/Sync-Ansicht sichtbar ist oder der Benutzer auf dem iPad den Statusendpunkt manuell abruft.

## Ziel

Das Pairing verbindet genau einen Desktop mit genau einem iPad fuer spaetere manuelle Sync-Laeufe im lokalen Firmennetz. Der Pairing-Code dient nur zur Erstkopplung. Danach erkennen sich Desktop und iPad ueber gespeicherte `DeviceId` und `TrustKey` wieder.

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

- keine Netzwerksuche
- kein automatisch gestarteter Netzwerkdienst
- kein automatisch geoeffneter TCP-/UDP-Port
- kein Bonjour/mDNS
- keine automatische Geraetesuche
- kein dauerhaftes Hintergrund-Polling
- kein FileSystemWatcher
- keine Datenuebertragung
- keine echte Pairing-Validierung ueber Netzwerk
- kein echter Pairing-Abschluss
- keine Speicherung von Pairing-Geheimnissen in zentralen Daten oder Exportdateien
