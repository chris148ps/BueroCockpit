# Projektstatus BüroCockpit

## Aktueller Entwicklungsstand

BüroCockpit ist eine lokale Avalonia/.NET-Desktopanwendung mit einer
lesenden SwiftUI-iPad-App für Snapshots und mobile Erfassung. Der aktuelle
Desktop-Stand enthält eine hierarchische Kategorie-Navigation mit klarer
Trennung zwischen Systemnavigation und Benutzerkategorien, eine ruhige
Tagesübersicht mit realen Termin-, Wiedervorlagen-, Mobile-Eingangs- und
Synchronisationsinformationen, konsolidierte Dark-/Light-Designressourcen,
lokale Schreibtisch-Notizen, zeitgesteuerten Autospeicher und eine
profilbasierte Technikerverwaltung ohne Sonderrolle.

## Architektur

- Desktop: Avalonia UI, führendes System, lokale SQLite-/Dateidaten.
- Übersicht: read-only berechnete Dashboard-Projektion aus vorhandenen
  Aufgaben-, Mobile-Inbox- und lokalen Gerätestatusdaten; keine neuen Daten
  oder Persistenzfelder.
- Navigation: Systemseiten sind über reservierte IDs/Namen und `__`-IDs
  geschützt; Benutzerkategorien werden für Verwaltung und Auftragsauswahl
  separat aufgebaut.
- iPad: SwiftUI Snapshot Reader und mobiler Erfassungsclient.
- Synchronisation: lokaler Netzwerk-Sync ist vorbereitet, aber kein echter
  produktiver Datentransfer aktiv.
- Veröffentlichung: größere Codex-Arbeiten werden über `codex/work` und einen
  Draft-Pull-Request nach `main` sichtbar gemacht; Merge bleibt manuell.

## Erledigte Hauptfunktionen

- Ruhige zentrale Übersicht mit Heute-Datum, Terminen für Heute/Diese Woche/Nächste Woche,
  heutigen Wiedervorlagen, neuen Mobile-Eingängen und read-only Sync-Status.
- Freundliche Leerzustände ohne Fantasiedaten; Wiedervorlagen werden nur bei
  realen Treffern rot hervorgehoben.
- Kategoriebaum mit Unterkategorien und aggregierten Auftragszahlen.
- Systemnavigation und Benutzerkategorien sind in den UI-Quellmengen getrennt;
  Systemseiten erscheinen nicht in Kategorienverwaltung oder Auftragsauswahl.
- Moderne Auftragsdetailansicht mit großen Karten, semantischen Flächen,
  einheitlichen Eingabefeldern, Gruppen und Aktionsbereich.
- Auftragsbezogene Schreibtisch-Notizzettel mit Abwahl/Löschung.
- Gleichberechtigte Technikerprofile mit Name, Kürzel, E-Mail und Telefon.
- Horizontale Desktop-Einstellungstabs und Windows-11-Dark-Technikeransicht.
- Desktop startet stets in der Übersicht; Backup liegt unter Daten & Pfade und
  die Kategorienverwaltung zeigt keine technischen Sonderbereiche.
- Semantische Desktop-Ressourcen für Dark-/Light-Modus.
- Native iPadOS-Toolbar und systemeigene Suche.

## Bekannte offene Punkte

- Der echte lokale Netzwerk-Sync und produktive Datenübertragung sind weiterhin
  nicht aktiviert.
- Ohne vorhandene Mobile-Inbox-Einträge zeigt die Übersicht den Leerzustand;
  ohne erfolgreiche Synchronisation wird kein Zeitpunkt erfunden.
- Der GitHub-Draft-PR wird automatisch aktualisiert, aber niemals gemergt.

## Wichtige Entscheidungen

- Die Übersicht bleibt bewusst auf die vier angeforderten Tagesbereiche
  beschränkt und zeigt keine zusätzlichen Auftragsstatistiken.
- `main` bleibt unverändert und erhält keine automatischen Pushes.
- `codex/work` ist der dauerhafte Arbeitsbranch für veröffentlichte Codex-
  Arbeitsstände.
- Produktivdaten, Tags, Releases und Versionsnummern bleiben außerhalb dieses
  Workflows.
