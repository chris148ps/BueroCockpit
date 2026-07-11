# Projektstatus BüroCockpit

## Aktueller Entwicklungsstand

BüroCockpit ist eine lokale Avalonia/.NET-Desktopanwendung mit einer
lesenden SwiftUI-iPad-App für Snapshots und mobile Erfassung. Der aktuelle
Desktop-Stand enthält eine hierarchische Kategorie-Navigation, konsolidierte
Dark-/Light-Designressourcen, lokale Schreibtisch-Notizen, zeitgesteuerten
Autospeicher und eine profilbasierte Technikerverwaltung ohne Sonderrolle.
Die iPad-App nutzt eine eigenständige native iPadOS-Navigation und Toolbar.

## Architektur

- Desktop: Avalonia UI, führendes System, lokale SQLite-/Dateidaten.
- iPad: SwiftUI Snapshot Reader und mobiler Erfassungsclient.
- Techniker: zentral gespeicherte, rückwärtskompatible Profile in den bestehenden
  Live-Settings; die Namensliste bleibt für Auftragsauswahlen verfügbar.
- Synchronisation: lokaler Netzwerk-Sync ist vorbereitet, aber kein echter
  produktiver Datentransfer aktiv.
- Veröffentlichung: größere Codex-Arbeiten werden über `codex/work` und einen
  Draft-Pull-Request nach `main` sichtbar gemacht; Merge bleibt manuell.

## Erledigte Hauptfunktionen

- Kategoriebaum mit Unterkategorien und aggregierten Auftragszahlen.
- Kategorieauswahl ohne nicht auswählbare Hauptkategorien.
- Dashboard-Navigation mit passender Kategorieauswahl.
- Auftragsbezogene Schreibtisch-Notizzettel mit Abwahl/Löschung.
- Gleichberechtigte Technikerprofile mit Name, Kürzel, E-Mail und Telefon.
- Horizontale Desktop-Einstellungstabs und Windows-11-Dark-Technikeransicht.
- Desktop startet stets in der Übersicht; Backup liegt unter Daten & Pfade und
  die Kategorienverwaltung zeigt keine technischen Sonderbereiche.
- Robusteres Leeren von Datumsfeldern und vorhandener Autospeicher-Timer.
- Semantische Desktop-Ressourcen für Dark-/Light-Modus.
- Native iPadOS-Toolbar und systemeigene Suche.

## Bekannte offene Punkte

- Der echte lokale Netzwerk-Sync und produktive Datenübertragung sind weiterhin
  nicht aktiviert.
- Der GitHub-Draft-PR wird automatisch aktualisiert, aber niemals gemergt.
- Nicht ausgewählte lokale Arbeitsbaumänderungen müssen vor einem Workflow-Lauf
  mit `--include` bewusst aus dem Commit ausgeschlossen werden.

## Wichtige Entscheidungen

- `main` bleibt unverändert und erhält keine automatischen Pushes.
- `codex/work` ist der dauerhafte Arbeitsbranch für veröffentlichte Codex-
  Arbeitsstände.
- Alte `technicianNames` bleiben beim Lesen und Schreiben kompatibel; neue
  Profilfelder liegen additiv in `technicians`, ohne Standard-Sonderrolle.
- Produktivdaten, Tags, Releases und Versionsnummern bleiben außerhalb dieses
  Workflows.
