#!/usr/bin/env bash
set -euo pipefail

version="${1:?Usage: package-release.sh yy.mmdd.nn [output-directory]}"
output="${2:-artifacts/releases}"
if [[ ! "$version" =~ ^[0-9]{2}\.[0-9]{4}\.[0-9]{2}$ ]]; then
  echo 'Version must use yy.mmdd.nn (for example 26.1019.01).' >&2
  exit 1
fi
repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo"
for file in artifacts/site/wwwroot/index.html artifacts/site/wwwroot/dotnetdide.js artifacts/site/wwwroot/_framework/dotnet.js artifacts/site/wwwroot/llm.md; do
  test -f "$file" || { echo "Missing published file: $file. Publish the browser project first." >&2; exit 1; }
done
mkdir -p "$output"
output="$(cd -- "$output" && pwd)"
archive="$output/dotnetdide-$version.zip"
if [[ -e "$archive" || -e "$archive.sha256" ]]; then
  echo "Release files already exist for $version; choose a new version or output directory." >&2
  exit 1
fi
staging="$(mktemp -d)"
trap 'rm -rf -- "$staging"' EXIT
cp -R artifacts/site/wwwroot "$staging/wwwroot"
cp README.md integration.md llm.md LICENSE NOTICE "$staging/"
cp -R docs "$staging/docs"
commit="$(git rev-parse HEAD)"
printf '{"version":"%s","commit":"%s"}\n' "$version" "$commit" > "$staging/release.json"
(
  cd "$staging"
  zip -q -r "$archive" .
)
zip -T "$archive"
(
  cd "$output"
  sha256sum "$(basename "$archive")" > "$(basename "$archive").sha256"
)
echo "Created $archive and SHA-256 checksum."
