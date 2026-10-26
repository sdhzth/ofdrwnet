<!-- Release steps (this comment is hidden on the Release page):
     1) Open https://github.com/sdhzth/ofdrwnet/releases/edit/v1.3.1
     2) Attach the 14 .nupkg files (and optionally the 14 .snupkg) from
        E:\电子病历\netofd\github-upload\artifacts\pack-1.3.1
     3) Copy & paste the content of this file into the description box -->

# NetOfd v1.3.1

## Patch release: fixed-layout pages can now be inserted at a specific page number

This is a **patch release** on top of v1.3.0: it completes one missing capability, hardens one
implementation detail, and adds documentation.

### New: insert a `VirtualPage` at a specific page number

`VirtualPage.SetPageNum(n)` previously had a property but no consumer — virtual pages could only be
appended at the end of the document. This release wires it up, with semantics identical to Java's
`VPageParseEngine.addNewPage`:

- `PageNum` unset → the page is appended at the end and its **actual page number is back-filled**
  (callers can query where the page landed after `Close()`);
- `PageNum` set → the page is inserted at position `n` (1-based); out-of-range values are clamped
  (below 1 → front of the document, beyond the page count → appended at the end);
- physical directories (`Page_N`) are still allocated in append order, while the **document page order
  is determined by the `<Page>` list in `Document.xml`** — exactly as in Java. Since `OFDReader` already
  reads pages in that order, page-number-based access stays correct.

Usage:

```csharp
var vPage = new VirtualPage(PageLayout.A4());
vPage.Add(new Div().SetPosition(Position.Absolute).SetXY(20, 30).SetWidth(170));
vPage.SetPageNum(2);      // insert at page 2
doc.AddVPage(vPage);
doc.Close();
// vPage.PageNum now holds the final page number of this page
```

### Hardening

- `OFDDoc.AddWatermark` used to iterate pages by **physical directory index**, which stamps the watermark
  on the wrong page once pages are inserted → it now follows the page order in `Document.xml`.
- New `PagesDir.ObtainPageDirByName(dirName)`: resolves the physical page directory from the directory name
  parsed out of `BaseLoc` (required when the physical index no longer matches the page order).

### Documentation

- The Chinese and English READMEs gained a "Fixed-layout (板式) pages: VirtualPage" section covering the
  absolute-positioning constraints, the `SetPageNum` insertion semantics, master/backdrop reuse
  (`AddTemplate`), layer assignment, and the pitfall that interleaving streaming `Add` with `AddPage`
  leaves the earlier pages blank.

### Verification

- Unit tests: **959 passing** (5 new ones: insert at a given page / insert at the front together with a
  streamed page / out-of-range clamping / append with page-number back-fill / reader round-trip of the
  resulting page order and content).
- Packaging check: all 14 `.nupkg` + 14 `.snupkg` are versioned `1.3.1`.

### Install

```bash
dotnet add package NetOfd --version 1.3.1        # common
dotnet add package NetOfd.Full --version 1.3.1   # incl. sign / crypto / archive / EMR
```

### License

- Overall: MIT (see `LICENSE`).
- Code ported from OFDRW: Apache-2.0 attribution retained (see `LICENSE-Apache-2.0` / `NOTICE`).
