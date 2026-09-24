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
- Làm trên branch `feature/document-scanner`, commit theo từng mục, **không push**.
- Việc đầu tiên trước mỗi đợt làm: cập nhật README.

## Project

- `Source/ImageCoreService` -- thư viện lõi không UI (codec, PDF/TIFF, scan, xử lý ảnh, OCR).
- `Source/ImageOptimizerTool` -- app WinForms .NET 9, chỉ là giao diện.
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
