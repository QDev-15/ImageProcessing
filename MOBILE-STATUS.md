# Doc Scanner (app mobile) -- trạng thái dự án và việc còn lại

Cập nhật lần cuối: 2026-09-26 (bước 7 + 8). Đọc file này đầu tiên khi làm tiếp; chi tiết kỹ thuật từng bước nằm ở mục
"App mobile" trong [CLAUDE.md](CLAUDE.md).

## 1. Tóm tắt

App scan tài liệu cho **Android** (.NET MAUI, `net10.0-android`, package `btk.docscanner`, tên "Doc Scanner"): nhập ảnh từ thư viện / camera
-> tự dò mép giấy hoặc kéo 4 điểm (có kính lúp, kéo được ra ngoài ảnh) -> cắt phối cảnh từ ảnh gốc ra trang **A4** -> **Màu / Xám / Đen trắng**
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
| `DocScanner.Core.Tests` | xUnit | **106 test** (+ PdfPig để đọc lại PDF) |
| `ImageCore.Shared.Tests` | xUnit | **60 test** (có bộ cảnh mô phỏng `SceneBuilder` cho bộ dò mép, test bộ nhớ trang A4) |
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

**Mới ở đợt này (bước 7-8):**

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

**Từ trước:**

6. Kéo điểm ra vùng đen rồi Xong; khổ A4 và nút "theo khung"; nút **Xong** trên trang kết quả.
7. Nút **Chụp ảnh / Chụp thêm** (camera hệ thống) -- chưa thử lần nào.
8. Xoá tài liệu bằng vuốt ở Home.
9. Ảnh gốc **48 MP thật** qua bước cắt; máy RAM thấp (3 GB).
10. Bản **Release**: đã build được trên PC (đợt này), **chưa cài / chạy trên máy** (trimming, JSON source-gen, PlatformImage).

## 5. Vấn đề đã biết / nợ kỹ thuật

- **Bộ dò mép chưa tốt với ảnh thật**: mới thử 2 ảnh thật. Cần bộ 20-30 ảnh thật (xem CLAUDE.md, Bước 4). Tốc độ ~1,7 s/trang/worker trên máy.
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

## 6. Việc còn lại (theo thứ tự nên làm)

### Bước 9 -- Hoàn thiện
- Owner thử bước 7-8 trên máy (mục 4); sửa theo phản hồi.
- Chỉnh bộ dò mép trên ảnh thật (mục 5); có thể thêm "kiểm tra bằng chứng ngoài khung" cho tờ bị cắt mép, lọc màu da (bàn tay), giảm thời gian dò.
- Màn hình Cài đặt (kiểu trang mặc định cho ảnh mới, khổ mặc định A4 / theo khung, độ đậm mặc định, chất lượng JPEG); tiếng Việt + tiếng Anh;
  sáng / tối; icon + splash riêng.
- Pinch-zoom trang kết quả; thử camera; xử lý nút Back của Android; trạng thái rỗng / lỗi; ghi log; thử bộ nhớ thấp và ảnh 48 MP thật.
- Có thể: xem trước nhanh khi kéo thanh Độ đậm (mục 5); PDF nén G4 cho trang đen trắng; chọn trang để xuất / xuất một phần.
- Ghi chú UX còn treo: chụp nhiều trang liên tiếp bằng camera trong app (camera hệ thống hiện chỉ chụp 1 ảnh mỗi lần).

### Bước 10 -- Phát hành Google Play
- Chạy thử bản **Release** trên máy (đã build được trên PC); keystore ký; versionCode / versionName; AAB.
- Play Console (25 USD một lần), Data safety (không INTERNET, ảnh không rời máy), privacy policy, ảnh chụp màn hình, mô tả.
- Thử ít nhất 5 máy Android (Samsung / Xiaomi / Oppo...). Cập nhật `THIRD-PARTY-NOTICES.md` cho mọi thư viện mới (hiện: MAUI, CommunityToolkit.Mvvm = MIT;
  xUnit, PdfPig chỉ test).

### Để sau (v2)
iOS (cần Mac hoặc Mac cloud: tạo lại `Platforms/iOS`, viết `IImageService` + `IDownloadsService` cho iOS), OCR (ML Kit / Vision), camera tuỳ biến có khung dò
thời gian thực, tự chụp khi ổn định, đồng bộ đám mây.

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
