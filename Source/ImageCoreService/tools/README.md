# Vendored external encoders

Both tools below are used as external processes (same pattern as `QpdfOptimizer`
in the main app: shell out, best-effort, no native P/Invoke). Neither is
referenced by `UniversalScanClient`/`ScanClient-FileOptics` -- this project is
a standalone test bench, per explicit request, to evaluate them before deciding
whether to integrate.

## tools/openjpeg/ -- OpenJPEG 2.5.4 (JPEG 2000 reference codec)

- Source: official upstream project, https://github.com/uclouvain/openjpeg
- Downloaded from: https://github.com/uclouvain/openjpeg/releases/download/v2.5.4/openjpeg-v2.5.4-windows-x64.zip (x64 -- the x86 build runs out of its 4 GB address space and fails with "opj_encode" on ~85 MP scans)
  (official release asset, built by the OpenJPEG project itself)
- License: BSD-2-Clause (see https://github.com/uclouvain/openjpeg/blob/master/LICENSE)
- Why: OpenJPEG has a real rate-distortion-optimized encoder (`-r`/`-q` actually
  target a size/quality), unlike CoreJ2K (the .NET library the main app
  currently uses), whose bitrate control was found to be a no-op this session
  (see Jp2Encoder.cs's remarks in the main OpenImaging project).
- Files: `opj_compress.exe`, `opj_decompress.exe`, `openjp2.dll`, plus the
  MSVC runtime DLLs the official build links against (`vcruntime140.dll`,
  `concrt140.dll`, `msvcp140*.dll`) so it runs on a machine without the VC++
  redistributable installed separately.

## tools/jbig2enc/ -- jbig2enc 0.29 (JBIG2 encoder)

- Original project: Google's agl/jbig2enc, https://github.com/agl/jbig2enc
  (Linux/source only -- no official Windows build from the original project).
- This Windows binary downloaded from: SourceForge, a long-standing
  third-party Windows packaging of the same source
  (https://sourceforge.net/projects/jbig2enc/files/win/jbig2enc-0.29-win32.zip/download).
  **This is a third-party build, not from the original Google/agl project --
  verify you're comfortable with that provenance before using this in
  anything beyond local testing.**
- License: Apache 2.0 (see COPYING.txt).
- **Patent notice**: ships its own PATENTS.txt -- JBIG2 (the standard itself,
  not this specific implementation) has historical patent disclosures. Same
  caveat would apply to any JBIG2 encoder, including GdPicture's. Read
  PATENTS.txt and make your own call before shipping this in a commercial
  product.
- Why: real symbol-dictionary (`-s`) JBIG2 encoding -- recognizes repeated
  glyphs on a text page and stores each shape once, unlike CCITT G4's
  line-differential coding. Typically 3-10x smaller than G4 on real scanned
  text at comparable legibility.
- Files: `jbig2.exe`, `leptonica-1.76.0.dll` (its only runtime dependency).

## Runtime DLL đi kèm (thêm 2026-09-25)

- `tools/jbig2enc/msvcr120.dll` (x86, VC++ 2013): `leptonica-1.76.0.dll` phụ thuộc file này.
  Nếu thiếu, JBIG2 lỗi trên máy chưa cài VC++ 2013 Redistributable. Được phép phân phối
  kèm app (Visual Studio Redistributable Code).
- VC++ 2015-2022 x64 (`vcruntime140.dll`, `vcruntime140_1.dll`, `msvcp140.dll`) cho
  Tesseract nằm trong `ImageCoreService/native/vcruntime-x64/` và được copy cạnh exe.
- Chi tiết license / bằng sáng chế: `THIRD-PARTY-NOTICES.md` ở gốc repo.
