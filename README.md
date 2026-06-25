# HumanReadableTrigger

A .NET class library for parsing human-readable trigger expressions.

> **Status:** early prototype. The library is currently an empty class library
> skeleton — implementation has not started yet.

## Target frameworks

The library multi-targets all currently supported .NET **LTS** releases:

- `net8.0`
- `net10.0`

## Building and testing

```sh
dotnet build
dotnet test
```

## Packaging

NuGet packaging is configured but **disabled while prototyping**. To re-enable,
set `GeneratePackageOnBuild` back to `true` in
`src/HumanReadableTrigger/HumanReadableTrigger.csproj` (and re-enable the pack
step in `.github/workflows/ci.yml`), then:

```sh
dotnet pack -c Release
```
