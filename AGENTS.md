# BueroCockpit - Agentenregeln

Die zentralen Projektregeln stehen in `docs/CODEX_PROJEKTREGELN.md`.

Start fuer Codex-Aufgaben:

```bash
cd "$HOME/AppProjekte/BueroCockpit"
codex -m gpt-5.5
```

Vor jeder Aufgabe immer zuerst lesen:

- `docs/CODEX_PROJEKTREGELN.md`
- bei UI- und Design-Aenderungen zusaetzlich `docs/DESIGN_RICHTLINIEN.md`
- bei iPad-, Sync-, Foto- oder Netzwerk-Themen zusaetzlich `docs/LOCAL_NETWORK_SYNC.md`

Arbeitsweise:

- Kleine Terminal-, Such- und Patch-Aufgaben moeglichst ohne Codex erledigen.
- Codex fuer groessere zusammenhaengende UI-, Datenmodell- oder Architekturarbeiten verwenden.
- Vor Aenderungen `git status` und `git pull origin main` pruefen.
- Nach Aenderungen `git diff --check` und `dotnet build` pruefen.
- Bei iPad-Code zusaetzlich `xcodebuild` pruefen.

Release-Regel:

- Kein Release ohne ausdrueckliche Freigabe.
- Wenn der Nutzer ausdruecklich `Release erstellen` sagt, bedeutet das immer der komplette Release-Ablauf: Version setzen, Release-Skript ausfuehren, Release-Commit, Tag, `git push origin main`, `git push origin v<version>`, GitHub Release erstellen, Artefakte hochladen und `gh release view` pruefen.
- Aktueller Release-Befehl: `./scripts/release.sh <version>`.
