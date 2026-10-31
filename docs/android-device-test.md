# Cài APK Android và kiểm thử quét thật qua LAN

## Cài đặt

Máy tính và điện thoại dùng cùng Wi-Fi. Địa chỉ Wi-Fi tại lúc build là **192.168.0.106**.

1. Trên điện thoại, mở `http://192.168.0.106:5082/health`. Phải thấy JSON `{"status":"ok"}`.
2. Mở `http://192.168.0.106:8765/`, tải **CheckInApp-LAN.apk**. Nếu Android hỏi, cho phép trình duyệt cài ứng dụng từ nguồn này rồi cài file vừa tải.
3. Mở **CheckInApp LAN**, bấm **Kiểm tra kết nối LAN**. APK có bundle sẵn, không cần Expo Go hay Metro. Thiết bị cần Android 7.0+ và CPU ARM64/ARM32.
4. Dùng email/password trong `.local/mobile-uat/credentials-and-fixture.json`. Đây là tài khoản GLV thử đã được duyệt, chỉ phụ trách **Mobile UAT A mvs8nnb0**. Không có mật khẩu được nhúng trong APK hoặc trang tải.
5. Chọn buổi hôm nay rồi cho phép quyền Camera khi bấm mở camera.

APK nội bộ dùng package `com.checkinapp.mobile.preview`, khóa ký thử riêng lưu tại `.local/android-preview/signing/`. Giữ khóa này để những bản cài thử sau có thể cập nhật ứng dụng hiện tại. Không đưa khóa/mật khẩu vào source control. Đây không phải khóa phát hành Play Store.

API đã nối SQL Server/Redis Docker. Điện thoại chỉ gọi HTTP API cổng **5082**; SQL Server và Redis vẫn chỉ publish trên loopback của máy tính.

## Thẻ thử

  Trên máy tính, mở `http://127.0.0.1:8765/cards.html`. Có bốn thẻ, mỗi thẻ có QR và Code 128 chứa cùng mã:

| Thẻ | Mục đích |
| --- | --- |
| 01 - Thử QR | Điểm danh lần đầu và thử quét lặp |
| 02 - Thử Barcode | Điểm danh bằng Code 128 |
| 03 - Thử mất mạng | Kiểm tra lỗi mạng và retry |
| 04 - Thẻ lớp khác | Học sinh lớp B, phải bị từ chối khi quét vào lớp A |

Có thể quét từ màn hình máy tính hoặc bấm **In thẻ thử**. In đen trắng ở tỷ lệ 100%, giữ nguyên viền trắng, tránh in mờ hoặc gấp ngang mã vạch. Đặt camera song song thẻ, thử khoảng cách 15–35 cm, tránh phản sáng. Chỉ cần toàn bộ QR hoặc toàn bộ chiều ngang Barcode nằm rõ trong ảnh camera.

Dữ liệu UAT có ba học sinh lớp A và một học sinh lớp B. Lịch thử chạy mỗi ngày từ **28/10/2026 đến hết 04/11/2026**, 00:00–23:59 theo giờ Việt Nam. Không quét trước qua API các thẻ này để giữ trạng thái ban đầu cho điện thoại.

## Trình tự nghiệm thu

Trên web `http://127.0.0.1:5174`, đăng nhập admin từ `.local/docker-integration/secrets.json`, chọn năm học **Mobile UAT mvs8nnb0**, mở đúng buổi/lớp A. Tải lại web sau mỗi bước để đối chiếu với điện thoại.

| Bước | Thao tác trên điện thoại | Kết quả cần thấy |
| --- | --- | --- |
| 1 | Quét QR thẻ 01 | Server xác nhận thành công; đã đến **1/3**, chưa đến **2**; có tên 01 trong danh sách vừa điểm danh |
| 2 | Quét lại QR, rồi Barcode của chính thẻ 01 | Báo đã điểm danh; vẫn **1/3**, không thêm lượt và không đổi thời điểm ghi ban đầu |
| 3 | Quét Barcode thẻ 02 | Thành công; đã đến **2/3**, chưa đến **1** |
| 4 | Quét QR hoặc Barcode thẻ 04 | Báo học sinh không thuộc danh sách buổi; vẫn **2/3** |
| 5 | Tắt Wi-Fi và dữ liệu di động, quét thẻ 03 | Sau tối đa khoảng 12 giây báo chưa xác nhận/lỗi kết nối; không hiện thành công giả |
| 6 | Bật lại Wi-Fi, bấm thử lại yêu cầu đang chờ | Server xác nhận, đã đến **3/3**; retry giữ request ID nên không tạo trùng kể cả nếu yêu cầu trước đã tới server |
| 7 | Tải lại roster trên web | Ba học sinh lớp A có mặt; phương thức QR/Barcode và thời điểm được giữ trong bản ghi; lớp B không bị ghi nhận nhầm |
| 8 | Thoát app hoàn toàn rồi mở lại | Phiên được khôi phục nếu còn hiệu lực; không mất các bản ghi đã lưu trong SQL |

Các con số trên giả định bắt đầu từ bộ thẻ chưa quét. Nếu tiếp tục một buổi đã thử, trạng thái được giữ trong SQL và các lần quét lặp phải trả “đã điểm danh”. Không xóa dữ liệu để ép kết quả.

Thử bổ sung:

- **Từ chối quyền Camera:** app giải thích cần quyền, không bị crash. Vào Cài đặt Android → Ứng dụng → CheckInApp LAN → Quyền → Camera để cho phép lại.
- **Thu hồi thẻ:** trên web admin cấp lại thẻ của học sinh 01; quét thẻ cũ trong trang in phải bị từ chối. Thao tác cấp lại thẻ không xóa lượt điểm danh đã có. Thẻ mới chỉ hiện một lần trên web để in.
- **Phạm vi lớp:** tài khoản GLV UAT không được xem hoặc điểm danh lớp B.
- **Chờ duyệt:** có thể đăng ký tài khoản mới từ app; phải bị chặn đăng nhập cho đến khi admin duyệt, gắn giáo lý viên và phân công lớp phù hợp. Không gắn hai tài khoản vào cùng một hồ sơ GLV đã liên kết.
- **Báo cáo:** trong khi buổi đang mở, số chưa đến chưa phải số ngày vắng. Báo cáo chuyên cần chỉ cộng buổi đã kết thúc; chọn đúng năm học/lớp/ngày. Không sửa đồng hồ thiết bị hoặc database để biến buổi đang mở thành buổi kết thúc.

## Nếu không kết nối được

- Nếu trình duyệt điện thoại không mở được `/health`, kiểm tra cùng Wi-Fi, tắt VPN đang chặn LAN, máy tính không sleep và API vẫn chạy. Wi-Fi khách có thể bật cách ly thiết bị; đổi sang mạng cho phép các thiết bị liên lạc với nhau.
- Windows Firewall cần quyền Administrator. Script chỉ mở TCP 5082/8765 trên IP Wi-Fi đã chọn với remote `LocalSubnet`, không tắt firewall.
- Nếu `/health` mở được nhưng APK không đăng nhập, kiểm tra dòng **API cài thử** ở màn đăng nhập có đúng IP hiện tại không, tài khoản đã được duyệt và mật khẩu đúng chưa.
- Nếu IP Wi-Fi đổi, build lại APK với API URL mới; HTTP trong bản thử chỉ được cho phép tới hostname/IP đã nhúng trong network security config.
- HTTP LAN chỉ dùng cho dữ liệu và tài khoản thử trong đợt này. Bản triển khai thực cần HTTPS.

## Chạy lại môi trường hoặc build lại

Terminal API:

```powershell
./scripts/Start-Integration.ps1 -LanAddress 192.168.0.106
```

PowerShell **Run as Administrator**, để tạo lại rule firewall khi cần:

```powershell
./scripts/Enable-IntegrationLan.ps1 -LanAddress 192.168.0.106
```

Trang tải file, từ một terminal khác:

```powershell
python scripts/serve-android-preview.py
```

Build lại bằng Docker, không cần cài Android Studio/Java lên Windows:

```powershell
./scripts/Build-AndroidPreview.ps1 -ApiUrl http://192.168.0.106:5082/api/v1
$env:EXPO_PUBLIC_API_URL = 'http://192.168.0.106:5082/api/v1'
node scripts/publish-android-preview.mjs
```

Docker giữ cache npm/Gradle và native workspace trong các volume `checkinapp-android-*`. Script tạo khóa ký preview một lần và dùng lại. Artifact cuối nằm trong `.local/android-preview/`; trang tải chỉ phục vụ `.local/android-download/`, không phục vụ file tài khoản hoặc thư mục khóa ký.

Muốn tạo một bộ UAT mới, chạy `node scripts/prepare-mobile-uat.mjs`; script tạo mới lớp/tài khoản/thẻ và thay file thông tin UAT cục bộ, không xóa bộ cũ. Tên lớp/năm học sẽ khác tên minh họa trên đây, xem file kết quả mới để chọn đúng.

Khi kiểm thử xong, dừng server tải bằng Ctrl+C. Có thể gỡ riêng hai rule trong PowerShell Administrator:

```powershell
Get-NetFirewallRule -Name 'CheckInApp-Preview-LAN-5082','CheckInApp-Preview-LAN-8765' | Remove-NetFirewallRule
```

## Phạm vi đã kiểm tra

Đã kiểm tra đường API từ địa chỉ LAN của máy, các rule firewall, bộ dữ liệu UAT và giải mã đủ **8/8 ảnh QR/Code 128** bằng phần mềm. Chữ ký APK, manifest, ABI và API URL trong bundle được kiểm tra bằng Android build tools sau khi build. Camera, autofocus, khả năng đọc thẻ in và thao tác cài đặt trên điện thoại của bạn vẫn cần thực hiện theo bảng trên.

APK có chữ ký v2 hợp lệ, minSdk 24, targetSdk 36, ABI ARM64/ARM32 và không ở chế độ debuggable. Không có quyền microphone. Network security config chặn HTTP mặc định, chỉ cho phép `192.168.0.106`. Đã tải APK qua LAN và so khớp SHA-256; kiểm tra các đường dẫn file mật khẩu/khóa không được trang tải phục vụ. Tài khoản GLV đăng nhập qua API LAN thành công, chỉ thấy một lớp với ba học sinh chưa được quét. Kết quả máy đọc: `.local/android-preview/delivery-qa.json`.

Tham khảo: [Expo: tạo bản release cục bộ](https://docs.expo.dev/guides/local-app-production/), [Expo: APK cài thiết bị](https://docs.expo.dev/build-reference/apk/), [Android Command-line Tools](https://developer.android.com/studio#command-line-tools-only).

