# ImageProcessing

- **Image Optimizer Tool**: app scan tài liệu (WinForms .NET 9). Tài liệu chính:
  [Source/ImageOptimizerTool/README.md](Source/ImageOptimizerTool/README.md).
- Quy tắc làm việc, danh sách việc và báo cáo từng đợt: [CLAUDE.md](CLAUDE.md).
- **ImageCore.Shared** (`Source/ImageCore.Shared`): thuật toán ảnh thuần managed (GrayImage, Sauvola /
  Otsu tiết kiệm RAM, làm phẳng nền / bóng, bộ lọc Màu / Xám / Đen trắng, ghi PNG 1-bit, deskew, dò viền đen,
  khử đốm, dò mép giấy, nắn phối cảnh), không GDI+, không native. Dùng chung cho app Windows và
  app mobile (.NET MAUI, Android trước). Test: `dotnet test Source/ImageCore.Shared.Tests`.
- **Doc Scanner** (`Source/DocScanner`): app mobile .NET MAUI, Android trước (package `btk.docscanner`).
  Solution: `Source/DocScanner.slnx`. Logic thuần .NET ở `Source/DocScanner.Core` (test:
  `dotnet test Source/DocScanner.Core.Tests`). Tiến độ và cách chạy trên máy thật: mục "App mobile" trong
  [CLAUDE.md](CLAUDE.md).
- Trạng thái app mobile và việc còn lại: [MOBILE-STATUS.md](MOBILE-STATUS.md).
  Hiện đã xong bước 1-8 (đến đen trắng, quản lý trang, xuất PDF, chia sẻ); còn bước 9 (hoàn thiện) và 10 (phát hành).
- **Đợt tối ưu tốc độ app mobile (2026-09-26)**: dò mép nhanh ~3,8 lần (kết quả giữ nguyên từng bit), bộ lọc
  Xám / Đen trắng nhanh ~1,3-1,8 lần, trang được **dựng sẵn ở nền** sau khi dò mép (mở "Kết quả" / xuất PDF gần như tức thì),
  đọc / ghi điểm ảnh Android không qua mảng trung gian. Đo lại bằng `dotnet run -c Release --project Source/MobileBench`.
  Chi tiết: MOBILE-STATUS.md mục "Tối ưu tốc độ".
- **Nhập ảnh chạy nền (2026-09-27)**: chọn ảnh xong vào ngay tài liệu, ảnh được chép ở nền (không còn spinner chặn),
  trạng thái "Đang nhập x/y" + nút Dừng trong tài liệu, vẫn còn khi thoát ra vào lại (thêm 100 ảnh không treo app).
  Trình chọn ảnh riêng (MAUI chép mọi ảnh vào cache trên luồng UI). Chi tiết: MOBILE-STATUS.md mục 5c.
- **Đợt 2026-09-27b**: ô "Đang tải..." cho từng ảnh ngay khi chọn (cập nhật tại chỗ, có thanh tiến trình, không giật
  danh sách; trang chưa tải xong bị khoá chỉnh sửa); màn kết quả xem trước tức thì khi đổi Màu / Xám / Đen trắng, thêm thanh
  Độ sáng / Độ tương phản kéo mượt (ColorMatrix GPU), nút Xoay trái / phải ở màn kết quả. Chi tiết: MOBILE-STATUS.md mục 5d.
- **Thiết kế lại giao diện (2026-09-27c)**: gọn theo phong cách app TapScanner (chỉ tham khảo bố cục / luồng, không dùng
  tên, logo, hình ảnh của họ): thanh công cụ biểu tượng + chữ nhỏ, nút chụp tròn ở giữa màn chính, ảnh chiếm phần lớn màn hình khi chỉnh.
  Biểu tượng: font Material Icons (Apache-2.0). Chi tiết: MOBILE-STATUS.md mục 5e.
- **Đợt 2026-09-27d**: so với TapScanner (thêm thẻ bộ lọc có ảnh xem trước) + đo bản Release, tối ưu: chép ảnh nhập ~4x, proxy ~4x,
  mở màn kết quả ~4x (dùng lại bản dựng sẵn), Release AOT + LLVM (dò mép -40%). Log thời gian: `adb logcat -s DocScanPerf`. Chi tiết: MOBILE-STATUS.md mục 5f.
- **Đợt 2026-09-27e**: mượt hơn khi chỉnh ảnh: pipeline xem trước có bộ nhớ đệm từng bước (đổi độ đậm / độ sáng chỉ
  so lại ngưỡng), xoay tức thì (hoán vị dữ liệu + hiệu ứng GPU), việc nền giới hạn lõi CPU và huỷ khi lỗi thời, tái sử dụng bitmap;
  thêm Độ sáng cho chế độ Đen trắng. **Đo tốc độ bằng bản Release** (Debug chạy trình thông dịch, chậm 10-30 lần; đã chuyển Debug sang JIT).
  Chi tiết: MOBILE-STATUS.md mục 5g.
- **Đợt 2026-09-27f**: sửa crash khi chụp ảnh bằng camera (MAUI đòi quyền bộ nhớ trên Android 12 -> tự gọi app camera qua
  FileProvider); nắn tài liệu theo **tỉ lệ thật** (tính từ phối cảnh, không còn bẹp / giãn) và theo **cạnh cong** của tờ giấy cầm tay
  (tìm lại mép giấy trên ảnh 1600 px); **camera trong app** (CameraX): khung tờ giấy hiện trực tiếp khi ngắm, tự chụp khi giữ yên,
  chụp nhiều trang liên tiếp. Đã thử trên máy ảo; **chưa thử camera mới trên Note 10+**. Chi tiết: MOBILE-STATUS.md mục 5h.
- **Đợt 2026-09-27g**: đen trắng sắc nét hơn cho ảnh chụp camera (làm nét trước khi phân ngưỡng + mép mềm; giữ được nét mảnh và dấu
  tiếng Việt, PDF Nhỏ / Vừa vẫn 1-bit); trình xem ảnh (zoom / kéo / vuốt); công tắc Xem / Sửa trong tài liệu; màn Sửa mở sẵn Bộ lọc
  (đứng đầu menu) và zoom được; chữ ký tay trên trang (vẽ, lưu, đặt, đổi cỡ; có trong PDF); trình xem PDF trong app. Đã thử trên máy ảo,
  chưa thử trên Note 10+. Chi tiết: MOBILE-STATUS.md mục 5i.
- **Đợt 2026-09-27h**: sửa lỗi trình xem PDF (trang sau bị đen: trang PdfRenderer không được đóng); sửa lỗi trang cắt ra dài hơn A4
  (tiêu cự "đo" từ 4 góc sai trên ảnh thật -> dùng tiêu cự điện thoại cố định, trang có dáng tờ giấy ra đúng A4; "Tài liệu 1" cả 10
  trang ra A4); màn chính: tìm kiếm (không dấu), thư mục, chọn nhiều / kéo thả vào thư mục / xoá; màn Cài đặt + Thông tin ứng dụng;
  tự cập nhật lúc 01:00 (hướng dẫn phát hành: [UPDATE-SERVER.md](UPDATE-SERVER.md)); màn khởi động. Phiên bản 1.1 (versionCode 2).
  Chi tiết: MOBILE-STATUS.md mục 5j.
- **Đợt 2026-09-28**: License / bán hàng qua **Google Play Billing** (mua đứt "Pro" một lần, gói `pro_upgrade`); dùng thử **5 lượt xuất PDF
  miễn phí** rồi mới yêu cầu mua (chụp/chỉnh sửa không giới hạn); mục "GÓI PRO" trong Cài đặt; mã giảm giá dùng thẳng "Mã khuyến mãi" có sẵn của
  Play Console, không cần máy chủ riêng. Chi tiết: MOBILE-STATUS.md mục 5k. Cài lên Note 10+, giao diện hiện đúng; **chưa mua thử thật** (cần tạo
  sản phẩm trên Play Console trước).
- License bên thứ ba: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

```
dotnet build Source/ImageProcessing.sln                                    # build
dotnet run --project Source/SmokeTests                                     # kiểm tra end-to-end
dotnet test Source/ImageCore.Shared.Tests                                  # unit test thuật toán dùng chung
dotnet test Source/DocScanner.Core.Tests                                   # unit test logic app mobile
dotnet build Source/DocScanner/DocScanner.csproj -f net10.0-android -t:Run  # cài lên điện thoại (cắm USB)
powershell -ExecutionPolicy Bypass -File Installer\build-installer.ps1     # tạo MSI
```
