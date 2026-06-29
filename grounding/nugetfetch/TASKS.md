# nugetfetch — tasks the grounding is evaluated on

Real jobs a developer asks an AI to do with this package. Each is gated by a
build + run with a deterministic anchor, so the grounding (AGENTS.md) is proven
to move an agent from "fails / hand-rolls" to "uses the API correctly, first try."
Machine form + fixtures: `eval.yaml`. Datasets/cards: `data/`.

| # | Task | Key API | Anchor |
| --- | --- | --- | --- |
| 1 | Look up the latest published version of a package | `GetLatestVersionAsync` | `latest: <n>.<n>.<n>` |
| 2 | Print the oldest published version of a package | `GetVersionsAsync` | `oldest: 3.5.8` |
| 3 | Download a specific package version to a .nupkg file | `.Download` | `downloaded: <bytes>` |
| 4 | Download and extract a package, then validate it | `PackageExtractor` | `valid: True` |
| 5 | Search nuget.org for packages by id prefix | `SearchByPrefixAsync` | `pkg: Newtonsoft.Json.Bson` |
| 6 | Resolve a wildcard version and normalize a version string | `ResolveVersionPatternAsync` | `resolved: 12.0.3 / normalized: 1.0.0` |
