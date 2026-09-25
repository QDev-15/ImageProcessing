# ImageProcessing

- **Image Optimizer Tool**: app scan tài liệu (WinForms .NET 9). Tài liệu chính:
  [Source/ImageOptimizerTool/README.md](Source/ImageOptimizerTool/README.md).
- Quy tắc làm việc, danh sách việc và báo cáo từng đợt: [CLAUDE.md](CLAUDE.md).
- **ImageCore.Shared** (`Source/ImageCore.Shared`): thuật toán ảnh thuần managed (GrayImage, Sauvola /
  Otsu, deskew, dò viền đen, khử đốm), không GDI+, không native. Dùng chung cho app Windows và
  app mobile (.NET MAUI, Android trước). Test: `dotnet test Source/ImageCore.Shared.Tests`.
- **Doc Scanner** (`Source/DocScanner`): app mobile .NET MAUI, Android trước (package `btk.docscanner`).
  Solution riêng: `Source/DocScanner.slnx`. Tiến độ và cách chạy trên máy thật: mục "App mobile" trong
  [CLAUDE.md](CLAUDE.md).
- License bên thứ ba: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

```
dotnet build Source/ImageProcessing.sln                                    # build
dotnet run --project Source/SmokeTests                                     # kiểm tra end-to-end
dotnet test Source/ImageCore.Shared.Tests                                  # unit test thuật toán dùng chung
powershell -ExecutionPolicy Bypass -File Installer\build-installer.ps1     # tạo MSI
```
