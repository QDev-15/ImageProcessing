# Vendored external encoders -- none left (2026-09-29)

This folder used to carry two external, shelled-out encoders (same pattern as `QpdfOptimizer` in
the main app: shell out, best-effort, no native P/Invoke). Both have since been removed by owner
decision, leaving the app with only its own managed codecs (CCITT G4, JPEG) -- see
`THIRD-PARTY-NOTICES.md` and `CLAUDE.md` at the repo root for the reasoning. Nothing needs to be
copied into this folder for the app to build or run.

## JPEG2000 (OpenJPEG) -- đã gỡ bỏ (2026-09-29)

Từng dùng `tools/openjpeg/` (OpenJPEG 2.5.4, `opj_compress.exe`, BSD-2-Clause, bản build chính
chủ) làm tuỳ chọn nén trang màu/xám thay JPEG. Owner quyết định bỏ hẳn để tăng tốc xuất file tối
đa: JPEG2000 gọi một tiến trình ngoài **cho từng trang một** (ghi file tạm, chạy `opj_compress.exe`,
đọc lại kết quả) -- đo thật trên 40 trang màu: xuất bằng JPEG mất 1,5 s, bằng JPEG2000 mất 38,6 s
(~25 lần chậm hơn), dù file JPEG2000 nhỏ hơn khoảng 2 lần. Owner ưu tiên tốc độ, chấp nhận file
màu/xám to hơn. Không còn file nào trong `tools/openjpeg/`, không còn `OpenJpegEncoder.cs`.

## JBIG2 (jbig2enc) -- đã gỡ bỏ (2026-09-29)

Từng dùng `tools/jbig2enc/` (jbig2enc 0.29, bản Windows do bên thứ ba build lại, xem lịch sử
git nếu cần tham khảo). Owner quyết định bỏ hẳn JBIG2 khỏi app -- chỉ dùng CCITT G4 cho trang
trắng đen, để không phải theo dõi rủi ro bằng sáng chế / nguồn gốc bản build của thư viện này
nữa (và vì JBIG2 Symbol cũng chậm hơn G4 khi xuất, dù ít hơn nhiều so với JPEG2000). Không còn
file nào trong `tools/jbig2enc/`, không còn `JBig2Encoder.cs`.

## Runtime DLL đi kèm (thêm 2026-09-25)

- VC++ 2015-2022 x64 (`vcruntime140.dll`, `vcruntime140_1.dll`, `msvcp140.dll`) cho
  Tesseract nằm trong `ImageCoreService/native/vcruntime-x64/` và được copy cạnh exe.
- Chi tiết license / bằng sáng chế: `THIRD-PARTY-NOTICES.md` ở gốc repo.
