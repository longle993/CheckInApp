# CheckInApp

Đặc tả hợp nhất và yêu cầu mới: [nghiệp vụ + brief ngày 01/11/2026](docs/requirements-2026-11-01.md). Đã triển khai đợt 01/11; xem kết quả và giới hạn trong hướng dẫn mới.

Chạy bản web và APK mới nhất: [hướng dẫn bản cập nhật 01/11/2026](docs/release-2026-11-01.md).

Ứng dụng quản lý lớp giáo lý và điểm danh: web quản trị theo phương án **A**, mobile điểm danh theo phương án **C**. Giao diện tiếng Việt, Inter, nền sáng, màu đỏ đô, trắng và trắng be. Web gọi API thật; không dùng dữ liệu giả hardcode trong React.

SQL Server và Redis Docker đã được kiểm thử thực tế. Script Start-WebBuild hiện phục vụ web tại http://127.0.0.1:5175 và trỏ API 5083; các lượt kiểm thử gần đây dùng web 5175 → API 5083. Cần chọn đúng cấu hình API khi khởi động. Xem [hướng dẫn chạy và 39 kiểm tra tích hợp](docs/docker-integration.md).

APK cài thử qua Wi-Fi và thẻ quét mẫu: xem [cài Android và kiểm thử camera thực tế](docs/android-device-test.md).

## Chạy cục bộ

Yêu cầu: .NET SDK 10.0.401 hoặc bản vá cùng nhánh, Node.js 24, npm. Thực hiện từ thư mục dự án.

Terminal 1 — API:

```powershell
dotnet restore CheckInApp.slnx
cd backend/CheckInApp.Api
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run -- --urls http://127.0.0.1:5080
```

Development dùng SQLite trong `backend/CheckInApp.Api/.local/checkin.db`, giúp chạy mà không cần SQL Server/Redis. Khóa JWT và mật khẩu admin được tạo ngẫu nhiên ở lần chạy đầu. Xem thông tin đăng nhập trên máy của bạn:

```powershell
Get-Content backend/CheckInApp.Api/.local/bootstrap-admin.json
```

File này chứa thông tin nhạy cảm, đã được `.gitignore` loại trừ. Không đưa file này vào bản triển khai. Không có mật khẩu mặc định dùng chung.

Terminal 2 — web:

```powershell
cd apps/web
npm ci
npm run dev
```

Mở http://127.0.0.1:5173. Vite proxy `/api` đến http://127.0.0.1:5080. OpenAPI Development: http://127.0.0.1:5080/openapi/v1.json.

Development có 4 lớp, 120 học sinh, giáo lý viên và lịch Chủ nhật. Có thêm buổi thử nghiệm trong ngày khởi tạo database để thử quét cả vào ngày thường. Các buổi đó là dữ liệu demo, không phải lịch giáo lý thật. Buổi thử nghiệm đóng sau hai giờ; muốn tiếp tục có thể cấu hình lịch/ngày bổ sung cho ngày tương lai. Tắt `SeedDemoData` trước khi khởi tạo một database Development không có dữ liệu mẫu.

Không xóa database đang chứa dữ liệu cần giữ. SQLite Development dùng `EnsureCreated`, không tự nâng schema sau khi sửa model. SQL Server sử dụng EF migrations.

## App React Native

App dùng Expo SDK 58 với bộ React/React Native do Expo quy định, `expo-camera` cho QR/Code 128, SecureStore cho refresh token và font Inter đóng gói cùng ứng dụng.

```powershell
cd apps/mobile
npm ci
Copy-Item .env.example .env
# Đổi EXPO_PUBLIC_API_URL thành địa chỉ API mà điện thoại truy cập được.
npm start
```

- Android Emulator mặc định gọi `http://10.0.2.2:5080/api/v1`.
- Điện thoại thật: đặt `EXPO_PUBLIC_API_URL=http://<IP-LAN-máy-chủ>:5080/api/v1`, chạy API với `--urls http://0.0.0.0:5080` trong mạng thử nghiệm và cho phép cổng qua firewall khi cần. Không dùng `localhost` của máy tính trên điện thoại.
- Môi trường phát hành phải dùng API HTTPS. Expo Go dùng cho thử nghiệm; cấu hình native cần quyền camera. Đã có APK Android preview ký cục bộ; APK đó chưa chứa toàn bộ các cập nhật web/mobile về sau. Chưa có bản phát hành lên cửa hàng hoặc IPA được xác nhận.

Đăng nhập, chọn buổi hôm nay, mở camera hoặc nhập mã thẻ. Mobile chỉ hiển thị thành công sau khi API commit. Mất mạng giữ request đang thử trong bộ nhớ để gửi lại cùng idempotency key; không có hàng đợi offline và không hứa đã lưu khi server chưa xác nhận.

## Luồng sử dụng

1. Admin đăng nhập; tạo năm học, lớp và hồ sơ giáo lý viên.
2. Giáo lý viên tự đăng ký hoặc admin tạo tài khoản chờ duyệt. Admin duyệt, chọn vai trò và liên kết hồ sơ giáo lý viên.
3. Phân công giáo lý viên vào lớp. Vai trò theo lớp chỉ thấy lớp đang được giao tại thời điểm sử dụng.
4. Thêm học sinh với Tên Thánh (không bắt buộc), họ tên, ngày sinh. Mã học sinh do server tạo. Chuyển lớp giữ lịch sử và không cho sửa hồi tố qua thao tác chuyển thông thường.
5. Cấu hình thứ trong tuần, khoảng hiệu lực, giờ mở/đóng. Có bản xem trước; thêm ngày nghỉ hoặc buổi bổ sung. Buổi đã bắt đầu không được thay lịch âm thầm.
6. Cấp thẻ cho học sinh. QR và Code 128 chứa cùng mã ngẫu nhiên; chỉ lưu hash phía server. Mã hiện một lần để in, cấp lại sẽ thu hồi thẻ cũ.
7. Quét đúng buổi và đúng lớp. Có danh sách vừa điểm danh/chưa đến. Admin có thể điều chỉnh bản ghi có sẵn với lý do và version chống ghi đè.
8. Xem ba báo cáo trong màn Báo cáo: các lớp, từng lớp, học sinh chưa điểm danh. Có xuất CSV.

Roster được chốt khi sinh buổi và giữ nguyên đối với buổi đã mở/kết thúc. Học sinh thêm sau khi buổi bắt đầu sẽ vào các buổi tương lai; thao tác thêm mới không tự sửa mẫu số của buổi đang diễn ra. Điều chỉnh ghi danh hồi tố/roster đã chốt chưa được cung cấp trong giao diện này.

## Công nghệ và cấu trúc

```text
backend/
  CheckInApp.Domain/          Entity, permission bitmask, quy tắc chuyên cần
  CheckInApp.Application/     Command/query, MediatR handlers, interface
  CheckInApp.Infrastructure/  EF Core, SQL Server/SQLite, Redis, nghiệp vụ
    Migrations/              Migration SQL Server
  CheckInApp.Api/             Controllers, JWT, AspNetRole, Installers, worker
  CheckInApp.Tests/           Kiểm thử với SQLite in-memory thật
apps/web/                    React + Vite + JavaScript + Tailwind 4
apps/mobile/                 React Native + Expo
tests/                       Kiểm thử HTTP và browser
```

DI được đăng ký qua `IInstaller`, `InstallerExtension.InsertServiceInAssembly`, `ServiceInstaller`. DbContext/service là scoped; Redis connection dùng singleton. MediatR được khóa ở 12.5.0; không tự nâng major khi chưa kiểm tra thay đổi và điều kiện sử dụng.

`[AspNetRole(Permission.CheckIn)]` tạo authorization policy động. Handler xác minh tài khoản Active và kiểm tra đủ mọi bit. Phạm vi lớp kiểm tra tiếp trong service. Quyền trong API JSON là chuỗi, không chuyển bitmask lớn sang JavaScript Number. Admin hệ thống được bảo vệ khỏi chỉnh sửa vai trò và khóa admin cuối cùng.

JWT kiểm tra chữ ký, issuer, audience, hạn dùng và phiên trong SQL ở mỗi request. Refresh token được hash, xoay sau mỗi lần sử dụng; phát hiện dùng lại sẽ thu hồi phiên. Web giữ access token trong bộ nhớ, refresh cookie HttpOnly; mobile giữ refresh token trong SecureStore. Mật khẩu hash bằng ASP.NET PasswordHasher.

Redis cache báo cáo tối đa 30 giây. Cache key bao gồm người dùng, phạm vi lọc, revision audit và thời điểm; kiểm tra quyền/phạm vi vẫn đọc SQL trước cache. Redis lỗi thì truy vấn SQL. SQL là nguồn dữ liệu điểm danh, không phụ thuộc Redis để ngăn trùng.

## Công thức báo cáo

- E: số lượt roster thuộc buổi không hủy, đã kết thúc, nằm trong khoảng lọc và năm học/lớp đã chọn.
- P: số lượt trong E có kết quả hiện hành là có mặt.
- A = E − P. Tỷ lệ = P/E × 100%; E = 0 hiển thị “Chưa có buổi”.
- Tổng các lớp tính bằng tổng lượt P / tổng lượt E, không trung bình tỷ lệ từng lớp.
- Buổi đang mở hiển thị chưa điểm danh, không kết luận vắng và chưa đưa vào mẫu số.
- Tối đa một buổi/lớp/ngày trong phiên bản này. Thời điểm lưu UTC; ngày nghiệp vụ và hiển thị theo UTC+7 Việt Nam.

## SQL Server và Redis qua Docker

```powershell
Copy-Item .env.example .env
# Thay toàn bộ giá trị REPLACE... bằng mật khẩu/khóa ngẫu nhiên của bạn.
docker compose up --build -d
```

Web: http://127.0.0.1:8088. API: http://127.0.0.1:5080. SQL Server dùng volume `sql-data`; Redis dùng `redis-data`. Chỉ API được nối đến Redis qua mạng Docker. Không dùng `docker compose down -v` với dữ liệu cần giữ.

Production chạy migrations SQL Server lúc khởi động và chỉ tạo admin đầu tiên khi database chưa có người dùng. Không seed học sinh/lớp demo trong Production. Script migration độc lập: `backend/schema.sql`.

Trước triển khai thực tế: dùng runtime .NET 10 đã cập nhật bản vá, HTTPS ở reverse proxy, secret store và tài khoản database ứng dụng riêng có quyền tối thiểu. Compose hiện dùng tài khoản `sa` để thuận tiện khởi tạo cục bộ; cần tách tài khoản migration/runtime khi vận hành. Chạy một API instance ở MVP; kiểm tra cơ chế điều phối worker trước khi scale nhiều instance.

## Kiểm thử

```powershell
dotnet test backend/CheckInApp.Tests/CheckInApp.Tests.csproj
npm run build --prefix apps/web
npx --prefix apps/mobile expo install --check
npm run export --prefix apps/mobile
# API đang chạy Development với dữ liệu demo:
node tests/api-smoke.mjs
```

HTTP smoke test tạo tài khoản/giáo lý viên thử nghiệm, duyệt rồi khóa, cấp lại thẻ và ghi một lượt điểm danh trong database Development. Không chạy trên database thật. Browser smoke test tạo thêm một lớp thử nghiệm.

`tests/ui-smoke.mjs` dùng Playwright từ runtime Codex hiện tại. Trên máy khác, đổi đường import Playwright sang bản đã cài của máy đó. Ảnh và báo cáo kiểm tra lưu ở `.local/ui-qa`.

Chi tiết kết quả và đối chiếu giao diện: [docs/verification.md](docs/verification.md).

Đã kiểm tra build .NET/web, bundle Android, APK release ký bằng khóa preview riêng, nghiệp vụ trên SQLite, migration và 39 kiểm tra tích hợp trên SQL Server/Redis Docker, cùng HTTP/browser cục bộ. APK đã xác minh chữ ký, manifest, ABI, API LAN trong bundle và hash khi tải qua LAN. Chưa xác minh cài đặt/camera trên điện thoại thật, quét thẻ in bằng thiết bị thực tế hoặc IPA. Đây là các bước nghiệm thu tiếp theo trước khi dùng dữ liệu thật.

## Các giới hạn hiện tại

- Chưa có đồng bộ offline, tài khoản học sinh/phụ huynh, điểm trễ/vắng có phép hoặc nhiều buổi trong ngày.
- Chưa có gửi email/reset mật khẩu tự phục vụ, notification, nhập Excel hay xuất PDF.
- Chưa có UI đổi vai trò của tài khoản đã hoạt động hoặc mở khóa tài khoản bị khóa; có thể quản lý permissions của vai trò hiện có. Không tự sửa database production để vượt quy trình duyệt.
- Roster hồi tố và nhật ký audit có API đọc giới hạn 100 bản ghi mới nhất; chưa có màn đối soát/audit riêng.
- Chưa kiểm thử tải, khôi phục backup và UAT thực địa. Cần thực hiện trước khi triển khai rộng.

## Nguồn tham khảo

- [ASP.NET Core policy authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0)
- [EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
- [Tailwind với Vite](https://tailwindcss.com/docs/installation/using-vite)
- [Expo Camera](https://docs.expo.dev/versions/latest/sdk/camera/)

Token màu/font của web nằm trong `apps/web/src/styles.css` và `production.css`; theme mobile nằm trong `apps/mobile/App.js`. Các lớp/học sinh được tạo trong Development là dữ liệu giả.
#   C h e c k I n A p p 
 
 