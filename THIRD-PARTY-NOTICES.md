# Thành phần bên thứ ba -- license và bằng sáng chế

Rà soát ngày 2026-09-25 cho bản phát hành thương mại của Image Optimizer Tool.
**Không có thành phần GPL / LGPL / AGPL.** Mọi thành phần dưới đây cho phép dùng và phân phối
thương mại, với điều kiện giữ thông báo bản quyền / license (file này được cài kèm app).

> Đây là rà soát kỹ thuật, không phải ý kiến pháp lý. Trước khi bán, nên nhờ luật sư xác nhận
> các mục đánh dấu ⚠.

## Thư viện .NET (NuGet)

| Thành phần | Phiên bản | License | Ghi chú |
|---|---|---|---|
| .NET Runtime + Windows Forms (self-contained)        | 9.0           | MIT    | |
| PDFsharp                                             | 6.2.4         | MIT    | Ghi PDF. Có kèm `sRGB2014.icc` (xem bên dưới). |
| PdfiumViewer                                         | 2.13.0        | Apache-2.0 | ⚠ Project đã ngừng phát triển (archived); nên thay bằng wrapper PDFium khác khi có điều kiện. |
| PdfiumViewer.Native.x86_64.v8-xfa (`pdfium.dll`)     | 2018.4.8      | BSD-3-Clause (PDFium) + các license kiểu BSD của thành phần con (V8, FreeType, libjpeg, zlib...) | |
| BitMiracle.LibTiff.NET                               | 2.4.649       | BSD-3-Clause | |
| NTwain                                               | 4.0.0-beta.4  | MIT    | ⚠ Bản beta. |
| Tesseract (wrapper .NET, charlesw)                   | 5.2.0         | Apache-2.0 | Kèm `tesseract50.dll` (Apache-2.0) và `leptonica-1.82.0.dll` (Leptonica license, kiểu BSD-2). |
| ZXing.Net                                            | 0.16.10       | Apache-2.0 | Đọc barcode để tách tài liệu. |
| .NET MAUI (Microsoft.Maui.Controls), app mobile `DocScanner` | 10.x | MIT | Chỉ app Android, không nằm trong app WinForms. |
| CommunityToolkit.Mvvm                                | 8.4.0         | MIT    | App mobile `DocScanner`. |
| AndroidX CameraX (Xamarin.AndroidX.Camera.Camera2 / Lifecycle / View, kéo theo Camera.Core) | 1.6.2 | Apache-2.0 (thư viện Google) + MIT (binding .NET) | Camera trong app mobile (dò tờ giấy trực tiếp, tự chụp). Kèm Xamarin.AndroidX.Fragment.Ktx 1.9.0 / Collection.Ktx 1.6.0.1 (Apache-2.0 + MIT) để khớp phiên bản. |
| Plugin.InAppBilling (jamesmontemagno) | 10.0.0 | MIT | Gọi Google Play Billing để bán gói Pro (mua đứt, xem "Giấy phép & khuyến mãi" trong MOBILE-STATUS.md). Kéo theo `Xamarin.Android.Google.BillingClient` (binding .NET của thư viện Play Billing Library chính chủ Google — điều khoản riêng của Google, xem license kèm gói) và `Xamarin.GooglePlayServices.Base/Basement/Tasks` (Google, điều khoản Google Play Services). Không thu thập / gửi dữ liệu người dùng nào ngoài luồng mua hàng qua Play. |
| xUnit / Microsoft.NET.Test.Sdk (chỉ project test)    | 2.9.2 / 17.12.0 | Apache-2.0 / MIT | Không phân phối kèm app. |
| PdfPig (UglyToad.PdfPig, chỉ project test)          | 0.1.16        | Apache-2.0 | Đọc lại PDF xuất ra trong `DocScanner.Core.Tests`. Không phân phối kèm app. |

## Dữ liệu

| Thành phần | License | Nguồn |
|---|---|---|
| `tessdata/vie.traineddata`, `eng.traineddata` | Apache-2.0 | github.com/tesseract-ocr/tessdata_fast |
| `tessdata/osd.traineddata` | Apache-2.0 | github.com/tesseract-ocr/tessdata |
| `Resources/pdf.ttf` (GlyphLessFont, font vô hình cho lớp chữ OCR) | Apache-2.0 | github.com/tesseract-ocr/tesseract (tessdata/pdf.ttf) |
| `sRGB2014.icc` (OutputIntent PDF/A, lấy từ resource của PDFsharp) | License của ICC: được dùng, sao chép, phân phối miễn phí | color.org |
| `DocScanner/Resources/Fonts/MaterialIcons-Regular.ttf` (biểu tượng giao diện app mobile) | Apache-2.0 (Google) | github.com/google/material-design-icons (thư mục `font/`) |
| `DocScanner/Resources/Fonts/OpenSans-Regular.ttf`, `OpenSans-Semibold.ttf` (chữ giao diện app mobile, đi kèm mẫu dự án .NET MAUI) | SIL Open Font License 1.1 (dùng thương mại được, được nhúng trong app; không bán riêng font) | fonts.google.com/specimen/Open+Sans |

**Công cụ chạy ngoài (thư mục `tools\`): không còn thành phần nào** (2026-09-29). Từng có OpenJPEG
2.5.4 (`opj_compress.exe`, BSD-2-Clause) và jbig2enc 0.29 (`jbig2.exe`, Apache-2.0) -- cả hai đã bị
owner gỡ bỏ hoàn toàn khỏi app (xem mục Codec bên dưới).

## Microsoft Visual C++ Runtime (triển khai trong thư mục app)

| File | Dùng cho |
|---|---|
| `vcruntime140.dll`, `vcruntime140_1.dll`, `msvcp140.dll` (x64, 14.44) | tesseract50.dll / leptonica-1.82.0.dll |

Các file này thuộc danh sách "Redistributable Code" của Visual Studio, được phép phân phối kèm
ứng dụng. ⚠ Các file x64 / msvcr120 hiện được copy từ máy dev (System32 / SysWOW64). Với bản
phát hành chính thức, nên lấy từ thư mục `VC\Redist` của bộ cài Visual Studio, hoặc cho bộ cài
chạy VC++ Redistributable chính thức.

## Công cụ build (không phân phối kèm app)

- WiX Toolset v5.0.2 (MS-RL): chỉ dùng để tạo MSI. File MSI tạo ra không bị ràng buộc bởi MS-RL.
  Không nâng lên WiX v6+ nếu chưa xem xét "Open Source Maintenance Fee" của v6.

## Thành phần của Windows (không phân phối kèm)

- WIA Automation Library (`wiaaut.dll`): có sẵn trong Windows, gọi qua COM.
- TWAIN Data Source Manager (`twaindsm.dll`): do driver máy scan cài đặt.

## Bằng sáng chế (codec)

| Codec | Đánh giá |
|---|---|
| CCITT G4 (T.6, 1988) | Các bằng sáng chế đã hết hạn từ lâu. |
| JPEG baseline (1992) | Các bằng sáng chế đã hết hạn (các vụ Forgent / "JPEG patent" đều hết hạn trước 2007-2011). |

**JBIG2 đã bị gỡ bỏ hoàn toàn khỏi app** (quyết định của owner, 2026-09-29): không còn `JBig2Encoder.cs`, không còn
`tools/jbig2enc/` (đã xoá khỏi repo), không còn tuỳ chọn JBIG2 trong Cài đặt. Trang trắng đen giờ luôn dùng **CCITT
G4** -- bằng sáng chế đã hết hạn từ lâu, không có rủi ro thay nhầm ký tự (sự cố máy Xerox năm 2013, 6 ↔ 8, chỉ áp
dụng cho JBIG2 chế độ Symbol) và không phải theo dõi nguồn gốc bản build / bằng sáng chế lịch sử của JBIG2 nữa.

**JPEG 2000 (Part 1) cũng đã bị gỡ bỏ hoàn toàn khỏi app** (quyết định của owner, cùng ngày 2026-09-29, lý do tốc
độ chứ không phải bằng sáng chế -- Part 1 vốn miễn phí bản quyền theo chính sách của uỷ ban JPEG, dù không loại trừ
hoàn toàn bằng sáng chế của bên thứ ba ⚠): không còn `OpenJpegEncoder.cs`, không còn `tools/openjpeg/`, không còn
tuỳ chọn JPEG2000 trong Cài đặt. Trang xám/màu giờ luôn dùng **JPEG** -- đo thật cho thấy JPEG2000 (gọi
`opj_compress.exe` cho từng trang) chậm hơn JPEG khoảng 25 lần khi xuất, dù file nhỏ hơn khoảng 2 lần; owner chọn
tốc độ.
