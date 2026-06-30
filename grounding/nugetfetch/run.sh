#!/usr/bin/env bash
# Reproduce NuGetFetch grounding eval. Requires the `grounding` CLI + skill-validator
# from github.com/richlander/dotnet-package-grounding (engine pinned there).
set -euo pipefail
# Add `claude-opus-4.8` to --model to also reproduce the frontier tier.
grounding run nugetfetch --source agents --runs 3 --model "claude-haiku-4.5"
grounding run nugetfetch --source readme --readme-file README.md --runs 3 --model "claude-haiku-4.5"
grounding analyze --card        data/nugetfetch.haiku.json
grounding analyze --source-diff data/nugetfetch.haiku.json data/nugetfetch-readme.haiku.json
