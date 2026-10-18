#!/usr/bin/env bash
# Where every branch stands against main: which ones carry commits main does not have and how long they
# have lingered, and which ones are entirely on main and can go. The branches workflow prints the same
# into its job summary and keeps the branch report issue current every week; run this before deleting
# anything locally. What to do about what it lists: docs/COLLABORATION.md, "Branch life".
#
#   scripts/branches.sh [--base BRANCH] [--remote NAME] [--markdown] [--count] [--remote-only] [--no-fetch]
#
#   --markdown     tables for a job summary or an issue instead of plain text
#   --count        print only the number of branches with commits not on the base
#   --remote-only  the remote's branches alone (what the workflow sees); by default the local branches
#                  follow, each with its worktree when it has one
#   --no-fetch     report from what is fetched already
#
# It reports and exits 0 either way: it is a report, not a gate.
set -euo pipefail

base=main
remote=origin
markdown=false
count_only=false
remote_only=false
fetch=true
while [ $# -gt 0 ]; do
  case "$1" in
    --base) base="$2"; shift ;;
    --remote) remote="$2"; shift ;;
    --markdown) markdown=true ;;
    --count) count_only=true ;;
    --remote-only) remote_only=true ;;
    --no-fetch) fetch=false ;;
    -h|--help) sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
  shift
done

cd "$(git rev-parse --show-toplevel)"
if $fetch && ! git fetch --prune --quiet "$remote" 2>/dev/null; then
  echo "note: could not fetch from $remote; reporting what is fetched already" >&2
fi

base_ref="refs/remotes/$remote/$base"
git show-ref --verify --quiet "$base_ref" || base_ref="refs/heads/$base"
git show-ref --verify --quiet "$base_ref" || { echo "no branch $base locally or on $remote" >&2; exit 2; }

now=$(date +%s)
age() { # epoch seconds -> today, 1 day, N days
  local days=$(( (now - $1) / 86400 ))
  if [ "$days" -lt 1 ]; then echo today; elif [ "$days" -eq 1 ]; then echo "1 day"; else echo "$days days"; fi
}

# The worktree of each local branch that has one.
declare -A worktree=()
if ! $remote_only; then
  path=""
  while IFS= read -r line; do
    case "$line" in
      "worktree "*) path="${line#worktree }" ;;
      "branch refs/heads/"*) worktree["${line#branch refs/heads/}"]="$path" ;;
    esac
  done < <(git worktree list --porcelain)
fi

# One row per branch: kind|name|ahead|oldest epoch|last epoch|author|subject|worktree
rows=()
add_rows() {
  local prefix="$1" kind="$2" ref name ahead last author subject oldest
  while IFS= read -r ref; do
    [ "$ref" = "$base_ref" ] && continue
    name="${ref#"$prefix"}"
    [ "$name" = HEAD ] && continue
    [ "$kind" = local ] && [ "$name" = "$base" ] && continue
    ahead=$(git rev-list --count "$base_ref..$ref")
    last=$(git log -1 --format=%ct "$ref")
    author=$(git log -1 --format=%an "$ref")
    subject=$(git log -1 --format=%s "$ref" | tr '|' '/')
    oldest=""
    if [ "$ahead" -gt 0 ]; then oldest=$(git log --format=%ct "$base_ref..$ref" | tail -1); fi
    rows+=("$kind|$name|$ahead|$oldest|$last|$author|$subject|${worktree[$name]:-}")
  done < <(git for-each-ref --format='%(refname)' "$prefix")
}
add_rows "refs/remotes/$remote/" remote
$remote_only || add_rows "refs/heads/" local

lingering=()
done_rows=()
for row in "${rows[@]+"${rows[@]}"}"; do
  IFS='|' read -r kind name ahead oldest last author subject wt <<<"$row"
  if [ "$ahead" -gt 0 ]; then lingering+=("$oldest|$row"); else done_rows+=("$last|$row"); fi
done
if $count_only; then echo "${#lingering[@]}"; exit 0; fi

display() { # kind name worktree -> how the branch is shown
  if [ "$1" = remote ]; then echo "$remote/$2"; elif [ -n "$3" ]; then echo "$2 (local, worktree $3)"; else echo "$2 (local)"; fi
}

if $markdown; then
  echo "### Branches with commits not on \`$base\`"
  echo
  if [ "${#lingering[@]}" -eq 0 ]; then
    echo "None: every branch is entirely on \`$base\`."
  else
    echo "| Branch | Commits not on $base | Oldest of them | Last commit | By | Last subject |"
    echo "|---|---|---|---|---|---|"
    while IFS='|' read -r _ kind name ahead oldest last author subject wt; do
      printf '| `%s` | %s | %s | %s | %s | %s |\n' "$(display "$kind" "$name" "$wt")" "$ahead" "$(age "$oldest")" "$(age "$last")" "$author" "$subject"
    done < <(printf '%s\n' "${lingering[@]}" | sort -n)
  fi
  echo
  echo "### Branches entirely on \`$base\`"
  echo
  if [ "${#done_rows[@]}" -eq 0 ]; then
    echo "None."
  else
    echo "Their work is on \`$base\`, so they can go: \`git push $remote --delete <branch>\` for the remote, \`git branch -d <branch>\` and \`git worktree remove <folder>\` locally."
    echo
    echo "| Branch | Last commit | By |"
    echo "|---|---|---|"
    while IFS='|' read -r _ kind name ahead oldest last author subject wt; do
      printf '| `%s` | %s | %s |\n' "$(display "$kind" "$name" "$wt")" "$(age "$last")" "$author"
    done < <(printf '%s\n' "${done_rows[@]}" | sort -n)
  fi
else
  echo "Branches with commits not on $base: ${#lingering[@]}"
  while IFS='|' read -r _ kind name ahead oldest last author subject wt; do
    printf '  %-48s %3s commit(s), oldest %-8s last %-8s %s: %s\n' "$(display "$kind" "$name" "$wt")" "$ahead" "$(age "$oldest")" "$(age "$last")" "$author" "$subject"
  done < <(printf '%s\n' "${lingering[@]+"${lingering[@]}"}" | sed '/^$/d' | sort -n)
  echo "Branches entirely on $base (their work is merged; delete them): ${#done_rows[@]}"
  while IFS='|' read -r _ kind name ahead oldest last author subject wt; do
    printf '  %-48s last %-8s %s\n' "$(display "$kind" "$name" "$wt")" "$(age "$last")" "$author"
  done < <(printf '%s\n' "${done_rows[@]+"${done_rows[@]}"}" | sed '/^$/d' | sort -n)
fi
