#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
LAST_RUN="$PROJECT_ROOT/docs/codex_last_run.md"
NEXT_TASK="$PROJECT_ROOT/docs/NEXT_TASK.md"
TARGET_BRANCH="codex/work"
BASE_BRANCH="main"
PR_TITLE="Codex-Arbeitsstand BüroCockpit"

usage() {
  cat <<'EOF'
Verwendung:
  ./scripts/publish-codex-work.sh \
    --description "kurze Beschreibung" \
    --include <datei> [--include <datei> ...]

Optional:
  --remote origin
  --dry-run

Der Helfer verwendet ausschließlich den festen Branch codex/work,
committet nur ausdrücklich angegebene Pfade und pusht ausschließlich diesen
Branch. main, Tags, Releases und Versionsdateien werden nicht verändert.
EOF
}

die() {
  echo "FEHLER: $*" >&2
  exit 1
}

SHORT_NAME="work"
DESCRIPTION=""
REMOTE="origin"
DRY_RUN=false
INCLUDE_PATHS=()

while [ "$#" -gt 0 ]; do
  case "$1" in
    --name)
      [ "$#" -ge 2 ] || die "Wert für --name fehlt."
      SHORT_NAME="$2"
      shift 2
      ;;
    --description)
      [ "$#" -ge 2 ] || die "Wert für --description fehlt."
      DESCRIPTION="$2"
      shift 2
      ;;
    --include)
      [ "$#" -ge 2 ] || die "Wert für --include fehlt."
      INCLUDE_PATHS+=("$2")
      shift 2
      ;;
    --remote)
      [ "$#" -ge 2 ] || die "Wert für --remote fehlt."
      REMOTE="$2"
      shift 2
      ;;
    --dry-run)
      DRY_RUN=true
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      die "Unbekanntes Argument: $1"
      ;;
  esac
done

[ -n "$DESCRIPTION" ] || die "--description ist erforderlich."
[ "${#INCLUDE_PATHS[@]}" -gt 0 ] || die "Mindestens ein --include ist erforderlich."
[[ "$SHORT_NAME" =~ ^[a-zA-Z0-9._-]+$ ]] || die "--name enthält ungültige Zeichen."
[[ "$DESCRIPTION" != *$'\n'* ]] || die "--description darf keine Zeilenumbrüche enthalten."

cd "$PROJECT_ROOT"

CURRENT_BRANCH="$(git branch --show-current)"
COMMIT_MESSAGE="Codex: $DESCRIPTION"

case "$TARGET_BRANCH" in
  codex/work) ;;
  *) die "Interner Fehler: Zielbranch verletzt das codex/work-Schema." ;;
esac

command -v gh >/dev/null 2>&1 || die "GitHub CLI gh ist nicht installiert."
gh auth status >/dev/null 2>&1 || die "GitHub CLI gh ist nicht authentifiziert."
[ -f "$LAST_RUN" ] || die "Fehlende Dokumentation: $LAST_RUN"
[ -f "$NEXT_TASK" ] || die "Fehlende Dokumentation: $NEXT_TASK"

for path in "${INCLUDE_PATHS[@]}"; do
  [ -e "$path" ] || die "Angegebener Pfad nicht gefunden: $path"
done

if ! git diff --cached --quiet; then
  die "Es gibt bereits vorgemerkte Änderungen. Diese müssen zuerst geklärt werden, damit nichts Fremdes mitcommittet wird."
fi

if [ "$DRY_RUN" = true ]; then
  cat <<EOF
DRY-RUN: Keine Branch-, Commit- oder Push-Aktion ausgeführt.
Aktueller Branch: $CURRENT_BRANCH
Zielbranch: $TARGET_BRANCH
Commit: $COMMIT_MESSAGE
Remote: $REMOTE
Ausgewählte Pfade:
$(printf '%s\n' "${INCLUDE_PATHS[@]}")
EOF
  exit 0
fi

if [ "$CURRENT_BRANCH" = "$TARGET_BRANCH" ]; then
  :
elif git show-ref --verify --quiet "refs/heads/$TARGET_BRANCH"; then
  git switch "$TARGET_BRANCH"
else
  if git ls-remote --exit-code --heads origin "$TARGET_BRANCH" >/dev/null 2>&1; then
    git branch "$TARGET_BRANCH" "origin/$TARGET_BRANCH"
    git switch "$TARGET_BRANCH"
  else
    git switch -c "$TARGET_BRANCH"
  fi
fi

[ "$(git branch --show-current)" = "$TARGET_BRANCH" ] || die "Branchwechsel nicht bestätigt."

git add -- "${INCLUDE_PATHS[@]}"
if git diff --cached --quiet; then
  die "Die ausgewählten Pfade enthalten keine neuen Änderungen."
fi

git commit -m "$COMMIT_MESSAGE"
WORK_COMMIT="$(git rev-parse HEAD)"

PUSH_SUCCESS="Nein"
if git push -u "$REMOTE" "$TARGET_BRANCH"; then
  PUSH_SUCCESS="Ja"
else
  echo "WARNUNG: Der Arbeitscommit wurde erstellt, aber der Push ist fehlgeschlagen." >&2
fi

update_last_run_field() {
  local heading="$1"
  local value="$2"
  local temp_file
  temp_file="$(mktemp "${TMPDIR:-/tmp}/buerocockpit-last-run.XXXXXX")"
  awk -v heading="## $heading" -v value="$value" '
    $0 == heading { print; in_field=1; found=1; next }
    in_field && /^## / { print value; print ""; print; in_field=0; next }
    in_field { next }
    { print }
    END {
      if (in_field) { print value }
      if (!found) { print ""; print heading; print ""; print value }
    }
  ' "$LAST_RUN" > "$temp_file"
  mv "$temp_file" "$LAST_RUN"
}

update_last_run_field "Branch" "$TARGET_BRANCH"
update_last_run_field "Commit" "$WORK_COMMIT"
update_last_run_field "Push erfolgreich" "$PUSH_SUCCESS"

git add -- "$LAST_RUN"
if ! git diff --cached --quiet; then
  git commit -m "Codex: Dokumentationsmetadaten aktualisieren"
  METADATA_COMMIT="$(git rev-parse HEAD)"
  if [ "$PUSH_SUCCESS" = "Ja" ]; then
    if ! git push "$REMOTE" "$TARGET_BRANCH"; then
      PUSH_SUCCESS="Nein"
      update_last_run_field "Push erfolgreich" "$PUSH_SUCCESS"
      git add -- "$LAST_RUN"
      git commit -m "Codex: Pushstatus dokumentieren"
      METADATA_COMMIT="$(git rev-parse HEAD)"
      echo "WARNUNG: Der Metadaten-Push ist fehlgeschlagen." >&2
    fi
  fi
else
  METADATA_COMMIT="$WORK_COMMIT"
fi

if [ "$PUSH_SUCCESS" != "Ja" ]; then
  echo "Kein Draft-PR wird erstellt, weil der Branch-Push nicht erfolgreich war." >&2
  exit 1
fi

case "$(git branch --show-current)" in
  codex/work) ;;
  *) die "Sicherheitsabbruch: Es darf nur ein codex/work-* Branch veröffentlicht werden." ;
esac

PR_BODY_FILE="$(mktemp "${TMPDIR:-/tmp}/buerocockpit-codex-pr.XXXXXX.md")"
trap 'rm -f "$PR_BODY_FILE"' EXIT
CHANGED_AREAS="$(git diff --name-only "$BASE_BRANCH...$TARGET_BRANCH")"
LAST_COMMIT="$(git log -1 --oneline)"
cat > "$PR_BODY_FILE" <<EOF
## Aktuelles Ziel

Arbeitsstand des größeren Codex-Auftrags nachvollziehbar auf `codex/work` bereitstellen.

## Letzter Codex-Lauf

$(cat "$LAST_RUN")

## Verwendetes Modell

GPT-5.6 Luna, Reasoning medium.

## Letzter Commit

$LAST_COMMIT

## Geänderte Bereiche

$CHANGED_AREAS

## Ausgeführte Tests und Testergebnis

Die im letzten Lauf dokumentierten Tests wurden ausgeführt; siehe oben in `docs/codex_last_run.md`.

## Risiken und offene Punkte

Siehe `docs/codex_last_run.md` und `docs/NEXT_TASK.md`. Nicht ausgewählte lokale Änderungen bleiben außerhalb dieses Branch-Commits.

## Nächste empfohlene Aufgabe

$(cat "$NEXT_TASK")

Nicht mergen ohne ausdrückliche Freigabe.
EOF

REPO="$(gh repo view --json nameWithOwner --jq '.nameWithOwner')"
OPEN_PR_COUNT="$(gh pr list --repo "$REPO" --state open --head "$TARGET_BRANCH" --base "$BASE_BRANCH" --json number --jq 'length')"
if [ "$OPEN_PR_COUNT" -gt 1 ]; then
  die "Mehr als ein offener PR von $TARGET_BRANCH nach $BASE_BRANCH gefunden; kein weiterer PR wird angelegt."
fi

if [ "$OPEN_PR_COUNT" -eq 1 ]; then
  PR_NUMBER="$(gh pr list --repo "$REPO" --state open --head "$TARGET_BRANCH" --base "$BASE_BRANCH" --json number,isDraft --jq '.[0] | if .isDraft then .number else "READY" end')"
  [ "$PR_NUMBER" != "READY" ] || die "Der bestehende PR ist nicht mehr Draft; keine automatische Änderung auf Ready/Merge."
  gh pr edit "$PR_NUMBER" --repo "$REPO" --title "$PR_TITLE" --body-file "$PR_BODY_FILE"
  PR_URL="$(gh pr view "$PR_NUMBER" --repo "$REPO" --json url --jq '.url')"
  PR_ACTION="Draft-PR aktualisiert"
else
  PR_URL="$(gh pr create --repo "$REPO" --base "$BASE_BRANCH" --head "$TARGET_BRANCH" --title "$PR_TITLE" --body-file "$PR_BODY_FILE" --draft)"
  PR_NUMBER="$(gh pr view "$PR_URL" --repo "$REPO" --json number --jq '.number')"
  PR_ACTION="Draft-PR erstellt"
fi

echo "Codex-Arbeitsstand veröffentlicht:"
echo "- Branch: $TARGET_BRANCH"
echo "- Arbeitscommit: $WORK_COMMIT"
echo "- Metadatencommit: $METADATA_COMMIT"
echo "- Push erfolgreich: $PUSH_SUCCESS"
echo "- $PR_ACTION: #$PR_NUMBER $PR_URL"
echo "- Aktueller Branch: $(git branch --show-current)"
echo "- Letzter Commit: $(git log -1 --oneline)"
git status --short

if [ "$PUSH_SUCCESS" != "Ja" ]; then
  exit 1
fi
