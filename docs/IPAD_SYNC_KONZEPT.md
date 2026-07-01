# iPad-Sync-Konzept

Dieser Stand dokumentiert nur das Konzept fuer die kuenftige iPad-Anbindung. Es wird keine Synchronisation implementiert, keine Migration ausgefuehrt und keine produktive Datei veraendert.

## Grundrichtung

iCloud ist kuenftig keine aktive Hauptdatenquelle fuer BueroCockpit. Alte iCloud-Testpfade koennen als Legacy- oder Uebergangsinformation sichtbar bleiben, werden aber nicht als empfohlene zentrale Datenquelle ausgebaut.

Die zentrale Datenquelle fuer Desktop, Windows und Mac bleibt:

```text
OneDrive/BueroCockpit_Daten
```

Der Desktop uebernimmt Originale dauerhaft in diesen zentralen Datenordner. Mobile Originalfotos bleiben auf dem iPad nur so lange erhalten, bis die Uebernahme bestaetigt ist.

## Variante A: Microsoft Graph / OneDrive-API

Das iPad koennte spaeter direkt ueber Microsoft Graph auf OneDrive zugreifen. Dabei wuerde die App gezielt definierte Sync- und Inbox-Dateien unter `BueroCockpit_Daten` lesen oder schreiben.

Diese Variante braucht eine gesonderte Planung fuer Anmeldung, Rechte, Fehlerbehandlung, Konflikte, Offline-Verhalten und Datenschutz.

## Variante B: Lokaler Netzwerk-Sync im Firmennetz

Alternativ kann ein lokaler Sync im Firmennetz geplant werden:

- Der Desktop startet einen lokalen Sync-Dienst.
- Das iPad findet den Desktop im gleichen Netzwerk.
- Die Kopplung erfolgt per Code oder QR-Code.
- Das iPad uebertraegt mobile Eingaenge, Fotos, Skizzen und Daten.
- Der Desktop uebernimmt diese nach `Sync/inbox` oder direkt in die zentrale Datenstruktur.
- Das iPad loescht lokale Originale erst nach bestaetigter Uebernahme.

## Empfehlung

Empfohlen ist spaeter ein lokaler Netzwerk-Sync mit manueller Uebertragung und sichtbarer Kopplung. Der Sync soll nicht still im Hintergrund laufen, sondern ueber einen manuellen Sync-Button gestartet werden.

Der Ablauf muss nachvollziehbar bleiben:

- sichtbarer Status
- Fehleranzeige
- Protokoll
- klare Bestaetigung nach erfolgreicher Uebernahme

Es gibt keine stille Live-Aktualisierung. Eine Umsetzung wird in einem separaten Auftrag geplant und implementiert.
