<!-- Release steps (this comment is hidden on the Release page):
     1) Open https://github.com/sdhzth/ofdrwnet/releases/edit/v1.3.2
     2) Attach the 14 .nupkg files (and optionally the 14 .snupkg) from
        E:\电子病历\netofd\github-upload\artifacts\pack-1.3.2
     3) Copy & paste the content of this file into the description box -->

# NetOfd v1.3.2

## Patch release: fixes binary resources being dropped when saving in edit mode, plus a release gate

This **patch release** fixes a data-corrupting defect and adds an automatic gate to the publish flow.

### 🔧 Fix: edit-mode saves dropped every binary resource

**Symptom**: after opening an existing OFD document with `OFDDoc.OpenEdit(...)`, adding content and
calling `Save(...)`, **all binary resources were gone** from the saved file — embedded fonts
(`Res/Font_FangSong.ttf`), embedded images (`Res/8.png`), attachments (`Attachs/Attach_1.png`),
signature values (`Signs/Sign_1/SignedValue.dat`) and the same resources inside version snapshots.
XML files (Document.xml / Content.xml / CustomTag_N.xml / Signatures.xml …) were all preserved.

**Root cause**: in `VirtualContainer.HydrateFromPhysical()`, existing files on disk are pulled into the
in-memory cache — XML via `GetFile()` (which **does** populate `FileCache`), but binary files via
`GetRaw()`, which **reads from disk without storing into `RawFiles`**. `WriteToZip()` then only walks
`RawFiles` for binary content, so those resources were read yet never cached, and were silently
discarded when the package was written.

**Fix**: use `AddRaw(name, File.ReadAllBytes(path))` so binary files really land in `RawFiles`.

**Impact**: the defect affected every "edit an existing OFD, then save" path (append page, insert page,
edit sessions…). It was **silent data corruption** — the save succeeded and the file opened fine, but
the resources were gone. After the fix, no entry of the original package is lost.

### 🛡️ New: release gate (automatic CI check)

New `tools/verify-packages.py`, wired into the `Publish NuGet` workflow **after Pack and before the
temporary API key is fetched**, as a `Verify package set (gate)` step. The gate verifies:

1. the expected **14 packages** are present (Core → Font → Pkg → Reader → Sign → Crypto → Archive →
   Layout → Graphics2D → Converter → Emr → Tool → NetOfd → NetOfd.Full);
2. **no version-mismatched** package exists (e.g. a csproj with a hardcoded old `<Version>` producing a
   `1.2.0` file name);
3. the `.nupkg` count is exactly 14;
4. **symbol packages pair one-to-one** with the nupkgs (same id, same version);
5. **belt and braces**: every embedded `.nuspec` is read and its `<id>` / `<version>` are compared with
   the file name.

A failing gate exits with code 1 and aborts — **nothing is pushed**, and the one-time NuGet API key is
not wasted.
> Background: during the v1.3.0 release `NetOfd.Core` hardcoded `<Version>1.2.0</Version>`, producing
> `NetOfd.Core.1.2.0.nupkg` while the publish script looked for `1.3.0` → missing-package failure.
> This gate exists to catch exactly that class of problem.

### 🧪 New test

- `tests/NetOfd.Tests/Emr/OfdHPageInsertTest.cs`: inside an **already generated OFD-H electronic
  medical record document (with a GM/SM2 signature)**, a fixed-layout “attachment sheet” is inserted
  with `VirtualPage.SetPageNum(2)`. It verifies that:
  - the page count goes 3 → 4, the inserted page lands at position 2, and the original three pages keep
    their content and relative order;
  - the OFD-H specific elements are untouched: `DocType=OFD-H`, the machine-readable tag
    (`urn:emr:machine-readable:2026`) and the attachment (ECG image) all read back correctly;
  - **no entry of the original package is missing** (locks in the regression for the binary-resource
    loss fixed above);
  - after the content is modified, the **original GM signature no longer validates** — as expected.

### Verification

- Unit tests: **960 passing** (959 + 1 new).
- Packaging check: all 14 `.nupkg` + 14 `.snupkg` are versioned `1.3.2`; the gate script was self-tested
  on both paths locally (correct artifacts pass; artifacts reproducing the v1.3.0 failure are caught
  precisely and abort the run).

### Install

```bash
dotnet add package NetOfd --version 1.3.2        # common
dotnet add package NetOfd.Full --version 1.3.2   # incl. sign / crypto / archive / EMR
```

### License

- Overall: MIT (see `LICENSE`).
- Code ported from OFDRW: Apache-2.0 attribution retained (see `LICENSE-Apache-2.0` / `NOTICE`).
