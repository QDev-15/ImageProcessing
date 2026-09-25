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
| `Source/Bench` | Đo tốc độ import / thumbnail / preview / xuất trên dữ liệu giả: `dotnet run -c Release --project Source/Bench -- pages=40 label=ten`. Kết quả ở `Source/Bench/results/`. |
| `Installer` | Bộ cài MSI (WiX v5): `powershell -ExecutionPolicy Bypass -File Installer\build-installer.ps1`. Kết quả ở `artifacts\installer\`. |
| `THIRD-PARTY-NOTICES.md` | License và bằng sáng chế của mọi thành phần bên thứ ba. |

### ImageCoreService

| File | Nội dung |
|---|---|
| `Imaging/Binarizer.cs` | Trắng đen **Sauvola** (mặc định, thích nghi theo vùng) / **Otsu**. Tự cài đặt, không dùng thư viện ngoài. |
| `Imaging/DocumentCleanup.cs` | Deskew (projection profile), cắt viền đen, khử đốm, xoay. |
| `Imaging/PageAnalyzer.cs` | Phát hiện trang trắng; phân loại trang trắng đen / xám / màu. |
| `Imaging/PageProcessor.cs` | Pipeline cũ ghi lại pixel (bỏ trắng → cắt viền → deskew → xoay). App không còn dùng; giữ cho API / test. |
| `Imaging/GrayImage.cs` | Buffer 8-bit dùng chung cho các thuật toán. |
| `Ocr/OcrEngine.cs` | Tesseract 5: OCR tiếng Việt + Anh theo từng từ; OSD phát hiện chiều trang. |
| `PdfPagePacker.cs` (`PdfBuilder`) | Ghi PDF nhúng nguyên byte đã nén; lớp chữ OCR ẩn (GlyphLessFont); PDF/A-2b + metadata. |
| `Pdf/PdfIncrementalXmp.cs` | Gắn XMP PDF/A-2b bằng incremental update (PDFsharp luôn ghi đè XMP của nó). |
| `DocumentExporter.cs` | Xuất PDF / TIFF: chọn codec **từng trang**, OCR, tiến trình, huỷ. |
| `Export/BatchExporter.cs`, `DocumentSplitter.cs`, `FileNamer.cs` | Tách tài liệu (trang trắng / barcode ZXing), đặt tên file theo mẫu, xuất nhiều file. |
| `TiffPagePacker.cs` | TIFF nhiều trang, các trang có thể dùng codec khác nhau. |
| `TwainScanner.cs`, `WiaScanner.cs`, `ScannerService.cs` | Scan TWAIN (NTwain) + WIA dự phòng; huỷ; báo kẹt giấy / double feed / hết giấy. |
| `Project/PageRecord.cs` | Mô hình trang: `PageRecord` (Id, nguồn, thao tác, trạng thái Pending / Ready / Failed), `PageSource` (file ảnh hoặc **một trang PDF**), `PageOps` (xoay, góc nghiêng, vùng cắt). |
| `Project/ScanProject.cs` | Dự án (project.xml **v2** + pages\ + cache\), undo/redo, tự lưu phiên; tự nâng cấp dự án v1 (giữ `project.xml.v1.bak`). |
| `Project/PageRenderer.cs` | Cầu nối duy nhất từ bản ghi trang → ảnh: nạp nguồn (ảnh hoặc render trang PDF ở DPI cần dùng) rồi áp thao tác. |
| `Project/PageCache.cs` | Ảnh proxy 1600 px + thumbnail lưu trong `cache\` (xoá được, tự tạo lại). |
| `Project/PageIngestor.cs`, `Imaging/PageAnalysis.cs` | Import / scan nền: trang hiện ngay dạng placeholder, phân tích trên proxy (trắng, cắt, nghiêng, chiều) rồi chỉ **ghi thao tác**, không ghi lại pixel. |
| `Pdf/PdfPageRenderer.cs` | Render từng trang PDF theo yêu cầu (pdfium). |
| `Ocr/OcrCache.cs`, `Ocr/BackgroundOcr.cs`, `Ocr/OsdEnginePool.cs` | OCR nền có cache; mỗi luồng một engine OSD. |
| `Project/PageImporter.cs` | Import ảnh / TIFF nhiều trang (PDF không còn render lúc import). |
| `Logging/Perf.cs` | Đo thời gian từng bước (dùng bởi `Source/Bench`). |
| `Settings/AppSettings.cs`, `SettingsStore.cs` | Mọi tuỳ chọn, lưu XML. |
| `Logging/Log.cs` | Log theo ngày, giữ 30 ngày. |
| `JBig2Encoder.cs`, `OpenJpegEncoder.cs`, `G4Encoder.cs`, `JpegEncoderSimple.cs` | Codec (JBIG2 / JPEG2000 gọi tool trong `tools\`). |

## Mô hình dự án (từ đợt 2026-09-25, M0-M4)

- **Trang = nguồn + thao tác.** File gốc (ảnh, PDF, file scan) nằm trong `pages\` và **không bao giờ bị sửa**.
  Xoay, cắt viền, deskew là `PageOps` ghi trong `project.xml`; `PageRenderer` áp chúng khi cần. Xoay vì vậy là tức thì.
- **PDF không render lúc import.** PDF được copy vào dự án một lần; mỗi trang là một nguồn (file + số trang) và chỉ
  render theo yêu cầu: proxy nhỏ để xem, đúng DPI đích (không vượt DPI thật của trang) khi xuất.
- **Import / scan nền.** Trang hiện ngay dạng placeholder (`...`), worker phân tích trên proxy rồi chuyển sang sẵn sàng;
  trang trắng tự bị bỏ, trang lỗi đánh dấu `!`. Kết quả nền không tạo bước undo. Xuất file chờ hết trang đang xử lý.
- **Danh sách trang ảo** (`VirtualMode`): mở dự án hàng trăm trang vẫn tức thì; thumbnail chỉ tạo cho phần đang nhìn thấy
  (`ThumbnailLoader`), lưu ở `cache\thumbs`. Xem trước: proxy hiện gần như ngay, ảnh đủ độ phân giải thay vào giữ nguyên zoom / vị trí.
- **OCR nền** (`BackgroundOcr`): khi rảnh, đọc trước các trang và lưu vào `cache\ocr`; lúc xuất chỉ ghép. Đổi trang / cài đặt
  thì khoá cache đổi, tự đọc lại.
- **Định dạng dự án v2** (`project.xml`, `Version="2"`): mỗi `<Page>` có `Id`, `File`, `PdfPage`, `NativeDpi`, `State`,
  `Rotate`, `Deskew`, `Crop`. Dự án v1 tự nâng cấp khi mở, bản gốc giữ ở `project.xml.v1.bak`; các trang cũ trở thành nguồn
  không có thao tác (ảnh không đổi).
- **Kiểm thử giao diện tự động:** đặt biến môi trường `IMAGEOPTIMIZER_DATA` để chạy app với dữ liệu riêng (không đụng
  `%LocalAppData%`).

## Cách hoạt động chính

- **Độ phân giải:** DPI lấy từ file / driver; file không có DPI (hoặc mang giá trị mặc định
  96/72 trên ảnh quá lớn) thì suy ra từ kích thước pixel theo khổ A4 / Letter / Legal. Trang có DPI
  **cao hơn** DPI của profile scan (mặc định 300) được thu nhỏ về đúng DPI đó **lúc hiển thị đủ
  độ phân giải và lúc xuất** (tuỳ chọn *Giới hạn DPI theo cài đặt scan*, mặc định bật); file gốc
  không bị đổi. DPI bằng hoặc thấp hơn thì giữ nguyên, không bao giờ phóng to. Trang PDF được
  render thẳng ở DPI đó (không vượt DPI thật của ảnh nhúng, tối đa 36 MP / trang).
- **Xuất file không bao giờ hỏng vì codec ngoài:** `jbig2.exe` (32-bit) hoặc `opj_compress.exe`
  lỗi / hết RAM thì trang đó tự chuyển codec (JBIG2 Symbol → JBIG2 generic → CCITT G4;
  JPEG2000 → JPEG), có ghi log. Các trang được mã hoá song song (tối đa 4, mỗi luồng một engine
  Tesseract riêng) và kết quả giữ đúng thứ tự.
- **Chất lượng:** file gốc giữ nguyên (không mất dữ liệu); mọi chỉnh sửa là thao tác trên bản ghi trang. Chỉ có đúng
  1 lần nén mất dữ liệu, lúc xuất file. File JPEG gốc chưa chỉnh sửa (không xoay / cắt / thu nhỏ) được nhúng nguyên
  byte vào PDF.
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
