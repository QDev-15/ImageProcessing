Build file .aab cho DocScanner
Lệnh (chạy ở thư mục gốc repo, PowerShell hoặc terminal):

```bash
dotnet publish Source/DocScanner/DocScanner.csproj -f net10.0-android -c Release -p:AndroidPackageFormat=aab
```

(Nếu chạy từ ImageProcessing/, bỏ tiền tố ImageProcessing/ trong đường dẫn.)

File kết quả nằm ở:
Source/DocScanner/bin/Release/net10.0-android/publish/btk.docscanner-Signed.aab

Vì sao không cần truyền mật khẩu keystore
DocScanner.csproj dòng 67 tự <Import Project="release\Signing.props" ...> khi build ở cấu hình Release và file đó tồn tại — nó đã có sẵn từ đợt trước (Source/DocScanner/release/Signing.props + docscanner-upload.keystore), nên lệnh publish ở trên tự ký đúng bằng upload key cũ, không cần nhập gì thêm.

Trước khi build, nhớ tăng version (bắt buộc với mỗi bản upload lên Play)
Sửa trong DocScanner.csproj (dòng 33-34):


<ApplicationDisplayVersion>1.3</ApplicationDisplayVersion>  <!-- số hiển thị cho người dùng -->
<ApplicationVersion>4</ApplicationVersion>                   <!-- versionCode, PHẢI tăng mỗi lần -->
Play Console từ chối thẳng nếu ApplicationVersion (versionCode) không lớn hơn bản đã có.

Kiểm tra chữ ký sau khi build (tuỳ chọn, để chắc ăn trước khi upload)

```bash
jarsigner -verify "Source/DocScanner/bin/Release/net10.0-android/publish/btk.docscanner-Signed.aab"
```


Phải thấy jar verified.