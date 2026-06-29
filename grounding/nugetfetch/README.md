# NuGetFetch grounding

Self-contained eval bundle so this package owns its grounding going forward.

- `AGENTS.md` — the grounding (also packed at the package root for NuGet/MCP).
- `TASKS.md` — the jobs-to-be-done the grounding is evaluated on (human-readable).
- `eval.yaml` + `fixtures/` — those tasks in machine form (scenarios + assertions).
- `data/` — committed n=3 results (haiku+opus, AGENTS+README) to re-render cards.
- `run.sh` — reproduce: needs the `grounding` CLI + skill-validator from
  github.com/richlander/dotnet-package-grounding.

Headline (n=3): AGENTS.md is BETTER than baseline on both tiers; vs a cared-for
README it's NEUTRAL on mini, BETTER on frontier (cost/archaeology).
