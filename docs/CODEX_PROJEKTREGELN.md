# BueroCockpit - zentrale Codex-Projektregeln

Diese Datei ist die zentrale Regelquelle fuer Codex- und Agentenarbeit im Projekt BueroCockpit.

## Grundregeln

- Sprache: Deutsch.
- Immer vollstaendige Terminalbefehle ausgeben.
- Moeglichst viel ohne Codex erledigen.
- Erst Terminalbefehle, gezielte Suche mit `grep`/`sed`, kleine Patch-Skripte oder einzelne ueberschaubare Dateiaenderungen nutzen, wenn das sicher reicht.
- Codex nur bei groesseren oder zusammenhaengenden Aenderungen, komplexem UI, Architekturarbeit, schwer nachvollziehbaren Fehlern oder mehreren betroffenen Dateien verwenden.
- Bei Codex-Aufgaben grundsaetzlich Modell `gpt-5.5` verwenden.
- `AGENTS.md` und `docs/CODEX_PROJEKTREGELN.md` sind bei jeder Codex-Aufgabe zuerst zu lesen.
- Wenn sich Projektregeln, Codex-Regeln, Sperren, Modellvorgaben, Arbeitsweise oder wiederkehrende Pruefpflichten aendern, muessen `AGENTS.md` und/oder `docs/CODEX_PROJEKTREGELN.md` automatisch mit angepasst werden.

Passender Codex-Startbefehl:

```bash
cd "$HOME/AppProjekte/BueroCockpit"
codex -m gpt-5.5
```

## Projektgrundsatz

- Die Desktop-App bleibt das fuehrende System.
- Die iPad-App bleibt vorerst lesend bzw. mobiler Erfassungsclient.
- Daten liegen lokal. Cloud-/OneDrive-/iCloud-/Live-Datei- oder Datei-Sync ist nicht mehr Zielarchitektur.
- Neuer Zielweg ist lokaler Netzwerk-Sync zwischen iPad und BueroCockpit-Desktop im Firmennetz.
- Pairing-Code, Live-Datei, Cloud-Datei und Datei-Kopplung sind nicht mehr aktueller Kopplungsweg fuer den lokalen Netzwerk-Sync.

## Strikte Sperren ohne ausdrueckliche Freigabe

- Kein Release.
- Kein Tag.
- Keine Versionserhoehung.
- Keine produktiven Daten verschieben oder loeschen.
- Keine iCloud-/OneDrive-Dateien verschieben.
- Keine Aenderungen an `tasks.json`, `categories.json`, `attachments` oder sonstigen Produktivdaten.
- Keine Datenmigration.
- Keine automatische Datenuebertragung.

## Netzwerk-/Sync-Sperren ohne ausdrueckliche Freigabe

- Kein `FileSystemWatcher`.
- Kein Polling.
- Kein LiveReload.
- Kein Netzwerkdienst starten.
- Kein TCP-Port oeffnen.
- Kein `LocalSyncService.StartAsync` aktiv verdrahten.
- Kein Bonjour/mDNS aktivieren, ausser ausdruecklich fuer den manuell gestarteten lokalen Testdienst.
- Keine automatische Geraetesuche.
- Keine Datenuebertragung zwischen Desktop und iPad.

## Lokale Einstellungen

Geraetespezifische Werte duerfen nur lokal gespeichert werden:

```text
BueroCockpitLocal/settings.local.json
```

Dazu gehoeren:

- Geraetename.
- Lokaler Port.
- Geraete-ID.
- Legacy-Felder wie alter Pairing-Code oder alte gekoppelte Geraete, falls sie in vorhandenen lokalen Einstellungen noch existieren.
- Legacy-Felder wie alte Datei-/Live-Datei-Pfade, falls sie in vorhandenen lokalen Einstellungen noch existieren.

Diese Werte duerfen nicht in `Sync/live/settings.json` geschrieben werden.

## Lokaler Netzwerk-Sync

- Aktueller Zielweg ist der lokale Netzwerk-Sync mit manuell gestartetem Desktop-Testdienst.
- Der aktuelle Bedienweg zeigt keinen Pairing-Code und keine Live-Datei-/Cloud-/Datei-Kopplung.
- Das iPad prueft den Desktop per lokaler Adresse oder findet ihn per Bonjour/mDNS, falls verfuegbar.
- Manuelle IP-Eingabe bleibt der Fallback.
- Windows benoetigt Bonjour/mDNS nur fuer die automatische Desktop-Suche; der lokale Testdienst darf ohne Bonjour laufen.
- Es gibt noch keinen echten Sync und keine Produktivdatenuebertragung, solange das nicht ausdruecklich beauftragt wird.

## Arbeitsweise vor Aenderungen

```bash
cd "$HOME/AppProjekte/BueroCockpit"
git status --short
git pull origin main
git status --short
dotnet build
```

Vor Codeaenderungen relevante Stellen mit `grep`/`sed` suchen und anzeigen.

## Pruefung nach Aenderungen

```bash
cd "$HOME/AppProjekte/BueroCockpit"
git diff --check
dotnet build
git status --short
```

Falls Netzwerk/Sync betroffen ist:

```bash
cd "$HOME/AppProjekte/BueroCockpit"
grep -RInE 'StartAsync|HttpListener|Kestrel|TcpListener|UdpClient|Socket|Listen|Bonjour|mDNS' .
```

Falls App/Netzwerk betroffen ist:

```bash
cd "$HOME/AppProjekte/BueroCockpit"
dotnet run
lsof -nP -iTCP -sTCP:LISTEN | grep -E 'BueroCockpit|dotnet'
```

Dabei muss geprueft werden, dass kein BueroCockpit-/dotnet-TCP-Listener geoeffnet wird, sofern Netzwerk nicht ausdruecklich freigegeben ist.

## Commit-Regeln

- Nur committen und pushen, wenn die Pruefung sauber ist.
- Keine Releases, Tags oder Versionserhoehungen.
- Keine Echtdaten ins Git.

Vor Commit und Push:

```bash
cd "$HOME/AppProjekte/BueroCockpit"
git diff --check
dotnet build
git status --short
```

Commit und Push nur nach sauberer Pruefung:

```bash
cd "$HOME/AppProjekte/BueroCockpit"
git add <geaenderte Dateien>
git commit -m "<kurze Beschreibung>"
git push origin main
git status --short
```

## Keine Echtdaten ins Git

Niemals committen:

- `AppData`
- Datenbanken
- Anhaenge
- Backups
- `publish`-Ordner
- echte Kundendaten
- Testdaten aus produktiven Datenordnern

## Kurzvorlage fuer kuenftige Codex-Auftraege

```text
Lies zuerst AGENTS.md und docs/CODEX_PROJEKTREGELN.md.

Aufgabe:
<Konkrete Aufgabe>

Aktueller HEAD:
<Commit>

Zusatzgrenzen:
- Kein Release.
- Kein Tag.
- Keine Versionserhoehung.
- Kein Netzwerkdienst/Port, falls nicht ausdruecklich gefordert.

Pruefen:
git diff --check
dotnet build
<ggf. grep/lsof/App-Test>

Commit:
<Commit-Message>
```

## Standardabschluss fuer Regeldatei-Aenderungen

Nach der Aenderung ausfuehren:

```bash
cd "$HOME/AppProjekte/BueroCockpit"
git diff --check
dotnet build
git status --short
git diff -- AGENTS.md docs/CODEX_PROJEKTREGELN.md
```

Wenn alles sauber ist:

```bash
cd "$HOME/AppProjekte/BueroCockpit"
git add AGENTS.md docs/CODEX_PROJEKTREGELN.md
git commit -m "Codex Projektregeln zentral dokumentieren"
git push origin main
```

Wichtig:

- Kein Release.
- Kein Tag.
- Keine Versionserhoehung.
