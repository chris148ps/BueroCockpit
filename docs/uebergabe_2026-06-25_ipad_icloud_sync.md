# BüroCockpit Übergabe – iPad iCloud Sync Stand 25.06.2026

Hinweis Stand 2026-07-02: Dieses Dokument beschreibt einen historischen Uebergangsstand. iCloud-Live ist nicht mehr die empfohlene Hauptloesung und wird nicht als zentrale Datenquelle weiterentwickelt. Der kuenftige iPad-Abgleich soll ueber einen separaten lokalen Netzwerk-Sync im Firmennetz geplant werden; siehe [IPAD_SYNC_KONZEPT.md](IPAD_SYNC_KONZEPT.md).

## Aktueller Git-Stand

Aktuelle gesicherte Commits:

- `82c334a iPad iCloud Sync Auswahl und Migration korrigieren`
- `b82f5e1 Desktop iCloud Live Datei Zielordner vorbereiten`

Beide Commits sind auf `origin/main` gepusht.

Kein Release wurde erstellt.

## Aktueller Funktionsstand

### Desktop

BüroCockpit Desktop ist vorbereitet für eine frei wählbare iPad-Live-Datei.

In den Einstellungen gibt es den Bereich:

- iPad-Live-Datei
- Zielordner frei wählbar
- vollständiger Pfad zu `live.bclive` sichtbar
- letzter erfolgreicher Export sichtbar
- letzte Fehlermeldung sichtbar
- Button „Jetzt Live-Datei schreiben“
- Legacy-/Uebergangsordner kann geprüft werden

Die Desktop-App ist provider-neutral. Es gibt keine harte iCloud-, Google-Drive- oder OneDrive-Abhängigkeit.

Historisch war auf dem Firmenrechner folgender Uebergangsordner vorgesehen:

`iCloud Drive\BueroCockpit_iPad_Live`

Die Desktop-App schreibt dort:

`live.bclive`

### iPad

Die iPad-App hat jetzt eine Sync-Quellen-Auswahl:

- Manuelle Datei
- iCloud Drive
- Google Drive Direktlink
- WebDAV / NAS
- Dropbox
- OneDrive / Microsoft Graph

Aktiv nutzbar:

- Manuelle Datei
- iCloud Drive
- Google Drive Direktlink

Vorbereitet/Platzhalter:

- WebDAV / NAS
- Dropbox
- OneDrive / Microsoft Graph

iCloud Drive funktioniert aktuell so:

1. iCloud Drive auswählen
2. `live.bclive` über Dateien-App auswählen
3. App speichert den Security-Scoped Bookmark
4. Datei wird lokal als `current.bclive` übernommen
5. Daten werden geladen
6. „Aus iCloud Drive aktualisieren“ lädt dieselbe Datei erneut, ohne erneute Dateiauswahl
7. Wenn der Zugriff ungültig wird, öffnet die App als Fallback die Dateiauswahl

Der alte Legacy-Startbildschirm mit:

- Live-Datei importieren
- Live-Ordner auswählen
- metadata.json auswählen

erscheint nicht mehr automatisch als Hauptbildschirm.

## Historische iCloud-Testschritte, nicht mehr Hauptempfehlung

Die folgenden Schritte bleiben nur zur Nachvollziehbarkeit des damaligen Teststands erhalten. Sie sind keine aktuelle Empfehlung fuer die zentrale BueroCockpit-Datenfuehrung.

### 1. iCloud für Windows installieren

Auf dem Firmenrechner iCloud für Windows installieren und anmelden.

Danach aktivieren:

- iCloud Drive

### 2. Ordner anlegen

In iCloud Drive folgenden Ordner anlegen:

`BueroCockpit_iPad_Live`

### 3. BüroCockpit Desktop konfigurieren

BüroCockpit starten.

Einstellungen öffnen.

Bereich „iPad-Live-Datei“ öffnen.

Zielordner auswählen:

`iCloud Drive\BueroCockpit_iPad_Live`

Dann Button drücken:

„Jetzt Live-Datei schreiben“

Erwartung:

Im Zielordner liegt danach:

`live.bclive`

### 4. iPad verbinden

Auf dem iPad:

1. BüroCockpit iPad-App öffnen
2. Sync-Einstellungen öffnen
3. iCloud Drive auswählen
4. Datei auswählen:

`iCloud Drive/BueroCockpit_iPad_Live/live.bclive`

5. Daten laden lassen
6. Button testen:

„Aus iCloud Drive aktualisieren“

Erwartung:

Die Datei wird ohne neue Dateiauswahl erneut geladen.

## Nicht tun

- Kein Release erstellen, bevor der Windows-/iCloud-Test abgeschlossen ist.
- Keine Microsoft-Graph-Umsetzung, solange Entra nicht zugänglich ist.
- Kein Google Drive für den Firmenrechner einplanen.
- Keine Polling-/Watcher-/Timer-Logik einbauen.
- Keine Originalanhänge in `live.bclive` aufnehmen.

## Prüfstand

Bisher erfolgreich geprüft:

- `dotnet build`
- iPad `xcodebuild ... CODE_SIGNING_ALLOWED=NO build`
- `git diff --check`
- Grep auf LiveReload/FileSystemWatcher/Polling/Timer ohne neue aktive Treffer
