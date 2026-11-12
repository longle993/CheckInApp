> Tài liệu vận hành/kiểm thử theo thời điểm ghi nhận. Xem [đặc tả hợp nhất 01/11/2026](requirements-2026-11-01.md) để phân biệt chức năng hiện có và yêu cầu đang chờ triển khai; không coi IP/phiên bản APK hoặc cổng trong ảnh/chỉ dẫn cũ là trạng thái hiện tại đã kiểm tra.

# SQL Server và Redis: kiểm thử tích hợp

Đã chạy thực tế ngày 27/10/2026 trên Docker Desktop Linux containers, SQL Server 2022 Developer **16.0.4305.5**, Redis **7.4.12** và API .NET 10.

## Môi trường đang chạy

| Thành phần | Địa chỉ |
| --- | --- |
| Web quản trị dùng SQL Server | http://127.0.0.1:5174 |
| API tích hợp | http://127.0.0.1:5082 |
| SQL Server | `127.0.0.1,14333`, database `CheckInAppIntegration` |
| Redis | `127.0.0.1:16379` |

Compose project là `checkinapp-integration`, sử dụng volume `checkinapp-integration_sql-data` và `checkinapp-integration_redis-data`. Các cổng chỉ bind loopback. API chạy trên máy host bằng build Release; SQL Server và Redis chạy trong Docker. Web/API SQLite ở cổng 5173/5080 được giữ riêng.

Thông tin đăng nhập admin và khóa dịch vụ được tạo ngẫu nhiên trong `.local/docker-integration/secrets.json`. Không chia sẻ hay đưa thư mục `.local` vào source control.

## Chạy lại

Yêu cầu Docker Desktop với Linux containers, Docker Compose có hỗ trợ `!override`, PowerShell 7, .NET SDK 10 và Node 24. Web cần đã chạy `npm ci` trong `apps/web`.

Từ thư mục dự án, terminal 1:

```powershell
./scripts/Start-Integration.ps1
```

Script tạo hoặc dùng lại secrets, chạy hai container và chờ healthcheck, build Release rồi chạy API ở cổng 5082. Môi trường `Integration` sử dụng `Database.MigrateAsync`, không dùng SQLite/`EnsureCreated` và không seed dữ liệu demo. Chỉ tài khoản admin cùng hai vai trò mặc định được bootstrap.

Migration `20261027140406_InitialCreate` đã tạo schema thực và có trong `__EFMigrationsHistory`. API đã khởi động lại trên cùng database mà không tạo lại bảng hay mất dữ liệu.

Terminal 2:

```powershell
./scripts/Start-IntegrationWeb.ps1
```

Terminal 3:

```powershell
./scripts/Test-Integration.ps1

# Kiểm tra giao diện với cùng SQL Server/Redis:
. ./scripts/Integration-Environment.ps1
$env:WEB_URL = 'http://127.0.0.1:5174'
node tests/ui-smoke.mjs
```

Test giao diện dùng Playwright từ runtime Codex của máy hiện tại; xem ghi chú trong README khi chạy máy khác.

## Kết quả đã xác minh

- Build Release không có warning/error; 12/12 test nghiệp vụ đạt.
- **39/39 kiểm tra tích hợp đạt**: tạo năm học/lớp/học sinh/giáo lý viên, tìm kiếm và lọc, mã lớp duy nhất, phân công GLV, đăng ký chờ duyệt, duyệt tài khoản và quyền theo lớp.
- Lịch ba ngày, hủy ngày nghỉ, đồng bộ lại không tạo trùng buổi, roster đúng số học sinh.
- QR và Barcode qua HTTP, retry idempotent, cùng key khác payload bị chặn, thẻ lớp khác và thẻ cũ bị thu hồi bị từ chối. Chưa phải kiểm thử camera hoặc đầu đọc vật lý.
- Sáu request quét đồng thời cùng học sinh: tất cả trả thành công, cùng một record ID và SQL xác nhận chỉ có một bản ghi.
- Báo cáo loại buổi đang mở, buổi tương lai và buổi hủy. Fixture kết thúc có 5 lượt phải điểm danh, 3 có mặt, 2 vắng, tỷ lệ 60%; lớp A 75%, lớp B 0%. Điều chỉnh có version làm số có mặt giảm còn 2 và vô hiệu cache ngay.
- Redis có JSON báo cáo và TTL tối đa 30 giây; request lặp tăng `keyspace_hits`. Dừng Redis vẫn đọc báo cáo đúng từ SQL; khởi động lại thì API tự kết nối và ghi cache.
- Refresh token xoay vòng; phát lại token cũ thu hồi phiên. Khóa tài khoản làm JWT đã cấp mất hiệu lực.
- **30/30 kiểm tra giao diện đạt** trên SQL Server/Redis: 10 màn × 1440/768/375px, không lỗi JavaScript/API hoặc tràn ngang toàn trang; tạo lớp qua form thành công.

Kiểm thử đã phát hiện và sửa hai vấn đề chưa lộ trên SQLite: parser Redis chưa xử lý chuỗi có mật khẩu; giao dịch quét đồng thời gây deadlock trên SQL Server. Luồng quét hiện lấy update lock cho session trước khi đọc và retry transaction khi SQL Server báo deadlock.

## Dữ liệu và phạm vi

Test tạo fixture mới qua API ở mỗi lần chạy và giữ dữ liệu trong database riêng. Để kiểm tra báo cáo kết thúc buổi mà không chờ hết ngày, test chỉ cập nhật giờ đóng của hai session fixture bằng SQL. Không sửa giờ hệ thống hoặc lịch của dữ liệu nghiệp vụ khác. Test cũng tạm dừng và khởi động lại **chỉ Redis của project tích hợp**.

Kết quả máy đọc nằm tại `.local/docker-integration/results.json`; ảnh và kết quả browser tại `.local/ui-qa/`. Không chạy bộ test trên production. Chưa kiểm thử tải lớn, failover SQL Server, khôi phục backup hoặc thiết bị quét thật.

Dừng các terminal web/API bằng Ctrl+C. Dừng container, giữ volume:

```powershell
. ./scripts/Integration-Environment.ps1
docker @integrationCompose stop sqlserver redis
```

Không dùng `down -v` nếu còn cần dữ liệu kiểm thử. Để chạy mobile với backend SQL này, API cần lắng nghe địa chỉ LAN phù hợp và mobile trỏ đến cổng 5082; cấu hình hiện tại chỉ mở trên loopback.
