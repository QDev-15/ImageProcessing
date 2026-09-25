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
- License bên thứ ba: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

```
dotnet build Source/ImageProcessing.sln                                    # build
dotnet run --project Source/SmokeTests                                     # kiểm tra end-to-end
dotnet test Source/ImageCore.Shared.Tests                                  # unit test thuật toán dùng chung
dotnet test Source/DocScanner.Core.Tests                                   # unit test logic app mobile
dotnet build Source/DocScanner/DocScanner.csproj -f net10.0-android -t:Run  # cài lên điện thoại (cắm USB)
powershell -ExecutionPolicy Bypass -File Installer\build-installer.ps1     # tạo MSI
```
