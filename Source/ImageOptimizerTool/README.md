# Image Optimizer Tool -- visual test bench

Standalone .NET 9 WinForms app, **not referenced by or wired into** `UniversalScanClient` /
`ScanClient-FileOptics`. Open `ImageOptimizerTool.sln` in VS2022 to run/edit `MainForm`
in the designer.

## What it's for

Testing whether **JBIG2** (bitonal) and **OpenJPEG's rate-distortion JPEG2000** (color)
are worth integrating into the main app's export pipeline, which currently uses CCITT
G4 and CoreJ2K respectively -- see `Goal.md` 2026-09-23/24 for the full background
(GdPicture comparison, CoreJ2K's rate-control bug, JBIG2's symbol-dictionary advantage).

## Using the form

1. **Temp folder** (default `C:\imageTool\Temp`, editable): where imported pages are saved.
2. **Import Images...** -- pick one or more image files; each is copied into the temp
   folder and added to the page list.
3. **Import PDF...** -- pick a PDF; it's split into one PNG per page (via PdfiumViewer,
   the same library the main app uses for PDF import), saved into the temp folder, and
   added to the list, in the same order as the source PDF.
4. Click a page in the list to preview it on the right.
5. **Export type** (B&W / Color, default B&W) decides which codec **Export PDF...**
   and **Export TIFF...** use:
   - **PDF, B&W** -> JBIG2, symbol/text-region mode, one shared symbol dictionary
     across every page in the document (glyphs repeated across pages are recognized
     once, not per page).
   - **PDF, Color** -> JPEG2000 via OpenJPEG (`-r 20`, i.e. targets roughly 1/20th of
     the raw size -- edit `MainForm.ExportPdfColor` to change the ratio).
   - **TIFF, B&W** -> CCITT G4 (same codec the main app already uses for TIFF export --
     TIFF export isn't part of what this tool is evaluating).
   - **TIFF, Color** -> LZW (lossless, standard TIFF compression).
6. **Remove Selected** / **Clear All** only affect the in-memory list, not files already
   written to the temp folder.

## Command-line options (bypass the GUI)

- `ImageOptimizerTool.exe --benchmark` -- the original headless comparison bench:
  generates synthetic bitonal/color test pages, runs CCITT G4 vs JBIG2 and OpenJPEG at
  a few compression ratios, writes result PDFs to `Desktop\usc_advanced_codec_compare\`.
- `ImageOptimizerTool.exe --smoketest` -- exercises every non-UI code path the form's
  buttons use (multi-page JBIG2 with shared globals, OpenJPEG, PdfBuilder, TiffExporter,
  and a PDF round-trip through PdfiumViewer) end to end and prints PASS/FAIL. Useful
  after editing any of the encoder/export classes, without clicking through the UI.

## Files

This project is only the WinForms front end:

- `MainForm.cs` / `.Designer.cs` -- the GUI.
- `ScanOptionsForm.cs` / `.Designer.cs` -- scan source/DPI/color/duplex dialog.

All core logic lives in the sibling **`..\ImageCoreService\`** class library
(namespace `ImageCoreService`), referenced via `ProjectReference` so other projects can
reuse it too. Referencing it also brings in its NuGet packages, `x64\pdfium.dll`, and
the vendored `tools\` encoders (copied next to the consuming exe automatically):

- `DocumentExporter.cs` -- multi-page PDF/TIFF export of a list of page files with any
  `ExportCodec` (CCITT G4 / JBIG2 / JPEG / JPEG2000), including the 200dpi cap.
- `JBig2Encoder.cs` / `OpenJpegEncoder.cs` / `G4Encoder.cs` / `JpegEncoderSimple.cs` --
  codec wrappers (the first two shell out to the vendored tools in
  `ImageCoreService\tools\`, see `tools/README.md` for their provenance).
- `PdfPagePacker.cs` (`PdfBuilder` class) -- low-level multi-page PDF writer, embeds
  each codec's bytes verbatim (no re-encoding), same principle as the main app's
  `PdfSharpPdfAArchiver`.
- `TiffPagePacker.cs` / `JpegSofReader.cs` -- multi-page TIFF writer (raw strips).
- `PdfSplitter.cs` -- PDF -> page images via PdfiumViewer.
- `TwainScanner.cs` -- TWAIN scanning via NTwain.
- `ImageUtils.cs` -- shared bitmap helpers (DPI cap, bitonal threshold, DPI resolution).
