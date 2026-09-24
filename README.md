# ImageProcessing

- **Image Optimizer Tool**: app scan tài liệu (WinForms .NET 9). Tài liệu chính:
  [Source/ImageOptimizerTool/README.md](Source/ImageOptimizerTool/README.md).
- Quy tắc làm việc, danh sách việc và báo cáo từng đợt: [CLAUDE.md](CLAUDE.md).
- License bên thứ ba: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

```
dotnet build Source/ImageProcessing.sln                                    # build
dotnet run --project Source/SmokeTests                                     # kiểm tra end-to-end
powershell -ExecutionPolicy Bypass -File Installer\build-installer.ps1     # tạo MSI
```
