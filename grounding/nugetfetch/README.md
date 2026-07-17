# NuGetFetch grounding

Self-contained eval bundle so this package owns its grounding going forward.

NuGetFetch is grounded with **pull-delivered skills**, not a packed `AGENTS.md`:

- `../../skills/` — a compact base `nugetfetch` skill plus five domain workflow skills
  (`version-resolution`, `tfm-asset-selection`, `package-sources-and-auth`,
  `signature-verification`, `caching`) and `plugin.json`. An agent pulls whichever a task needs.
- `eval.yaml` + `fixtures/` — a **CT-24** workflow-scenario eval (24 dual-graded scenarios:
  CODE = the right NuGetFetch API, not a hallucinated `NuGet.Protocol` type; OUTPUT = a stable,
  deterministic anchor). Ladder: CT01–06 basics floor, then four scenarios per domain.
- `run.sh` — reproduce: needs the `grounding` CLI + skill-validator from
  github.com/richlander/dotnet-package-grounding.

The README the eval scores against as the ungrounded baseline is the package README that ships in
the nupkg (`../../README.md`), kept only as a baseline.

## Ladder

| Scenarios | Skill | Focus |
| --- | --- | --- |
| CT01–CT06 | base `nugetfetch` | latest/oldest, download, extract+validate, prefix search, parse reference |
| CT07–CT10 | version-resolution | wildcard+normalize, stable-vs-prerelease, frozen release lines, normalize battery |
| CT11–CT14 | tfm-asset-selection | list-by-TFM, best-for-net8.0, best-for-net472, compatibility rules |
| CT15–CT18 | package-sources-and-auth | resolve config sources, `<clear/>` precedence, credential construction, multi-source latest |
| CT19–CT21 | signature-verification | status, author-vs-repository + counter-signature, three-state policy |
| CT22–CT24 | caching | package-cache round-trip, latest-cached-version, response-cache TTL |

Deterministic anchors are verified against live nuget.org and the published `NuGetFetch 0.7.0`.
