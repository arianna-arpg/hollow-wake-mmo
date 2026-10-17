#!/usr/bin/env bash
# Builds the client archives a release carries and the manifest.json the launcher reads.
#
# For each platform: export the Godot client with its preset in src/HollowWake.Client/export_presets.cfg
# (the executable name comes from the preset's export_path), put content/core beside the executable,
# write hollowwake-build.json, and zip the folder. Then write manifest.json with
# launcher/scripts/make-manifest.mjs. The release workflow runs this on Linux; it also runs from Git
# Bash on Windows (zip, or Windows' own tar, builds the archives).
#
#   scripts/package-client.sh --godot PATH --version 0.1.0 [--channel stable|nightly]
#       [--out artifacts/release] [--platforms "windows-x64 linux-x64"] [--commit SHA] [--check]
#
# --check boots each exported build that can run on this machine headless and fails unless it loads
# its content (the line the client prints: "content loaded from ...: 0 errors").

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$root/src/HollowWake.Client"
godot=""
version=""
channel=""
out="$root/artifacts/release"
platforms="windows-x64 linux-x64"
commit="$(git -C "$root" rev-parse HEAD 2>/dev/null || echo unknown)"
check=false

while [ $# -gt 0 ]; do
  case "$1" in
    --godot) godot="$2"; shift 2 ;;
    --version) version="${2#v}"; shift 2 ;;
    --channel) channel="$2"; shift 2 ;;
    --out) out="$2"; shift 2 ;;
    --platforms) platforms="$2"; shift 2 ;;
    --commit) commit="$2"; shift 2 ;;
    --check) check=true; shift ;;
    *) echo "package-client: unknown argument $1" >&2; exit 2 ;;
  esac
done
if [ -z "$godot" ] || [ -z "$version" ]; then
  echo "usage: scripts/package-client.sh --godot PATH --version X.Y.Z [--channel stable|nightly] [--out DIR] [--platforms LIST] [--commit SHA] [--check]" >&2
  exit 2
fi
if [ -z "$channel" ]; then
  case "$version" in *-nightly*) channel=nightly ;; *) channel=stable ;; esac
fi

# Native paths for native programs (Godot, node, tar) when running under Git Bash on Windows.
native() {
  if command -v cygpath > /dev/null 2>&1; then cygpath -m "$1"; else printf '%s\n' "$1"; fi
}

# The export preset each platform key uses. The executable name lives in the preset itself.
preset_for() {
  case "$1" in
    windows-x64) echo "Windows Desktop" ;;
    linux-x64) echo "Linux" ;;
    *) echo "package-client: no export preset for platform $1" >&2; return 1 ;;
  esac
}

export_path_of() {
  awk -v want="$1" '
    /^\[preset\.[0-9]+\]$/ { section = 1; name = ""; next }
    /^\[/ { section = 0 }
    section && /^name=/ { name = $0; sub(/^name="/, "", name); sub(/"$/, "", name) }
    section && /^export_path=/ && name == want { p = $0; sub(/^export_path="/, "", p); sub(/"$/, "", p); print p; exit }
  ' "$project/export_presets.cfg"
}

zip_folder() { # zip the current folder's contents into $1
  if command -v zip > /dev/null 2>&1; then
    zip -q -r -X -9 "$1" .
  elif [ -x /c/Windows/System32/tar.exe ]; then
    /c/Windows/System32/tar.exe -a -c -f "$(native "$1")" -- *
  else
    echo "package-client: needs zip (or Windows tar) to build archives" >&2
    exit 1
  fi
}

can_run_here() {
  case "$1:$(uname -s)" in
    windows-x64:MINGW* | windows-x64:MSYS* | windows-x64:CYGWIN*) return 0 ;;
    linux-x64:Linux) return 0 ;;
    *) return 1 ;;
  esac
}

stage="$root/artifacts/stage"
rm -rf "$stage"
mkdir -p "$stage" "$out"
out="$(cd "$out" && pwd)"

echo "== importing the project"
"$godot" --headless --path "$(native "$project")" --import

build_args=()
for platform in $platforms; do
  preset="$(preset_for "$platform")"
  exe="$(basename "$(export_path_of "$preset")")"
  if [ -z "$exe" ]; then
    echo "package-client: preset \"$preset\" has no export_path" >&2
    exit 1
  fi
  dir="$stage/$platform"
  mkdir -p "$dir"
  echo "== exporting $platform with preset \"$preset\" as $exe"
  "$godot" --headless --path "$(native "$project")" --export-release "$preset" "$(native "$dir/$exe")"
  if [ ! -f "$dir/$exe" ]; then
    echo "package-client: the $preset export produced no $exe" >&2
    exit 1
  fi
  chmod +x "$dir/$exe" 2> /dev/null || true

  mkdir -p "$dir/content"
  cp -R "$root/content/core" "$dir/content/core"
  printf '{\n  "product": "Hollow Wake MMO",\n  "version": "%s",\n  "channel": "%s",\n  "platform": "%s",\n  "commit": "%s",\n  "builtAt": "%s"\n}\n' \
    "$version" "$channel" "$platform" "$commit" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "$dir/hollowwake-build.json"

  if $check && can_run_here "$platform"; then
    echo "== checking that the $platform build loads its content"
    log="$stage/$platform-boot.log"
    "$dir/$exe" --headless --quit-after 120 > "$log" 2>&1 || true
    cat "$log"
    if ! grep -Eq "content loaded from .*: 0 errors" "$log"; then
      echo "package-client: the exported $platform client did not load content/core" >&2
      exit 1
    fi
  fi

  archive="$out/HollowWakeMMO-client-$version-$platform.zip"
  rm -f "$archive"
  (cd "$dir" && zip_folder "$archive")
  echo "== $(basename "$archive"): $(wc -c < "$archive") bytes"
  build_args+=(--build "$platform" "$(native "$archive")" "$exe")
done

node "$(native "$root/launcher/scripts/make-manifest.mjs")" \
  --version "$version" --channel "$channel" --commit "$commit" \
  --out "$(native "$out/manifest.json")" "${build_args[@]}"
