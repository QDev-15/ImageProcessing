# Thành phần bên thứ ba -- license và bằng sáng chế

Rà soát ngày 2026-09-25 cho bản phát hành thương mại của Image Optimizer Tool.
**Không có thành phần GPL / LGPL / AGPL.** Mọi thành phần dưới đây cho phép dùng và phân phối
thương mại, với điều kiện giữ thông báo bản quyền / license (file này được cài kèm app).

> Đây là rà soát kỹ thuật, không phải ý kiến pháp lý. Trước khi bán, nên nhờ luật sư xác nhận
> các mục đánh dấu ⚠.

## Thư viện .NET (NuGet)

| Thành phần | Phiên bản | License | Ghi chú |
|---|---|---|---|
| .NET Runtime + Windows Forms (self-contained) | 9.0 | MIT | |
| PDFsharp | 6.2.4 | MIT | Ghi PDF. Có kèm `sRGB2014.icc` (xem bên dưới). |
| PdfiumViewer | 2.13.0 | Apache-2.0 | ⚠ Project đã ngừng phát triển (archived); nên thay bằng wrapper PDFium khác khi có điều kiện. |
| PdfiumViewer.Native.x86_64.v8-xfa (`pdfium.dll`) | 2018.4.8 | BSD-3-Clause (PDFium) + các license kiểu BSD của thành phần con (V8, FreeType, libjpeg, zlib...) | |
| BitMiracle.LibTiff.NET | 2.4.649 | BSD-3-Clause | |
| NTwain | 4.0.0-beta.4 | MIT | ⚠ Bản beta. |
| Tesseract (wrapper .NET, charlesw) | 5.2.0 | Apache-2.0 | Kèm `tesseract50.dll` (Apache-2.0) và `leptonica-1.82.0.dll` (Leptonica license, kiểu BSD-2). |
| ZXing.Net | 0.16.10 | Apache-2.0 | Đọc barcode để tách tài liệu. |

## Dữ liệu

| Thành phần | License | Nguồn |
|---|---|---|
| `tessdata/vie.traineddata`, `eng.traineddata` | Apache-2.0 | github.com/tesseract-ocr/tessdata_fast |
| `tessdata/osd.traineddata` | Apache-2.0 | github.com/tesseract-ocr/tessdata |
| `Resources/pdf.ttf` (GlyphLessFont, font vô hình cho lớp chữ OCR) | Apache-2.0 | github.com/tesseract-ocr/tesseract (tessdata/pdf.ttf) |
| `sRGB2014.icc` (OutputIntent PDF/A, lấy từ resource của PDFsharp) | License của ICC: được dùng, sao chép, phân phối miễn phí | color.org |

## Công cụ chạy ngoài (thư mục `tools\`)

| Thành phần | License | Ghi chú |
|---|---|---|
| OpenJPEG 2.5.4 (`opj_compress.exe`, `openjp2.dll`) | BSD-2-Clause | Bản build chính thức của dự án OpenJPEG. |
| jbig2enc 0.29 (`jbig2.exe`) | Apache-2.0 | ⚠ Bản Windows do bên thứ ba build (SourceForge), không phải từ dự án gốc agl/jbig2enc. Nên tự build lại từ source trước khi bán. |
| leptonica 1.76.0 (đi kèm jbig2enc) | Leptonica license (kiểu BSD-2) | |

## Microsoft Visual C++ Runtime (triển khai trong thư mục app)

| File | Dùng cho |
|---|---|
| `vcruntime140.dll`, `vcruntime140_1.dll`, `msvcp140.dll` (x64, 14.44) | tesseract50.dll / leptonica-1.82.0.dll |
| `tools\jbig2enc\msvcr120.dll` (x86, VC++ 2013) | leptonica-1.76.0.dll của jbig2enc |
| `tools\openjpeg\vcruntime140.dll`, `msvcp140*.dll`, `concrt140.dll` (x86) | OpenJPEG |

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
| JPEG 2000 Part 1 | Ủy ban JPEG có chính sách Part 1 miễn phí bản quyền (royalty-free) từ các bên đóng góp chính. ⚠ Không loại trừ hoàn toàn bằng sáng chế của bên thứ ba. |
| JBIG2 (ISO/IEC 14492:2001) | Có các khai báo bằng sáng chế lịch sử (xem `tools/jbig2enc/PATENTS.txt`). Chuẩn công bố năm 2000-2001, nên các bằng sáng chế cốt lõi nộp giai đoạn đó hẳn đã quá thời hạn 20 năm. ⚠ Cần luật sư xác nhận trước khi bán. JBIG2 là tuỳ chọn và **mặc định TẮT** (mặc định dùng CCITT G4). |

Ngoài ra, JBIG2 ở chế độ Symbol có thể **thay nhầm ký tự rất giống nhau** (sự cố máy Xerox năm
2013, 6 ↔ 8). App mặc định dùng ngưỡng 0.92 (thận trọng) và có chế độ `Lossless` để loại bỏ
hoàn toàn rủi ro này. Với tài liệu pháp lý / tài chính nên dùng `Lossless` hoặc CCITT G4.
