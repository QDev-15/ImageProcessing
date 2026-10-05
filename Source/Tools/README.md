# Công cụ thử nghiệm (không phải một phần của app)

Không nằm trong solution nào; chạy tay khi cần chỉnh bộ dò mép giấy.

## `gen_test_scans.py`
Sinh ảnh "tờ giấy trên bàn" 4000x3000 (bàn tối có vân sọc, bàn sáng có bóng đổ + phối cảnh, tờ bị khung cắt ở đáy, biên lai nhỏ trên vải xanh)
để thử bộ dò mép. Cần Python + Pillow. Chạy trong thư mục muốn lưu ảnh:

    python gen_test_scans.py

Đẩy sang điện thoại để thử trong app (nhớ dọn sau khi thử: xoá thư mục và xoá dòng trong MediaStore):

    adb push . /sdcard/Pictures/DocTest/
    adb shell am broadcast -a android.intent.action.MEDIA_SCANNER_SCAN_FILE -d file:///sdcard/Pictures/DocTest/<tên>.jpg

## `EdgeProbe/`
Console .NET (Windows) chạy `DocumentEdgeDetector` của `ImageCore.Shared` trực tiếp trên ảnh và vẽ khung dò được ra PNG, để chỉnh thuật toán
nhanh mà không cần cài lên điện thoại (khoảng 0,1-0,7 s / ảnh trên PC, đọc EXIF như app).

    dotnet run -c Release --project Source/Tools/EdgeProbe -- <cạnh dài phân tích, vd 1600> <thư mục ra> anh1.jpg anh2.jpg ...

Tham số đầu khác `1600` thì bật `Trace` của bộ dò (in đường thẳng, điểm từng ứng viên, các mốc). In ra: đã dò được hay chưa, độ tin cậy, thời gian, 4 góc chuẩn hoá.
Ảnh ra: `<tên>_det.png` (khung xanh, chấm đỏ ở 4 góc).
