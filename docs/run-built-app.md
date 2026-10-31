# Chạy CheckInApp đã build — 28/10/2026

## Web trên máy tính

- Mở **http://127.0.0.1:5174**. Đây là bản `apps/web/dist` được phục vụ bằng Vite preview, nối API SQL Server/Redis.
- Admin: đọc hai trường `email` và `password` trong `.local/docker-integration/secrets.json`.
- Bộ dùng thử mới: năm học **Mobile UAT mvs8nnb0**, lớp **Mobile UAT A mvs8nnb0**. Các volume Docker cũ không còn ở đầu lần chạy này; database SQL Server đang dùng là database mới.

## Android trên điện thoại

1. Kết nối cùng Wi-Fi với máy tính.
2. Mở **http://192.168.0.106:5082/health**, cần thấy `{"status":"ok"}`.
3. Mở **http://192.168.0.106:8765/**, tải và cài APK. Android có thể hỏi quyền cài ứng dụng từ trình duyệt này.
4. Mở **CheckInApp LAN**, đăng nhập bằng thông tin trong `.local/mobile-uat/login.txt` trên máy tính.
5. Chọn buổi hôm nay, cho phép Camera. Mở thẻ mẫu trên máy tính tại **http://127.0.0.1:8765/cards.html** để quét.

Ba thẻ đầu thuộc lớp A; thẻ 04 thuộc lớp B để kiểm tra báo sai lớp. Quét QR thẻ 01, Barcode thẻ 02, rồi thử mất mạng/retry với thẻ 03. Quét lặp không được tăng số học sinh có mặt. Xem checklist đầy đủ trong [hướng dẫn thiết bị](android-device-test.md).

APK ký bằng khóa preview cũ, có bundle sẵn, không cần Expo Go hoặc Metro. API đã nhúng trong APK là **http://192.168.0.106:5082/api/v1**. Nếu IP máy tính đổi, cần build APK mới.

## Mở lại sau khi đóng terminal hoặc khởi động lại máy

Khởi động Docker Desktop, rồi mở các terminal ở thư mục `D:\LongLe\Code\CheckInApp`:

```powershell
# Terminal 1: API + SQL Server/Redis; thay IP nếu Wi-Fi đã đổi.
./scripts/Start-Integration.ps1 -LanAddress 192.168.0.106

# Terminal 2: bản web đã build
./scripts/Start-WebBuild.ps1

# Terminal 3: trang tải APK/thẻ thử
python scripts/serve-android-preview.py
```

Nếu cần build lại:

```powershell
dotnet build CheckInApp.slnx -c Release
npm run build --prefix apps/web
./scripts/Build-AndroidPreview.ps1
node scripts/publish-android-preview.mjs
```

Script Android tự tìm IP LAN đang kết nối; có thể truyền `-ApiUrl http://<IP>:5082/api/v1` rõ ràng. Cập nhật firewall bằng `./scripts/Enable-IntegrationLan.ps1` trong PowerShell Administrator khi IP thay đổi. Rule chỉ mở API 5082 và trang tải 8765 cho mạng nội bộ.

Giữ volume SQL, file secrets và thư mục khóa ký preview nếu muốn tiếp tục dữ liệu/tài khoản và cập nhật APK. Các công cụ Android nằm trong Docker; lần build này dùng các chứng chỉ CA công khai Windows đã tin cậy để xác minh HTTPS, không tắt kiểm tra TLS.
