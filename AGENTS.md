# BueroCockpit - Agentenregeln

Die zentralen Projektregeln stehen in:

    docs/CODEX_PROJEKTREGELN.md

Vor jeder Codex-Aufgabe immer zuerst lesen:

    cd "$HOME/AppProjekte/BueroCockpit"
    sed -n '1,220p' AGENTS.md
    sed -n '1,260p' docs/CODEX_PROJEKTREGELN.md

Kurzregeln:

- Sprache: Deutsch.
- Immer vollstaendige Terminalbefehle ausgeben.
- Moeglichst viel ohne Codex per Terminal, Suche oder kleinem Patch erledigen.
- Wenn Codex noetig ist, in diesem Projekt grundsaetzlich dieses Modell verwenden:

    cd "$HOME/AppProjekte/BueroCockpit"
    codex -m gpt-5.5

- Keine Releases, Tags oder Versionserhoehungen ohne ausdrueckliche Freigabe.
- Keine produktiven Daten, iCloud-Dateien oder OneDrive-Dateien verschieben, loeschen oder migrieren.
- Keine Netzwerk-/Sync-Aktivierung ohne ausdrueckliche Freigabe.
- Wenn sich Projektregeln, Codex-Regeln, Sperren, Modellvorgaben, Arbeitsweise oder wiederkehrende Pruefpflichten aendern, muessen `AGENTS.md` und/oder `docs/CODEX_PROJEKTREGELN.md` automatisch mit angepasst werden.
