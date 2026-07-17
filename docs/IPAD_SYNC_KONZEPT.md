# iPad-Sync-Konzept

Stand: 2026-07-16.

Dieses Dokument beschreibt die Zielrichtung und die freigegebenen Grenzen der iPad-Anbindung. Der vorhandene manuell gestartete lokale Desktop-Dienst und die iPad-Verbindungssuche duerfen fuer einen bewusst per Schaltflaeche gestarteten, gerichteten Upload mobiler Eingaenge erweitert werden. Eine automatische Datenuebertragung, Datenmigration oder stille Aenderung produktiver Desktopdaten bleibt unzulaessig.

Der detaillierte Architektur- und Schnittstellenentwurf fuer den geplanten manuellen lokalen Netzwerk-Sync steht in [LOCAL_NETWORK_SYNC.md](LOCAL_NETWORK_SYNC.md). Dieses Dokument bleibt die fachliche Kurzfassung fuer die iPad-Zielrichtung.

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

## Implementierter erster lokaler Netzwerk-Sync

Der aktuelle erste Nutzweg ist ein manueller, gerichteter lokaler Netzwerk-Sync zwischen iPad und BueroCockpit-Desktop im Firmennetz.

- BueroCockpit Desktop startet den lokalen Sync-Dienst ausschließlich nach Benutzeraktion.
- Das iPad verbindet sich bewusst mit einem bekannten BueroCockpit-Desktop im gleichen Firmennetz.
- Die Kopplung erfolgt durch ausdrueckliche Freigabe des vorgemerkten iPads am Desktop; der alte Pairing-Code-Weg wird nicht reaktiviert.
- Das iPad kann mobile Eingaenge samt Originalfotos, Vorschauen, Skizzen und Dateien über `Jetzt synchronisieren` übertragen.
- Der Desktop prüft und übernimmt die Pakete zunächst atomar nach `Sync/inbox`; ein direkter Produktivimport findet nicht statt.
- Das iPad loescht lokale Originale erst nach bestaetigter Uebernahme.
- Es gibt keine stille Dauer-Live-Aktualisierung.
- Die Synchronisation erfolgt manuell ueber Button und Statusanzeige.

## Technische Architektur

- Desktop-Dienst lokal im LAN, nur manuell gestartet.
- Bonjour/mDNS fuer die begrenzte automatische Desktop-Suche; manuelle IP-Auswahl bleibt der Fallback.
- Ein erfolgreich geprueftes iPad wird am Desktop zunaechst nur vorgemerkt. Erst die bewusste Desktop-Freigabe schliesst die lokale Vertrauensbeziehung ab.
- Die Wiedererkennung verwendet stabile DeviceId und einen lokal gespeicherten, widerrufbaren Vertrauensnachweis. Der Vertrauensnachweis wird nicht offen angezeigt und nicht zentral synchronisiert.
- Status, Abruf und Uebergabe ueber klar begrenzte lokale Endpunkte.

Das gemeinsame lokale Pairing-Datenformat ist in `docs/LOCAL_NETWORK_PAIRING.md` beschrieben.

Aktuelle und fuer den gerichteten Upload vorgesehene Endpunkte:

```text
GET /health
GET /local-sync/status
GET /local-sync/pairing/status
POST /local-sync/devices/remember
POST /local-sync/mobile-inbox
```

Der Dienst startet weiterhin ausschliesslich manuell am Desktop. Status- und Erkennungspruefungen duerfen im vorhandenen begrenzten Umfang laufen; die eigentliche Uebertragung beginnt nur durch `Jetzt synchronisieren` auf dem iPad.

## Mobile Eingaenge

Bestehende manuelle mobile Eingaenge nutzen historisch die Struktur `mobile-inbox/mobile-*` mit `aufgabe.json`, Fotos, Vorschauen, markierten Versionen, Skizzen und Dateien. Neue Netzwerk-Uploads nimmt der Desktop unter `BueroCockpit_Daten/Sync/inbox/mobile-*` an; nach bewusster fachlicher Übernahme werden sie nach `Sync/processed` verschoben.

Der Desktop bestaetigt dem iPad die Uebernahme erst, wenn Manifest, Dateien und Pruefsummen erfolgreich abgelegt wurden. Die aktuelle iPad-Stufe bereinigt auch nach Bestätigung noch keine lokalen Originale; dadurch bleiben Wiederholungsversuche sicher möglich.

## Sicherheitsregeln

- Zugriff nur im lokalen Netzwerk.
- Pairing ist erforderlich.
- TrustKeys werden nicht dauerhaft offen angezeigt.
- Import erfolgt nur nach Bestaetigung.
- Sync-Ergebnisse werden protokolliert.

## Datenregeln

- Der Desktop ist fuehrend.
- Produktivdaten bleiben in `BueroCockpit_Daten`.
- Das iPad ist Erfassungsclient.
- Mobile Originale werden erst nach bestaetigter Uebernahme bereinigt.
- Es gibt keine automatische Loeschung ohne bestaetigten Sync.

## Dauerhafte Nicht-Ziele

- Kein automatisch gestarteter Desktop-Sync-Dienst.
- Keine automatische Hintergrund-Datenuebertragung.
- Keine Reaktivierung von Pairing-Code, Live-Datei oder Cloud-Datei als Transportweg.
- Keine Live-Dateien loeschen.
- Keine iCloud- oder OneDrive-Dateien verschieben.
- Keine Datenmigration.
- Keine stille Uebernahme in oder Ueberschreibung von produktiven Desktopdaten.
