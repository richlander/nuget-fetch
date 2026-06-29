#!/usr/bin/env bash
# Reproduce NuGetFetch grounding eval. Requires the `grounding` CLI + skill-validator
# from github.com/richlander/dotnet-package-grounding (engine pinned there).
set -euo pipefail
grounding run nugetfetch --source agents --runs 3 --model "claude-haiku-4.5 claude-opus-4.8"
grounding run nugetfetch --source readme --readme-file README.md --runs 3 --model "claude-haiku-4.5 claude-opus-4.8"
grounding analyze --card        data/nugetfetch.haiku.json data/nugetfetch.opus.json
grounding analyze --source-diff data/nugetfetch.haiku.json data/nugetfetch-readme.haiku.json
