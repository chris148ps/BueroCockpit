# iPad-Funktionsmatrix

Stand: 2026-07-18.

Legende: `ja` bedeutet eine reale, bedienbare Implementierung. `teilweise`
bezeichnet einen klar benannten Teilpfad und keinen Platzhalter.

| Desktopfunktion | Bereits auf iPad vorhanden | Datenmodell vorhanden | Sync unterstützt | In diesem Auftrag umgesetzt | Später erforderlich |
|---|---:|---:|---:|---:|---|
| Bonjour-Desktopsuche mit manueller IP als Fallback | ja | ja | ja | geprüft und beibehalten | physische Zielgeräte-Abnahme |
| Geräte vormerken, am Desktop freigeben und widerrufen | ja | ja | ja | geprüft und beibehalten | Bedienkomfort später verfeinern |
| Desktop-Grunddaten abrufen | ja | ja | ja | gerätebezogener Delta-Abruf plus Erstabgleich | physische Abnahme |
| Desktopdaten lokal und neustartfest speichern | ja | ja | ja | atomare Voll- und Delta-Übernahme | physische Abnahme |
| Auftragsliste und Detailansicht | ja | ja | ja | Adresse, Monteur und Wiedervorlagegrund ergänzt | weitere seltene Desktopfelder |
| Suche und rekursive Kategorieauswahl | ja | ja | ja | vorhandene rekursive Logik geprüft | weitere Filter |
| Offline-Neuanlage | ja, als mobiler Eingang | ja | ja | auf gemeinsames v2-Aufgabenmodell umgestellt | physische Abnahme |
| Bestehenden Desktopauftrag offline bearbeiten | ja | ja | ja | versionierte Änderungspakete ergänzt | physische Abnahme |
| Notiz, Kategorie und Stammdaten im mobilen Entwurf | ja | ja | ja | beibehalten | direkte Änderung bestehender Aufträge |
| Status, Termin, Wiedervorlage, Grund und Monteur im mobilen Entwurf | ja | ja | ja | vollständige zentrale Monteurliste und Desktop-Konfliktprüfung | physische Abnahme |
| Fotos aufnehmen und importieren | ja | ja | ja | auch bestehenden Desktopvorgängen zuordenbar | physische Kamera-Abnahme |
| Foto markieren, Original und Markup erhalten | ja | ja | ja | beibehalten | physische Pencil-/Kamera-Abnahme |
| PencilKit-Skizzen | ja | ja | ja | beibehalten | physische Pencil-Abnahme |
| Dateien-App-Anhänge | ja | ja | ja | stabile, idempotente Zuordnung zu neuen und bestehenden Aufträgen | physische Abnahme |
| iPad-Daten zum Desktop senden | ja, als Mobile-Inbox-Paket | ja | ja | v1 tolerant, v2 mit Clientsequenz und Desktopbestätigung | Chunk-Streaming später |
| Direkter fachlicher Desktopimport | ja, konfliktfrei automatisch | ja | ja | sichere Feldzusammenführung, Konflikte bleiben sichtbar | physische Abnahme |
| Duplikatschutz und Wiederholung | ja | ja | ja | Objekt-/Dateifingerprints, Ack und stabile IDs geprüft | physische Abnahme |
| Konfliktinhalt erhalten | ja, Paket und Feldwerte | ja | ja | feldweise Desktopentscheidung ergänzt | physische Abnahme |
| Bidirektionale Änderung bestehender Aufträge | ja, manuell ausgelöst | ja | ja | gerätebezogener Delta-Abruf plus konfliktgesicherter Rückweg | kein Hintergrundsync |
| Löschen und Archivieren | teilweise | teilweise | Desktop-Tombstones zum iPad | sichere Desktop-Tombstones | iPad-Befehle später |
| Mobile Übersicht, Termine und Wiedervorlagenansicht | teilweise über Liste/Detail | ja | ja | nein | erforderlich |
| Seltene Desktopfunktionen und vollständige Parität | nein | teilweise | nein | nein | Stufe E |

## Erreichter Etappenstand

- Stufe A ist vollständig umgesetzt: gemeinsame stabile IDs, Pairing,
  Bonjour-Erkennung, geschützte Desktop-Sync-API, Desktop-Grunddaten und lokale
  iPad-Persistenz.
- Stufe B ist funktional umgesetzt: Liste, Detail, Suche, Kategorien,
  Offline-Neuanlage und die begrenzte Offline-Bearbeitung bestehender
  Desktopvorgänge sind vorhanden.
- Stufe C ist funktional umgesetzt: Fotos, Markup, PencilKit, Dateien,
  authentisierter Upload und Zuordnung zu neuen oder bestehenden Vorgängen
  laufen über dasselbe versionierte Paketmodell.
- Stufe D ist funktional umgesetzt: Basisrevision und Basiswerte,
  Paketidentität, Wiederholung, atomare Ablage, Konflikterhalt und sichtbare
  feldweise Desktopentscheidung sind vorhanden. Gerätebezogene Checkpoints,
  Delta-Abruf, Ack, Referenzdaten und Dateiprüfsummen sind ergänzt.
- Stufe E ist nicht umgesetzt.

Konfliktfreie Netzwerk-Upserts sind ausschließlich innerhalb des bewusst
gestarteten und authentisierten manuellen Laufs erlaubt. Der Desktop bleibt
führend; gleichzeitige Feldänderungen werden nicht automatisch überschrieben,
sondern im Inbox-/Konfliktpfad erhalten und sichtbar entschieden.
