# CLAUDE.md

## Quy tắc vàng (BẮT BUỘC)

- **Trong lúc owner đi ngủ, Claude tự quyết mọi việc, KHÔNG hỏi owner bất cứ điều gì**
  -- kể cả thêm / xoá / đổi tên file, chọn thư viện, chọn thuật toán, đổi kiến trúc.
  Gặp chỗ phải chọn: tự chọn phương án tốt nhất, ghi lý do vào commit / README, làm tiếp.
- **Owner bắt đầu làm việc lúc 06:00 sáng** (giờ Việt Nam, UTC+7) và sẽ kiểm tra kết quả
  lúc đó. Trước 06:00 mọi thứ phải: build được, có README cập nhật, có tóm tắt những gì
  đã làm / chưa làm / cần owner test tay (ví dụ với máy scan thật).
- App dùng để **bán thương mại**: mọi thư viện / tool / dữ liệu đưa vào phải có license
  cho phép dùng thương mại (MIT, BSD, Apache-2.0...). Tránh GPL / AGPL. Ghi rõ license
  và rủi ro bằng sáng chế trong `THIRD-PARTY-NOTICES.md`.
- Làm trên branch `feature/document-scanner`, commit theo từng mục, **không push**.
- Việc đầu tiên trước mỗi đợt làm: cập nhật README.

## Project

- `Source/ImageCoreService` -- thư viện lõi không UI (codec, PDF/TIFF, scan, xử lý ảnh, OCR).
- `Source/ImageOptimizerTool` -- app WinForms .NET 9, chỉ là giao diện.
- `Source/ImageCore.Shared` -- thuật toán ảnh thuần managed (net9.0, KHÔNG System.Drawing.Common,
  KHÔNG native; chỉ được dùng Rectangle/Point). Dùng chung với app mobile. Phần GDI+ nằm ở
  `ImageCoreService` (`GdiGray`, `BitmapTransforms`).
- Solution: `Source/ImageProcessing.sln`.
- Build kiểm tra khi app đang chạy (file DLL bị khoá): build ra thư mục khác bằng
  `dotnet build Source/ImageOptimizerTool/ImageOptimizerTool.csproj -p:OutDir=<thư mục tạm>/`.
- Code comment bằng tiếng Anh; README / CLAUDE.md bằng tiếng Việt.

## Danh sách việc (đợt 2026-09-25, owner giao trước khi đi ngủ)

0. Cập nhật README trước khi làm.
1. Chuyển trắng đen: bỏ ngưỡng cố định, dùng Otsu hoặc Sauvola (Claude chọn, phải dùng
   thương mại được).
2. Không hạ DPI ảnh nữa, giữ nguyên độ phân giải. File không có DPI thì tự tính từ
   width / height của ảnh.
3. Ảnh màu từ máy scan giữ nguyên 100% (không nén mất dữ liệu 2 lần), chất lượng tốt nhất.
4. JBIG2 / JPEG2000 là một option trong cài đặt. App có form cài đặt + menu Cài đặt riêng,
   đưa tất cả option vào cài đặt. Lưu cấu hình ra file XML, khởi động thì đọc file này;
   lần đầu chạy dùng file mặc định.
5. Option JBIG2 / JPEG2000 không tích -> quay về mặc định CCITT G4 / JPEG.
6. Tính năng app thật:
   - 6.1 Chụp: WIA dự phòng khi không có TWAIN, lưu profile máy scan, nút huỷ, xử lý kẹt
     giấy, tự bỏ trang trắng.
   - 6.2 Xử lý ảnh tài liệu: deskew, tự cắt viền đen, tự xoay đúng chiều, khử đốm,
     trắng đen ngưỡng thích nghi, tự nhận biết trang màu / trắng đen để chọn codec từng trang.
   - 6.3 Quản lý trang: thumbnail, kéo thả đổi thứ tự, xoay, xoá, chèn trang, undo, lưu
     phiên làm việc ra thư mục dự án.
   - 6.4 OCR + PDF tìm kiếm được (Tesseract có tiếng Việt, hoặc Windows.Media.Ocr);
     PdfBuilder thêm lớp chữ ẩn dưới ảnh.
   - 6.5 Đầu ra: PDF/A-2b, metadata, quy tắc đặt tên file, tách tài liệu theo trang trắng
     hoặc barcode.
   - 6.6 Hạ tầng: xử lý + xuất file trên luồng nền có thanh tiến trình; lưu cài đặt, ghi
     log; bộ cài (MSIX/WiX) kèm DLL native x64; rà soát license và bằng sáng chế.

## Báo cáo đợt 2026-09-25 (03:50 → 04:40, branch `feature/document-scanner`)

### Đã xong (build OK, SmokeTests ALL PASS, veraPDF PDF/A-2b compliant)

| # | Việc | Quyết định chính |
|---|---|---|
| 0 | README | Cập nhật trước khi làm, viết lại khi xong. |
| 1 | Trắng đen | **Sauvola** mặc định (chịu được nền ngả màu / sáng không đều), Otsu là tuỳ chọn. Tự cài đặt từ bài báo gốc: không phụ thuộc thư viện ngoài, không có vấn đề license / bằng sáng chế. |
| 2 | DPI | Bỏ hạ 200 DPI. Thiếu DPI (0, hoặc 96/72 trên ảnh cỡ trang scan) thì suy ra từ pixel theo A4 / Letter / Legal. |
| 3 | Nén 2 lần | Scan lưu PNG / TIFF G4 (không mất dữ liệu); mọi chỉnh sửa lưu PNG; JPEG gốc nhúng nguyên byte. |
| 4 | Cài đặt | Menu Cài đặt + form (checkbox JBIG2 / JPEG2000 + PropertyGrid cho mọi option); XML ở `%LocalAppData%\ImageOptimizerTool\settings.xml`, lần đầu lấy từ `Config\settings.default.xml`. |
| 5 | Mặc định codec | Không tích → CCITT G4 / JPEG. Codec chọn **từng trang** theo nhận dạng màu. |
| 6.1 | Chụp | WIA dự phòng (COM late-bound), profile máy scan, huỷ scan (F6 / link Huỷ), báo kẹt giấy / double feed / hết giấy, tự bỏ trang trắng. |
| 6.2 | Xử lý ảnh | Deskew, cắt viền đen, tự xoay (Tesseract OSD), khử đốm, Sauvola, phân loại trắng đen / xám / màu. **Không dùng OpenCvSharp**: thuật toán tự viết đủ tốt và tránh ~60 MB native + ffmpeg LGPL. |
| 6.3 | Quản lý trang | Thumbnail, kéo thả, xoay, xoá, chèn, undo / redo, lưu / mở dự án, tự lưu phiên + khôi phục khi crash. |
| 6.4 | OCR | Tesseract 5 (vie + eng), lớp chữ ẩn GlyphLessFont. Không chọn Windows.Media.Ocr vì cần gói ngôn ngữ tiếng Việt cài trên từng máy. |
| 6.5 | Đầu ra | PDF/A-2b (XMP qua incremental update), metadata, mẫu tên file, tách theo trang trắng / barcode. |
| 6.6 | Hạ tầng | Chạy nền + tiến trình + huỷ, log theo ngày, MSI WiX v5 self-contained x64 (kèm VC++ runtime), THIRD-PARTY-NOTICES.md. Chọn WiX thay vì MSIX vì MSIX bắt buộc chứng chỉ ký. |

### Owner cần kiểm tra tay (không có máy scan / môi trường để tự test)

1. **Scan với máy scan thật**, cả TWAIN lẫn WIA: ADF, duplex, huỷ giữa chừng, kẹt giấy (rút giấy
   khi đang scan), hết giấy. Code TWAIN / WIA chỉ mới được build, chưa chạy với thiết bị.
2. Cài MSI trên máy sạch (chưa có .NET / VC++): mở app, OCR, xuất JBIG2 / JPEG2000.
3. Thử ngưỡng trang trắng (0.03%) và Sauvola k (0.34) trên bản scan thật của công ty.
4. App đang mở từ `bin\Debug` (PID lúc 04:20 là 20648) giữ khoá DLL. Đóng nó trước khi build trong VS.

### Chưa làm / đề xuất đợt sau

- Ký số exe / MSI (cần chứng chỉ code signing).
- Tự build jbig2enc từ source (bản Windows hiện tại do bên thứ ba build), thay PdfiumViewer đã
  ngừng phát triển, cân nhắc NTwain bản ổn định.
- OCR song song nhiều trang (hiện ~2-3 s/trang, chạy tuần tự).
- Form Cài đặt dạng tab "đẹp" thay cho PropertyGrid, nếu cần cho người dùng cuối.
- Unit test chuẩn (xUnit) thay cho SmokeTests dạng console, và chạy trên CI.

## App mobile (đợt 2026-09-25, kế hoạch đã chốt với owner)

- Framework: **.NET MAUI**, làm **Android trước** (owner không có Mac, iOS để sau).
- Bản đầu: nhập ảnh từ thư viện / chụp camera -> tự dò mép giấy hoặc kéo 4 điểm -> cắt phối cảnh
  -> đen trắng (Sauvola) -> **nhiều trang + xuất PDF**. **Chưa có OCR.**
- Dò mép giấy tự viết bằng C# sau interface `IEdgeDetector` (không OpenCV / Emgu vì license).

| Bước | Nội dung | Trạng thái |
|---|---|---|
| 1 | Tách `ImageCore.Shared` khỏi System.Drawing, xUnit | **xong** (SmokeTests ALL PASS, 10 unit test PASS) |
| 2 | Khung app MAUI, quyền camera / thư viện | chưa |
| 3 | Nhập ảnh (thư viện, camera, EXIF, giảm ảnh lớn) | chưa |
| 4 | Tự dò mép giấy | chưa |
| 5 | Kéo 4 điểm + kính lúp | chưa |
| 6 | Cắt phối cảnh (homography) | chưa |
| 7 | Đen trắng (Sauvola / Otsu), giới hạn RAM | chưa |
| 8 | Nhiều trang, sắp xếp, xuất PDF, chia sẻ | chưa |
| 9-10 | Hoàn thiện, test máy thật, phát hành Google Play | chưa |

### Bước 1: ghi chú
- Đã chuyển sang Shared: `GrayImage`, `Binarizer`, `EnumDescriptionConverter`, phần phân tích của
  `DocumentCleanup` (DetectSkew, DetectContentBounds, Despeckle, Percentile).
- Ở lại `ImageCoreService`: `GdiGray` (FromBitmap, ToBitmap8bpp / 1bpp) và `BitmapTransforms`
  (RotateArbitrary, RotateRight, Crop). Call site cũ chỉ đổi tên lớp; namespace vẫn `ImageCoreService`.
- `PageAnalyzer` (IsBlank / Classify) chưa chuyển vì `Classify` còn dùng Bitmap; chuyển khi mobile cần.
- Cần lưu ý cho bước 7: Sauvola dùng 2 integral image kiểu `long` (~16 byte/pixel). Ảnh 8,7 MP tốn
  ~140 MB, phải xử lý theo dải hoặc dùng bản tiết kiệm RAM trước khi chạy trên máy 3 GB.
