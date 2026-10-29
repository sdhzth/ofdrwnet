# NetOfd - Cross-Platform OFD Read/Write Library

[![license](https://img.shields.io/badge/license-MIT-blue)](./LICENSE) [![license](https://img.shields.io/badge/ported%20code-Apache--2.0-blue)](./LICENSE-Apache-2.0)

**[简体中文](README.md) | [English](README.en.md)**

## Overview

NetOfd is a .NET cross-platform port of [OFDRW (OFD Reader & Writer)](https://github.com/ofdrw/ofdrw) v2.3.8.
It provides a complete read/write implementation of the Chinese national standard
**GB/T 33190-2016** *Electronic document storage and exchange format — Layout document* (OFD),
and covers the companion standards for digital signatures (GB/T 35275 / SES_V1-V5),
Chinese national cryptography (GM/T 0099-2020) and OFD-A archiving (GB/T 42133-2022).

### Beyond the Java version

- **DrawParam style inheritance**: resolved along the `ParentID` chain (line width, color, fill, alpha), matching the Java renderer
- **Clip chain (Clips)**: model parsing, Graphics2D recording and renderer `ClipPath` support end-to-end
- **Batch edit session** `OfdEditSession`: many edits (annotations, versions, watermarks) persisted in one pass; `OfdDocEditor` supports parallel export
- **Real-corpus guardrail**: all 67 upstream Java test documents pass open / render / extract smoke tests (100%)
- **Text decoration**: underline, strikethrough, bold, italic and paragraph alignment are all persisted

## OFD-H Electronic Medical Record support (GB/T 48666-2026)

The `NetOfd.Emr` module implements **GB/T 48666-2026 *Technical requirements for electronic medical
record layout documents***: as a library API it generates OFD-H conformant layout documents and
embeds machine-readable structured clinical data inside them.
`samples/OfdHWriter` / `samples/OfdHExporter` are command-line wrappers around it.

### Standards covered

| Standard | Title | How NetOfd implements it |
|---|---|---|
| **GB/T 48666-2026** | Technical requirements for electronic medical record layout documents | Primary standard: DocType identifier, package structure, metadata, custom tags, attachments, versions, annotations, signatures, anti-tampering |
| **GB/T 33190-2016** | Electronic document storage and exchange format — Layout document | Base OFD format (`Doc_0` single document, `ofd:` namespace prefix, ZIP linearization) |
| **WS/T 500.32-2016** | Electronic medical record shared document specification — Part 32: Inpatient medical record front page | HL7 CDA R2 `ClinicalDocument` construction and parsing (`Wst500Builder`) |
| **WS 445.10-2014** | Basic dataset for electronic medical record — Part 10: Inpatient medical record front page | 56 DE-coded data elements (`Wst500Codes`) and structured export |
| **WS/T 482-2016** | Compilation specification for health information shared documents | Shared document body constraints |
| **GB/T 35275** | Information security technology — Digital signature format specification | SM2withSM3 national-crypto signature / verification (`OfdSigner`) |
| **GM/T 0099-2020** | Electronic document security technology — Signature evidence and non-repudiation | §7.4.3 anti-tampering / integrity protection (`OfdIntegrity`) |

Code tables referenced: GB/T 2261.1 (gender), GB/T 2261.2 (marital status), GB 3304 (ethnic group),
ISO 3166-1 (nationality), ICD-10 (disease classification), ICD-9-CM-3 (procedures), LOINC (section codes).

### Clause-by-clause mapping

The header comment of `src/NetOfd.Emr/OfdHDocumentGenerator.cs` records the generation pipeline against
the standard clauses:

| Clause | Requirement | Implementation |
|---|---|---|
| 6.1.2 | Single-document carrier (`Doc_0`, OFD format) | `OFDDoc` single document |
| 6.1.3 | ZIP 6.2 + Deflate, linearized (`OFDEntries.xml` index) | `RepackLinearized` |
| 6.2 | Canonical package directory structure (Table 1) | `NormalizeStructure` |
| 7.1 a) | `DocType="OFD-H"` | `OFRoot.OfdDocType` |
| 7.1 c) | DocInfo metadata (patient / responsible party / time / keywords) | `ConfigureDocInfo` |
| 7.2 b) | Embedded font data (FangSong FontFile) | `EmbedFonts` |
| 7.2 c) | Embedded image resources (DocumentRes + `Res/`) | `Img` element |
| 7.3 c) | Structured data stored as custom tags | `OfdTagger.AddCustomTag` x2 |
| 7.4 | Attachments (`Attachs/Attachments.xml` + `Attach_N.png`) | `OfdAttacher` + normalization |
| 7.5 | Machine-readable information (file header / metadata / identification, UTF-8) | Machine-readable XML custom tag |
| 7.6 | Version snapshots (full history retained) | `OfdVersioner` |
| 3.12 / 5.3.2 e) | Annotation records (review annotations) | `OfdAnnotator` |
| 6.3 / 8 c) d) e) | SM2 + SM3 national-crypto signature, protecting everything except the annotation list and the signature list | `OfdSigner` + `GBT35275DSContainer` |
| GM/T 0099 §7.4.3 | Anti-tampering / integrity protection | `OfdIntegrity` + `GmProtectSigner` |

### Generator: NetOfd.Emr.OfdHDocumentGenerator

```csharp
using NetOfd.Emr;

var result = OfdHDocumentGenerator.Generate(new OfdHOptions
{
    OutputPath = "record.ofd",
    CaseData   = myCaseData,          // pass an EmrCaseData; null uses the built-in demo data
    // FontPath = "/path/to/chinese.ttf",   // auto-detected per platform when omitted
});

if (!result.Success)
{
    Console.Error.WriteLine(result.Error);
    return;
}

Console.WriteLine($"{result.PageCount} pages / {result.SizeBytes} bytes / {result.SignatureCount} signature(s)");
Console.WriteLine($"All signatures valid: {result.AllSignaturesValid}, integrity: {result.IntegrityVerified}");
```

Produces a **3-page A4 portrait** document (20 mm margins) in OFD-H layout form:

| Page | Content |
|---|---|
| 1 | Inpatient medical record front page (6 basic-info rows / 6 encounter rows / 2 diagnosis rows) |
| 2 | Admission record (general items, chief complaint, present illness, past history, personal + family history, physical examination) |
| 3 | Auxiliary examination (ECG text + image + figure caption), preliminary diagnosis and treatment plan, physician signature, date |

The document also embeds: CJK font data, an ECG image resource, an internal attachment,
a quality-control review annotation, a version snapshot, a machine-readable information tag plus
a WS/T 500 shared document tag, an SM2withSM3 national-crypto signature, and GM/T 0099
anti-tampering protection.

Common `OfdHOptions` properties:

| Property | Default | Description |
|---|---|---|
| `OutputPath` | required | Output .ofd path |
| `CaseData` | built-in demo data | The medical record data (`EmrCaseData`) |
| `SkipSignature` | `false` | Skip signing and anti-tampering (structural debugging only) |
| `DocumentId` | auto-generated GUID | Document identifier DocID; pass a fixed value for reproducible output |
| `FontPath` | auto-detected per platform | CJK font file to embed |
| `SkipFontEmbeddingWhenMissing` | `true` | Log a warning and skip embedding when no font is found (`false` throws instead) |
| `SigningTime` | current time | Signature timestamp |
| `Log` | `Console.Out` | Log output target |

### Command-line wrapper

```bash
dotnet run --project samples/OfdHWriter -- <out.ofd> [--work <work-dir>] [--font <font-path>] [--no-sign] [--no-preview]
```

Exit codes: `0` success with signature self-check passed / `1` invalid arguments /
`2` generation error / `3` signature self-check failed.

### Structured data: the NetOfd.Emr module

| Class | Responsibility |
|---|---|
| `OfdHDocumentGenerator` | OFD-H layout document generator (see above) |
| `OfdHOptions` / `OfdHResult` | Generation options and result object |
| `EmrCaseData` | Medical record data model (patient, encounter, diagnoses, orders, …) |
| `EmrStructuredData` | Bridge between layout page and machine-readable data: one field set is both rendered into the OFD-H layout and written into the structured tag using standard DE codes |
| `Wst500Builder` | Serializes `EmrStructuredData` into a WS/T 500.32 shared document (HL7 CDA R2 `ClinicalDocument`) |
| `Wst500Codes` | Encoding constant dictionary: 13 section LOINC codes, 56 data element DE codes, 11 OID roots, 11 value-set codes |
| `EmrStructuredExporter` | Reads custom tags back from an OFD-H file and restores standard structured data (JSON / text) |

```csharp
using NetOfd.Emr;

var data = new EmrStructuredData
{
    PatientName = "张建国",
    SexCode     = Wst500Codes.SexMale,   // "1"
    BirthDate   = "19651120",
};
data.Diagnoses.Add(new EmrDiagnosis { Name = "不稳定型心绞痛", Code = "I20.0" });

// Build a WS/T 500.32 shared document (usable as an OFD-H custom tag payload)
byte[] sharedDoc = Wst500Builder.BuildBytes(data);

// Export structured data back from an OFD-H document
var result = EmrStructuredExporter.Export("record.ofd");
Console.WriteLine(EmrStructuredExporter.ToJson(result));
```

The `ClinicalDocument` produced by `Wst500Builder` covers 10 sections (LOINC coded);
`entry` elements are nested under their `section`, and observations are typed as
`PQ` (value + unit) / `CD` (coded value) / `ST` (text).

### Exporter: samples/OfdHExporter

```bash
dotnet run --project samples/OfdHExporter -- <in.ofd> [--out <output-dir>] [--format json|xml|text|all]
```

| Artifact | Content |
|---|---|
| `<base>_结构化数据.json` | WS 445.10 flat data elements (`DE code` → value) plus a diagnoses array |
| `<base>_WS_T500共享文档.xml` | The raw WS/T 500.32 shared document |
| `<base>_结构化数据.txt` | Human-readable summary |

Exit codes: `0` success / `1` invalid arguments or missing input / `2` export error /
`3` no structured data tag found in the document.

### Test coverage

OFD-H automated tests live in `tests/NetOfd.Tests/Emr/`:

| Test class | Cases | Coverage |
|---|---|---|
| `OfdHDocumentGeneratorTest` | 18 | End-to-end generation pipeline: 3-page structure, DocType, DocID, package directory structure (Table 1), linearization order, font embedding, version snapshot, two custom tags, machine-readable information triple, structured data reverse export, CDA structure, attachment byte equality, annotations, signature verification, anti-tampering integrity, three-page text extraction |
| `OfdHGeneratorOptionTest` | 5 | Skipping signatures, non-existent font path (skip vs. throw), custom case data, automatic DocID |
| `Wst500SharedDocumentTest` | 12 | WS/T 500.32 constant self-consistency, CDA structural constraints, tag write/read round trip |

### Known limitations

Stated plainly so the usable scope can be assessed:

- **Section coverage 10/13**: the three defined-but-unimplemented sections are transfer record (`28570-0`), discharge diagnosis (`56846-4`) and administration (`32473-6`).
- **Data element coverage 33/56**: about 23 of the 56 DE code constants are not yet written into the shared document (medication, blood type, birth weight, autopsy flag, pathology number, external cause, etc.).
- **Some fields reach the layout but not the machine-readable tag**: encounter-related fields of `EmrStructuredData` (age, admission count, admission/discharge times, admission route, admission/discharge departments, ward, payment method, responsible nurse, coding physician, …) currently appear on the layout page only, and are not yet part of the structured tag.
- **Only one shared document part is covered**: only WS/T 500.32 (inpatient medical record front page) is implemented; the remaining WS/T 500 series parts are out of scope for now.
- **Font embedding depends on a CJK font being available**: when `FontPath` is not set explicitly, the generator probes the platform for FangSong / CJK fonts. If none is found it logs a warning and skips embedding (clause 7.2 b is then not satisfied), so production use should supply a font file explicitly.

## Project structure

```
netofd/
├── src/
│   ├── NetOfd.Core/          # Core: OFD XML element model + base types (ST_*/CT_*)
│   ├── NetOfd.Emr/           # OFD-H electronic medical record (GB/T 48666-2026): layout document generation + WS/T 500 shared document + standard export
│   ├── NetOfd.Pkg/           # Virtual container + ZIP packaging
│   ├── NetOfd.Font/          # Font management and metrics
│   ├── NetOfd.Reader/        # OFD parser / text extraction / keyword location
│   ├── NetOfd.Layout/        # Layout engine + editors (annotation/attachment/custom tag/version)
│   ├── NetOfd.Graphics2D/    # java.awt-style drawing API
│   ├── NetOfd.Converter/     # OFD -> PNG/SVG/HTML rendering
│   ├── NetOfd.Sign/          # Digital signature / electronic seal (SES_V1/V4/V5, GB/T 35275)
│   ├── NetOfd.Crypto/        # Chinese national crypto document encryption (SM4/KDF, password and certificate schemes)
│   ├── NetOfd.Tool/          # Document utilities (merge / mix / delete pages)
│   ├── NetOfd.Archive/       # OFD-A archive compliance check and conversion
│   ├── NetOfd.Full/          # Aggregate of all modules
│   └── NetOfd/               # Unified entry point (meta-package)
├── tests/
│   └── NetOfd.Tests/         # 950+ unit / integration / performance tests
├── samples/                  # Samples (write / sign / render / convert / OFD-H generation / structured export)
│   └── OfdHWriter/           # GB/T 48666-2026 electronic medical record OFD-H generator
└── Directory.Build.props     # Shared build properties
```

## Quick start

### Creating an OFD file

```csharp
using NetOfd.Layout;
using NetOfd.Layout.Element;

using var doc = new OFDDoc("output.ofd");
doc.AddPage();
doc.Add(new Paragraph().SetWidth(100)
    .Add(new Span("你好，OFD 世界！").SetFontSize(5d).SetBold()));
```

### Reading and text extraction

```csharp
using NetOfd.Reader;
using NetOfd.Converter; // ContentExtractor namespace may vary — check the actual assembly

using var reader = new OFDReader("input.ofd");
var pages = reader.GetPages().ToList();
foreach (var text in ContentExtractor.ExtractAllPagesText(pages))
    Console.WriteLine(text);
```

### Page editing chain (annotation → attachment → custom tag → version snapshot)

```csharp
using NetOfd.Layout.Edit;

// Approach 1: edit step by step (each call returns new bytes)
var edited = OfdAnnotator.AddAnnotation(ofdBytes, 1,
    new Annotation(10, 20, 40, 16, AnnotType.Path, drawer));
edited = OfdAttacher.AddAttachment(edited, new Attachment("合同扫描件", "scan.pdf"));
edited = OfdTagger.AddCustomTag(edited, "urn:example:keyword", tagBytes);
OfdVersioner.AddVersion(edited, "定稿", out _);      // retain a version
var old = OfdVersioner.ExtractVersion(edited, "v1"); // restore a historic version

// Approach 2: batch edit session (many operations, single write — far faster in bulk)
using (var session = OfdEditSession.Open(ofdBytes))
{
    for (int p = 1; p <= session.PageCount; p++)
        session.AddAnnotation(p, annotation);
    session.AddVersion("批注定稿");
    ofdBytes = session.Save();   // Save() must be called explicitly
}
```

### Fixed-layout (板式) pages: VirtualPage

`VirtualPage` targets **fixed layouts with hand-computed coordinates** (forms, reports, covers):
all elements inside are absolutely positioned, so **there is no automatic pagination**.

```csharp
using NetOfd.Layout;
using NetOfd.Layout.Element;

var vPage = new VirtualPage(PageLayout.A4());
vPage.Add(new Div()
    .SetPosition(Position.Absolute)   // absolute positioning is mandatory
    .SetXY(20, 30)                    // X / Y are required
    .SetWidth(170)                    // Width is required
    .SetHeight(10)
    .SetBackgroundColor(new CT_Color { Value = "0,0,255" }));
doc.AddVPage(vPage);   // only enqueued — the real page is emitted on Close()
doc.Close();
```

- **Insert at a specific page number** (new in v1.3.1): `vPage.SetPageNum(2)` makes the page land at
  position 2 when the document is rendered; without it the page is appended at the end.
  After rendering, `vPage.PageNum` is back-filled with the page's **final page number** so callers can query it.
  Physical directories (`Page_N`) are still allocated in append order, while the **document page order is
  determined by the `<Page>` list in `Document.xml`** — so page-number-based reads
  (`OFDReader.GetPageText(2)`) always stay correct.
- **Master / background reuse**: `vPage.AddTemplate(tpl)` reuses a fixed backdrop (rendered into its own
  `Tpl_M/Content.xml` and overlaid below the body via a `<Template>` reference).
- **Layer assignment**: `Div.Layer` decides whether an element lands in the foreground / body / background
  layer (default: body, `Body`).
- ⚠️ To build multi-page documents prefer `VirtualPage` or `PageAreaFiller`: interleaving streaming `Add`
  with `AddPage` leaves the earlier pages blank (streamed content is laid out from the latest page onwards).

### Digital signature and verification

```csharp
using NetOfd.Sign;

var signed = OfdSigner.Sign(ofdBytes, signerKeyPair, signerCert,
    "2026-08-23 10:00:00", seal: sealAsn1, isSeal: true, version: 4);

var result = OfdValidator.Verify(signed);
Console.WriteLine(result.AllValid ? "全部有效" : result.Error);
```

### Rendering to images

```csharp
using NetOfd.Converter;

using var exporter = new ImageExporter("input.ofd", outDir, "png", ppm: 15);
var pngPaths = exporter.Export();   // one PNG per page
```

## Command-line tools

```bash
dotnet run --project tests/NetOfd.TestConsole -- info   <file.ofd>
dotnet run --project tests/NetOfd.TestConsole -- verify <file.ofd>
dotnet run --project tests/NetOfd.TestConsole -- text   <file.ofd>
dotnet run --project tests/NetOfd.TestConsole -- render <file.ofd> <outDir> [ppm]
dotnet run --project tests/NetOfd.TestConsole -- merge  <out.ofd> <in1.ofd> <in2.ofd> ...
dotnet run --project tests/NetOfd.TestConsole -- sample <out.ofd>
dotnet run --project tests/NetOfd.TestConsole -- sign <in.ofd> <out.ofd> --p12 <cert.p12> --pwd <password> [--seal <seal.der>] [--version 1|4|5]
dotnet run --project tests/NetOfd.TestConsole -- protect <in.ofd> <out.ofd> --user <username> --pwd <password>
dotnet run --project tests/NetOfd.TestConsole -- decrypt <in.ofd> <out.ofd> [--user <username>] --pwd <password>
dotnet run --project tests/NetOfd.TestConsole -- ofda-check <file.ofd>
dotnet run --project tests/NetOfd.TestConsole -- ofda-convert <in.ofd> <out.ofd> [--user <u> --pwd <p>]
```

### Generating an OFD-H electronic medical record

`samples/OfdHWriter` is the command-line wrapper around `NetOfd.Emr.OfdHDocumentGenerator`.
It emits 3 pages of layout content with an embedded CJK font, an ECG attachment, a quality-control
review annotation, a version snapshot, custom tags (machine-readable information + structured
clinical data), an SM2withSM3 national-crypto signature and GM/T 0099 anti-tampering protection:

```bash
dotnet run --project samples/OfdHWriter -- <out.ofd> [--work <work-dir>] [--font <font-path>] [--no-sign] [--no-preview]
```

Exit codes: `0` success with signature self-check passed / `1` invalid arguments /
`2` generation error / `3` signature self-check failed.

> See [OFD-H Electronic Medical Record support](#ofd-h-electronic-medical-record-support-gbt-48666-2026)
> for the full clause mapping, the library API, the test coverage and the known limitations.

## Technical highlights

- **Cross-platform**: Windows, Linux and macOS (.NET 10; CJK fonts must be installed for Linux rendering)
- **Editor architecture**: the ZIP is expanded into an in-memory map and the XML is edited directly; chaining is safe because object IDs increase monotonically
- **Version management**: document-level snapshots (`Doc_0/V_N`) and historic version extraction
- **Chinese national cryptography**: full SM2/SM3/SM4 coverage, with interoperability verified against both BouncyCastle and the system certificate store
- **Robustness**: corrupt input yields semantically explicit exceptions or structured error results (950+ tests)

## Technology mapping

| Java (OFDRW) | .NET (NetOfd) |
|---|---|
| dom4j `Element` | `System.Xml.Linq.XElement` |
| zip4j | `System.IO.Compression.ZipArchive` |
| BouncyCastle SM2/SM3/SM4 | `BouncyCastle.Cryptography` |
| `java.awt.Graphics2D` | SkiaSharp (rendering) / `NetOfd.Graphics2D` interface layer |
| Maven multi-module | .NET Solution + csproj |

## NuGet dependencies

| Package | Purpose |
|---|---|
| `BouncyCastle.Cryptography` | SM2/SM3/SM4 national cryptography algorithms |
| `SkiaSharp` | Cross-platform rendering (Converter/Graphics2D) |

## Release notes

Release notes for every version (one Chinese and one English file per version) live in
[`docs/releases/`](docs/releases/):

| Version | 中文 | English |
|---|---|---|
| **v1.3.3** (current) | [v1.3.3.md](docs/releases/v1.3.3.md) | [v1.3.3.en.md](docs/releases/v1.3.3.en.md) |
| v1.3.2 | [v1.3.2.md](docs/releases/v1.3.2.md) | [v1.3.2.en.md](docs/releases/v1.3.2.en.md) |
| v1.3.1 | [v1.3.1.md](docs/releases/v1.3.1.md) | [v1.3.1.en.md](docs/releases/v1.3.1.en.md) |
| v1.3.0 | [v1.3.0.md](docs/releases/v1.3.0.md) | [v1.3.0.en.md](docs/releases/v1.3.0.en.md) |

Package dependency graph: [PNG](docs/package-dependency-graph.png) · [SVG](docs/package-dependency-graph.svg)

## License

This project uses a **dual license**:

| Scope | License | File |
|---|---|---|
| NetOfd's own code (e.g. `NetOfd.Emr` and other non-ported modules) | MIT License | [LICENSE](LICENSE) |
| Code ported from OFDRW | Apache License 2.0 | [LICENSE-Apache-2.0](LICENSE-Apache-2.0) |

Each NuGet package declares the matching `PackageLicenseExpression`:

| Package | Declared |
|---|---|
| `NetOfd.Emr` | `MIT` |
| `NetOfd.Core` / `Pkg` / `Font` / `Reader` / `Layout` / `Graphics2D` / `Converter` / `Sign` / `Crypto` / `Tool` / `Archive` | `Apache-2.0` |
| `NetOfd` / `NetOfd.Full` (aggregate packages) | `Apache-2.0 OR MIT` |

NetOfd is a .NET port of [OFDRW](https://github.com/ofdrw/ofdrw) (Copyright (c) 2020 Quan guanyu,
Apache-2.0). Under Apache-2.0 §4, the ported portions retain the original license, state the
changes made and keep the upstream copyright notice — see [NOTICE](NOTICE).

> The root `LICENSE` file contains the verbatim MIT License text (so that GitHub can auto-detect
> the license). The scope split above is defined by `NOTICE` and by the table above.

This project is an independent implementation and is not affiliated with or endorsed by the
OFDRW project.
