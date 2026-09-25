# Doc Scanner (app mobile) -- trạng thái dự án và việc còn lại

Cập nhật lần cuối: 2026-09-25 (cuối phiên). Đọc file này đầu tiên khi làm tiếp; chi tiết kỹ thuật từng bước nằm ở mục
"App mobile" trong [CLAUDE.md](CLAUDE.md).

## 1. Tóm tắt

App scan tài liệu cho **Android** (.NET MAUI, `net10.0-android`, package `btk.docscanner`, tên "Doc Scanner"): nhập ảnh từ thư viện / camera
-> tự dò mép giấy hoặc kéo 4 điểm (có kính lúp, kéo được ra ngoài ảnh) -> cắt phối cảnh từ ảnh gốc ra trang **A4** -> (bước 7) đen trắng
-> (bước 8) nhiều trang + xuất PDF. Không OCR ở bản đầu. iOS để sau (owner chưa có Mac).

**Đã xong: bước 1-6** (+ nhập ảnh nhanh chạy nền). **Chưa làm: bước 7 (đen trắng), 8 (quản lý nhiều trang + xuất PDF), 9 (hoàn thiện), 10 (phát hành).**
Tất cả code hiện chưa commit (branch `feature/document-scanner`, commit cuối `c176916`; ~41 file thay đổi / mới). Owner đã đổi quy tắc:
**"Không tự ý commit code"** -- chỉ commit khi owner yêu cầu, không push.

## 2. Bản đồ dự án (`Source/`)

| Project | Loại | Vai trò |
|---|---|---|
| `ImageCore.Shared` | net9.0, thuần managed | Thuật toán ảnh dùng chung Windows + mobile: `GrayImage`, `RgbImage`, `Binarizer` (Sauvola / Otsu), `DocumentCleanup` (deskew, viền đen, khử đốm), `DocumentEdgeDetector` (dò mép giấy), `PerspectiveWarp` (nắn phối cảnh, khổ A4), `Homography`, `Quad` |
| `DocScanner.Core` | net9.0, thuần .NET | Logic app, test được trên PC: `DocumentStore` (kho tài liệu, 1 bản in-memory duy nhất), `PageRecord` / `DocumentRecord`, `ImportService`, `PageIngestQueue` (hàng đợi nền), `CropDetectionService`, `CropRenderService`, `CropPlanner`, `PageEditService`, `ImageGeometry`, `IImageService` |
| `DocScanner` | net10.0-android | UI MAUI (MVVM): trang Home / Document / Crop (`QuadEditor`) / Result; `AndroidImageService` (BitmapFactory, ExifInterface, BitmapRegionDecoder); `ImportCoordinator` (MediaPicker) |
| `DocScanner.Core.Tests` | xUnit | **91 test** |
| `ImageCore.Shared.Tests` | xUnit | **47 test** (có bộ cảnh mô phỏng `SceneBuilder` cho bộ dò mép) |
| `ImageCoreService`, `ImageOptimizerTool`, `SmokeTests`, `Bench` | WinForms cũ | App Windows từ đợt trước; SmokeTests ALL PASS (baseline sau bước 1). Không đụng tới ở phiên mobile ngoài việc tách GDI+ (`GdiGray`, `BitmapTransforms`) |
| `Tools/` | công cụ tay | `EdgeProbe` (chạy bộ dò mép trên PC, vẽ khung ra PNG) và `gen_test_scans.py` (sinh ảnh giấy trên bàn); xem `Tools/README.md` |

Solution: `Source/DocScanner.slnx` (mobile) và `Source/ImageProcessing.sln` (owner đã thêm `DocScanner` vào đây, nên build sln này cần workload MAUI Android).

## 3. Đã làm và đã kiểm chứng ở đâu

| # | Nội dung | Kiểm chứng |
|---|---|---|
| 1 | Tách `ImageCore.Shared` khỏi System.Drawing | SmokeTests ALL PASS, unit test |
| 2 | Khung app MAUI, quyền camera (chỉ `CAMERA`, không `INTERNET`) | Chạy trên Note 10+ |
| 3 | Nhập ảnh (chọn nhiều / camera hệ thống), EXIF, ảnh 48 MP | **Máy thật**: WebP, JPEG 12 & 48 MP, EXIF 6/3/8, PNG trong suốt; không crash |
| 3b | **Nhập nhanh**: chỉ chép bản gốc, trang hiện ngay ở trạng thái `Pending`; nền làm thumbnail -> proxy 1600 px -> dò mép (2 worker, ưu tiên nghiêm ngặt); khôi phục sau khi app bị tắt (`ResumePending`) | **Máy thật**: 6 trang có thumbnail sau ~3 s, không kẹt spinner (đã sửa lỗi `Progress<T>` gửi callback muộn) |
| 4 | Dò mép giấy (Canny 3 kênh, Hough có trọng số theo độ mạnh cạnh, chấm điểm tứ giác) | 36 test mô phỏng (IoU >= 0,92); **máy thật**: 2 ảnh thật (1 đúng, 1 sai; xem mục 5) |
| 5 | Trình chỉnh 4 điểm: kéo góc / cạnh / cả khung, kính lúp 3x, chặn khung lồi ngược, Tự động, Toàn ảnh, **Xoay 90°**, Xong; lưu `CropManual` | **Máy thật**: kéo mượt (168 khung / 3 s, p99 15 ms), khung được lưu và khôi phục; Xoay 90° owner đã thử OK |
| 6 | Cắt phối cảnh từ **ảnh gốc** (giải mã vùng, đổi khung về toạ độ file theo EXIF + xoay), trang kết quả, thumbnail đã cắt trong danh sách | **Máy thật**: ảnh thật 1996x2582 nắn thẳng đúng |
| 6b | Kéo điểm ra **vùng đen ngoài ảnh** (tối đa 20% mỗi phía; phần ngoài ảnh điền trắng) | Test PC; owner yêu cầu, **chưa xác nhận trên máy** |
| 6c | Trang cắt ra khổ **A4** (dọc / ngang theo khung, tối đa 3508x2480), nút "Khổ giấy: A4 / theo khung" trên trang kết quả | Test PC; **chưa xác nhận trên máy** |

## 4. Chưa kiểm chứng trên máy thật (cần owner bấm thử, hoặc tự thử khi có máy)

1. Kéo điểm ra vùng đen (trang 2 tài liệu 15:10) rồi Xong: trang kết quả có đúng hình, phần ngoài ảnh có trắng không.
2. Khổ A4 và nút đổi "theo khung"; các trang đã cắt trước đó phải tự cắt lại khi mở trang kết quả.
3. Nút **Xong** trên trang kết quả (điều hướng `../..` về danh sách trang).
4. Nút **Chụp ảnh / Chụp thêm** (camera hệ thống) -- chưa thử lần nào.
5. Xoá tài liệu bằng vuốt ở Home.
6. Ảnh gốc **48 MP thật** đi qua bước cắt (đường giải mã vùng + dự phòng); thời gian cắt trên máy chưa đo.
7. Máy RAM thấp (3 GB); `android:largeHeap` đã bật nhưng chưa thử.
8. Bản **Release** chưa từng build / chạy (trimming, JSON source-gen, PlatformImage).

## 5. Vấn đề đã biết / nợ kỹ thuật

- **Bộ dò mép chưa tốt với ảnh thật**: mới thử 2 ảnh thật. Ảnh 1 (tờ cầm tay trên bàn gỗ): đúng nhưng mất ~8% bề ngang phía phải. Ảnh 2 (tờ bị khung cắt mép trái, tài liệu 15:10 trang 2): khung hình diều, tin cậy 50%. Ca dự kiến yếu: giấy trắng trên bàn trắng, nhiều tờ chồng, tay che mép, tờ bị khung cắt. Cần bộ 20-30 ảnh thật. Tốc độ trên máy ~1,7 s/trang/worker (nền, không chặn UI); có thể giảm `AnalysisEdge` 480 -> 360.
- Chỉ có test tự động cho `ImageCore.Shared` và `DocScanner.Core`; **ViewModel / XAML / QuadEditor không có test tự động** (kiểm bằng tay + adb).
- `doc.json` ghi tiếng Việt dạng escape unicode (hợp lệ, khó đọc). Tên tài liệu chưa đổi được. Ô tài liệu ở Home luôn dùng thumbnail trang đầu (bản gốc, chưa cắt).
- Trang kết quả chưa có phóng to / kéo (pinch). Icon và splash vẫn là mặc định của template MAUI.
- `DocumentEdgeDetector.Trace` là hook debug (giữ lại vì hữu ích khi chỉnh).
- `Source/DocScanner/DocScanner.csproj.user` và `Source/DocScanner/Properties/launchSettings.json` do Visual Studio sinh, **không nên commit** (nên thêm vào `.gitignore`).
- Bước 7 phải cẩn thận RAM: `Binarizer.Sauvola` dùng 2 mảng `long` (~16 byte / điểm) = ~140 MB cho ảnh 8,7 MP.
- Lưu JPEG q94 rồi chạy đen trắng trên ảnh đã nén sẽ nén hai lần: bước 7 phải chạy trên `RgbImage` sau warp (trong RAM).
- Dữ liệu test còn trên máy owner: 3 tài liệu (`14:16` 9 trang, `15:10` ~33 trang gồm ảnh thật của owner, `15:30` 12 trang giả lập); an toàn để xoá.

## 6. Việc còn lại (theo thứ tự nên làm)

### Bước 7 -- Đen trắng (Sauvola / Otsu)
- Chạy trên `RgbImage` ngay sau `PerspectiveWarp` (mở rộng `CropRenderService`, trả ảnh trong RAM); không đọc lại JPEG.
- Chế độ mỗi trang (lưu trong `PageRecord`, tính vào `NeedsRender`): **Màu / Xám / Đen trắng** (Sauvola mặc định `k = 0,34`, tuỳ chọn Otsu), thanh trượt độ đậm, khử đốm (`DocumentCleanup.Despeckle`), tuỳ chọn làm phẳng nền / bóng. Có deskew nhỏ sau khi cắt nếu cần (`DetectSkew`).
- **Giới hạn RAM**: viết bản Sauvola tiết kiệm (xử lý theo dải / tile, hoặc integral image `uint` + bình phương theo dải) cho ảnh 8,7 MP; xem trước trên bản thu nhỏ rồi mới chạy full khi lưu.
- Lưu đầu ra: màu / xám -> JPEG; đen trắng -> PNG 8-bit (Android không có bitmap 1-bit) hoặc buffer 1-bit riêng cho PDF.
- UI: chọn chế độ trên `ResultPage` (chip + thanh trượt), thumbnail trong danh sách theo chế độ.
- Test: pipeline đen trắng, so bản tiết kiệm RAM với bản gốc, ước lượng bộ nhớ.

### Bước 8 -- Nhiều trang, sắp xếp, xuất PDF, chia sẻ
- Danh sách trang: kéo thả đổi thứ tự, xoá nhiều trang, chèn / thêm trang giữa, đổi tên tài liệu, hoàn tác.
- **Xuất PDF**: PdfSharp 6.2.4 (MIT) đã dùng ở app Windows -- kiểm tra chạy trên Android. Trang PDF cỡ A4 (595x842 pt) theo khổ trang. Màu / xám nhúng JPEG nguyên byte; đen trắng -> CCITT G4 (xem `ImageCoreService/G4Encoder.cs`, `PdfPagePacker.cs`, `TiffPagePacker.cs` để tái dùng / chuyển sang Shared nếu thuần managed). Trang chưa có bản dựng thì dựng trước (`EnqueueRender` rồi chờ). Tiến độ + nút huỷ.
- Chia sẻ (`Share.Default.RequestAsync`), lưu vào Downloads (MediaStore), đặt tên file theo tên tài liệu.
- Không OCR; không PDF/A ở bản đầu.

### Bước 9 -- Hoàn thiện
- Chỉnh bộ dò mép trên ảnh thật (mục 5); có thể thêm "kiểm tra bằng chứng ngoài khung" cho tờ bị cắt mép, lọc màu da (bàn tay), giảm thời gian dò.
- Màn hình Cài đặt (khổ mặc định A4 / theo khung, tham số đen trắng, chất lượng xuất); tiếng Việt + tiếng Anh; sáng / tối; icon + splash riêng.
- Pinch-zoom trang kết quả; thử camera; xử lý nút Back của Android; trạng thái rỗng / lỗi; ghi log; thử bộ nhớ thấp và ảnh 48 MP thật.
- Ghi chú UX còn treo: chụp nhiều trang liên tiếp bằng camera trong app (camera hệ thống hiện chỉ chụp 1 ảnh mỗi lần).

### Bước 10 -- Phát hành Google Play
- Build và thử **Release** (trimming, source-gen JSON, `PlatformImage`); keystore ký; versionCode / versionName; AAB.
- Play Console (25 USD một lần), Data safety (không INTERNET, ảnh không rời máy), privacy policy, ảnh chụp màn hình, mô tả.
- Thử ít nhất 5 máy Android (Samsung / Xiaomi / Oppo...). Cập nhật `THIRD-PARTY-NOTICES.md` cho mọi thư viện mới (hiện: MAUI, CommunityToolkit.Mvvm = MIT; xUnit chỉ test).

### Để sau (v2)
iOS (cần Mac hoặc Mac cloud: tạo lại `Platforms/iOS`, viết `IImageService` cho iOS), OCR (ML Kit / Vision), camera tuỳ biến có khung dò thời gian thực, tự chụp khi ổn định, đồng bộ đám mây.

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
