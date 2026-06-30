# NuGetFetch grounding

Self-contained eval bundle so this package owns its grounding going forward.

- `AGENTS.md` — the grounding (also packed at the package root for NuGet/MCP).
- `TASKS.md` — the jobs-to-be-done the grounding is evaluated on (human-readable).
- `eval.yaml` + `fixtures/` — those tasks in machine form (scenarios + assertions).
- `data/` — committed n=3 results (haiku, AGENTS+README) to re-render cards.
  Frontier (opus) is reproducible with `run.sh` (add the model).
- `run.sh` — reproduce: needs the `grounding` CLI + skill-validator from
  github.com/richlander/dotnet-package-grounding.

Headline (haiku, n=3, 12 tasks): AGENTS.md is **BETTER** than no grounding —
success 11/12 → 12/12, archaeology 39 → 1, cost −62%. Versus a README cared for
to the same bar it is **NEUTRAL** (both 12/12); AGENTS.md reaches it in fewer
tokens (~1000 vs ~1400) and is marginally more self-sufficient on archaeology.
