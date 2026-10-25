<!-- Release steps (this comment is hidden on the Release page):
     1) Open https://github.com/sdhzth/ofdrwnet/releases/edit/v1.3.0
     2) Drag package-dependency-graph.png into the "Attach binaries…" box at the bottom and upload it
     3) Copy & paste the entire content of this file into the description box
     4) The image link below points to a Release asset, so it only renders after the PNG is uploaded -->

# NetOfd v1.3.0

## First publish to NuGet.org

This release is the **first official publish of NetOfd to NuGet.org** — **14 packages** (plus 14 symbol packages, `snupkg`),
published via NuGet Trusted Publishing (keyless OIDC). Package owner: `sdhzsmart`.

NetOfd is the **.NET cross-platform port** of [OFDRW (OFD Reader & Writer)](https://github.com/ofdrw/ofdrw) v2.3.8,
implementing the Chinese national standard **GB/T 33190-2016** (OFD, the page-description format for electronic
documents), plus companion standards: digital signature (GB/T 35275 / SES_V1-V5), domestic cryptography
(GM/T 0099-2020), and OFD-A long-term archiving (GB/T 42133-2022).

---

## Package list (14 packages, dependency order)

| Package ID | Description |
|---|---|
| `NetOfd.Core` | OFD base types, XML element model, document structure |
| `NetOfd.Font` | Font parsing, metrics, and embedding |
| `NetOfd.Pkg` | Package structure (ZIP linearization, `OFDEntries.xml` index, `RepackLinearized`) |
| `NetOfd.Reader` | OFD reading & parsing (with two-tier annotation fallback) |
| `NetOfd.Layout` | Layout and coordinate transforms |
| `NetOfd.Graphics2D` | 2D graphics draw-recording and render abstraction |
| `NetOfd.Converter` | Converter (render / export, based on SkiaSharp) |
| `NetOfd.Crypto` | Domestic cryptography & encryption (GM/T 0099-2020) |
| `NetOfd.Sign` | Digital signature (GB/T 35275, SM2withSM3) |
| `NetOfd.Tool` | Command-line tooling |
| `NetOfd.Archive` | OFD-A long-term archiving (GB/T 42133-2022) |
| `NetOfd.Emr` | **OFD-H electronic medical records** (GB/T 48666—2026, see below) |
| `NetOfd` | Aggregator package — references **all 12 submodule packages** |
| `NetOfd.Full` | Aggregator package — references **all 12 submodule packages** |

> Both `NetOfd` and `NetOfd.Full` aggregate the same 12 submodules; pick either one as your entry point.

```bash
# Core aggregator (all functional modules)
dotnet add package NetOfd

# Full aggregator (all functional modules, identical set)
dotnet add package NetOfd.Full
```

### Package dependency graph

![NetOfd package dependency graph](https://github.com/sdhzth/ofdrwnet/releases/download/v1.3.0/package-dependency-graph.png)

---

## Changes since v1.2.0

The functional code was already complete at v1.2.0; v1.3.0 closes the **publishing loop** and fixes one **packaging bug**:

- **First publish to NuGet.org**: configured NuGet Trusted Publishing (keyless OIDC), pushing 14 packages + 14 symbol
  packages in dependency-topological order, with `--skip-duplicate` for idempotency.
- **Fixed `NetOfd.Core` package version mismatch**: removed the hardcoded `<Version>1.2.0</Version>` from its csproj so it
  now inherits `VersionPrefix` from `Directory.Build.props` (previously the Core package name stayed on the old version after an upgrade).
- **Publishing automation**: added `tools/sync-publish.py` (dev → publish workspace whitelist sync) and
  `tools/publish-nuget.sh` (dependency-ordered push); the publish workflow is bound to a protected `release` environment
  and supports a `dry_run` rehearsal mode plus a version-consistency guard.
- **CI hardening**: fixed the long-red CI (Java corpus smoke test made environment-independent, CI now pulls the real corpus);
  Windows / Ubuntu both green.
- **Docs**: publishing flow and NuGet configuration written into the bilingual README.

---

## Core capabilities

### Beyond the Java original
- **DrawParam style cascade**: resolves inheritance along the ParentID chain (line width / color / fill / opacity), matching the Java renderer.
- **Clip chain**: full pipeline support — model parsing, Graphics2D recording, renderer `ClipPath`.
- **Batch edit session** `OfdEditSession`: multiple edits (annotations / versions / watermarks) flushed once; `OfdDocEditor` supports parallel export.
- **Real-corpus guardrail**: 67 Java test documents fully opened / rendered / extracted in smoke tests (100% pass).
- **Text decoration**: underline / strike-through / bold / italic / paragraph alignment persisted correctly.

### OFD-H electronic medical records (GB/T 48666—2026)
`NetOfd.Emr` exposes an **API** to generate OFD-H-compliant page documents with embedded machine-readable structured
clinical data (`samples/OfdHWriter` / `samples/OfdHExporter` wrap it as CLIs):

- Covers GB/T 48666, WS/T 500.32, WS 445.10, WS/T 482, GB/T 35275, GM/T 0099.
- Clause-by-clause implementation: `DocType` marker, package structure, metadata, font/image embedding, custom tags,
  attachments, machine-readable info, version snapshots, QC annotations, SM2+SM3 signature, anti-tamper integrity.
- Built-in `EmrCaseData` model, `Wst500Builder` (HL7 CDA R2 `ClinicalDocument`), `Wst500Codes` (56 DE codes + 13 LOINC
  sections + 11 OID roots), `EmrStructuredExporter` (read tags back into structured data).

---

## Verification

- Unit tests: **954 passing** (including 23 OFD-H end-to-end tests).
- Real-corpus guardrail: 67 Java test documents fully opened / rendered / extracted — 100% pass.

## License

- Overall: MIT (see `LICENSE`).
- Code ported from OFDRW: Apache-2.0 attribution retained (see `LICENSE-Apache-2.0` / `NOTICE`).
