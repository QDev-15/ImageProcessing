# Image Optimizer Tool

App WinForms (.NET 9, Windows) để scan / import tài liệu, xử lý ảnh và xuất PDF/TIFF.
Ban đầu là test bench so sánh codec (JBIG2 / OpenJPEG vs CCITT G4 / JPEG), đang được
nâng cấp thành app scan tài liệu thương mại (xem **Lộ trình** bên dưới và `CLAUDE.md`
ở gốc repo).

Mở `Source/ImageProcessing.sln` (hoặc `ImageOptimizerTool.sln`) bằng Visual Studio 2022+.

## Cấu trúc

| Project | Vai trò |
|---|---|
| `ImageCoreService` (class library) | Toàn bộ logic không có UI: codec, ghi PDF/TIFF, tách PDF, scan TWAIN. Dùng lại được cho project khác (service, CLI...). |
| `ImageOptimizerTool` (WinForms exe) | Chỉ là giao diện, gọi vào `ImageCoreService`. |

`ImageOptimizerTool` tham chiếu `ImageCoreService` qua `ProjectReference`, nên NuGet,
`x64\pdfium.dll` và các tool trong `ImageCoreService\tools\` được copy cạnh exe tự động.

### ImageCoreService (trạng thái hiện tại, trước đợt nâng cấp)

- `DocumentExporter.cs` -- xuất PDF/TIFF nhiều trang với `ExportCodec`
  (CCITT G4 / JBIG2 / JPEG / JPEG2000). Hiện vẫn **hạ ảnh xuống 200 DPI** trước khi xuất.
- `JBig2Encoder.cs` / `OpenJpegEncoder.cs` -- gọi `tools\jbig2enc\jbig2.exe` và
  `tools\openjpeg\opj_compress.exe` (nguồn gốc / license: `tools/README.md`).
- `G4Encoder.cs` / `JpegEncoderSimple.cs` -- CCITT G4 và JPEG qua GDI+.
- `PdfPagePacker.cs` (`PdfBuilder`) -- ghi PDF nhiều trang, nhúng nguyên byte đã nén (không nén lại).
- `TiffPagePacker.cs` / `JpegSofReader.cs` -- ghi TIFF nhiều trang bằng raw strip.
- `PdfSplitter.cs` -- tách PDF thành ảnh từng trang (PdfiumViewer).
- `TwainScanner.cs` -- scan TWAIN qua NTwain 4 (beta).
- `ImageUtils.cs` -- helper: hạ DPI, chuyển trắng đen (**ngưỡng cố định 128**), đọc DPI.

### ImageOptimizerTool

- `MainForm.cs` / `.Designer.cs` -- form chính: import ảnh/PDF, scan, xem trước, xuất PDF/TIFF.
- `ScanOptionsForm.cs` / `.Designer.cs` -- chọn máy scan / DPI / màu / duplex.

Lưu ý: app **chưa có** tham số dòng lệnh `--benchmark` / `--smoketest` (README cũ ghi
nhầm) và không có `TiffExporter.cs` (đã thay bằng `TiffPagePacker.cs`).

## Hạn chế đã biết (sẽ sửa trong đợt nâng cấp)

1. Chuyển trắng đen dùng ngưỡng cố định 128 -> kém với giấy ngả màu / mực nhạt.
2. Ảnh bị hạ xuống 200 DPI khi xuất (kể cả ảnh trắng đen) -> giảm độ nét, giảm chất lượng OCR.
3. Ảnh màu từ máy scan bị lưu JPEG rồi nén lại lần nữa khi xuất (mất dữ liệu 2 lần).
4. JBIG2 / JPEG2000 đang là mặc định cứng, chưa có cài đặt; không có file cấu hình.
5. Xuất file chạy trên luồng UI (treo giao diện), danh sách trang chỉ nằm trong bộ nhớ.

## Lộ trình (đợt nâng cấp 2026-09-25)

1. Trắng đen thích nghi (Sauvola / Otsu), tự cài đặt, không phụ thuộc thư viện ngoài.
2. Giữ nguyên độ phân giải gốc; ảnh thiếu DPI thì suy ra từ kích thước pixel.
3. Ảnh màu từ máy scan lưu không mất dữ liệu (PNG), chỉ nén 1 lần khi xuất.
4. Form + menu Cài đặt, lưu cấu hình XML (có file mặc định cho lần chạy đầu).
5. JBIG2 / JPEG2000 là tuỳ chọn; tắt thì dùng CCITT G4 / JPEG.
6. Tính năng app thật: WIA dự phòng, profile máy scan, huỷ scan, xử lý kẹt giấy, bỏ trang
   trắng; deskew / cắt viền / xoay chiều / khử đốm / nhận biết màu; quản lý trang
   (thumbnail, kéo thả, xoay, xoá, chèn, undo, lưu dự án); OCR + PDF tìm kiếm được;
   PDF/A-2b, metadata, đặt tên file, tách tài liệu; chạy nền + tiến trình, log, bộ cài,
   rà soát license.
