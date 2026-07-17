#!/usr/bin/env bash
# Reproduce NuGetFetch grounding eval (pull-delivered skills). Requires the `grounding` CLI +
# skill-validator from github.com/richlander/dotnet-package-grounding (engine pinned there).
# Run from a clone of richlander/nuget-fetch; --root points the runner at the skills/ + eval here.
set -euo pipefail
REPO="$(cd "$(dirname "$0")/../.." && pwd)"
grounding run nugetfetch --root "$REPO" --source skill --delivery pull --runs 3 \
  --model "claude-opus-4.8 claude-haiku-4.5" --out ./data
grounding analyze --view card ./data/nugetfetch.opus.json
grounding analyze --view card ./data/nugetfetch.haiku.json
