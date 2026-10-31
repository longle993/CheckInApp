> **Cập nhật 01/11:** Đã triển khai toàn bộ đợt thay đổi. Xem [hướng dẫn và bằng chứng mới](release-2026-11-01.md). Nội dung bên dưới ghi lại các mốc cũ; đặc tả 01/11 được ưu tiên.

# Kiểm tra bản MVP

Ngày kiểm tra: 27/10/2026. Môi trường Windows, .NET 10, SQLite Development, Chromium và Node 24.

| Nội dung | Kết quả |
| --- | --- |
| Build solution .NET | Thành công, không có warning/error |
| Kiểm thử nghiệp vụ | 12/12 đạt |
| HTTP với API thật | 22/22 đạt: chờ duyệt, phạm vi lớp, quét trùng, idempotency, cấp lại thẻ, khóa phiên, số liệu báo cáo |
| Build web production | Thành công |
| Browser 10 màn × 3 kích thước | 30 trường hợp đạt ở 1440, 768, 375px; không có lỗi JavaScript, thông báo lỗi API hoặc tràn ngang toàn trang; tạo lớp qua form thành công |
| Export Android | Thành công; đây là bundle JavaScript/Hermes, chưa phải APK native đã ký |
| Probe bố cục nhanh | Không có cảnh báo ở 1440 và 375px; xem giải thích P1 bên dưới |

Probe P1 ở 768px báo cột “Lớp học” xuống hai dòng. Đã kiểm tra ảnh: tên lớp nằm trên một dòng; mã lớp và khung giờ là dòng thông tin phụ được chủ động đặt bên dưới. Loại cảnh báo này vì không có tên lớp bị ép xuống dòng. Bảng có thể cuộn ngang trên tablet; cột thao tác được giữ ở cạnh phải.

Đối chiếu phương án A: giữ sidebar, bộ chọn năm học, bốn thẻ thống kê, bảng lớp có lịch, cột giáo lý viên và nút mở điểm danh. Ngày, tên lớp, giáo lý viên và số liệu thay đổi theo dữ liệu API. Các màn CRUD và modal bổ sung trạng thái tải, rỗng, lỗi và kiểm tra dữ liệu.

Phương án C được triển khai bằng React Native: chọn buổi trong ngày, quét thẻ, kết quả xác nhận từ server, danh sách vừa điểm danh/chưa đến. Chưa nghiệm thu trực quan trên điện thoại hoặc giả lập native, camera thật, quét thẻ in, tải lớn hay khôi phục backup. Không suy diễn kết quả export bundle thành nghiệm thu thiết bị.

SQL Server/Redis Docker đã được kiểm thử tiếp theo, với 39 kiểm tra tích hợp và 30 trường hợp giao diện đạt. Xem [báo cáo Docker](docker-integration.md).

Ảnh và dữ liệu đo tự động nằm trong `.local/ui-qa/` của máy chạy kiểm tra, không đưa vào source control. Các smoke test có tạo dữ liệu trong database Development; không chạy chúng trên dữ liệu thật.
# Chỉnh lịch buổi tương lai ngày 31/10/2026

- API ScheduleManage kiểm tra phạm vi lớp, giờ cũ chưa bắt đầu, giờ mới lớn hơn hiện tại, kết thúc sau bắt đầu, buổi không hủy/chưa có điểm danh, version và lý do. Lưu ngoại lệ, snapshot buổi, roster, audit trong transaction; đồng bộ lịch cũng chặn chuyển buổi tương lai về quá khứ.
- 39/39 kiểm thử .NET đạt; web/API build đạt.
- `session-schedule-ui.mjs` đạt trên SQL Server: sửa buổi tương lai qua form, hiển thị giờ Việt Nam đúng, từ chối kết thúc trước bắt đầu, buổi đã bắt đầu và version cũ; không lỗi JS.

# Đổi mật khẩu ngày 31/10/2026

- Auth endpoint đổi mật khẩu của chính người đang đăng nhập, kiểm tra mật khẩu hiện tại, xác nhận, 12–128 ký tự có chữ/số, khác mật khẩu cũ; áp dụng rate limit auth. Lưu hash và audit không chứa mật khẩu, thu hồi mọi AuthSession và xóa refresh cookie.
- UI nút chìa khóa cạnh tài khoản ở cuối sidebar, form ba trường password có autocomplete phù hợp; thành công quay về đăng nhập với thông báo.
- 35/35 kiểm thử .NET đạt, build API/web đạt.
- `tests/password-change-ui.mjs` đạt trên SQL Server: admin mở form, QA đổi mật khẩu qua UI, từ chối mật khẩu hiện tại sai, JWT của hai phiên cũ và refresh token cũ bị chặn; mật khẩu cũ thất bại, mật khẩu mới đăng nhập được. Không đổi mật khẩu admin thật; tài khoản QA được khóa sau kiểm thử.

# Thanh công cụ và cửa sổ nhập Excel ngày 31/10/2026

- Ba nút Excel cùng hàng bộ lọc, icon FileOutput/Download/Upload, trên màn Lớp học/Học sinh/GLV; responsive xuống hàng khi thiếu chỗ.
- Nhập mở modal có file picker, checkbox chép đè, chọn lớp cho HS; chỉ gửi khi bấm Nhập dữ liệu. Trạng thái chép đè/file được đặt lại khi mở, lỗi giữ trong modal.
- Web build đạt; `tests/excel-toolbar-ui.mjs` đạt trên ba màn với phản hồi import giả lập để tránh ghi dữ liệu: cùng hàng, đủ icon, checkbox chỉ trong modal, gửi đúng overwrite/class/year, nút nhập chỉ bật khi có file/lớp; không lỗi JS, kiểm tra 375px và ảnh modal thực tế.

# Giáo lý viên và sửa nút nhập Excel ngày 31/10/2026

- Nguyên nhân nút tải mẫu/import bị khóa được người dùng xác nhận: chưa chọn lớp cụ thể khi đang ở màn Học sinh. Đã thay điều kiện disable bằng hộp chọn lớp và tiếp tục thao tác. Nhập dùng Button thật + file input ref, mở bộ chọn file bằng thao tác bấm. Lỗi API/file hiển thị rõ trên màn hình.
- GLV có FamilyName/GivenName; FullName đồng bộ và giữ tương thích màn thẻ/điểm danh/phân công. Migration SplitCatechistNames backfill dữ liệu cũ, không đổi tài khoản liên kết/thẻ/lịch sử.
- Mẫu/xuất GLV có đúng ba cột: Tên Thánh, Họ và tên đệm, Tên. ID tự quản lý; đối chiếu trùng theo FullName + SaintName trong phạm vi được phép và từ chối kết quả mơ hồ.
- 34/34 kiểm thử .NET đạt; API/web build thành công.
- `catechist-template-integration.mjs` đạt trên SQL Server: ba tiêu đề Việt, tự ID, tên tách/gộp, export, skip/overwrite, chặn trùng mơ hồ, chuyển dữ liệu cũ.
- `import-clicks-ui.mjs` đạt: bấm tải mẫu và import từ Toàn bộ lớp, chọn lớp rồi tải/mở filechooser thực, nhập HS/GLV thành công, form GLV tách tên và thẻ hiển thị FullName; không lỗi JS, viewport 375px không tràn.
- `extensions-integration.mjs` đạt lại các luồng điểm danh, ba loại Excel, file lỗi và phân quyền.

# Mẫu Excel học sinh và tách họ tên ngày 31/10/2026

- Migration `SplitStudentNames` thêm FamilyName/GivenName và chuyển dữ liệu cũ theo từ cuối là Tên. Giữ FullName, mã học sinh, ID và các bảng lịch sử. FullName được đồng bộ khi ghi và tiếp tục dùng cho thẻ, điểm danh, báo cáo/mobile.
- Mẫu/xuất học sinh chỉ có bốn cột tiếng Việt: Tên Thánh, Họ và tên đệm, Tên, Ngày sinh. Nhập lấy ClassId từ lớp được chọn, tự cấp ID/code; bắt buộc chọn lớp ở UI và API. Đổi năm học xóa lựa chọn lớp cũ để tránh nhập nhầm.
- Đối chiếu trùng bằng họ tên đầy đủ + ngày sinh trong lớp; từ chối kết quả nhiều hồ sơ. Chép đè giữ ID/code.
- Build API/web đạt; 31/31 kiểm thử .NET đạt.
- `student-template-integration.mjs` đạt trên SQL Server: template/export đúng bốn cột, ngày Việt Nam, tự lớp/ID/code, tên tách/gộp, skip/overwrite, ngày sai, thiếu lớp, trùng mơ hồ, chuyển dữ liệu legacy.
- `student-template-ui.mjs` đạt: tải file qua trình duyệt lấy đúng lớp, không cho nhập khi chưa chọn lớp, hai cột/ô tên, sửa tên và hiển thị FullName trên thẻ, viewport 375px không tràn và không lỗi JS.
- `extensions-integration.mjs` đạt lại cho luồng điểm danh HS/GLV, ba loại Excel và phân quyền.

# Sửa hiển thị lớp và lịch ngày 31/10/2026

- Sau khi tạo lớp, form trả dữ liệu đã lưu cho trang danh sách; chuyển về năm học của lớp mới, trang 1, lọc theo mã lớp mới để bản ghi xuất hiện ngay, kể cả khi danh sách đang ở năm học/trang/từ khóa khác.
- DatePicker render qua portal ngoài vùng cuộn modal, định vị theo viewport và tự mở trên/dưới. Giữ trong màn hình khi đổi tháng, cuộn hoặc resize; quản lý focus và Escape không làm mất form.
- Web production build đạt. `tests/class-calendar-regression.mjs` đạt với SQL Server/API thực: tạo lớp khác năm học và bộ lọc hiện tại; lịch đầy đủ/click được ngày cuối ở viewport 1440×900, 768×650, 375×640; không lỗi JS.

# Bổ sung ngày 29/10/2026

## Thẻ và quét giáo lý viên

- 22/22 kiểm thử .NET đạt; bổ sung cấp lại/thu hồi thẻ, QR/Barcode/retry, loại đối tượng, quyền và phân công.
- Migration `CatechistScanCredentials` áp dụng SQL Server; chỉ lưu hash mã thẻ GLV.
- `catechist-scan-integration.mjs` đạt trên SQL Server: buổi GLV-only, QR, Barcode quét lặp, retry, roster tách biệt, đối tượng tắt, sai phân công và thẻ thu hồi.
- `catechist-card-ui.mjs` đạt: cấp thẻ GLV, chọn QR/Barcode, kích thước 86×50 mm, quét Barcode trong buổi GLV-only, kết quả GLV, không lỗi JS.
- Expo Android export/Hermes đạt ngoài sandbox; chưa build APK mới hay thử camera điện thoại/thẻ in vật lý trong lượt này.

## Điều chỉnh chép đè và đối tượng buổi học

- Build API/web đạt. Tổng 19/19 kiểm thử .NET đạt; gồm snapshot HS/GLV, giữ buổi đã mở, chặn đối tượng không được bật.
- Migration `ScheduleAttendanceTargets` áp dụng trên SQL Server, mặc định true cho dữ liệu cũ.
- `extensions-integration.mjs` kiểm tra thêm: ba loại Excel mặc định bỏ qua dòng có ID và cập nhật khi `overwriteExisting=true`; ba loại lịch HS/GLV/cả hai, số roster đúng, API từ chối đối tượng bị tắt và từ chối lịch bỏ chọn cả hai.
- `overwrite-targets-ui.mjs` đạt: checkbox chép đè mặc định tắt trong cả ba danh mục; HS/GLV chọn độc lập trong lịch và ngoại lệ; không lỗi JavaScript.

- .NET build: đạt, 0 warnings/errors; 15/15 unit tests.
- Migration `20261029125848_AttendanceTimesAndCatechists` đã áp dụng SQL Server; giữ dữ liệu cũ.
- `tests/extensions-integration.mjs`: đạt trên API 5083, SQL Server/Redis thật; giờ học sinh/giáo lý viên, version conflict, ba vòng xuất/nhập Excel, file lỗi và bảy yêu cầu bị chặn vì thiếu quyền.
- `tests/ui-smoke.mjs`: 30 màn/kích thước, không lỗi JS, lỗi API hoặc tràn ngang; tạo lớp qua giao diện đạt.
- `tests/extensions-ui.mjs`: đạt; chọn QR/Barcode, đủ thông tin, kích thước CSS 86×50 mm, xuất PDF, dropdown trong modal, bảng giờ vào/ra. PDF mỗi định dạng có một trang; MediaBox khoảng 86.11×50.12 mm do làm tròn Chromium. Chưa kiểm thử in vật lý và quét lại từ thẻ giấy.
- Bản thử mới: web 5175 → API 5083. API cũ 5082 chạy quyền Administrator không thể dừng từ phiên này. Để dùng web chuẩn 5174, dừng cửa sổ API cũ và chạy lại `Start-Integration.ps1`.


