# Übergabe Codex-App – BueroCockpit – 2026-07-04

## Aktueller Stand

Branch: main

Aktueller bestätigter Commit:

970ee58 Desktop Testdienst im lokalen Netzwerk erreichbar machen

Der Commit ist auf origin/main gepusht.

## Projektregeln

- Kein Release ohne ausdrückliche Freigabe.
- Kein Tag ohne ausdrückliche Freigabe.
- Keine Versionserhöhung ohne ausdrückliche Freigabe.
- Keine Produktivdaten löschen, verschieben oder verändern.
- Keine zentrale settings.json mit lokalen Gerätewerten verändern.
- Lokale Gerätewerte nur lokal speichern.
- Desktop-App bleibt führendes System.
- iPad-App ist aktuell lokaler Prüf-/Erfassungsclient.
- Kein echter Sync, solange nicht ausdrücklich beauftragt.
- Kein Autostart von Netzwerkdiensten.
- Lokaler Testdienst startet nur manuell.
- Bonjour/mDNS nur während des manuell gestarteten lokalen Testdienstes und nur für `_buerocockpit._tcp`.
- Keine automatische Hintergrundsuche, kein UDP-Broadcast, keine Subnetzsuche und kein Portscan.
- Kein FileSystemWatcher.
- Kein unbegrenztes Hintergrund-Polling.

## Bestätigter technischer Stand

Der Desktop-Testdienst:

- startet manuell über BüroCockpit-Einstellungen
- lauscht im lokalen Netzwerk auf Port 53941
- kündigt sich nur während dieses manuellen Testdienstlaufs per Bonjour/mDNS als `_buerocockpit._tcp` an
- ist vom iPad erreichbar
- stoppt sauber
- überträgt keine Produktivdaten

Endpunkte:

/health
/pairing/status

Antwort von /pairing/status:

{"app":"BueroCockpit","status":"ok","mode":"pairing-test","version":"0.4.14"}

LAN-Test erfolgreich mit:

curl -v http://192.168.178.52:53941/pairing/status

iPad meldet inzwischen, dass der Desktop erreichbar ist.

## SQLite-Thema

Es gab einen Startfehler:

SQLite Error 10: disk I/O error
SQLite ExtendedErrorCode: 266

Ursache war sehr wahrscheinlich OneDrive/CloudStorage.
Nach lokalem Laden war die Datenbank wieder lesbar.

Prüfung erfolgreich:

PRAGMA integrity_check; -> ok
PRAGMA quick_check; -> ok

Die Diagnose wurde mit Commit e2a3f9e eingebaut und soll erhalten bleiben.

## Letzte relevante Commits

970ee58 Desktop Testdienst im lokalen Netzwerk erreichbar machen
e2a3f9e SQLite Startfehler diagnostisch abfangen
02f63de Lokalen iPad Netzwerk-Sync Bedienweg bereinigen
2885de3 iPad automatische Desktop-Testdienstpruefung vorbereiten
8bc6b6c iPad Desktop-Testdienst manuell pruefen

## Nächster geplanter Schritt

Das iPad soll den erfolgreich geprüften Desktop lokal als Sync-Partner vormerken.

Noch kein echter Sync.

Ziel:

- Button „Diesen Desktop verwenden“
- nur sichtbar/aktiv nach erfolgreicher Prüfung
- lokale Speicherung auf dem iPad: Desktop-Adresse/IP, Port 53941, letzter erfolgreicher Prüfzeitpunkt, Status
- Anzeige beim erneuten Öffnen wiederherstellen
- bei Änderung von Adresse/Port Status wieder prüfbedürftig setzen
- kein Pairing-Code
- kein OneDrive-/Live-Datei-Hinweis im lokalen Netzwerk-Sync-Bereich
- keine Aufgaben/Kategorien/Anhänge übertragen
- kein echter Sync
- kein echter Pairing-Abschluss

## Codex-Modell und Start

Das Codex-Modell wird abhängig vom Aufgabentyp gewählt: Für kleine und mittlere Aufgaben genügt ein effizientes Modell; für komplexe Architektur-, Refactoring- oder schwierige Fehlersuchaufgaben ist ein leistungsfähigeres Modell angemessen. Eine deutliche Abweichung vom üblichen Modellstandard wird kurz begründet.

Allgemeiner Start im Projekt:

cd "$HOME/AppProjekte/BueroCockpit"
codex

## Statushinweis 2026-07-05

Der lokale Netzwerk-Sync ist als neuer Zielweg bereinigt:

- aktueller Bedienweg ohne Pairing-Code
- aktueller Bedienweg ohne Live-Datei-/OneDrive-Kopplung
- Desktop-Testdienst weiter nur manuell
- neuer Statuspfad `/local-sync/status`
- `/pairing/status` nur noch als tolerierter Alt-Endpunkt
- Bonjour/mDNS optional fuer automatische Desktop-Suche
- manuelle IP-Eingabe bleibt Fallback
- Windows-Bonjour-Erkennung fuer `mDNSResponder` und `dns_sd.dll` vorbereitet
- weiterhin kein echter Sync und keine Produktivdatenuebertragung
