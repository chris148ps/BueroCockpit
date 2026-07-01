# iPad-Sync-Konzept

Stand: 2026-07-02.

Dieses Dokument beschreibt nur die Zielrichtung fuer die kuenftige iPad-Anbindung. In diesem Stand wird kein Netzwerk-Sync implementiert, keine Migration ausgefuehrt und keine produktive Datei veraendert.

## Aktueller Grundsatz

OneDrive/BueroCockpit_Daten ist die zentrale Datenquelle fuer BueroCockpit Desktop auf Windows und Mac.

```text
OneDrive/BueroCockpit_Daten
BueroCockpit_Daten/Sync/
```

iCloud ist keine aktive Hauptdatenquelle mehr. Bestehende iCloud-Live-Pfade und `IpadLiveFileTargetPath` duerfen als Legacy- oder Uebergangsinformation sichtbar bleiben, werden aber nicht als zentrale Hauptloesung weiterentwickelt.

`AppProjekte` ist nur Quellcode. GitHub ist nur Quellcode, Dokumentation und Releases, nicht Produktivdaten. Produktive Daten, Anhaenge, Backups und mobile Eingaenge gehoeren in `BueroCockpit_Daten`.

## Warum iCloud-Live nicht weiter ausgebaut wird

- iCloud-Live ist im praktischen Betrieb zu fehleranfaellig.
- Die Dateiverfuegbarkeit auf dem iPad ist nicht immer eindeutig oder sofort garantiert.
- Mischbetrieb aus OneDrive als Desktop-Datenquelle und iCloud als mobiler Live-Schicht ist nicht robust genug.
- Live-Dateien koennen mit wachsendem Datenbestand gross werden.
- Originalfotos sollen nicht dauerhaft mobil herumliegen oder ueber iCloud-Live als Dauerbestand verteilt werden.

## Zielbild: lokaler Netzwerk-Sync

Der kuenftige Weg ist ein manueller lokaler Netzwerk-Sync zwischen iPad und BueroCockpit-Desktop im Firmennetz.

- BueroCockpit Desktop startet spaeter optional einen lokalen Sync-Dienst.
- Das iPad findet BueroCockpit im gleichen Firmennetz.
- Die Kopplung erfolgt per QR-Code oder Einmal-Code.
- Das iPad kann den aktuellen Stand abrufen.
- Das iPad kann mobile Eingaenge, Fotos, Skizzen und Daten uebertragen.
- Der Desktop uebernimmt die Daten in den zentralen Datenordner.
- Das iPad loescht lokale Originale erst nach bestaetigter Uebernahme.
- Es gibt keine stille Dauer-Live-Aktualisierung.
- Die Synchronisation erfolgt manuell ueber Button und Statusanzeige.

## Technische Zielarchitektur

Dies ist nur ein Konzept, keine Umsetzung.

- Desktop-Dienst lokal im LAN.
- Geraeteerkennung per Bonjour/mDNS oder manuellem QR-Code.
- Pairing mit Einmal-Code oder Token.
- Status, Abruf und Uebergabe ueber klar begrenzte lokale Endpunkte.

Beispielhafte Endpunkte:

```text
GET /status
GET /snapshot
POST /mobile-inbox
POST /pairing
```

## Sicherheitsregeln

- Zugriff nur im lokalen Netzwerk.
- Pairing ist erforderlich.
- Token werden nicht dauerhaft offen angezeigt.
- Import erfolgt nur nach Bestaetigung.
- Sync-Ergebnisse werden protokolliert.

## Datenregeln

- Der Desktop ist fuehrend.
- Produktivdaten bleiben in `BueroCockpit_Daten`.
- Das iPad ist Erfassungsclient.
- Mobile Originale werden erst nach bestaetigter Uebernahme bereinigt.
- Es gibt keine automatische Loeschung ohne bestaetigten Sync.

## Nicht in diesem Auftrag

- Kein Desktop-Sync-Dienst.
- Keine iPad-Codeaenderung fuer Netzwerk.
- Keine Live-Dateien loeschen.
- Keine iCloud- oder OneDrive-Dateien verschieben.
- Keine Datenmigration.
