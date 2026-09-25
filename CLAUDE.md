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
- Không tự ý commit code.
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

> **Bản tổng hợp trạng thái, việc chưa kiểm chứng và việc còn lại: [MOBILE-STATUS.md](MOBILE-STATUS.md).** Đọc file đó trước; các mục
> "Bước N: ghi chú" bên dưới là chi tiết kỹ thuật từng bước.

- App: `Source/DocScanner` (solution mobile `Source/DocScanner.slnx`; owner cũng đã thêm nó vào
  `ImageProcessing.sln`, nên build sln đó cần workload MAUI Android). Package `btk.docscanner`, tên hiển thị "Doc Scanner"
  (không dùng tên IMIP, app không liên quan công ty). Máy test thật: Samsung Galaxy Note 10+.
- Framework: **.NET MAUI** (net10.0-android), làm **Android trước** (owner không có Mac, iOS để sau).
- Bản đầu: nhập ảnh từ thư viện / chụp camera -> tự dò mép giấy hoặc kéo 4 điểm -> cắt phối cảnh
  -> đen trắng (Sauvola) -> **nhiều trang + xuất PDF**. **Chưa có OCR.**
- Dò mép giấy tự viết bằng C# sau interface `IEdgeDetector` (không OpenCV / Emgu vì license).

| Bước | Nội dung | Trạng thái |
|---|---|---|
| 1 | Tách `ImageCore.Shared` khỏi System.Drawing, xUnit | **xong** (SmokeTests ALL PASS, 10 unit test PASS) |
| 2 | Khung app MAUI, quyền camera / thư viện | **xong**, chạy được trên Note 10+ |
| 3 | Nhập ảnh (thư viện, camera, EXIF, giảm ảnh lớn) | **xong**, thử trên Note 10+ (thư viện OK; camera chưa thử, xem ghi chú Bước 3) |
| 4 | Tự dò mép giấy | **xong**; test mô phỏng PASS, mới thử 2 ảnh thật (1 đúng, 1 sai: tờ bị khung cắt mép) -> còn phải chỉnh cho ảnh thật |
| 5 | Kéo 4 điểm + kính lúp | **xong, thử trên Note 10+** (riêng nút Xoay 90° mới có unit test, chưa bấm thử trên máy) |
| 6 | Cắt phối cảnh (homography), khổ A4, kéo điểm ra ngoài ảnh | **xong**; ảnh thật nắn thẳng đúng trên Note 10+; A4 và kéo ra ngoài ảnh chưa được owner xác nhận; chưa thử 48 MP thật |
| 7 | Đen trắng (Sauvola / Otsu), giới hạn RAM | **xong** (unit test + PC); chưa thử trên máy |
| 8 | Nhiều trang, sắp xếp, xuất PDF, chia sẻ | **xong** (unit test + PDFium); chưa thử trên máy |
| 9-10 | Hoàn thiện, test máy thật, phát hành Google Play | chưa |

### Bước 1: ghi chú
- Đã chuyển sang Shared: `GrayImage`, `Binarizer`, `EnumDescriptionConverter`, phần phân tích của
  `DocumentCleanup` (DetectSkew, DetectContentBounds, Despeckle, Percentile).
- Ở lại `ImageCoreService`: `GdiGray` (FromBitmap, ToBitmap8bpp / 1bpp) và `BitmapTransforms`
  (RotateArbitrary, RotateRight, Crop). Call site cũ chỉ đổi tên lớp; namespace vẫn `ImageCoreService`.
- `PageAnalyzer` (IsBlank / Classify) chưa chuyển vì `Classify` còn dùng Bitmap; chuyển khi mobile cần.
- Cần lưu ý cho bước 7: Sauvola dùng 2 integral image kiểu `long` (~16 byte/pixel). Ảnh 8,7 MP tốn
  ~140 MB, phải xử lý theo dải hoặc dùng bản tiết kiệm RAM trước khi chạy trên máy 3 GB.

### Bước 2: ghi chú
- `DocScanner`: chỉ target `net10.0-android`, min SDK 26 (Android 8). Đã xoá Platforms iOS / MacCatalyst /
  Windows của template; khi làm iOS thì tạo lại từ `dotnet new maui`.
- MVVM (CommunityToolkit.Mvvm, MIT) + DI trong `MauiProgram`; Shell routes `viewer` / `crop` / `export` đang là
  trang giữ chỗ (`Views/PlaceholderPages.cs`), mỗi bước sau thay bằng trang thật.
- Quyền: chỉ `CAMERA` (+ `queries` IMAGE_CAPTURE cho Android 11+). Nhập từ thư viện dùng system photo picker
  nên không cần quyền lưu trữ. Cố ý KHÔNG khai báo INTERNET (ảnh không rời máy; bản Debug tự có INTERNET cho debugger).
- Màn hình Trang chủ hiển thị dòng "Lõi xử lý ảnh: Sauvola k = 0.34" để xác nhận ImageCore.Shared chạy được trên máy.
- Chạy trên máy: bật Tuỳ chọn nhà phát triển + Gỡ lỗi USB, cắm cáp, `adb devices` phải thấy máy, rồi
  `dotnet build Source/DocScanner/DocScanner.csproj -f net10.0-android -t:Run`
  (adb: `C:/Program Files (x86)/Android/android-sdk/platform-tools`).
- Cần owner kiểm tra tay: mở app trên Note 10+, bấm "Chụp ảnh" (hộp thoại xin quyền camera hiện đúng, từ chối 2 lần
  thì có nút mở Cài đặt), bấm 3 nút chuyển màn hình, thấy dòng "Lõi xử lý ảnh".

### Bước 3: ghi chú
- Cấu trúc: `Source/DocScanner.Core` (net9.0, thuần .NET, test được trên PC: `DocumentRecord` / `PageRecord`,
  `DocumentStore`, `ImportService`, `ImageGeometry`) + `DocScanner` (Android: `AndroidImageService`,
  `ImportCoordinator`, ViewModel, trang Home / Document). Test: `dotnet test Source/DocScanner.Core.Tests` (24 test).
- (Đã thay đổi ở mục "Nhập nhanh" bên dưới: nhập ảnh chỉ chép bản gốc, phần còn lại chạy nền.)
- Lưu trữ: `files/documents/{docId}/doc.json` + mỗi trang một thư mục gồm `original.<ext>` (copy nguyên byte,
  không đụng EXIF), `proxy.jpg` (cạnh dài 1600, đã xoay đúng chiều) và `thumb.jpg` (320). `doc.json` ghi nguyên tử
  (file tạm + thay thế) sau MỖI trang nhập, nên crash / huỷ giữa chừng vẫn giữ các trang đã xong.
- Giải mã ảnh lớn: `BitmapFactory.inSampleSize` (co luỹ thừa 2 ngay trong decoder), rồi resize + xoay theo EXIF
  trên bitmap nhỏ; gặp `OutOfMemoryError` thì tự tăng sample và thử lại (tối đa 3 lần). PNG trong suốt được đặt
  lên nền trắng trước khi ghi JPEG. Bước 6 (cắt phối cảnh trên ảnh gốc) phải áp dụng đúng `ExifOrientation` đã
  lưu trong `PageRecord`.
- Trình chọn ảnh trên Samsung Android 12 là giao diện chọn tệp cũ: **bấm giữ** một ảnh rồi chạm thêm ảnh để chọn
  nhiều, sau đó bấm "Chọn" (Android 13+ dùng photo picker chuẩn). Nút Huỷ trên lớp phủ dừng ngay, giữ các trang đã nhập.
- Đã kiểm chứng trên Note 10+ (SM-N975F, Android 12): nhập 12 trang gồm WebP, JPEG 12 MP, JPEG 48 MP (8000x6000),
  EXIF 6 / 3 / 8 (cả ba dựng thẳng), PNG trong suốt; bản gốc lưu đúng byte; không crash; PSS ~314 MB khi đang xử lý.
- CHƯA thử trên máy: nút "Chụp ảnh" / "Chụp thêm" (camera hệ thống), xoá tài liệu bằng vuốt, xoá trang bằng nút x.
  Đường xử lý ảnh hỏng (bỏ qua ảnh lỗi, báo cuối đợt) mới được kiểm bằng unit test, vì trình chọn của hệ thống
  không trả về file giả.
- Nợ nhỏ: `doc.json` ghi tiếng Việt dạng escape unicode (hợp lệ, chỉ khó đọc); chưa đổi được tên tài liệu (Bước 8).

### Bước 4: ghi chú (dò mép giấy)
- Code: `ImageCore.Shared/DocumentEdgeDetector.cs` (+ `Geometry.cs` Quad, `Homography.cs`, `RgbImage.cs`,
  `IEdgeDetector.cs`). Thuần C#, không OpenCV. `DocScanner.Core/CropDetectionService.cs` chạy bộ dò trên proxy
  (cạnh dài 480) và ghi `CropQuad` (8 số 0..1, thứ tự TL TR BR BL, toạ độ ảnh đã xoay đúng chiều),
  `CropConfidence`, `CropDetected` vào `PageRecord`. `ImportService` gọi nó sau mỗi trang (lỗi dò không làm hỏng
  việc nhập). Trang Crop (`CropPage` + `QuadOverlay`) hiện khung màu xanh (dò được) hoặc cam (chỉ là toàn khung)
  kèm độ tin cậy và thời gian; chạm vào một trang trong tài liệu để mở. Trang cũ chưa có khung thì dò lúc mở.
- Thuật toán: Canny trên 3 kênh R/G/B (lấy kênh mạnh nhất, để giấy trắng trên bàn sáng vẫn thấy qua màu) ->
  Hough, mỗi điểm cạnh bỏ phiếu theo độ lớn gradient (cạnh giấy thật áp đảo vân bàn) -> tinh chỉnh từng đường bằng
  bình phương tối thiểu -> ghép cặp đường gần song song thành tứ giác lồi -> chấm điểm.
- Điểm tứ giác = phần trăm mỗi cạnh có điểm cạnh đúng hướng x tương phản trong/ngoài cạnh (cửa sổ 9x9, cách 7 px:
  loại dòng chữ giữa trang) x "bên ngoài không phải màu giấy" (loại lề trắng quanh khối chữ) x diện tích x độ vuông
  góc. Cạnh nằm trên khung ảnh (giấy tràn ra ngoài) được chấm bằng màu dải sát mép khung so với vùng trong tờ / màu bàn.
  Nhiều khung gần giống nhau thì chỉ đổi sang khung có mép mạnh gấp >= 2 lần (giấy vs vân bàn).
- Không dò được (điểm < 0,42) thì `Detected=false` và trả toàn khung lùi 3%.
- Kết quả đo trên ảnh giả lập (test `EdgeDetectorTests`, 19 cảnh + cảnh không có giấy): nền tối / sáng / xanh (chỉ khác
  màu), xoay 25 / 60 độ, phối cảnh gắt (trên hẹp 35%), tờ nhỏ (4% khung), tờ bị khung cắt, bóng đổ mềm, nhiễu mạnh, vân gỗ
  song song mép, tương phản 20 mức: đều IoU >= 0,92 (đa số >= 0,99). Cảnh chỉ có bàn + vật lạ: không báo có giấy.
  Tốc độ: khoảng 0,2-0,7 s cho ảnh 1600x1200 trên PC (chưa đo trên máy, dự kiến gấp 3-5 lần).
- CHƯA có ảnh chụp thật: mọi số liệu trên là ảnh giả lập tự sinh. Owner nên chụp 20-30 tờ thật (bàn tối / sáng /
  vân gỗ, nghiêng, tờ bị cắt, nhiều tờ chồng, có bàn tay giữ giấy) rồi xem khung trên trang Crop. Ca có thể còn yếu: giấy
  trắng trên bàn trắng, nhiều tờ chồng lên nhau, bàn tay che mép. Khi thấy sai, gửi ảnh + khung để chỉnh ngưỡng.
- Đã có sẵn `Homography` (giải 4 điểm, nghịch đảo) cho Bước 6 (cắt phối cảnh).

### Nhập nhanh (sau phản hồi của owner: spinner nhập ảnh quá lâu)
- Nhập ảnh giờ chỉ **chép bản gốc** và ghi trang ở trạng thái `Pending` (`ImportService`); UI vào ngay tài liệu, mỗi trang là
  một ô đang xoay. Phần nặng chạy nền trong `PageIngestQueue` (2 worker, ưu tiên nghiêm ngặt): (1) **thumbnail** cho mọi
  trang của lô (giải mã thu nhỏ mạnh, chục ms) -> `Preview`; (2) ảnh **proxy** 1600 px -> `Ready` (mở được); (3) **dò mép**.
  Ô trang hiện thumbnail ngay khi có; nhãn "Đang xử lý n ảnh..." hiện ở đầu tài liệu.
- Trạng thái lưu trong doc.json (`PageState`: Pending / Preview / Ready / Failed; tài liệu cũ không có trường này = Ready).
  Tắt app giữa chừng: lần mở sau `ResumePending()` làm tiếp (trang Pending làm lại, Preview làm nốt proxy, Ready chưa có khung thì dò).
  Ảnh không đọc được thành trang `Failed` (ô đỏ, chạm để xem lỗi, xoá được), không còn hộp thoại lỗi cuối đợt.
- `DocumentStore` giờ giữ **một bản DocumentRecord duy nhất** trong bộ nhớ (UI, nhập và nền dùng chung); mọi thay đổi qua
  `Update(docId, ...)` (khoá + lưu), đọc danh sách trang qua `Pages(docId)` (bản chụp). Không còn `Load` / `Save` công khai.
- Đo trên Note 10+ (26 ảnh: 20 ảnh 12 MP + 1 ảnh 48 MP + 5 ảnh chụp giả lập): mọi trang có thumbnail và mở được sau vài giây;
  dò mép chạy nền ~1,7 s/trang/worker (khoảng 25 s cho 43 trang) nên là phần chậm nhất, nhưng không chặn giao diện.
- Lỗi đã sửa: spinner nhập ảnh kẹt mãi. `Progress<T>` gửi callback muộn qua UI thread; sau khi nhập nhanh, callback cuối chạy
  SAU khi `IsBusy = false` và bật lại lớp phủ. Đã thêm cờ `_importing` chặn callback muộn (`ImportViewModelBase`).
- Ảnh thật đầu tiên của owner (tờ giấy cầm tay trên bàn gỗ, nền là màn hình + bàn phím): dò được khung, độ tin cậy 63%,
  nhưng cạnh phải và góc trên phải nằm hơi lệch vào trong tờ giấy (mất ~8% bề ngang). Ghi lại để chỉnh ở đợt sau.

### Bước 5: ghi chú (trình chỉnh 4 điểm)
- `Views/QuadEditor.cs` (GraphicsView tự vẽ ảnh + khung + tay cầm + kính lúp, dùng chung một hệ toạ độ) thay cho `QuadOverlay`.
  Kéo góc (chấm lớn); kéo ô vuông giữa cạnh = đẩy cả cạnh theo pháp tuyến; kéo trong khung = dời cả khung. Kính lúp 3x hiện phía
  trên ngón tay (xuống dưới khi gần mép trên), có chữ thập + khung vẽ bên trong. Khung không bao giờ thành lõm / tự cắt / quá
  nhỏ: bước kéo nào làm vậy thì không áp dụng (tay cầm dừng ở giới hạn). Màu: xanh lá = tự dò, cam = chỉ là toàn khung,
  xanh dương = đã chỉnh tay. Ngoài khung được làm tối. Lề 34 dp quanh ảnh để góc sát mép không dính cử chỉ "quay lại".
- Nút: **Tự động** (dò lại, ghi đè cả khung chỉnh tay), **Toàn ảnh**, **Xoay 90°**, **Xong**. Chỉ dùng được khi trang đã `Ready`.
- Lưu: thả tay là `PageEditService.SetCrop` (ghi doc.json ngay, `CropManual = true`). Dò tự động về sau KHÔNG ghi đè khung chỉnh
  tay (trừ nút Tự động); cũng bỏ kết quả dò nếu trang bị xoay trong lúc đang dò.
- Xoay: `PageRecord.UserRotation` (0/90/180/270 thuận chiều kim đồng hồ) cộng với EXIF của file thành `EffectiveOrientation`
  (`ImageGeometry.ComposeRotation`, dùng ma trận D4 cho 8 mã EXIF). File gốc không đổi. Xoay = khung xoay theo, trang về `Pending`,
  pipeline nền tạo lại thumbnail + proxy; khung cũ giữ nguyên, không dò lại. **Bước 6 phải dùng `EffectiveOrientation`.**
- Đo trên Note 10+ khi kéo góc 3 giây: 168 khung hình, trung vị 8 ms, p99 15 ms, 0,6% khung giật (gfxinfo).
- Đã thấy trên máy: khung chỉnh tay được lưu và khôi phục sau khi khởi động lại app; kính lúp đúng vị trí, kể cả khi chạm sát mép
  phải màn hình.
- Test: `DocScanner.Core.Tests` 56 test (xoay, ghép EXIF, xoay khung, ghi đè khung tay, đua giữa xoay và dò...).
- Lưu ý khi thử tự động bằng adb: nếu owner đang cầm máy hoặc chuyển sang app khác thì KHÔNG bấm giả lập, dễ chạm nhầm vào
  app khác (đã xảy ra một lần: chạm vào Thư viện của owner; không có thao tác nào được thực hiện).

### Bước 6: ghi chú (cắt phối cảnh)
- Thuật toán: `ImageCore.Shared/PerspectiveWarp.cs` (homography 4 điểm -> chữ nhật, nội suy song tuyến; khi ảnh nguồn dày hơn
  đầu ra > 1,25 lần thì lấy 2x2 điểm con rồi lấy trung bình để chữ nhỏ không bị răng cưa). Quy ước toạ độ "cạnh điểm ảnh"
  (điểm chuẩn hoá x rộng ảnh rơi đúng mép ảnh). Sai số trung bình 1,5-3 mức xám so với bản gốc trên ảnh mô phỏng có chữ.
- Luôn cắt từ **ảnh gốc**, không từ proxy: `CropRenderService` đổi khung từ toạ độ ảnh đã dựng thẳng về toạ độ file gốc bằng
  `ImageGeometry.UprightToStored` (ma trận trực giao của 8 mã EXIF, dùng `EffectiveOrientation` = EXIF + xoay của người dùng),
  chỉ giải mã vùng chứa tờ giấy (`BitmapRegionDecoder`, có dự phòng giải mã cả ảnh thu nhỏ cho định dạng không hỗ trợ).
- `CropPlanner`: đầu ra tối đa ~A4 300 DPI (cạnh dài 3508, 8,7 MP). Giải mã thu nhỏ luỹ thừa 2 lớn nhất mà vẫn đủ độ phân giải đầu ra
  (48 MP thường giải mã ở 1/2), và nếu vùng giải mã vượt 16 MP thì thu nhỏ thêm và đầu ra nhỏ theo. `android:largeHeap` bật.
- Kết quả lưu `cropped_{revision}.jpg` (JPEG q94) + `cropped_thumb_{revision}.jpg` (512 px) trong thư mục trang; tên theo revision để
  không dính cache hình cũ, file cũ xoá sau khi bản mới đã ghi vào doc.json. `PageRecord.NeedsRender` = chưa có bản dựng, hoặc khung /
  góc xoay đã đổi kể từ bản dựng gần nhất (lưu `CroppedQuad`, `CroppedRotation`).
- Luồng: trang Crop bấm **Xong** -> lưu khung (nếu chưa có) -> `ResultPage`; trang này xếp hàng `PageIngestQueue.EnqueueRender` (giai đoạn Render,
  ưu tiên cao nhất vì người dùng đang chờ) và tự cập nhật khi xong. Nút **Chỉnh lại** (về trang chỉnh) / **Xong** (về danh sách trang).
  Ô trang trong tài liệu hiện thumbnail đã cắt khi có bản dựng còn mới.
- Test: `DocScanner.Core.Tests` 86 test, gồm ánh xạ đủ 8 mã EXIF và 12 tổ hợp EXIF + xoay đầu-cuối (sai số < 1,5 mức xám; ánh xạ sai hướng
  sẽ lệch 40+), kế hoạch giải mã cho ảnh 12 / 48 MP / rất lớn, giai đoạn Render (bỏ qua khi không đổi, cũ bị thay, lỗi được ghi lại).
  `ImageCore.Shared.Tests` 44 test (thêm 8 test warp / resize).
- CHƯA làm: phóng to / kéo (pinch) trên trang kết quả; chưa thử ảnh gốc 48 MP thật trên máy; chưa đo thời gian dựng ảnh trên máy.
- Bước 7 (đen trắng) nên chạy trên chính `RgbImage` sau warp (gọi lại `CropRenderService` nhưng trả về ảnh trong RAM) để không nén JPEG hai lần,
  và nhớ giới hạn RAM của Sauvola (integral image `long`) cho ảnh 8,7 MP.


### Cập nhật sau Bước 6: kéo điểm ra ngoài ảnh
- Owner có tờ giấy bị khung ảnh cắt (trang 2 của tài liệu 15:10): góc thật của tờ giấy nằm ngoài ảnh. `QuadEditor` giờ cho kéo các
  điểm ra **vùng đen quanh ảnh**, tối đa `OutsideFraction = 0.2` (20% bề rộng / bề cao ảnh mỗi phía). Vùng đó được vẽ sáng hơn nền một chút,
  ảnh co lại còn ~71% để chừa chỗ. Toạ độ khung trong doc.json có thể nằm ngoài 0..1 (âm hoặc > 1); xoay / ánh xạ EXIF vẫn đúng.
- `PerspectiveWarp`: phần khung nằm ngoài ảnh gốc được điền **trắng** (giấy), hình học của khung vẫn nguyên; chỉ phần chưa từng chụp là trắng.
  Test: `Parts_of_the_outline_outside_the_photo...` (Shared) và `A_sheet_cut_by_the_frame...` (Core).
- Khung tự dò ở trang này sai (hình diều, tin cậy 50%): việc chỉnh bộ dò cho tờ giấy bị cắt mép vẫn còn tồn đọng.

### Cập nhật sau Bước 6: trang cắt ra khổ A4
- Mặc định trang sau khi cắt là **khổ A4** (tỉ lệ 1 : sqrt(2)), dọc hoặc ngang theo khung (rộng hơn cao = ngang). Cạnh dài giữ độ phân giải
  của khung (không thấp hơn cạnh dài của khung, cũng không thấp hơn cạnh ngắn x 1,414), rồi bị chặn ở 3508 px (A4 300 DPI, 3508 x 2480 ~ 8,7 MP).
  Code: `PerspectiveWarp.A4Size` (Shared), `CropAspect { A4, Free }` trong `CropPlanner`.
- Trang kết quả có nút **"Khổ giấy: A4 / theo khung"** đổi từng trang (biên lai, thẻ, khổ giấy khác): `PageRecord.FreeAspect`, và
  `CroppedFreeAspect` lưu khổ của bản dựng gần nhất để bản dựng cũ bị coi là cũ (`NeedsRender`). Đổi là tự dựng lại nền.
- Lưu ý: khổ A4 kéo giãn ảnh cho khớp tỉ lệ (khung gần vuông sẽ bị giãn chiều rộng); nếu tờ giấy không phải A4 thì chọn "theo khung".
- Test: `DocScanner.Core.Tests` 91 test (thêm khổ A4 dọc / ngang / 300 DPI / ảnh 48 MP, dựng A4 đầu-cuối và chuyển qua "theo khung"),
  `ImageCore.Shared.Tests` 46 test.

### Bước 7: ghi chú (đen trắng, đợt 2026-09-26)
- `PageRecord` có thêm `ColorMode` (Color / Gray / BlackWhite), `BwDarkness` (0..100, 50 = Sauvola k 0,34), `CleanBackground`; bản dựng ghi
  lại `Cropped*` tương ứng + `CroppedExtension` (".jpg" / ".png"). `NeedsRender` tính cả kiểu trang, nhưng chỉ những giá trị có ảnh hưởng
  (`PageRecord.SameLook`: độ đậm chỉ tính khi đen trắng, làm sạch nền chỉ tính khi không phải màu). doc.json cũ đọc ra là trang màu, không phải dựng lại.
- `CropRenderService`: warp -> `DocumentFilter.Apply` ngay trên `RgbImage` trong RAM (không đọc lại JPEG) -> lưu: màu / xám = JPEG q94
  (xám ghi dạng RGB vì Android không ghi được JPEG 1 kênh), đen trắng = **PNG 1-bit** do `PngWriter` tự viết (không cần codec nền tảng).
- `Binarizer.Sauvola` viết lại: tổng trượt theo cột + theo hàng, chạy song song theo dải; bộ nhớ O(rộng); kết quả **giống hệt từng điểm**
  bản integral cũ (test so với bản tham chiếu, nhiều kích thước). Cả bộ lọc đen trắng trên A4 300 DPI cấp phát ~34 MB (test `FilterMemoryTests`).
- `BackgroundFlattener`: ước lượng nền trên ảnh ~256 px (max filter + box blur), chia từng điểm cho nền; không làm sáng quá 1/0,3 lần mức giấy
  (bóng đổ thật có thể còn ~35% độ sáng). Dùng cho Xám và trước Sauvola.
- UI `ResultPage`: 3 nút kiểu trang, thanh trượt Độ đậm (`DragCompletedCommand`: chỉ dựng lại khi thả tay), công tắc Làm sạch nền, "Áp dụng
  kiểu này cho mọi trang" (`PageEditService.ApplyFilterToAll`, xếp hàng dựng lại các trang đổi kiểu). Ảnh cũ vẫn hiện trong lúc dựng lại.

### Bước 8: ghi chú (nhiều trang, PDF, chia sẻ)
- `ImagePdfWriter` (DocScanner.Core/Export): tự ghi PDF 1.7, ghi thẳng ra stream, mỗi lúc chỉ giữ 1 file trang trong RAM, **không giải mã ảnh**:
  JPEG nhúng nguyên byte (`/DCTDecode`, đọc kích thước / số kênh từ SOF), PNG 1-bit nhúng nguyên dữ liệu IDAT (`/FlateDecode` +
  `/Predictor 15`). Tiêu đề PDF UTF-16 (tiếng Việt). Không dùng PdfSharp trên điện thoại (tránh rủi ro trimming / phụ thuộc).
- `PdfExportService`: trang còn đang nhập hoặc chưa có bản dựng mới thì xếp hàng dựng qua `PageIngestQueue` (không tự dựng song song với
  hàng đợi -> không đụng số revision), chờ bằng polling 150 ms; trang lỗi bị bỏ và báo số trang. Khổ trang: A4 chuẩn 595x842 pt
  (theo chiều của bản dựng), trang "theo khung" giữ tỉ lệ với cạnh dài = A4. Ghi ra `.partial` rồi đổi tên.
- File xuất ở `FileSystem.CacheDirectory/exports/<tên tài liệu>.pdf` (`PdfExportService.FileNameFor`). Sau khi xuất: Chia sẻ
  (`Share.Default`), Lưu vào Tải xuống (`AndroidDownloadsService`, MediaStore.Downloads, Android 10+, ghi ở trạng thái pending rồi mới công bố),
  Mở (`Launcher`).
- `DocumentStore`: `Rename`, `MovePage`, `SetOrder` (hoàn tác chuyển trang), `TrashPage` / `RestorePage` / `EmptyTrash` (thư mục
  `{doc}/.trash`, hoàn tác 1 bước; thùng rác dọn khi thao tác mới, rời tài liệu, hoặc lúc mở app).
- UI `DocumentPage`: thanh tiêu đề "Xuất PDF" + menu "Đổi tên"; mỗi ô trang có nút ⋯ (mở, đưa lên đầu / trước / sau / cuối, xoá) và
  kéo thả (`DragGestureRecognizer` / `DropGestureRecognizer`: nhấn giữ một trang rồi thả lên trang khác); thanh "Đã xoá Trang n · Hoàn tác".
  Lớp phủ bận dùng chung (`ImportViewModelBase.RunBusyAsync`, nút Huỷ).
- Đã xoá trang giữ chỗ `ExportPage` / route `export`.
- Test: `DocScanner.Core.Tests` 103 (xuất PDF đọc lại bằng PdfPig: số trang, khổ A4 / theo khung, JPEG nguyên byte, PNG giải nén đúng,
  tiêu đề tiếng Việt, bỏ trang lỗi, huỷ không để lại file; đổi thứ tự / thùng rác / đổi tên). PDF thật (JPEG + PNG đen trắng) mở đúng bằng PDFium.

### Bước 8b: ghi chú (duyệt trang, menu, PDF đã xuất, icon; đợt 2026-09-26)
- Màn chỉnh khung (`CropPage`): hàng "‹ Trước · Kết quả · Sau ›" thay nút Xong ("Kết quả" = Xong cũ). `CropViewModel.Go(±1)` giữ lại khung đang
  hiện (`KeepShownOutline`, như Xong cũ) rồi đổi sang trang kề (`DocumentStore.Neighbor`) ngay trong cùng màn hình. `QuadEditor` có
  `SwipeCommand`: chạm bắt đầu **ngoài khung** (hoặc khi chưa có khung) rồi vuốt ngang >= 20% bề rộng, chủ yếu theo chiều ngang, thì chuyển trang;
  chạm trong khung vẫn là kéo khung / góc / cạnh.
- Màn kết quả (`ResultPage`): "‹ Trước · Chỉnh khung · Sau ›" + `SwipeGestureRecognizer` trái / phải trên ảnh. "Chỉnh khung" quay về bằng
  `..?docId=&pageId=` để màn chỉnh khung chuyển sang đúng trang đang xem.
- Xuất PDF: `Services/ExportCoordinator` (singleton, dùng chung cho Document / Crop / Result) + `Views/ExportOverlay` (tự bind vào singleton).
  PDF lưu ở `AppDataDirectory/exports` (`Core/Export/ExportLibrary`: tên "<tài liệu> yyyy-MM-dd HH.mm.pdf", thêm " (2)" nếu trùng; không còn dùng cache).
- Menu: `AppShell` bật flyout (header có `Resources/Images/scanner_logo.svg`): "Tài liệu" (Home), "PDF đã xuất" (`ExportsPage` /
  `ExportsViewModel`: danh sách mới nhất trước, chạm = Mở / Chia sẻ / Lưu vào Tải xuống / Xoá, vuốt = Xoá, kéo xuống = làm mới).
- Icon: `Resources/AppIcon/appicon.svg` (nền gradient xanh) + `appiconfg.svg` (tờ giấy + 4 góc khung + tia quét; nằm trong vùng an toàn
  của adaptive icon, `ForegroundScale="1"`), splash cùng hình, màu `#1A5FD6`.
- Test ổn định hoá: `FilterMemoryTests` đo cấp phát toàn tiến trình nên phải chạy riêng (collection `DisableParallelization`), trước đó
  thỉnh thoảng báo sai 80-126 MB do test khác chạy song song.

### Chất lượng PDF (đợt 2026-09-26, owner báo PDF 9 trang = 14 MB)
- Nguyên nhân (đo trên máy): trang màu nhúng nguyên bản dựng trong app, ~230-300 DPI, JPEG q94 = 1,25-2,2 MB / trang; trang đen trắng PNG 1-bit ~140 KB.
- Sửa: `Core/Export/PdfQuality` (Nhỏ 150 DPI / JPEG 70, **Vừa 200 DPI / JPEG 80 (mặc định)**, Cao 300 DPI / JPEG 90). Khi xuất, `PdfExportService`
  nén lại riêng các trang JPEG (`IImageService.LoadRgbAsync` với cạnh dài theo DPI, không phóng to; `SaveJpegAsync` theo chất lượng) vào thư mục tạm
  `.work_*` cạnh file PDF, xoá sau khi xong; trang PNG đen trắng nhúng nguyên. Bản dựng lưu trong app không đổi (vẫn q94).
- `ExportCoordinator` hỏi chất lượng mỗi lần xuất (action sheet, đánh dấu ✓ lựa chọn trước, nhớ bằng `Preferences` "pdf_quality").
- Ước lượng trên chính 8 trang màu của tài liệu đó (nén thử trên PC): hiện tại 13,2 MB -> Nhỏ 2,1 MB, Vừa 3,9 MB, Cao 10,7 MB (+0,14 MB trang đen trắng).

### Làm sạch nền màu: đã thử và BỎ (2026-09-26)
- Owner yêu cầu thử làm sạch nền cho trang màu để PDF nhỏ hơn. Đo trên 6 trang màu thật: không giảm (200 DPI / q72: 434 -> 448 KB/trang;
  nền trắng hẳn + xám cũng 440-454 KB). Dung lượng JPEG nằm ở nét chữ, không ở nền. Owner quyết định bỏ: trang Màu giữ nguyên ảnh,
  `BackgroundFlattener.WhitePoint = 1` (Xám / Đen trắng như cũ). Giữ `BackgroundFlattener.Flatten(RgbImage)` theo từng kênh (có test, chưa dùng).
- Giữ: JPEG khi xuất Nhỏ 150 DPI / q60, Vừa 200 DPI / q72, Cao 300 DPI / q90.
- Cách thật sự giảm dung lượng trang chữ: **Đen trắng** (đo: 152 KB/trang ở độ phân giải đầy đủ, 6 trang = 0,9 MB).

### Trang 7 bị méo khi xuất (2026-09-26)
- Owner báo trang 7 "không đúng A4, gần vuông". Dữ liệu thật: tờ giấy bị khung ảnh cắt, khung chỉnh tay có 1 góc ra ngoài ảnh -> hình thang
  rất lệch (cạnh 1349 / 2576 x 2378 / 2329 px). `PerspectiveWarp.A4Size` chọn dọc / ngang theo cạnh DÀI NHẤT mỗi cặp -> chọn A4 ngang
  (3344x2365) trong khi dáng trung bình là dọc 1 : 1,2 -> chữ bị kéo giãn ngang 1,7 lần.
- Sửa: (1) dọc / ngang theo dáng trung bình (`PerspectiveWarp.IsLandscapeShape`); (2) khung không có dáng tờ giấy (tỉ lệ ngoài 1,15-1,75:
  `IsA4Like`) giữ tỉ lệ thật dù đang ở chế độ A4 (`CropPlanner`), khi xuất PDF được đặt giữa trang A4 có lề trắng (`PdfExportService.PageLayout`,
  `PdfPageSource.ImageRect`); (3) bản dựng cũ bị méo tự dựng lại (`PageRecord.RenderStretchedToA4` trong `NeedsRender`, chỉ ảnh hưởng trang méo).
- Kiểm chứng: test với đúng số liệu trang 7; dựng lại trang 7 thật trên PC từ ảnh gốc -> A4 dọc 2481x3508, chữ đúng tỉ lệ (bản sao đã xoá).
