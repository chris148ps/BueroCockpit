# Lokale Gerätefreigabe für Netzwerk-Sync

Stand: 2026-07-16.

Der Dateiname ist historisch. Der aktuelle Bedienweg verwendet keinen Pairing-Code und reaktiviert keinen alten Code- oder Cloud-Assistenten.

## Ablauf

1. Der Benutzer startet den Desktop-Sync-Dienst manuell, standardmäßig auf Port `53941`.
2. Das iPad findet den Desktop per Bonjour/mDNS oder prüft eine manuelle Adresse/IP.
3. `Diesen Desktop verwenden` speichert den Desktop lokal, erzeugt eine stabile iPad-Geräte-ID und bei Bedarf einen zufälligen lokalen Vertrauensnachweis.
4. Das iPad sendet Geräteinformationen und Nachweis an `POST /local-sync/devices/remember`.
5. Der Desktop speichert nur Geräteinformationen und SHA-256-Hash lokal; Zustand ist zunächst `pending`.
6. Der Benutzer gibt das angezeigte iPad am Desktop mit `Für Uploads freigeben` ausdrücklich frei.
7. `GET /local-sync/pairing/status` bestätigt nur die Kombination aus stabiler Geräte-ID, passendem Nachweis und Zustand `trusted`.
8. Der Benutzer kann die Freigabe am Desktop widerrufen; dann sind weitere Uploads blockiert.

## Speicherung

iPad, nur lokale `UserDefaults`:

- Desktop-Adresse, Port und stabile Desktop-Zuordnung
- stabile iPad-Geräte-ID und Anzeigename
- zufälliger Vertrauensnachweis
- letzter Verbindungs- und erfolgreicher Sync-Zeitpunkt sowie Abschlusszahlen

Desktop, nur `BueroCockpitLocal/local-network-devices.json`:

- Geräte-ID, Gerätename, Plattform, App-Version
- Kontaktzeiten und letzte Remote-Adresse
- SHA-256-Hash des Nachweises
- `pending`, `trusted` oder `revoked`
- letzter bestätigter Upload

Diese Daten werden weder in `Sync/live/settings.json` noch in die Produktivdatenbank oder Cloud-Dateien geschrieben. Der offene Nachweis wird am Desktop nicht gespeichert oder angezeigt.

## Regeln

- Ein Bonjour-Fund oder eine erfolgreiche Statusprüfung ist keine Freigabe.
- Unbekannte Geräte und falsche Nachweise werden abgelehnt.
- Ein neuer Nachweis für dieselbe Geräte-ID setzt die Freigabe wieder auf `pending`.
- Ein anderer gefundener Desktop ersetzt die gespeicherte Zuordnung nur nach Benutzeraktion.
- Keine automatische Wiederfreigabe nach Widerruf.
- Kopplung erlaubt nur die begrenzten authentisierten Endpunkte, keinen direkten Datenbankzugriff.
- Der eigentliche Upload startet ausschließlich durch `Jetzt synchronisieren`.

Endpunkte und Paketregeln stehen verbindlich in [LOCAL_NETWORK_SYNC.md](LOCAL_NETWORK_SYNC.md).

## Legacy

Alte Felder wie `LocalNetworkSyncPairingCode` oder alte dateibasierte Kopplungsdaten dürfen tolerant gelesen werden, sind aber nicht Teil des aktuellen Netzwerk-Syncs. Sie werden nicht automatisch migriert, gelöscht oder wieder aktiviert.
