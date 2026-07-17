---
name: tfm-asset-selection
version: 0.7.1
description: >-
  Use when you need to pick the right assembly out of an extracted NuGet package for a target
  framework — a package ships lib/netstandard2.0 + lib/net8.0 + lib/net48 and you must choose the
  one compatible with, say, net8.0. NuGetFetch's static TfmResolver does the priority + cross-family
  compatibility decision (it is NOT NuGet.Frameworks / NuGetFramework). Requires the base
  `nugetfetch` pattern; typically follows a download + PackageExtractor.ExtractAsync.
---

# TFM asset selection — choose the right lib/<tfm> assembly

Trap this prevents: hand-globbing `lib/*/**.dll` and guessing which TFM is "best," or reaching for
`NuGet.Frameworks.NuGetFramework` (not present). `TfmResolver` (static) encodes NuGet's priority and
cross-family compatibility rules. Run it against an already-extracted package directory.

## Auto-select the best asset

```csharp
await PackageExtractor.ExtractAsync(nupkg, extractDir);

// Highest-priority TFM present in the package (net8.0 > net6.0 > netstandard2.0 > net48 …):
string? bestDir = TfmResolver.ResolvePackagePath(extractDir);

// Best asset COMPATIBLE with a specific target framework:
string? forNet8 = TfmResolver.ResolvePackagePath(extractDir, targetTfm: "net8.0");
```

`ResolvePackagePath(extractedPath, tfm?, targetTfm?)` returns the path to the chosen TFM directory,
or `null` if the package has no compatible managed assets. With `targetTfm`, it picks the
highest-priority TFM whose priority ≤ the target (i.e. the best asset that still runs on that target).

## Enumerate assemblies by TFM

```csharp
IReadOnlyList<PackageDll> dlls = TfmResolver.GetPackageDlls(extractDir); // each: .Path, .Tfm
foreach (PackageDll d in dlls)
    Console.WriteLine($"{d.Tfm}: {d.Path}");
```

## The compatibility rules (why a naive pick is wrong)

```csharp
TfmResolver.IsTfmCompatible("netstandard2.0", "net8.0"); // true  — netstandard runs everywhere
TfmResolver.IsTfmCompatible("net6.0", "net8.0");         // true  — modern .NET, priority <= target
TfmResolver.IsTfmCompatible("net481", "net8.0");         // false — .NET Framework is a DIFFERENT family
TfmResolver.GetTfmFamily("net8.0");   // TfmFamily.NetModern
TfmResolver.GetTfmFamily("net481");   // TfmFamily.NetFramework
```

- `GetTfmFamily` → `NetModern` (net5.0+), `NetCore` (netcoreappX), `NetStandard`, `NetFramework`, `Unknown`.
- Cross-family is blocked: modern .NET (net5.0+) accepts `NetModern` (≤ target), `NetCore`, and
  `NetStandard`; .NET Framework accepts `NetFramework` (≤ target) and `NetStandard`. `netstandard*`
  is compatible with **all** families. That's why you must not just grab the numerically-highest
  `net*` folder — `net481` is newer-numbered than `net8.0` but incompatible with it.
- `GetTfmPriority(tfm)` → int (higher is better) if you need to rank manually.

## Parsing / detection helpers

```csharp
TfmResolver.ExtractTfmFromPath("lib/net8.0/My.dll"); // "net8.0"
TfmResolver.IsTfmLike("net8.0");                      // true
```

## Guardrails

- Extract first; `TfmResolver` operates on a directory, not a `.nupkg` or a stream.
- Don't rank TFMs lexically or by number — use `IsTfmCompatible` / `ResolvePackagePath`; family matters.
- A `null` from `ResolvePackagePath` means "no compatible managed asset" (e.g. a tools-only or
  ref-only package) — handle it, don't assume a DLL exists.
