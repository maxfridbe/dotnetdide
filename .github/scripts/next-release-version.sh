#!/usr/bin/env bash
set -euo pipefail
day="${1:-$(date -u +%y.%m%d)}"
[[ "$day" =~ ^[0-9]{2}\.[0-9]{4}$ ]] || { echo 'Expected yy.mmdd date prefix.' >&2; exit 1; }
highest=0
while IFS= read -r tag; do
  tag="${tag#refs/tags/}"
  [[ "$tag" == "$day".* ]] || continue
  sequence="${tag#"$day."}"
  [[ "$sequence" =~ ^[0-9]{2}$ ]] || continue
  number=$((10#$sequence))
  if (( number > highest )); then highest=$number; fi
done
(( highest < 99 )) || { echo "All two-digit release numbers are used for $day." >&2; exit 1; }
printf '%s.%02d\n' "$day" "$((highest + 1))"
