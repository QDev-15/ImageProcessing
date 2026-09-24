# Image Optimizer Tool

App WinForms (.NET 9, Windows x64) để scan / import tài liệu, tự làm sạch ảnh, quản lý trang,
OCR và xuất **PDF/A-2b tìm kiếm được** hoặc TIFF nhiều trang. Toàn bộ logic nằm trong thư viện
`ImageCoreService` (không có UI, dùng lại được).

Mở `Source/ImageProcessing.sln` bằng Visual Studio 2022+ (hoặc `dotnet build`).

## Cấu trúc

| Thư mục | Vai trò |
|---|---|
| `Source/ImageCoreService` | Thư viện lõi: codec, PDF/TIFF, xử lý ảnh, OCR, scan, cài đặt, dự án. |
| `Source/ImageOptimizerTool` | App WinForms (chỉ là giao diện). |
| `Source/SmokeTests` | Kiểm tra end-to-end không cần UI: `dotnet run --project Source/SmokeTests` (exit 0 = ALL PASS). |
| `Installer` | Bộ cài MSI (WiX v5): `powershell -ExecutionPolicy Bypass -File Installer\build-installer.ps1`. Kết quả ở `artifacts\installer\`. |
| `THIRD-PARTY-NOTICES.md` | License và bằng sáng chế của mọi thành phần bên thứ ba. |

### ImageCoreService

| File | Nội dung |
|---|---|
| `Imaging/Binarizer.cs` | Trắng đen **Sauvola** (mặc định, thích nghi theo vùng) / **Otsu**. Tự cài đặt, không dùng thư viện ngoài. |
| `Imaging/DocumentCleanup.cs` | Deskew (projection profile), cắt viền đen, khử đốm, xoay. |
| `Imaging/PageAnalyzer.cs` | Phát hiện trang trắng; phân loại trang trắng đen / xám / màu. |
| `Imaging/PageProcessor.cs` | Pipeline tự xử lý mỗi trang khi scan / import (bỏ trắng → cắt viền → deskew → xoay chiều). |
| `Imaging/GrayImage.cs` | Buffer 8-bit dùng chung cho các thuật toán. |
| `Ocr/OcrEngine.cs` | Tesseract 5: OCR tiếng Việt + Anh theo từng từ; OSD phát hiện chiều trang. |
| `PdfPagePacker.cs` (`PdfBuilder`) | Ghi PDF nhúng nguyên byte đã nén; lớp chữ OCR ẩn (GlyphLessFont); PDF/A-2b + metadata. |
| `Pdf/PdfIncrementalXmp.cs` | Gắn XMP PDF/A-2b bằng incremental update (PDFsharp luôn ghi đè XMP của nó). |
| `DocumentExporter.cs` | Xuất PDF / TIFF: chọn codec **từng trang**, OCR, tiến trình, huỷ. |
| `Export/BatchExporter.cs`, `DocumentSplitter.cs`, `FileNamer.cs` | Tách tài liệu (trang trắng / barcode ZXing), đặt tên file theo mẫu, xuất nhiều file. |
| `TiffPagePacker.cs` | TIFF nhiều trang, các trang có thể dùng codec khác nhau. |
| `TwainScanner.cs`, `WiaScanner.cs`, `ScannerService.cs` | Scan TWAIN (NTwain) + WIA dự phòng; huỷ; báo kẹt giấy / double feed / hết giấy. |
| `Project/ScanProject.cs`, `PageImporter.cs` | Dự án (project.xml + pages\), undo/redo, tự lưu phiên; import ảnh / TIFF nhiều trang / PDF. |
| `Settings/AppSettings.cs`, `SettingsStore.cs` | Mọi tuỳ chọn, lưu XML. |
| `Logging/Log.cs` | Log theo ngày, giữ 30 ngày. |
| `JBig2Encoder.cs`, `OpenJpegEncoder.cs`, `G4Encoder.cs`, `JpegEncoderSimple.cs` | Codec (JBIG2 / JPEG2000 gọi tool trong `tools\`). |

## Cách hoạt động chính

- **Độ phân giải:** không bao giờ resample. DPI lấy từ file / driver; file không có DPI (hoặc
  mang giá trị mặc định 96/72 trên ảnh quá lớn) thì suy ra từ kích thước pixel theo khổ
  A4 / Letter / Legal.
- **Chất lượng:** trang scan được lưu không mất dữ liệu (1-bit → TIFF G4, xám / màu → PNG). Mọi
  thao tác chỉnh sửa cũng ghi PNG. Chỉ có đúng 1 lần nén mất dữ liệu, lúc xuất file. File JPEG gốc
  chưa chỉnh sửa được nhúng nguyên byte vào PDF.
- **Codec từng trang** (chế độ màu *Tự động*):
  - trang trắng đen → **CCITT G4**, hoặc **JBIG2** nếu tích tuỳ chọn;
  - trang xám / màu → **JPEG**, hoặc **JPEG2000** nếu tích tuỳ chọn.
- **Cài đặt:** menu *Cài đặt → Cài đặt... (F9)*. File người dùng:
  `%LocalAppData%\ImageOptimizerTool\settings.xml`. Lần chạy đầu, file này được tạo từ
  `Config\settings.default.xml` (có sẵn cạnh exe). File hỏng thì được sao lưu (`.bad-*`) rồi
  dùng mặc định.
- **Log:** `%LocalAppData%\ImageOptimizerTool\logs\app-yyyyMMdd.log` (menu *Cài đặt → Mở thư mục log*).
- **Phiên làm việc:** các trang chưa lưu nằm trong `...\ImageOptimizerTool\Work\session_*` và
  được tự lưu sau mỗi thay đổi. Nếu app bị tắt đột ngột, lần mở sau app sẽ hỏi có khôi phục không.
- **Dòng lệnh:** `ImageOptimizerTool.exe a.pdf b.jpg ...` mở app và import luôn các file đó;
  `--write-default-settings PATH` ghi file cấu hình mặc định.

## Kiểm tra đã chạy (2026-09-25)

- `SmokeTests`: 36 kiểm tra, **ALL PASS**. Bao gồm:
  - Sauvola trên nền sáng không đều;
  - deskew ±3°, cắt viền;
  - trang trắng và phân loại màu;
  - OCR tiếng Việt, OSD 90° / 180°;
  - PDF G4 / JPEG / JBIG2 / JPEG2000 mở được bằng pdfium, lớp chữ tìm kiếm được;
  - JPEG gốc được nhúng nguyên byte; TIFF nhiều trang giữ đúng DPI;
  - tách tài liệu bằng barcode / trang trắng, mẫu tên file;
  - undo / lưu / mở dự án; cài đặt XML.
- **veraPDF 1.30.2** (profile PDF/A-2b): PDF có OCR, PDF JBIG2 + JPEG2000 và PDF có metadata tiếng
  Việt đều **compliant**.
- Chạy app thật: import ảnh lệch + có viền đen, trang trắng, JPEG và PDF bằng dòng lệnh → trang
  trắng bị bỏ, trang lệch được cắt viền và chỉnh nghiêng, thumbnail hiển thị đúng. Form Cài đặt và
  form Scan hiển thị đúng.
- Bộ cài MSI build được (57 MB, self-contained). Đã giải nén MSI và kiểm tra đủ file (runtime .NET,
  DLL native, VC++ runtime, tessdata, tools).
