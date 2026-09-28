# Doc Scanner (app mobile) -- trạng thái dự án và việc còn lại

Cập nhật lần cuối: 2026-09-26 (bước 7 + 8, tối ưu tốc độ -- mục 5b). Đọc file này đầu tiên khi làm tiếp; chi tiết kỹ thuật từng bước nằm ở mục
"App mobile" trong [CLAUDE.md](CLAUDE.md).

## 1. Tóm tắt

App scan tài liệu cho **Android** (.NET MAUI, `net10.0-android`, package `btk.docscanner`, tên "Doc Scanner"): nhập ảnh từ thư viện / camera trong app
(dò tờ giấy trực tiếp, tự chụp khi giữ yên, nhiều trang liên tiếp)
-> tự dò mép giấy (cả cạnh cong) hoặc kéo 4 điểm (có kính lúp, kéo được ra ngoài ảnh) -> nắn từ ảnh gốc theo tỉ lệ thật (A4 khi đúng là A4) -> **Màu / Xám / Đen trắng**
(làm phẳng nền, Sauvola tiết kiệm RAM) -> nhiều trang (kéo thả, hoàn tác) -> **xuất PDF**, chia sẻ / lưu Tải xuống. Không OCR ở bản đầu.
iOS để sau (owner chưa có Mac).

**Đã xong: bước 1-8.** **Chưa làm: bước 9 (hoàn thiện), 10 (phát hành).**
Bước 7-8 (đợt 2026-09-26) **chưa commit** và **chưa chạy thử trên điện thoại** (không có máy cắm lúc làm): đã kiểm bằng unit test,
render PDF thật bằng PDFium trên PC, build Debug + Release sạch. Quy tắc owner: **"Không tự ý commit code"**, không push.

## 2. Bản đồ dự án (`Source/`)

| Project | Loại | Vai trò |
|---|---|---|
| `ImageCore.Shared` | net9.0, thuần managed | Thuật toán ảnh dùng chung Windows + mobile: `GrayImage`, `RgbImage`, `Binarizer` (Sauvola tiết kiệm RAM / Otsu), `BackgroundFlattener` (xoá bóng, giấy ngả vàng), `DocumentFilter` (Màu / Xám / Đen trắng), `PngWriter` / `PngReader` (PNG 1-bit / 8-bit), `DocumentCleanup` (deskew, viền đen, khử đốm), `DocumentEdgeDetector` (dò mép giấy), `PerspectiveWarp` (nắn phối cảnh, khổ A4), `Homography`, `Quad` |
| `DocScanner.Core` | net9.0, thuần .NET | Logic app, test được trên PC: `DocumentStore` (kho tài liệu, 1 bản in-memory; đổi tên, đổi thứ tự, thùng rác trang), `PageRecord` / `DocumentRecord`, `ImportService`, `PageIngestQueue` (hàng đợi nền), `CropDetectionService`, `CropRenderService` (cắt + lọc), `CropPlanner`, `PageEditService` (khung, xoay, kiểu trang), `Export/ImagePdfWriter` (PDF không cần thư viện), `Export/PdfExportService`, `ImageGeometry`, `IImageService` |
| `DocScanner` | net10.0-android | UI MAUI (MVVM): trang Home / Document / Crop (`QuadEditor`) / Result; `AndroidImageService`; `AndroidDownloadsService` (MediaStore); `ImportCoordinator` (MediaPicker) |
| `DocScanner.Core.Tests` | xUnit | **140 test** (+ PdfPig để đọc lại PDF) |
| `ImageCore.Shared.Tests` | xUnit | **87 test** (có bộ cảnh mô phỏng `SceneBuilder` cho bộ dò mép, test bộ nhớ trang A4) |
| `ImageCoreService`, `ImageOptimizerTool`, `SmokeTests`, `Bench` | WinForms cũ | App Windows; SmokeTests ALL PASS sau khi thay Sauvola (kết quả giống hệt từng điểm) |
| `Tools/` | công cụ tay | `EdgeProbe` và `gen_test_scans.py`; xem `Tools/README.md` |

Solution: `Source/DocScanner.slnx` (mobile) và `Source/ImageProcessing.sln` (owner đã thêm `DocScanner` vào đây, nên build sln này cần workload MAUI Android).

## 3. Đã làm và đã kiểm chứng ở đâu

| # | Nội dung | Kiểm chứng |
|---|---|---|
| 1 | Tách `ImageCore.Shared` khỏi System.Drawing | SmokeTests ALL PASS, unit test |
| 2 | Khung app MAUI, quyền camera (chỉ `CAMERA`, không `INTERNET`) | Chạy trên Note 10+ |
| 3 | Nhập ảnh (chọn nhiều / camera hệ thống), EXIF, ảnh 48 MP | **Máy thật**: WebP, JPEG 12 & 48 MP, EXIF 6/3/8, PNG trong suốt; không crash |
| 3b | **Nhập nhanh**: chép bản gốc, xử lý nền (thumbnail -> proxy -> dò mép), khôi phục sau khi app bị tắt | **Máy thật** |
| 4 | Dò mép giấy | 36 test mô phỏng; **máy thật**: 2 ảnh thật (1 đúng, 1 sai; xem mục 5) |
| 5 | Trình chỉnh 4 điểm + kính lúp, Tự động, Toàn ảnh, Xoay 90°, Xong | **Máy thật** |
| 6 | Cắt phối cảnh từ **ảnh gốc**, trang kết quả, thumbnail đã cắt | **Máy thật**: ảnh thật 1996x2582 nắn thẳng đúng |
| 6b | Kéo điểm ra vùng đen ngoài ảnh | Test PC; **chưa xác nhận trên máy** |
| 6c | Trang cắt ra khổ **A4** / theo khung | Test PC; **chưa xác nhận trên máy** |
| 7 | **Màu / Xám / Đen trắng** mỗi trang, độ đậm, làm sạch nền, áp dụng cho mọi trang; bộ lọc chạy trên ảnh trong RAM ngay sau warp (nén đúng 1 lần); đen trắng lưu PNG 1-bit | Unit test (Sauvola mới giống hệt bản cũ từng điểm; trang A4 8,7 MP chỉ cấp phát **34 MB**, 1,1 s trên PC; ảnh bóng đổ đậm ra chữ sạch). **Chưa thử trên máy** |
| 8 | **Nhiều trang**: kéo thả / menu ⋯ để đổi thứ tự, xoá không cần xác nhận + **Hoàn tác**, đổi tên tài liệu. **Xuất PDF** (A4, trang JPEG nhúng nguyên byte, trang đen trắng nhúng nguyên dữ liệu PNG), tự dựng trang còn thiếu, tiến trình + Huỷ; sau đó **Chia sẻ / Lưu vào Tải xuống / Mở** | Unit test + PdfPig; PDF thật render đúng bằng PDFium (2 trang: 52 KB, trang đen trắng 7 KB). **Chưa thử trên máy** |
| 8b | **Duyệt trang như trình xem ảnh**: màn hình chỉnh khung và màn hình kết quả có **‹ Trước / Sau ›** (thay nút Xong) và vuốt trái / phải (màn chỉnh khung: vuốt bắt đầu ngoài khung). Nút **Xuất PDF** ở thanh trên cả 3 màn hình. **Menu** (☰) ở màn hình chính: Tài liệu, **PDF đã xuất** (danh sách, mở / chia sẻ / lưu / xoá). PDF lưu trong thư mục dữ liệu app, tên kèm thời điểm (không ghi đè). Icon + splash máy quét mới | Unit test (thư viện PDF, trang kề); build sạch. **Chưa thử trên máy** |

## 4. Chưa kiểm chứng trên máy thật (cần owner bấm thử, hoặc tự thử khi có máy)

**Mới nhất (đợt 2026-09-27h, mục 5j):**

0g. **PDF đã xuất**: mở một PDF nhiều trang, vuốt / ‹ › qua mọi trang (trước bị đen từ trang 2).
0h. **"Tài liệu 1"**: các trang tự dựng lại -> phải ra A4 (trang 7 đang ở "Theo khung": chuyển lại "Khổ A4" nếu muốn A4 chuẩn); kéo góc vài lần:
    tỉ lệ không được dài ra nữa.
0i. **Màn chính**: tìm kiếm; tạo thư mục (nút trên thanh tiêu đề, hoặc ⋮ > Tạo thư mục mới và chuyển vào); nhấn giữ tài liệu -> chọn nhiều,
    kéo thả lên thư mục; Chuyển / Xoá nhiều; mở thư mục, chụp trong thư mục.
0j. **Cài đặt / Thông tin**; **splash** khi mở app; **tự cập nhật**: cần đặt update.json + APK lên https (UPDATE-SERVER.md) rồi Kiểm tra ngay.

**Đợt 2026-09-27g (mục 5i):**

0c. **Đen trắng** trên ảnh chụp camera thật (chữ nhỏ, dấu tiếng Việt): so với trước có rõ / đủ dấu hơn không; trang đen trắng cũ tự dựng lại.
    Xuất PDF Vừa (1-bit) và Cao (mép mềm): xem dung lượng và độ nét.
0d. **Zoom**: chụm 2 ngón / chạm đúp / kéo ở trình xem ảnh, màn Sửa, trình xem PDF; vuốt ngang khi chưa zoom = đổi trang.
0e. **Xem / Sửa** ở đầu tài liệu; **Chữ ký**: vẽ, lưu, đặt, dời, đổi cỡ, xoá, xoay trang (chữ ký xoay theo), xuất PDF có chữ ký.
0f. **PDF đã xuất**: chạm = mở trình xem trong app, ⋮ = Chia sẻ / Lưu / Mở bằng app khác / Xoá.

**Đợt 2026-09-27f (mục 5h):**

0a. **Camera trong app** (nút chụp tròn ở Home, "Chụp thêm" trong tài liệu): đưa máy qua tờ giấy trên bàn -> khung xanh bám mép; giữ yên ~1 s
    -> tự chụp (chớp trắng, số trang tăng); lật / đổi tờ -> tự chụp tờ tiếp; tắt "Tự chụp" rồi bấm nút tròn; flash; chạm để lấy nét; Xong ->
    tài liệu có đủ trang, đã nắn. Xem tốc độ khung xanh: `adb logcat -s DocScanPerf` dòng "camera: frame analysis ... ms avg".
    Thử cả: giấy trắng trên bàn trắng, bàn tay cầm giấy, ánh sáng yếu, nhiều tờ chồng.
0b. **Nắn thẳng**: mở vài trang chụp cầm tay -> bấm Tự động ở màn chỉnh khung: khung có cạnh cong bám mép; kết quả chữ thẳng hàng, đúng tỉ lệ.
    Trang cũ tự dựng lại theo cách mới (lần đầu mở có thể chờ một chút).

**Bước 7-8:**

1. Trang kết quả: 3 nút Màu / Xám / Đen trắng; thanh trượt Độ đậm (dựng lại khi thả tay); công tắc Làm sạch nền; nút "Áp dụng kiểu này cho mọi trang".
   Đo thời gian dựng lại một trang trên Note 10+ (ước lượng 2-4 s, vì mỗi lần đổi kiểu là giải mã + nắn lại từ ảnh gốc).
2. Chất lượng đen trắng trên ảnh chụp thật (bóng tay / điện thoại, giấy ngả vàng, chữ bút chì, con dấu đỏ -> chuyển xám / đen).
3. Màn hình tài liệu: **nhấn giữ rồi kéo** một trang thả lên trang khác (đổi thứ tự); nút ⋯; xoá trang rồi bấm **Hoàn tác**; menu "Đổi tên".
4. **Xuất PDF** (nút trên thanh tiêu đề): tiến trình, Huỷ giữa chừng, rồi Chia sẻ (Zalo / Gmail / Drive), **Lưu vào Tải xuống** (xem trong app Files),
   Mở (cần có app đọc PDF). Tài liệu có trang lỗi: PDF bỏ trang đó và báo số trang bị bỏ.
5. Ô tài liệu ở Home giờ hiện thumbnail trang đầu **đã cắt** (khi có) và tên mới sau khi đổi tên.
5b. **Trước / Sau / vuốt** ở màn chỉnh khung (khung đang hiện được lưu khi chuyển trang) và màn kết quả; nút "Chỉnh khung" từ màn kết quả mở đúng trang đang xem.
5c. **Menu ☰** -> "PDF đã xuất": danh sách, chạm để Mở / Chia sẻ / Lưu / Xoá, vuốt để xoá, kéo xuống để làm mới. Icon app + splash mới (có thể phải gỡ app cài lại để launcher đổi icon).
5d. **Xuất PDF hỏi chất lượng** (Nhỏ / Vừa / Cao): kiểm tra dung lượng thật trên máy; tài liệu 9 trang (8 màu) ước tính Vừa ~4 MB, Nhỏ ~2 MB.
5e. **Trang 7** (và trang méo tương tự) tự dựng lại khi mở hoặc xuất: phải ra A4 dọc, chữ đúng tỉ lệ. Khung không có dáng tờ giấy (tờ bị cắt, hoá đơn) giữ tỉ lệ, trong PDF nằm giữa trang A4.

5i. **Giao diện mới (kiểu TapScanner)**: xem lại 4 màn (chính, tài liệu, chỉnh khung, kết quả), thử menu ⋮ ở màn chính và trên ô trang,
    nút chụp tròn, "Chỉnh sửa", bảng Bộ lọc / Điều chỉnh, nút Xong. Góp ý chỗ nào còn to / chưa hợp lý.
5h. **Ô "Đang tải..." + xem trước tức thì**: chọn nhiều ảnh -> hiện ngay đủ số ô "Đang tải..." có thanh tiến trình, ô xong thì chỉ ô đó đổi,
    danh sách không giật; chạm ô chưa xong -> mọi nút bị khoá. Màn kết quả: bấm Màu / Xám / Đen trắng thấy ngay; kéo Độ sáng / Tương phản / Độ đậm
    thấy ngay; xuất PDF -> trang giống hệt lúc xem. Chuyển Trước / Sau nhanh (trang kề đã chuẩn bị sẵn). Nút Xoay trái / phải: xoay ngay,
    PDF ra trang đã xoay (trang ngang = A4 ngang).
5g. **Nhập ảnh chạy nền**: chọn ~100 ảnh (Nhập từ thư viện): phải vào tài liệu ngay, ô trang hiện dần, dòng "Đang nhập ảnh x/100" + nút Dừng; trong lúc
    đó vẫn cuộn, mở trang, kéo thả được. Về Home (ô tài liệu hiện "đang nhập x/y"), vào lại tài liệu: trạng thái vẫn chạy tiếp. Bấm Dừng: giữ các trang
    đã nhập. Chụp camera: ảnh vào tài liệu như cũ. Kiểm tra trình chọn ảnh nào hiện ra trên Note 10+ (photo picker mới hay giao diện chọn tệp cũ).
5f. **Tốc độ (đợt tối ưu)**: nhập 10-20 ảnh, chờ vài giây rồi mở "Kết quả" và "Xuất PDF": phải hiện gần như ngay (trang đã dựng sẵn ở nền).
    Trong lúc đang dựng nền, kéo khung / cuộn danh sách vẫn phải mượt. Màu trang kết quả đúng (không bị ngả xanh = đảo kênh màu).

**Từ trước:**

6. Kéo điểm ra vùng đen rồi Xong; khổ A4 và nút "theo khung"; nút **Xong** trên trang kết quả.
7. Camera hệ thống (dự phòng khi camera trong app không mở được): đã thử chụp + huỷ trên Note 10+ (27/09).
8. Xoá tài liệu bằng vuốt ở Home.
9. Ảnh gốc **48 MP thật** qua bước cắt; máy RAM thấp (3 GB).
10. Bản **Release**: đã build được trên PC (đợt này), **chưa cài / chạy trên máy** (trimming, JSON source-gen, PlatformImage).

## 5. Vấn đề đã biết / nợ kỹ thuật

- **Bộ dò mép chưa tốt với ảnh thật**: mới thử 2 ảnh thật. Cần bộ 20-30 ảnh thật (xem CLAUDE.md, Bước 4). Tốc độ ~1,7 s/trang/worker trên máy trước đợt tối ưu; bản mới nhanh ~3,8 lần trên PC (mục 5b), chưa đo lại trên máy.
- Đổi kiểu trang (Màu / Xám / Đen trắng, độ đậm) dựng lại cả trang từ ảnh gốc. Nếu trên máy thấy chậm: giữ bản màu đã nắn trong bộ nhớ đệm
  (hoặc xem trước trên bản thu nhỏ) rồi mới dựng bản đầy đủ khi thả tay.
- Trang đen trắng trong PDF nén bằng Flate (PNG). CCITT G4 / JBIG2 sẽ nhỏ hơn khoảng 2-3 lần; hiện đã rất nhỏ (vài chục KB / trang A4) nên để sau.
- Lưu vào Tải xuống chỉ có trên Android 10+ (MediaStore). Android 8-9 chỉ có Chia sẻ / Mở.
- Hoàn tác chỉ **1 bước** (lần xoá / chuyển trang gần nhất); trang đã xoá bị xoá hẳn khi thao tác tiếp, rời tài liệu khác, hoặc mở lại app.
- Chỉ có test tự động cho `ImageCore.Shared` và `DocScanner.Core`; **ViewModel / XAML / QuadEditor không có test tự động** (kiểm bằng tay + adb).
- `doc.json` ghi tiếng Việt dạng escape unicode (hợp lệ, khó đọc).
- Trang kết quả chưa có phóng to / kéo (pinch). Icon và splash vẫn là mặc định của template MAUI.
- `DocumentEdgeDetector.Trace` là hook debug.
- `Source/DocScanner/DocScanner.csproj.user` và `Source/DocScanner/Properties/launchSettings.json` do Visual Studio sinh, **không nên commit** (nên thêm vào `.gitignore`).
- Dữ liệu test còn trên máy owner: 3 tài liệu (`14:16`, `15:10`, `15:30`); an toàn để xoá.
- **Đã giải quyết ở đợt này**: RAM của Sauvola (140 MB -> dải trượt, 34 MB cho cả bộ lọc); nén JPEG hai lần (lọc trên ảnh trong RAM);
  đổi tên tài liệu; thumbnail Home dùng trang đã cắt.

## 5b. Tối ưu tốc độ (đợt 2026-09-26)

Đo bằng `Source/MobileBench` (`dotnet run -c Release --project Source/MobileBench`; ảnh giả lập 12 MP, proxy 1600 px, trang A4 300 DPI).
Số dưới đây là trung vị A/B **trên cùng một PC 8 nhân, cùng lượt chạy** (code cũ = HEAD). Trên Note 10+ các khâu thuần C# chậm hơn khoảng 3-5 lần
nhưng tỉ lệ cải thiện tương tự. `MobileBench detect-dump <file>` in kết quả bộ dò trên 40 cảnh ngẫu nhiên đủ độ chính xác để so trước / sau.

| Khâu | Cũ | Mới | Cách làm |
|---|---|---|---|
| Dò mép (ảnh 480 px) | ~380 ms | **~100 ms** | `AngleDiff` bỏ phép `%` số thực (chạy hàng triệu lần); tinh chỉnh các đường Hough song song; chấm điểm tứ giác song song với ngưỡng loại sớm dùng chung (lock-free); Sobel / NMS song song theo hàng. **Kết quả giống hệt từng bit** trên 40 cảnh (`detect-dump`) và 67 test. |
| Bộ lọc Xám (A4) | ~92 ms | ~50-58 ms | `ToGray` song song; `BackgroundFlattener` tính trước toạ độ nội suy theo cột (giống hệt kết quả). |
| Bộ lọc Đen trắng (A4) | ~160 ms | ~119 ms | như trên (Sauvola / Despeckle giữ nguyên). |
| `ToGray` / `FromGray` / `FromArgb` / `Resize` | 1 luồng | song song theo hàng | kết quả giống hệt. |
| Warp 12 MP -> A4 | ~110 ms | ~78 ms (nhiễu đo) | không đổi code (đã song song). |

**Android (`AndroidImageService`, file mới `Platforms/Android/BitmapPixels.cs`):**
- Đọc / ghi điểm ảnh bằng `AndroidBitmap_lockPixels` (thư viện NDK `jnigraphics`, có sẵn trên mọi Android) thay cho `GetPixels(int[])` /
  `CreateBitmap(int[])`: bỏ 2 bản sao (mảng Java + mảng .NET, ~2 x 64 MB với vùng 16 MP) mỗi lần giải mã vùng / lưu JPEG. Vừa nhanh hơn vừa
  giảm nguy cơ hết RAM trên máy 3 GB. Bitmap không phải RGBA_8888 tự quay về cách cũ.
- JPEG ghi thẳng vào `FileOutputStream` Java (có đệm 64 KB) thay vì gọi ngược vào `FileStream` .NET mỗi vài KB.
- `AllowUnsafeBlocks` bật trong `DocScanner.csproj` (cho con trỏ điểm ảnh).

**Dựng trước ở nền (`PageIngestQueue.Prerender`, bật trong `MauiProgram`):** trang có khung (dò xong, hoặc người dùng rời trang ở màn chỉnh khung)
được nắn + lọc luôn ở mức ưu tiên thấp nhất, **tối đa 1 luồng** (luồng còn lại luôn rảnh cho việc người dùng đang chờ). Mở "Kết quả" và "Xuất PDF"
gần như tức thì thay vì chờ 1-3 s mỗi trang.
- Việc dựng người dùng yêu cầu (`EnqueueRender`) **vượt lên** việc dựng trước đang chờ, không xếp trùng; một trang **không bao giờ được dựng
  bởi 2 luồng cùng lúc** (trước đây xuất PDF và màn kết quả có thể cùng tạo revision n+1). `IsPreparing(page)` = trang còn việc nhập (thumbnail /
  proxy / dò mép); `ResultViewModel` và `PdfExportService` dùng nó thay cho `IsBusy`, nên không phải chờ hàng dựng trước.
- Dựng trước lỗi (ví dụ hết RAM) không ghi `RenderError`: lần dựng người dùng yêu cầu sẽ thử lại và báo lỗi nếu có.
- Đánh đổi: tốn pin / CPU cho cả trang người dùng sẽ chỉnh lại khung (khi đó trang được dựng lại), và ~0,5-2 MB lưu trữ / trang sớm hơn
  (đằng nào xuất PDF cũng cần).
- Test: `DocScanner.Core.Tests/PrerenderTests.cs` (5 test: tự dựng sau khi dò, tắt option thì không dựng, vượt hàng, không dựng trùng / chạy lại khi khung
  đổi giữa chừng, lỗi nền không bị ghi lại). Tổng **119 test** DocScanner.Core, **67 test** ImageCore.Shared, đều PASS (chạy 5 lần liên tiếp ổn định).

**Đã chạy trên máy ảo Android** (emulator x86_64 trên Hyper-V, bản Debug): tài liệu 3 trang Màu / Xám / Đen trắng đặt bằng `run-as`, mở app ->
`ResumePending` tự làm thumbnail (0,7 s), proxy (1,5 s), dò mép (tin cậy 0,79) rồi **dựng trước cả 3 trang** (xong sau ~9 s) mà không cần bấm gì.
Kiểm tra file: trang màu đúng thứ tự kênh (R > G > B như giấy ngả vàng; nếu đảo kênh sẽ ra B > R), trang xám R = G = B, trang đen trắng PNG 1-bit,
ảnh nắn thẳng đúng. Hộp thoại "System UI isn't responding" thấy lúc đó là ANR của máy ảo lúc khởi động (13:42, trước khi mở app 27 phút), không liên quan.

**Chưa làm (đề xuất tiếp):**
- Đo trên **Note 10+ bản Release** (máy ảo Debug không đại diện): thời gian dò mép / dựng một trang, và cảm giác mượt khi đang dựng nền.
- Đổi Màu / Xám / Đen trắng vẫn giải mã + nắn lại từ ảnh gốc; có thể giữ bản màu đã nắn để đổi kiểu nhanh hơn.
- `Despeckle` (~40 ms) và `PerspectiveWarp` (~80 ms) chưa tối ưu thêm (lợi ích còn nhỏ).

## 5c. Nhập ảnh chạy nền (đợt 2026-09-27)

Owner yêu cầu: bỏ spinner chặn khi nhập ảnh; nhập chạy nền, trạng thái hiện trong tài liệu và vẫn còn khi thoát ra / vào lại;
thêm 100 ảnh không được treo app.

- **Nguyên nhân treo**: `MediaPicker.PickPhotosAsync` của MAUI chép **mọi ảnh đã chọn vào cache ngay trên luồng UI** trước khi trả về
  (`FileSystemUtils.EnsurePhysicalPath` với URI `content://`), rồi app chép lần 2 vào tài liệu; cache không bao giờ được dọn.
- **Trình chọn ảnh riêng** `Platforms/Android/AndroidPhotoPicker.cs` (+ `Services/IPhotoPicker.cs`, `MainActivity.OnActivityResult`): chỉ trả URI.
  Android 13+ (hoặc 11-12 có module photo picker mới) dùng `ACTION_PICK_IMAGES` (tối đa `MediaStore.PickImagesMaxLimit`, thường 100 ảnh / lần);
  máy cũ hơn dùng `ACTION_GET_CONTENT` chọn nhiều (Note 10+ Android 12 có thể rơi vào nhánh này, giao diện chọn tệp như trước). Tên file thật
  (để lấy đuôi .png / .heic...) hỏi ContentResolver ở luồng nền (`ImportSource.ResolveName`). Ảnh chép **1 lần** thẳng từ URI vào thư mục trang.
- **`DocScanner.Core/BackgroundImporter`** (singleton): `Start(docId, ảnh)` trả về ngay; 1 worker chép lần lượt các lô (theo thứ tự chọn), mỗi ảnh
  chép xong là thành trang `Pending` và vào `PageIngestQueue` như cũ. Trạng thái theo tài liệu: `Status(docId)` (tổng / đã xong / lỗi / đã dừng),
  sự kiện `Changed`, `Stop(docId)` (giữ các trang đã nhập, bỏ phần còn lại), `Dismiss(docId)`. Trạng thái không có gì để báo (xong hết, không lỗi)
  tự biến mất; có lỗi hoặc đã dừng thì còn đến khi người dùng bấm Xem / Đóng. Xoá tài liệu ở Home thì dừng việc nhập của nó.
- **UI**: bỏ `BusyOverlay` + `ImportViewModelBase` (đã xoá file). Home: chọn ảnh xong mở tài liệu mới ngay; ô tài liệu hiện "đang nhập x/y".
  Màn tài liệu: dòng trạng thái "Đang nhập ảnh 23/100 · đang xử lý 12 ảnh" + nút **Dừng**; hết lô: "Không nhập được n/y ảnh" (**Xem**) hoặc
  "Đã dừng: nhập x/y ảnh" (**Đóng**). Ô trang được **thêm dần** khi từng ảnh chép xong (chỉ thêm ô mới, không dựng lại cả danh sách). Người dùng vẫn
  kéo thả, mở trang, chọn thêm ảnh... trong lúc đang nhập. Thoát ra Home / vào lại tài liệu vẫn thấy trạng thái (nằm trong singleton, không trong màn hình).
- Camera: ảnh chụp cũng nhập qua hàng nền; file JPEG camera để trong cache tự xoá khi chép xong (`FileOptions.DeleteOnClose`).
- **Giới hạn**: việc nhập sống cùng tiến trình app. App bị tắt hẳn (vuốt khỏi đa nhiệm, hệ thống giết khi thiếu RAM) thì các ảnh **chưa chép** không
  được nhập (quyền đọc URI của trình chọn cũng hết khi đó); các trang đã chép vẫn còn và được xử lý tiếp bằng `ResumePending`. Chuyển sang app khác
  thì việc chép vẫn chạy tới khi Android dừng tiến trình.
- Test: `DocScanner.Core.Tests/BackgroundImportTests.cs` (6 test: trả về ngay + 100 ảnh thêm dần, thứ tự 2 lô, dừng giữa chừng không để lại trang dở,
  báo ảnh lỗi, xoá tài liệu giữa chừng, tên file thật). Tổng **125 test** PASS (chạy 5 lần liên tiếp). Build Android sạch.
- Đã tìm và sửa 2 lỗi đua khi viết: `CancellationTokenSource.Cancel()` chạy tiếp phần chép ngay trên luồng gọi (đang giữ khoá) nên phải huỷ ngoài khoá,
  và đánh dấu lô bị bỏ trong khoá để worker không kịp lấy lô kế tiếp.
- **Chưa thử trên máy**: bản Debug đã cài lên Note 10+ lúc 06:30 ngày 27/09 (máy đang tắt màn hình, app không chạy). Không tự bấm thử vì trình chọn ảnh
  là app khác (quy tắc adb của owner).

## 5d. Ô "Đang tải..." + xem trước tức thì, độ sáng / tương phản (đợt 2026-09-27b)

Owner báo: thêm ảnh thì danh sách bị giật vì cả danh sách được làm mới. Yêu cầu: mỗi ảnh có ngay một ô "loading" đúng vị trí, ảnh xong
thì chỉ ô đó cập nhật, có thanh tiến trình; trang chưa xong thì khoá chỉnh sửa. Màn xem: đổi Màu / Xám / Đen trắng thấy ngay; thêm 2 thanh
độ sáng / độ tương phản, kéo tới đâu thấy tới đó, thật mượt.

**Ô chờ (placeholder)**
- Trạng thái trang mới `PageState.Importing`. `ImportService.AddPlaceholders` thêm cả n trang một lần (1 lần ghi doc.json) ngay khi chọn xong;
  `FillAsync` chép từng ảnh vào đúng trang đó (-> `Pending`, rồi pipeline như cũ). Ảnh lỗi thành trang `Failed` **tại chỗ**; bấm Dừng gỡ các ô chưa chép;
  ô bị người dùng xoá trong lúc chờ thì bỏ qua. App bị tắt giữa chừng: lần mở sau `DocumentStore.RemoveUnfinishedImports` gỡ các ô còn sót
  (quyền đọc ảnh của trình chọn đã hết). Hoàn tác xoá một ô đang chép -> trang `Failed` ("hãy nhập lại"), không thành ô chờ vĩnh viễn.
- Tiến trình từng ô: `BackgroundImporter.PageChanged` + `CopyProgress(pageId)` (% byte đã chép, báo mỗi 4%, cần stream có độ dài: trình chọn
  Android đưa luồng đọc có độ dài). Thanh tiến trình trên ô: chờ 0% -> đang tải 5-50% -> tạo ảnh xem trước 55% -> xử lý 75% -> dò mép 90% -> xong (ẩn).
- `DocumentViewModel` không còn làm mới cả danh sách: tra ô theo id (dictionary), chỉ `Refresh()` đúng ô đó (chỉ thuộc tính nào đổi mới báo view).
  Quay lại từ màn khác: cập nhật tại chỗ (`Sync`). Xoá / chuyển trang: gỡ / di chuyển đúng một ô rồi đánh số lại (trước đây dựng lại cả danh sách).
- Ô chờ dùng vòng xoay native + chữ "Đang tải..." thay cho ảnh GIF: 100 GIF động cùng lúc phải giải mã từng khung hình, vòng xoay native gần như không tốn gì.
- Khoá: màn chỉnh khung với trang `Importing` / `Pending` / `Preview` không cho chỉnh (mọi nút khoá, hiện "Ảnh đang được tải vào..."); màn kết quả
  khoá mọi điều khiển khi trang chưa `Ready`.

**Xem trước tức thì + độ sáng / độ tương phản**
- `CropRenderService.RenderPreviewAsync`: trang đã nắn ở cỡ màn hình (`CropPlanner.PreviewLongEdge` = 1800 px, ~2 MP) từ **ảnh gốc** (giải mã
  thu nhỏ theo `CropPlanner.Plan(..., maxLongEdge)`), cùng hình học với bản lưu. `ResultViewModel` giữ bản này trong RAM (+ tải trước trang trước / sau,
  bộ đệm 3 trang) và:
  - đổi Màu / Xám / Đen trắng, độ đậm, làm sạch nền: lọc lại trên bản nhỏ (đo PC: Xám 26 ms, Đen trắng 45 ms; máy dự kiến 0,1-0,2 s; Màu tức thì);
    đang lọc mà có yêu cầu mới thì chỉ giữ yêu cầu mới nhất; thanh Độ đậm giờ cập nhật **trong lúc kéo**;
  - độ sáng / độ tương phản: **không xử lý điểm ảnh**, view đặt `ColorMatrixColorFilter` lên ImageView (GPU) mỗi lần thanh trượt nhích.
- `ToneAdjust` (ImageCore.Shared): out = in x 2^(tương phản/70) + 128(1 - hệ số) + độ sáng x 1,28 (tương phản xoay quanh xám giữa). Cùng công thức cho
  ColorMatrix (xem) và bảng tra (lưu) -> ảnh lưu / PDF giống ảnh đang thấy. Chỉ áp cho Màu / Xám; Đen trắng dùng Độ đậm (Sauvola không đổi theo phép tuyến tính).
- `PageRecord.Brightness / Contrast` (+ `CroppedBrightness / CroppedContrast`), `FilterOptions.Tone`, `FilterOptions.SameLook` thay `PageRecord.SameLook`;
  "Áp dụng cho mọi trang" chép cả độ sáng / tương phản. Nút "Mặc định" đưa hai thanh về 0.
- Ảnh trên màn kết quả **không còn là file đã lưu** mà là bản xem trước (vẽ thẳng bitmap vào ImageView, không qua ImageSource / mã hoá file).
  Bản đầy đủ (A4 300 DPI) lưu ở nền 0,8 s sau lần chỉnh cuối (hoặc ngay khi chuyển trang / rời màn hình); dòng trạng thái "Đang lưu bản đầy đủ...".
- Test: `ImageCore.Shared.Tests/ToneTests.cs` (11), `DocScanner.Core.Tests/PlaceholderAndPreviewTests.cs` (8) + sửa test cũ theo hành vi mới.
  **133 + 78 test PASS**, SmokeTests ALL PASS, build Android sạch. Bản Debug cài lên Note 10+ lúc 07:19 27/09 (lúc đó 45 trang đều Ready, không có gì đang nhập).
- **Chưa thử trên máy**: độ mượt thật khi 100 ô cập nhật, thời gian mở màn kết quả (giải mã vùng ảnh gốc ~0,2-0,4 s dự kiến), màu ColorMatrix khớp ảnh lưu.

**Xoay trang ở màn kết quả (2026-09-27)**
- Nút **↺ Xoay trái** / **Xoay phải ↻** hai bên nút khổ giấy. Xoay **trang đã nắn** (`PageRecord.OutputRotation`, 0/90/180/270; `PageEditService.RotateOutput`),
  không đụng ảnh gốc, khung hay màn chỉnh khung (khác nút "Xoay 90°" ở màn chỉnh khung: nút đó xoay cả ảnh gốc và làm lại thumbnail / proxy, trang bị khoá
  một lúc). Trang vẫn `Ready`, chỉnh tiếp được ngay.
- Thấy ngay: bản xem trước trong RAM được xoay (`RgbImage.RotateClockwise`, vài ms), không nắn lại. Bản đầy đủ: nắn -> xoay -> lọc (cùng thứ tự với
  bản xem trước), lưu nền sau 0,8 s; `CroppedOutputRotation` trong `NeedsRender`; kiểm tra "A4 bị méo" tính cả phần xoay 90 độ. PDF: trang xoay ngang
  thành A4 ngang.
- Test: `RotateTests` (2, Shared), `OutputRotationTests` (Core): ảnh lưu = ảnh cũ xoay đúng từng điểm, bản xem trước cũng vậy, khung / ảnh gốc không đổi.
  **134 + 80 test PASS**. Bản Debug cài lên Note 10+ lúc 07:32 27/09.

## 5e. Thiết kế lại giao diện theo phong cách TapScanner (đợt 2026-09-27c)

Owner: giao diện quá to, chiếm nhiều diện tích; tham khảo app TapScanner, nút đổi thành biểu tượng + chữ nhỏ. Chỉ tham khảo **bố cục / luồng**
(không dùng tên, logo, hình ảnh của TapScanner).

- **Biểu tượng**: font Material Icons (`Resources/Fonts/MaterialIcons-Regular.ttf`, Apache-2.0, ghi trong THIRD-PARTY-NOTICES), tên font "Icons",
  mã ký tự ở `Views/Icons.cs`. Control dùng chung `Views/ToolButton.cs`: biểu tượng 24 + chữ 11, không nền, `IsActive` tô màu thương hiệu, bị khoá thì mờ.
- **Màu**: bảng màu template tím (#512BD4) đổi sang màu thương hiệu xanh **#1A5FD6** (trùng icon app), cả `Colors.xaml` và `colors.xml` Android.
  Nền màn hình xám nhạt #F5F7FA, thẻ / thanh trắng; tối: #121212 / #1C1C1E.
- **Bỏ menu trượt ☰**: một màn gốc (Tài liệu); "PDF đã xuất" mở từ thanh dưới (route `exports`).
- **Màn chính**: danh sách dạng hàng mảnh (ảnh 48x64, tên, "n trang · ngày", nút ⋮ = Đổi tên / Xuất PDF / Xoá; vẫn vuốt trái để xoá). Thanh dưới:
  **Nhập ảnh · [nút chụp tròn nổi ở giữa] · PDF đã xuất**. Bỏ dòng "Lõi xử lý ảnh" (chỉ để debug).
- **Màn tài liệu**: lưới **3 cột**, ô nhỏ (156 dp), số trang dạng nhãn tròn ở góc, nút ⋮ tròn nhỏ (xoá trang nằm trong ⋮, bỏ nút ✕ to).
  Dòng trạng thái nhập ảnh mảnh màu xanh nhạt. Hoàn tác dạng snackbar gọn. Thanh dưới: **Chụp thêm · Thư viện · Chỉnh sửa · Xuất PDF**
  ("Chỉnh sửa" mở trang đầu tiên sửa được, rồi ‹ › qua các trang). Đổi tên: biểu tượng bút ở thanh trên.
- **Màn chỉnh khung**: dòng mảnh "‹ trạng thái ›" trên cùng, ảnh chiếm phần còn lại, thanh dưới **Tự động · Toàn ảnh · Xoay ảnh · [Tiếp ✓]**.
- **Màn kết quả** (luồng như TapScanner): ảnh gần hết màn hình; thanh dưới **Khung · Bộ lọc · Điều chỉnh · Xoay trái · Xoay phải · Khổ A4**;
  **một bảng mỗi lúc** hiện trên thanh dưới (bấm lại để đóng): Bộ lọc = 3 thẻ Màu / Xám / Đen trắng + "Áp dụng cho mọi trang"; Điều chỉnh =
  độ sáng / tương phản (biểu tượng) + Mặc định, hoặc Độ đậm khi đen trắng, + Làm sạch nền. Thanh trên: Xuất PDF + **Xong ✓** (về tài liệu).
- Màn PDF đã xuất: hàng mảnh với biểu tượng PDF đỏ.
- Kiểm chứng trên **máy ảo Pixel 7 (API 36)**, bản Debug: chụp màn hình cả 4 màn; bấm Bộ lọc -> Đen trắng, kéo Tương phản, Xoay phải: ảnh đổi ngay;
  Xong -> về tài liệu, ô trang 1 đã là bản đen trắng đã xoay. 134 test Core PASS. **Chưa cài lên Note 10+** (máy không cắm lúc làm).

## 5f. Đánh giá giao diện so với TapScanner + đo / tối ưu tốc độ bản Release (đợt 2026-09-27d)

**Giao diện: đã giống / còn khác**
- Đã giống: thanh công cụ biểu tượng + chữ nhỏ; nút chụp tròn giữa màn chính; danh sách gọn; màn chỉnh sửa ảnh gần toàn màn hình, một bảng
  công cụ mỗi lúc; **dải bộ lọc có ảnh xem trước của chính trang** (thêm ở đợt này: 3 thẻ Màu / Xám / Đen trắng, ảnh ~200 px tính từ bản xem trước).
- Còn khác (lớn nhất trước): (1) **camera trong app** chụp liên tục nhiều trang, tự dò mép khi đang ngắm (app đang dùng camera hệ thống, 1 ảnh / lần) —
  cần CameraX (Apache-2.0), ước lượng 1-2 ngày; (2) tìm kiếm / sắp xếp / thư mục ở màn chính; (3) chọn nhiều trang (xoá / chia sẻ nhiều);
  (4) chia sẻ ảnh / lưu vào thư viện trực tiếp; (5) OCR, chữ ký — để sau.

**Công cụ đo**: `DocScanner.Core/Perf.cs` ghi thời gian từng khâu ra logcat (tag `DocScanPerf`): khởi động, chép ảnh, thumbnail, proxy, dò mép
(tải proxy / bộ dò), dựng (giải mã vùng, nắn, lọc, lưu), mở bản xem trước, lọc bản xem trước. Xem: `adb logcat -s DocScanPerf` (thụ động, không cần bấm).

**Số đo bản Release trên máy ảo Pixel 7 API 36** (x86_64 trên CPU PC; 12 MP; 2 luồng nền chạy song song nên có tranh CPU; Note 10+ sẽ chậm hơn):

| Khâu | Trước | Sau | Cách làm |
|---|---|---|---|
| Chép 1 ảnh 2,9 MB (nhập) | 74-418 ms (7 MB/s) | **31-50 ms** thường | Đọc qua file descriptor bằng FileStream .NET (native), không qua OpenInputStream (JNI mỗi khối) |
| Proxy 1600 px | 400-1900 ms | **147-168 ms** | Bỏ 2 lần sao chép bitmap thừa (xoay "không xoay" và làm phẳng alpha cho ảnh không có alpha đều copy cả bitmap) |
| Thumbnail | 230-580 ms | **103-222 ms** | như trên + LLVM |
| Bộ dò mép | 550-1100 ms (TB 815) | **295-855 ms (TB 481)** | Release biên dịch **AOT toàn bộ + LLVM** (Mono JIT / AOT thường sinh mã vòng lặp kém) |
| Mở màn kết quả (lần đầu, trang Màu đã dựng sẵn) | 1119-1178 ms | **296 ms** | Trang đã dựng sẵn ở chế độ Màu = chính là trang đã nắn: giải mã file đó thay vì giải mã ảnh gốc + nắn lại |
| Đổi Xám / Đen trắng (bản xem trước) | 105 / 175 ms | — | đã đạt "thấy ngay" |
| Khởi động lạnh | ~2,9 s (có lúc 6 s khi máy ảo quá tải) | ~2,6-3,0 s | Bỏ Shadow của nút chụp (vẽ bóng mờ tốn ~1 s luồng UI lần đầu trên máy ảo; thay bằng viền trắng). AOT toàn bộ không cải thiện khởi động |

Trang đã đổi sang Xám / Đen trắng vẫn đi đường cũ (giải mã vùng ảnh gốc + nắn, ~1,1 s trên máy ảo) vì bản dựng của nó đã bị lọc.

- Đã sửa thêm: trình chọn ảnh (ACTION_PICK_IMAGES) hiện cả **video** -> giới hạn `image/*`.
- `DocScanner.csproj`: Release `AndroidEnableProfiledAot=false` + `EnableLLVM=true`. Đổi lại: build Release ~12 phút (trước ~4), APK 29 -> 42 MB (2 ABI:
  arm64 + x86_64; gói Play tách theo ABI ~21 MB). Debug không đổi.
- `PageRecord.HasPlainColorRender`: bản dựng có đúng khung / 2 kiểu xoay / khổ hiện tại, Màu, độ sáng / tương phản 0 -> dùng làm bản xem trước.
- Test: 135 PASS (thêm test đường nhanh; test xoay trước đây so sánh trên ảnh giả lập đồng màu nên luôn đúng — đã chuyển sang trang Xám để so thật).
- Bản Release cuối (LLVM) đã chạy trên máy ảo: thẻ bộ lọc có ảnh xem trước đúng, mở trang Màu 296 ms. **Chưa cài / đo trên Note 10+** (máy khoá màn hình, sau đó rút cáp). Khi owner dùng app, chạy `adb logcat -s DocScanPerf` để lấy số thật.

## 5g. Mượt khi chỉnh ảnh + Độ sáng cho Đen trắng (đợt 2026-09-27e)

Owner: xoay / lưu chưa tức thì, nhiều chức năng bấm phải đợi; đề xuất tách nhiều tác vụ chạy nền; thêm độ sáng ở màn độ đậm.

**Nguyên nhân lớn nhất (đo trên Note 10+):** app trên máy là **bản Debug** (`DEBUGGABLE`), MAUI chạy bản Debug bằng **trình thông dịch Mono**
(`UseInterpreter=true`, để có C# Hot Reload): vòng lặp ảnh chậm 10-30 lần. Log thật: lọc đen trắng bản xem trước 1-5 s, nắn 1,5-2,6 s, dựng
bản đầy đủ 5-12 s. Cùng thao tác bản Release (máy ảo): lọc 0,1 s, nắn ~0,2 s. **Đánh giá tốc độ phải dùng bản Release.**
- Debug: tắt trình thông dịch (`UseInterpreter=false`, dùng JIT; mất C# Hot Reload, XAML Hot Reload vẫn còn) + `Optimize=true` cho
  `ImageCore.Shared` / `DocScanner.Core`. Máy ảo Debug: nắn 2,7-7,3 s -> 0,7-1,1 s, lọc đen trắng 3,7 s -> 1,2 s (vẫn chậm hơn Release nhiều lần).
- Bản Release (AOT + LLVM) đã cài lên Note 10+ lúc 11:04 27/09 theo yêu cầu owner.

**Tách tác vụ / ưu tiên (đồng ý với hướng của owner, nhưng "nhiều luồng hơn" không đủ; cần ưu tiên + huỷ + bộ nhớ đệm):**
- `ImageCore.Shared/ParallelScope`: giới hạn số luồng theo luồng công việc (AsyncLocal). Việc nền (`PageIngestQueue`) chỉ dùng 1/4 số lõi
  mỗi job (Note 10+: 2 lõi / job, 2 job) -> phần xem trước người dùng đang nhìn luôn còn lõi trống.
- Bản dựng đầy đủ **tự huỷ khi lỗi thời** (`CropRenderService` kiểm tra sau nắn và sau lọc: khung / xoay / khổ / kiểu đã đổi thì dừng,
  không lưu); hàng đợi coi là "chưa làm", không phải lỗi.
- `DocScanner.Core/LookPreview`: pipeline xem trước có bộ nhớ đệm từng bước (ảnh xám -> nền phẳng -> thống kê Sauvola). Độ đậm / độ sáng
  đen trắng chỉ so ngưỡng lại (`Binarizer.Stats` + `Binarizer.Threshold`); xoay = hoán vị các bước đã có (thống kê của ảnh xoay đúng bằng
  thống kê xoay, có test); `Warm` chuẩn bị sẵn các bước ở nền (nửa số lõi) ngay khi mở trang. Đang kéo thanh trượt thì bỏ bước khử đốm, thả tay mới làm.
  Đo PC: một bước kéo độ đậm 53 -> 19 ms (khi kéo, bỏ khử đốm: ~6 ms), xoay 30 ms không lọc lại.
- Xoay: hiệu ứng xoay + co giãn trên GPU ngay khi bấm (180 ms), ảnh đã xoay thay vào cuối hiệu ứng, không nháy.
- Màn kết quả dùng lại 3 bitmap (ghi điểm ảnh vào bitmap có sẵn: `BitmapPixels.WriteGray/WriteRgb`) thay vì tạo bitmap ~9 MB mỗi khung ->
  hết GC liên tục khi kéo.

**Độ sáng cho Đen trắng:** bảng Điều chỉnh ở chế độ Đen trắng có Độ đậm + **Độ sáng** (Màu / Xám: Độ sáng + Tương phản). Độ sáng dịch ngưỡng:
t = (m + b)(1 + k(s/R - 1)) - b (`Binarizer.Sauvola(..., offset)`; b = 0 giống hệt Sauvola cũ, SmokeTests ALL PASS); Otsu: ngưỡng - b.
Đen trắng so sánh "kiểu" theo độ sáng (không theo tương phản).

**Nghiên cứu / việc tiếp theo nếu cần mượt hơn nữa:** xử lý bằng GPU (AGSL RuntimeShader Android 13+ / OpenGL ES) cho ngưỡng và làm phẳng nền;
SIMD (`Vector128`) cho các vòng lặp; thống kê Sauvola ở độ phân giải thấp + nội suy (nhanh 3-5 lần); CoreCLR cho Android khi ổn định.

Test: 87 (Shared) + 140 (Core) PASS, SmokeTests ALL PASS, build Debug + Release sạch.

## 5h. Camera sửa crash + nắn tài liệu thẳng, đúng tỉ lệ + camera trong app dò tài liệu trực tiếp (đợt 2026-09-27f)

Owner: (1) cắt / nắn tài liệu còn cong và méo hình; (2) bấm chụp bằng camera thì app crash; khi chụp hết lỗi thì làm camera nhận diện
tài liệu khi đưa máy qua, để chụp được ảnh chuẩn nhất.

**Crash camera (đã thử trên Note 10+: chụp và huỷ đều chạy):** `MediaPicker.CapturePhotoAsync` của MAUI trên Android 12 đòi quyền
`WRITE_EXTERNAL_STORAGE` (app không khai báo) -> ném lỗi -> crash. Thay bằng `AndroidPhotoCapture` (ACTION_IMAGE_CAPTURE + FileProvider
`btk.docscanner.capture`, ghi thẳng vào cache). Mọi lỗi camera / thư viện ảnh giờ hiện hộp thoại, không làm sập app. Giờ nó là **dự phòng**
khi camera trong app không mở được.

**Méo hình (tỉ lệ sai):** trước đây tỉ lệ trang lấy theo độ dài cạnh trên ảnh; dưới phối cảnh, cạnh xa ngắn hơn nên trang bị bẹp / giãn.
- `ImageCore.Shared/PageGeometry`: tỉ lệ **thật** của tờ giấy từ 4 góc + tiêu cự ước lượng từ chính 4 góc (Zhang & He 2007, hai điểm tụ
  vuông góc); không đo được (nhìn gần thẳng) thì dùng tiêu cự điện thoại phổ biến. Chế độ A4: chỉ ép về đúng A4 khi tỉ lệ thật trong 8%
  quanh sqrt 2; giấy Letter / hoá đơn giữ tỉ lệ thật -> không bao giờ kéo giãn.

**Cong (tờ giấy cầm tay / không nằm phẳng):**
- `PageOutlineRefiner`: sau khi dò khung thô (480 px), tìm lại mép giấy trên proxy 1600 px: ~60 mẫu mỗi cạnh, lấy bậc sáng
  "giấy -> không phải giấy" ngoài cùng, khớp đường cong bằng hồi quy bền (Tukey) -> góc chính xác 1-2 px + độ cong từng cạnh (`PageBends`, lưu
  `PageRecord.CropBend`). Có kiểm tra hợp lý (lồi, diện tích / góc không đổi quá nhiều) nên không thể làm khung tệ hơn.
- `PerspectiveWarp` nắn theo cả 4 cạnh cong (homography + bù cong kiểu Coons) -> chữ và mép giấy thẳng hàng.
- Màn chỉnh khung vẽ **cạnh cong** đúng như sẽ cắt; kéo tay là về tứ giác thẳng như cũ (`CropBend` bị xoá).
- `PageRecord.GeometryVersion = 2`: trang dựng theo quy tắc cũ tự dựng lại một lần ở nền.
- Kiểm chứng: 6 ảnh thật của owner (EdgeProbe trên PC) đều ra trang thẳng, hết dải bàn gỗ; trên Note 10+ trang "11:24" bấm Tự động -> khung bám
  sát mép, kết quả A4 2024x2862 dòng chữ nằm ngang. (Khung chỉnh tay của owner ở trang đó đã được trả lại nguyên.)

**Camera trong app (`Platforms/Android/Camera/`, CameraX 1.6.2, Apache-2.0):**
- `DocumentCameraActivity` (Activity Android riêng, dọc): xem trước toàn khung 4:3, khung giấy dò trực tiếp (xanh dương = đang thấy, xanh lá = đang
  giữ yên sắp chụp), trượt mượt giữa các lần dò. Chạm để lấy nét, đèn flash, **Tự chụp: Bật / Tắt** (nhớ lựa chọn), chụp liên tiếp nhiều trang
  (ảnh nhỏ + số trang góc trái), **Xong** -> các ảnh vào tài liệu như nhập ảnh (dò mép chính xác + cạnh cong như trên). Nút X / Back khi đã có ảnh thì hỏi
  "Thêm vào tài liệu / Bỏ ảnh / Chụp tiếp". Ảnh JPEG độ phân giải cao nhất 4:3, EXIF xoay.
- Dò trực tiếp: khung phân tích 640x480 -> thu còn 320 px -> `DocumentEdgeDetector.Live()` (40 đường, 200 ứng viên, **không** cho cạnh nằm trên
  mép khung: phải thấy cả tờ; nhờ vậy căn phòng bừa bộn không bị coi là "tờ giấy cỡ cả khung"). Chỉ xử lý khung mới nhất, khung chậm bị bỏ.
  Đo PC: 40-125 ms / khung (chế độ ảnh chụp: 220-500 ms). Test: mọi cảnh giả lập có cả tờ giấy trong khung đều IoU >= 0,98 ở chế độ này.
- Tự chụp (`DocScanner.Core/Camera/CaptureStabilizer`, 12 test): khung đứng yên (lệch <= 2,5% khung) 0,9 s, tờ giấy >= 12% khung, tin cậy >= 0,5,
  không chạm mép khung ("Lùi máy ra để thấy cả tờ giấy"). Sau khi chụp **không chụp lại cùng trang**: chờ đổi trang (khung dời đi, mất khung > 0,5 s,
  hoặc nội dung ảnh đổi nhiều - so "vân tay" 8x8 độ sáng).
- Bộ dò giờ **dùng lại bộ đệm lớn theo luồng** (trước: ~5-6 MB cấp phát mỗi lần dò). Trên Android mỗi lần GC các mảng lớn còn dừng cả phía Java
  (đo trên máy ảo: 70 ms đến hơn 1 s mỗi lần). Kết quả dò giống hệt từng bit (so `MobileBench detect-dump`, 40 cảnh). Máy ảo bản Debug: 2,4 s -> 0,6 s / khung.
- Lỗi đã gặp khi làm: mã request trùng với trình chọn ảnh (0x5043) -> kết quả camera bị trình chọn ảnh nuốt; đổi sang 0x5045.
- Đã thử trên **máy ảo** (camera cảnh 3D): mở camera, khung bám đúng màn TV (vật chữ nhật duy nhất), chụp, Xong -> tài liệu mới có trang đã nắn.
  **CHƯA thử trên Note 10+** (máy bị khoá màn hình rồi rút cáp trong lúc làm). Bản Release đã cài Note 10+ lúc 13:51 27/09.

Test: 114 (Shared) + 152 (Core) PASS, SmokeTests ALL PASS.

## 5i. Đen trắng sắc nét, trình xem ảnh / PDF, Xem / Sửa, chữ ký (đợt 2026-09-27g)

Owner (sau khi thử camera mới, "đã khá ổn"): ảnh chụp rõ nhưng sang đen trắng không sắc nét; cần trình xem ảnh có zoom / kéo; công tắc Xem / Sửa
trong tài liệu; màn Sửa mở sẵn Bộ lọc (đứng đầu menu) và zoom được; chữ ký trong màn Sửa; trình xem PDF đã xuất.

**Đen trắng sắc nét** (nguyên nhân đo được, không đoán):
- Ảnh camera hơi mềm (ống kính + phóng lên 300 DPI) nên nét mảnh và **dấu tiếng Việt** chỉ còn màu xám nhạt; Sauvola 1-bit bỏ mất chúng
  ("sửa" -> "sưa", "bổ" -> "bô", "11/2024" -> "11 2024"). Thêm mép 1-bit răng cưa, màn hình thu nhỏ trang thì nét vỡ.
- Công cụ đo `Tools/EdgeProbe` chế độ `bw <thư mục>`: trang chữ tiếng Việt 300 DPI -> giả lập chụp điện thoại (thu nhỏ, mờ, sáng lệch, nhiễu, JPEG)
  -> so các cách. Mực bị mất: cũ 8,7% -> **làm nét rồi phân ngưỡng 3,7-3,9%**; PNG trang A4: 1-bit 184-202 KB, xám mép mềm ~730 KB.
- Sửa: `ImageCore.Shared/Sharpen` (unsharp mask, bán kính ~1/3 nét chữ, tại chỗ, 2 byte/điểm ảnh RAM) trước ngưỡng; `Binarizer.Shade` /
  tham số `ramp`: **mép mềm** (điểm ảnh gần ngưỡng ±12 mức được giữ mức xám tương ứng; giấy và mực vẫn trắng / đen tuyệt đối). `ramp = 0`
  giống hệt Sauvola cũ (SmokeTests ALL PASS). Khử đốm chạy trên bản đen trắng rồi áp lại (`DocumentFilter.Despeckle`).
- Trang lưu trong app: PNG xám 8-bit (đẹp khi xem / zoom). **Xuất PDF Nhỏ / Vừa: tự chuyển 1-bit** (`PngReader.DecodeGray8` -> `EncodeBilevel`,
  dung lượng như cũ); **Cao: giữ mép mềm**. Trang đen trắng cũ tự dựng lại một lần (`PageRecord.BlackWhiteVersion = 2`); trang màu / xám không bị đụng.
- Xem trước (màn Sửa) cùng công thức (`LookPreview` thêm tầng "đã làm nét", thống kê Sauvola tính trên tầng đó).

**Zoom / kéo** (`Platforms/Android/ZoomController`): chụm 2 ngón (tối đa 6x), chạm đúp (2,5x / về vừa màn), kéo khi đang zoom (không vượt mép trang),
vuốt ngang khi chưa zoom = trang sau / trước. Dùng ma trận của ImageView nên không tốn thêm bộ nhớ. Dùng ở màn Sửa (thay SwipeGestureRecognizer),
trình xem ảnh và trình xem PDF. `ZoomImageHost`: giải mã nền, cạnh dài tối đa 4096 (giới hạn texture GPU), chỉ hiện yêu cầu mới nhất.

**Trình xem ảnh** (`ViewerPage`, route `viewer`): trang đã hoàn thiện ở độ phân giải đầy đủ (đang dựng lại thì hiện bản trước rồi tự thay), Trước / Sau,
Sửa, Xuất PDF, Chia sẻ ảnh.

**Công tắc Xem / Sửa** ở đầu màn tài liệu: Xem = chạm trang mở trình xem; Sửa = chạm trang vào chỉnh khung như cũ. Nhớ lựa chọn (`Preferences`
"document_open_mode", mặc định Xem).

**Màn Sửa**: menu dưới: **Bộ lọc** (đầu tiên, mở sẵn) · Điều chỉnh · Khung · Xoay · **Chữ ký** · Khổ giấy. "Xoay trái" + "Xoay phải" gộp thành
"Xoay" (xoay phải 90°) để đủ chỗ cho Chữ ký. Ảnh zoom / kéo được.

**Chữ ký** (chữ ký tay đặt lên trang):
- `SignaturePage`: khung vẽ (ngón tay, mực đen / xanh / đỏ, Xoá nét / Huỷ / Lưu) -> lưu vào **thư viện chữ ký** (`Core/Signatures/SignatureLibrary`:
  `files/signatures/{id}.png` mặt nạ mực 8-bit + `{id}_view.png` màu có nền trong suốt + `index.json`), dùng lại cho mọi trang; xoá được (×).
- Đặt lên trang (`Views/StampEditor`): chạm chữ ký trong dải bên dưới -> hiện trên trang; kéo để dời, kéo chấm xanh để đổi cỡ (giữ tỉ lệ), × đỏ để
  bỏ; nhiều chữ ký một trang. Xong (hoặc nút Back) -> lưu vào trang.
- Lưu: `PageRecord.Stamps` (`PageStamp`: tâm theo tỉ lệ trang, cỡ theo cạnh ngắn, số lần xoay 1/4). Xoay trang thì chữ ký xoay theo.
  `Stamper` in vào **bản dựng** (trang màu: màu mực; xám / đen trắng mép mềm: mức xám của mực; 1-bit: đen) -> có trong PDF. Màn Sửa vẽ chữ ký
  lên bản xem trước (bản sao, không đụng bộ đệm).
- Nét vẽ -> mặt nạ: `SignatureInk.Rasterize` (đoạn thẳng đầu tròn, khử răng cưa, cắt sát mực, cạnh dài 1200 px), thuần C#.
- **Chưa làm: chữ ký số theo nghĩa chứng thư số** (ký PDF bằng USB token / chứng thư CA, PAdES). Nếu owner cần loại này thì là việc riêng ở bước
  xuất PDF (cần thư viện ký PDF có license thương mại + chứng thư của người dùng).

**Trình xem PDF** (`PdfViewerPage`, route `pdfviewer`): PdfRenderer có sẵn của Android (không thêm thư viện), từng trang dựng ~2900 px (A4 ~250 DPI),
zoom / kéo / vuốt, Trước / Sau, nút chia sẻ = menu Chia sẻ / Lưu vào Tải xuống / Mở bằng app khác / Xoá. "PDF đã xuất": chạm = xem, ⋮ = menu.
Sau khi xuất PDF có thêm lựa chọn "Xem".

Đã thử trên **máy ảo** (Debug): công tắc Xem / Sửa, trình xem (chạm đúp zoom, kéo khi zoom, vuốt chuyển trang), màn Sửa (thứ tự menu, vuốt, chạm đúp),
vẽ chữ ký -> lưu -> đặt -> dời -> đổi cỡ -> Xong -> hiện trên bản xem trước -> xuất PDF Vừa -> mở trình xem PDF: chữ ký có trong PDF (đen, 1-bit).
**Bản Release đã cài Note 10+ lúc 19:54 27/09** (owner yêu cầu), mở được, chưa bấm thử: chụm 2 ngón để zoom (adb không giả lập được), đen trắng mới trên ảnh chụp thật.

Test: 117 (Shared) + 161 (Core) PASS, SmokeTests ALL PASS.

## 5j. Sửa lỗi xem PDF, lỗi trang dài hơn A4; màn chính: tìm kiếm, thư mục, chọn nhiều; Cài đặt, Thông tin; tự cập nhật; splash (đợt 2026-09-27h)

**Xem PDF: trang sau bị đen.** `PdfPages.Render` dùng `using` trên trang PdfRenderer: `Dispose()` chỉ giải phóng lớp bọc .NET, KHÔNG gọi
`close()` của trang Java, mà PdfRenderer chỉ cho mở một trang một lúc -> từ trang 2 `OpenPage` ném lỗi -> ảnh trống trên nền đen (Android 12;
máy ảo Android 16 không lỗi). Sửa: `page.Close()` trong `finally`; ghi log khi dựng trang lỗi; bitmap cũ giải phóng trễ 0,5 s.

**Trang dài hơn A4 ("Tài liệu 1", trang 7 nặng nhất, 8 nhẹ hơn; mỗi lần chỉnh lại dài hơn).** Dữ liệu thật (lấy từ máy bằng bản Debug, 10 trang,
ảnh 4608x2592 EXIF 6): trang 7 ra 1248x3508 (dài 2,81 lần), 8: 1,92, 6: 1,71, 4: 1,57. Nguyên nhân: đợt 2026-09-27f tính "tỉ lệ thật" bằng tiêu cự
**đo từ 4 góc** (Zhang & He); trên ảnh thật phép đo này bị sai số góc và giấy cong chi phối: cùng một camera mà ra 0,19 / 0,39 / 0,47 / 0,68 / 1,06 /
2,38 lần đường chéo, hoặc không xác định -> mỗi lần kéo góc là một tỉ lệ khác. Sửa (`PageGeometry.OutputAspect`): bù phối cảnh bằng **tiêu cự
điện thoại cố định** (0,62 x đường chéo) và **giới hạn ±20%** so với tỉ lệ cạnh; chế độ A4: khung có dáng tờ giấy (tỉ lệ cạnh 1,15-1,75, hoặc sau
bù trong 12% quanh A4) ra **đúng A4**. Cả 10 trang ra A4; trang 7 ở "Theo khung" ra 1,41 (trước 3,07). `GeometryVersion = 3`: mọi trang dựng lại
một lần. Test hồi quy dùng đúng 10 khung thật (`PageShapeTests.The_owners_real_A4_pages...`). Trang Letter (1,29) giờ cũng ra A4 ở chế độ A4
(chọn "Theo khung" để giữ 1,29); hoá đơn / thẻ vẫn giữ tỉ lệ riêng.

**Màn chính** (`HomePage` / `HomeViewModel`, route `folder` dùng lại cùng màn cho bên trong thư mục):
- Tìm kiếm (biểu tượng kính lúp): theo tên, **không phân biệt dấu** ("hop dong" ra "Hợp đồng", `Core/TextSearch`), tìm trong mọi thư mục.
- Thư mục (`DocumentRecord.FolderId`, `folders.json`, `DocumentStore.CreateFolder / RenameFolder / DeleteFolder / MoveToFolder`): một cấp; **thư mục
  đứng trước**, theo tên; tài liệu theo thời gian tạo (mới nhất trước) nên chuyển qua lại **không đổi thứ tự**. Xoá thư mục = tài liệu ra ngoài,
  không mất. Chụp / nhập ảnh khi đang ở trong thư mục thì tài liệu mới nằm trong thư mục đó.
- Menu ⋮ tài liệu: Đổi tên, Xuất PDF, Chọn nhiều, **Tạo thư mục mới và chuyển vào**, **Chuyển vào thư mục...**, (Chuyển ra ngoài), Xoá.
  Menu ⋮ thư mục: Đổi tên, Xoá thư mục. Nút "Thư mục mới" trên thanh tiêu đề.
- **Chọn nhiều**: nhấn giữ một tài liệu (hoặc ⋮ > Chọn nhiều) -> ô tích; chạm để chọn / bỏ; thanh đáy đổi thành "Đã chọn n · Tất cả · Chuyển · Xoá";
  nhấn giữ rồi **kéo thả lên thư mục** = chuyển các tài liệu đang chọn vào đó; chạm thư mục khi đang chọn cũng chuyển vào. Back thoát chọn / tìm.
  (Thanh chọn nằm ở đáy để danh sách không xê dịch khi đang kéo.)

**Cài đặt** (`SettingsPage`, bánh răng trên màn chính): Tự chụp khi giữ yên máy; Chạm vào trang để Xem / Sửa; Chất lượng PDF mặc định; Cập nhật
(phiên bản, tự động 01:00, địa chỉ update.json, Kiểm tra ngay, kết quả lần gần nhất); Bộ nhớ đang dùng; Thông tin ứng dụng. Mọi cài đặt lưu
ngay vào đúng khoá `Preferences` mà các màn khác đọc.

**Thông tin ứng dụng** (`AboutPage`): tác giả Nguyễn Hữu Quỳnh, email nguyenquynhvp.ictu@gmail.com (chạm để gửi mail), 0988 632 841 (chạm để gọi),
phiên bản, điều khoản sử dụng, quyền riêng tư, bản quyền © 2026 + danh sách thành phần mã nguồn mở. (Điều khoản là bản tôi soạn, owner nên đọc lại.)

**Tự cập nhật** (`Platforms/Android/Updates/AndroidAppUpdater`, `Core/Updates/UpdateManifest`, hướng dẫn: `UPDATE-SERVER.md`): JobScheduler mỗi đêm 01:00
(cần mạng, giữ qua khởi động lại) -> đọc `update.json` -> versionCode mới hơn thì tải APK, kiểm tra SHA-256, cài bằng PackageInstaller.
Quyền mới: INTERNET (chỉ dùng cho cập nhật), REQUEST_INSTALL_PACKAGES, UPDATE_PACKAGES_WITHOUT_USER_ACTION, POST_NOTIFICATIONS, RECEIVE_BOOT_COMPLETED;
chỉ https (http chỉ cho 127.0.0.1 để thử). Cài từ Google Play thì tắt. **Chưa có máy chủ**: owner cần đặt update.json + APK lên https và nhập địa chỉ.
Phiên bản app nâng lên **1.1 (versionCode 2)**.

**Splash**: `SplashPage` nối tiếp splash hệ thống (cùng màu xanh): logo phóng to, vạch quét chạy, tên app, phiên bản, © tác giả; màn chính được tạo trong
lúc đó, hiện sau tối thiểu 1,3 s.

## 6. Việc còn lại (theo thứ tự nên làm)

### Bước 9 -- Hoàn thiện
- Owner thử bước 7-8 trên máy (mục 4); sửa theo phản hồi.
- Chỉnh bộ dò mép trên ảnh thật (mục 5); có thể thêm "kiểm tra bằng chứng ngoài khung" cho tờ bị cắt mép, lọc màu da (bàn tay), giảm thời gian dò.
- Màn hình Cài đặt (kiểu trang mặc định cho ảnh mới, khổ mặc định A4 / theo khung, độ đậm mặc định, chất lượng JPEG); tiếng Việt + tiếng Anh;
  sáng / tối; icon + splash riêng.
- Pinch-zoom trang kết quả; thử camera; xử lý nút Back của Android; trạng thái rỗng / lỗi; ghi log; thử bộ nhớ thấp và ảnh 48 MP thật.
- Có thể: xem trước nhanh khi kéo thanh Độ đậm (mục 5); PDF nén G4 cho trang đen trắng; chọn trang để xuất / xuất một phần.
- Camera trong app: đã có (mục 5h); còn có thể thêm chụp ngang, zoom, chụp liên tục kiểu sách (2 trang).

### Bước 10 -- Phát hành Google Play
- Chạy thử bản **Release** trên máy (đã build được trên PC); keystore ký; versionCode / versionName; AAB.
- Play Console (25 USD một lần), Data safety (không INTERNET, ảnh không rời máy), privacy policy, ảnh chụp màn hình, mô tả.
- Thử ít nhất 5 máy Android (Samsung / Xiaomi / Oppo...). Cập nhật `THIRD-PARTY-NOTICES.md` cho mọi thư viện mới (hiện: MAUI, CommunityToolkit.Mvvm = MIT;
  xUnit, PdfPig chỉ test).

### Để sau (v2)
iOS (cần Mac hoặc Mac cloud: tạo lại `Platforms/iOS`, viết `IImageService` + `IDownloadsService` + camera cho iOS), OCR (ML Kit / Vision),
đồng bộ đám mây.

## 7. Cách làm việc lại

```
# build / cài lên máy (điện thoại cắm USB, bật gỡ lỗi USB)
dotnet build Source/DocScanner/DocScanner.csproj -f net10.0-android -t:Run

# test
dotnet test Source/DocScanner.Core.Tests
dotnet test Source/ImageCore.Shared.Tests
dotnet run --project Source/SmokeTests          # app Windows (baseline ALL PASS)

# app Windows đang chạy (khoá DLL): build ra thư mục khác
dotnet build Source/ImageOptimizerTool/ImageOptimizerTool.csproj -p:OutDir=<thư mục tạm>/
```

- adb: `C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe`. Máy test: Samsung Note 10+ (SM-N975F, Android 12), màn hình đặt 1080x2280 (toạ độ `input tap` theo hệ này).
- Xem dữ liệu app: `adb shell "run-as btk.docscanner ..."`; tài liệu ở `files/documents/{docId}/doc.json`, mỗi trang một thư mục (`original.*`, `thumb.jpg`, `proxy.jpg`, `cropped_{n}.jpg`, `cropped_thumb_{n}.jpg`). Đo độ mượt: `adb shell dumpsys gfxinfo btk.docscanner`.
- Trình chọn ảnh trên máy này là kiểu cũ: **bấm giữ** một ảnh để chọn nhiều, rồi bấm "Chọn".
- Sinh ảnh thử và chạy bộ dò trên PC: xem `Source/Tools/README.md`.

## 8. Quy tắc của owner (bắt buộc)

- **Không tự ý commit code**; không push. Việc đầu mỗi đợt: cập nhật README. Code comment tiếng Anh, README / CLAUDE.md tiếng Việt.
- Sản phẩm bán thương mại: chỉ dùng thư viện MIT / BSD / Apache-2.0, tránh GPL / AGPL; ghi vào `THIRD-PARTY-NOTICES.md`.
- **Máy Note 10+ là máy owner đang dùng cùng lúc**: trước khi gửi `adb input tap/swipe` phải kiểm tra `dumpsys window | grep mCurrentFocus` là `btk.docscanner`; ưu tiên kiểm tra thụ động (screencap cửa sổ app, `run-as`, `gfxinfo`); không giữ ảnh chụp màn hình app khác; dọn ảnh test đã đẩy vào `/sdcard/Pictures`.
- Không ghi đè / xem lại nội dung tài liệu cá nhân của owner ngoài phạm vi đang thử tính năng; xoá bản sao tạm sau khi xem.
