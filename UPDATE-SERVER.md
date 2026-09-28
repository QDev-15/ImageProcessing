# Phát hành bản cập nhật cho Doc Scanner (tự cập nhật lúc 01:00)

App (cài bằng APK, không qua Google Play) mỗi đêm lúc 01:00 đọc tệp `update.json` ở địa chỉ nhập trong
**Cài đặt > Cập nhật**. Có bản mới (versionCode lớn hơn bản đang cài) thì tự tải APK, kiểm tra SHA-256 và cài.
Có thể bấm **Kiểm tra ngay** để thử bất cứ lúc nào.

## 1. Tăng số phiên bản trước khi build

Trong `Source/DocScanner/DocScanner.csproj`:

```xml
<ApplicationDisplayVersion>1.2</ApplicationDisplayVersion>  <!-- tên hiển thị -->
<ApplicationVersion>3</ApplicationVersion>                 <!-- versionCode: PHẢI lớn hơn bản trước -->
```

Build Release: `dotnet build Source/DocScanner/DocScanner.csproj -f net10.0-android -c Release`
-> `bin/Release/net10.0-android/btk.docscanner-Signed.apk`.

**Luôn ký bằng cùng một khoá** (keystore). APK ký khoá khác sẽ bị Android từ chối cài đè. (Hiện tại bản build dùng
khoá debug mặc định của máy build; trước khi phát hành thật nên tạo keystore riêng và giữ cẩn thận.)

## 2. Tính SHA-256 của APK

PowerShell: `(Get-FileHash .\DocScanner-1.2.apk -Algorithm SHA256).Hash`
Git Bash: `sha256sum DocScanner-1.2.apk`

## 3. Đặt 2 tệp lên máy chủ HTTPS

Bất kỳ nơi nào có https và cho tải trực tiếp: GitHub Releases, Google Cloud Storage, máy chủ công ty...
`update.json`:

```json
{
  "versionCode": 3,
  "versionName": "1.2",
  "apkUrl": "https://example.com/docscanner/DocScanner-1.2.apk",
  "sha256": "4D2A8852...",
  "notes": "Sửa lỗi xem PDF, trang A4 đúng tỉ lệ"
}
```

Trong app: Cài đặt > Cập nhật > nhập `https://example.com/docscanner/update.json`.

Chỉ chấp nhận `https`; riêng `http://127.0.0.1` / `localhost` được phép để thử qua `adb reverse`.

## 4. Người dùng thấy gì

- **Lần tự cập nhật đầu tiên** trên một máy (bản đang cài được cài bằng cách khác: adb, tải APK, Zalo...): Android hỏi
  "Cho phép cài ứng dụng từ nguồn này" (một lần) và "Cập nhật ứng dụng này?". Nếu lúc 01:00 máy đang khoá, app gửi
  thông báo "Có bản cập nhật Doc Scanner - chạm để cài".
- **Từ lần sau** (Android 12+): app là "installer of record" của chính nó và có quyền
  `UPDATE_PACKAGES_WITHOUT_USER_ACTION`, nên cài im lặng lúc 01:00, không hỏi.
- App cài từ Google Play: tắt tự cập nhật (chính sách Google Play cấm app tự cập nhật ngoài Play; Play tự cập nhật).

## 5. Thử trên máy tính (không cần máy chủ)

```
# thư mục chứa update.json + APK, phục vụ ở http://127.0.0.1:8080/
adb reverse tcp:8080 tcp:8080
# Cài đặt > địa chỉ: http://127.0.0.1:8080/update.json > Kiểm tra ngay
# Chạy thử job 01:00 ngay lập tức (app phải đang được hẹn job, đừng force-stop):
adb shell cmd jobscheduler run -f btk.docscanner 21840
```
