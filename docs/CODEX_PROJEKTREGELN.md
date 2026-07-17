# BueroCockpit - zentrale Codex-Projektregeln

Diese Datei ist die zentrale Regelquelle fuer Codex- und Agentenarbeit im Projekt BueroCockpit.

## Grundregeln

- Sprache: Deutsch.
- Immer vollstaendige Terminalbefehle ausgeben.
- Moeglichst viel ohne Codex erledigen.
- Erst Terminalbefehle, gezielte Suche mit `grep`/`sed`, kleine Patch-Skripte oder einzelne ueberschaubare Dateiaenderungen nutzen, wenn das sicher reicht.
- Codex nur bei groesseren oder zusammenhaengenden Aenderungen, komplexem UI, Architekturarbeit, schwer nachvollziehbaren Fehlern oder mehreren betroffenen Dateien verwenden.
- Das Codex-Modell abhaengig vom Aufgabentyp waehlen: Fuer kleine und mittlere Aufgaben ein effizientes Modell, fuer komplexe Architektur-, Refactoring- oder schwierige Fehlersuchaufgaben ein leistungsfaehigeres Modell. Eine deutliche Abweichung vom ueblichen Modellstandard kurz begruenden.
- Vor jeder Codex-Aufgabe `AGENTS.md`, `docs/CODEX_AUFTRAGSPRUEFUNG.md` und diese Datei lesen.
- Wenn sich Projektregeln, Codex-Regeln, Sperren, Modellvorgaben, Arbeitsweise oder wiederkehrende Pruefpflichten aendern, muessen `AGENTS.md` und/oder `docs/CODEX_PROJEKTREGELN.md` automatisch mit angepasst werden.

Codex-Startbefehl:

```bash
cd "$HOME/AppProjekte/BueroCockpit"
codex
```

## Projektgrundsatz

- Die Desktop-App bleibt das fuehrende System.
- Die iPad-App startet direkt in die Hauptansicht und bleibt vorerst lesend bzw. mobiler Erfassungsclient.
- Daten liegen lokal.
- Zielweg fuer spaetere Zusammenarbeit ist lokaler Netzwerk-Sync zwischen iPad und BueroCockpit-Desktop im Firmennetz.
- Pairing-Code, Live-Datei, Cloud-Datei und Datei-Kopplung sind nicht mehr der aktuelle Kopplungsweg.
- Spaetere Freigabe- oder Transportfunktionen werden nur bewusst und getrennt eingefuehrt.

## Verbindliche Fachlogik fuer Vorgaenge

Die vollstaendige Fachquelle ist `docs/ARBEITSKATEGORIEN.md`.

- Jeder Vorgang besitzt genau einen Vorgangstyp und einen Workflowstatus.
- Jeder neue oder bewusst geaenderte Vorgang hat genau eine normale Kategorie und darf niemals gleichzeitig in mehreren normalen Kategorien erscheinen.
- Normale Kategorien und ihre Hierarchie sind vollstaendig benutzerdefiniert; Kategorienamen wie `Angebote`, `Material` oder `Termin` sind nur moegliche Beispiele und keine feste Systemvorgabe.
- Fest eingebaute Navigation ist auf technische Systemansichten wie Uebersicht, Alle Vorgaenge, Papierkorb, Einstellungen, Archiv und einen technisch erforderlichen mobilen Eingang begrenzt. Vorgangstyp und Workflowstatus erzeugen keine parallelen Angebots-, Auftrags-, Material- oder Terminansichten.
- Fuer jede zulaessige Kombination aus Vorgangstyp und Workflowstatus kann der Benutzer genau eine Zielkategorie konfigurieren.
- Statuszuordnungen speichern ausschliesslich stabile Kategorie-IDs. Umbenennen oder Verschieben einer Kategorie darf die Zuordnung nicht zerstoeren.
- Bei einem bewussten Statuswechsel wird die konfigurierte Zielkategorie angewendet. Fehlt eine gueltige Zuordnung, darf keine beliebige Ersatzkategorie gewaehlt werden.
- Ein manueller Drag & Drop darf die aktuelle Kategorie aendern, ohne Vorgangstyp oder Workflowstatus zu aendern. Beim naechsten bewussten Statuswechsel greift wieder die konfigurierte Zuordnung.
- Variante A ist verbindlich: keine automatische Produktivdatenmigration; neue und bewusst geaenderte Vorgaenge verwenden sofort die neue Logik, unveraenderte Altbestaende werden nicht still zurueckgeschrieben oder neu zugeordnet.
- Navigation, Suche, Zaehler, Uebersicht, Detail, Import und Export muessen dieselbe aktuelle Kategorie-ID verwenden.

## Sicherheitsregeln

- Kein Release ohne ausdrueckliche Freigabe.
- Kein Tag und keine Versionserhoehung ohne ausdrueckliche Freigabe.
- Keine produktiven Daten verschieben oder loeschen.
- Keine riskante Datenmigration ohne Freigabe.
- Fuer ausdruecklich beauftragte Status- und Workflowfunktionen sind additive, rueckwaertskompatible Erweiterungen von Datenmodell und Persistenz ohne separate Einzelfreigabe erlaubt.
- Diese Ausnahme erlaubt weder das Loeschen noch das stille Umsortieren oder Neu-Zuordnen bestehender Produktivdaten, Kategorien oder Auftraege; riskante Migrationen bleiben freigabepflichtig.
- Keine alte Cloud-/Live-Datei-/Pairing-Code-Logik wieder aktivieren.
- Kein Desktop-Autostart.
- Kein UDP-Broadcast oder Portscan.
- Keine Nebenbaustellen.
- Build- und Testpflicht einhalten.
- Ohne ausdruecklichen Auftrag keine Benutzerdateien, CloudStorage-, iCloud- oder OneDrive-Dateien loeschen, verschieben oder veraendern.
- Wenn der Nutzer solche Dateien ausdruecklich pruefen, bearbeiten, bereinigen, importieren, synchronisieren oder fuer Release-/Projektzwecke verwenden will, ist das erlaubt.
- Trotzdem niemals unkontrolliert loeschen oder massenhaft verschieben.
- Vor riskanten Aenderungen an Benutzer- oder Cloud-Dateien Rueckfrage oder eine klare Sicherung/Pruefung einplanen.
- Keine Aenderungen an `tasks.json`, `categories.json`, `attachments` oder sonstigen Produktivdaten ohne ausdrueckliche Freigabe.

## Netzwerk-/Sync-Sperren ohne ausdrueckliche Freigabe

- Kein `FileSystemWatcher`.
- Kein Polling.
- Kein LiveReload.
- Kein Netzwerkdienst starten.
- Kein TCP-Port oeffnen.
- Kein `LocalSyncService.StartAsync` aktiv verdrahten.
- Kein Bonjour/mDNS aktivieren, ausser ausdruecklich fuer den manuell gestarteten lokalen Sync-Dienst.
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

- Aktueller Zielweg ist der lokale Netzwerk-Sync mit manuell gestartetem Desktop-Sync-Dienst.
- Der aktuelle Bedienweg zeigt keinen Pairing-Code und keine Live-Datei-/Cloud-/Datei-Kopplung.
- Das iPad prueft den Desktop per lokaler Adresse oder findet ihn per Bonjour/mDNS, falls verfuegbar.
- Manuelle IP-Eingabe bleibt der Fallback.
- Der Statuspunkt in der iPad-Hauptansicht muss die lokale Desktop-Vormerkung selbst laden und ohne Oeffnen der Einstellungen automatisch aktualisieren.
- Windows benoetigt Bonjour/mDNS nur fuer die automatische Desktop-Suche; der lokale Sync-Dienst darf ohne Bonjour laufen.
- Implementiert ist ausschließlich der bewusst am iPad gestartete, gerichtete Upload mobiler Eingänge nach `Sync/inbox`; er schreibt nicht direkt in die Desktop-Datenbank.
- Jede Erweiterung auf Desktop -> iPad, automatische Übertragung oder bidirektionale Zusammenführung benötigt einen neuen ausdrücklichen Auftrag.
- Geräte-ID, Vertrauensnachweis, Freigabestatus und letzter Geräte-Sync bleiben lokal; offene Nachweise dürfen am Desktop nur als Hash gespeichert werden.

## Release-Ablauf

- Kein Release ohne ausdrueckliche Freigabe.
- Ein ausdruecklicher Release-Auftrag bedeutet immer den kompletten Ablauf inklusive GitHub-Upload.
- Vor jedem Release automatisch als ersten Arbeitsschritt die Konsistenzpruefung aus `docs/CODEX_AUFTRAGSPRUEFUNG.md` durchfuehren und dokumentieren. Geprueft werden widerspruechliche oder veraltete Regeln, Dokumentation gegen tatsaechliche App, Releaseprozess gegen `AGENTS.md` sowie Designrichtlinien gegen Implementierung.
- Jeder Fund stoppt den Release vor Versionsaenderung, Build, Commit, Tag oder Upload. Der Nutzer entscheidet, ob zuerst Regeldateien oder Implementierung angepasst werden.
- Erst nach erfolgreicher Konsistenzpruefung folgen `git status` und `git pull origin main`, danach `dotnet build` und bei iPad-Code zusaetzlich `xcodebuild`.
- Der aktuelle Release-Befehl ist `./scripts/release.sh <version>`.
- Danach Artefakte pruefen, Release-Commit erstellen, `git tag v<version>` setzen, `git push origin main` ausfuehren, `git push origin v<version>` ausfuehren, `gh release create` mit Upload aller passenden Artefakte ausfuehren und zuletzt `gh release view` pruefen.

## Arbeitsweise vor Aenderungen

```bash
cd "$HOME/AppProjekte/BueroCockpit"
git status --short
git fetch origin main codex/work
git pull --ff-only origin "$(git branch --show-current)"
git status --short
dotnet build
```

Der aktuelle Branch darf nur per Fast-Forward aktualisiert werden. Ein normaler Codex-Auftrag auf `codex/work` darf `main` nicht automatisch einmischen.

Bei einem ausdruecklich reinen Dokumentationsauftrag ohne Code-, Projekt-, Build- oder Skriptaenderung entfaellt der Build. Vor und nach der Aenderung bleiben Branch-/Statuspruefung, `git diff --check` und eine gezielte Konsistenzsuche Pflicht.

Vor Codeaenderungen relevante Stellen mit `grep`/`sed` suchen und anzeigen.

## Pruefung nach Aenderungen

```bash
cd "$HOME/AppProjekte/BueroCockpit"
git diff --check
dotnet build
git status --short
```

Bei reinen Dokumentationsaenderungen gilt stattdessen mindestens:

```bash
cd "$HOME/AppProjekte/BueroCockpit"
git diff --check
rg -n '<betroffene Fachbegriffe und Altregeln>' AGENTS.md docs
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

Groessere Codex-Arbeiten werden nach sauberer Pruefung ausschliesslich ueber den dokumentierten Arbeitsbranch veroeffentlicht:

```bash
cd "$HOME/AppProjekte/BueroCockpit"
./scripts/publish-codex-work.sh \
  --description "<kurze Beschreibung>" \
  --include <geaenderte Datei> [--include <weitere Datei> ...]
git status --short
```

Ein direkter Push nach `main` gehoert ausschliesslich zu einem ausdruecklich freigegebenen Release- oder Mergeablauf und ist kein normaler Codex-Abschluss.

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
dotnet build, ausser bei einem reinen Dokumentationsauftrag
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

Bei ausschliesslichen Regel- und Dokumentationsaenderungen ohne Code-, Projekt-, Build- oder Skriptaenderung entfaellt `dotnet build`.

Wenn alles sauber ist:

```bash
cd "$HOME/AppProjekte/BueroCockpit"
./scripts/publish-codex-work.sh \
  --description "Projektregeln zentral dokumentieren" \
  --include AGENTS.md \
  --include docs/CODEX_PROJEKTREGELN.md
```

Wichtig:

- Kein Release.
- Kein Tag.
- Keine Versionserhoehung.

## Dauerhafte Regel: Mobile Änderungen schützen

Bei allen künftigen Arbeiten am iPad-/iPhone-/lokalen Netzwerk-Sync gilt:

Der Desktop darf lokale Änderungen auf iPad oder iPhone niemals ungefragt überschreiben.

Neue mobile Aufträge, Fotos, markierte Fotos, Notizen, Skizzen und Anhänge müssen vor jeder Desktop-Aktualisierung erkannt, gesichert und an den Desktop übertragen werden.

Synchronisation bedeutet Zusammenführen, nicht Ersetzen.

Mobile Originaldateien dürfen erst nach bestätigter Übernahme auf dem Desktop bereinigt werden.

Wenn Desktop und mobiles Gerät denselben Inhalt geändert haben, muss ein Konflikt erkannt werden. Es darf keine automatische, stille Überschreibung geben.

Diese Regel ist verbindlich und darf bei späteren Codex-Aufträgen nicht entfernt oder umgangen werden.
