# Giao diện điểm danh và tối ưu tìm kiếm — 02/11/2026

## Trạng thái

Đã triển khai mã web/API theo [đặc tả](design-attendance-ui-2026-11-02.md), bổ sung [thuật toán tìm kiếm và số đo](search-performance-2026-11-02.md). Build web/API thành công. Bản này được kiểm thử trên môi trường QA riêng, không dùng thao tác xóa để thử trên dữ liệu vận hành. Không có thay đổi APK/native trong đợt này.

## Sử dụng

- Điểm danh và Báo cáo có icon nhóm và dấu thu/mở. Điểm danh ưu tiên năm IsActive duy nhất, nếu không có thì năm duy nhất chứa hôm nay; có nhiều năm mâu thuẫn thì yêu cầu chọn. Cookie lớp không ghi đè năm hiện hành.
- Học sinh/GLV có hai nút icon danh sách/thẻ. Bộ lọc, phân trang dùng chung; mỗi dòng/card có sửa. Năm học có tìm kiếm/phân trang và sửa tại dòng.
- Xóa cạnh Thêm đưa danh sách vào chế độ checkbox. Chọn mục, bấm Xóa (n), xem dữ liệu liên quan rồi xác nhận câu cảnh báo đã yêu cầu. API xem lại fingerprint trước khi xóa trong transaction; dữ liệu thay đổi thì từ chối và yêu cầu xem lại. Hủy không đổi dữ liệu. Tối đa 1.000 mục/lô, phù hợp số dòng tối đa của trang danh mục.
- Lịch hiện khung cha trước; mở ra thấy buổi theo tháng. Sửa/thêm ở đúng khung hoặc buổi. Chọn cha bao gồm các buổi ngoài tháng đang xem; không đếm trùng con. Xóa một buổi lưu ngoại lệ ngăn worker sinh lại. Nhóm Lịch bổ sung dành cho buổi không có khung cha.
- Bộ chọn ngày có tháng và năm trực tiếp. Báo cáo chuyên cần đã sửa lỗi thiếu import Stats, chờ năm hợp lệ và cập nhật khoảng ngày khi đổi năm; giữ ba chế độ báo cáo.
- Tổng quan: Tùy chỉnh → kéo tay nắm hoặc dùng mũi tên/Chọn vị trí → Lưu bố cục. Vùng thiếu chỗ bị từ chối. Có Ẩn, thêm lại và khôi phục mặc định; vị trí lưu riêng desktop/tablet/mobile, theo tài khoản và có version chống ghi đè. Ẩn widget không xóa dữ liệu nghiệp vụ.
- Bảng điểm danh: chọn đối tượng/người, tháng/năm. Mỗi buổi là một dải màu trong ô ngày, kể cả 2 hoặc 3+ buổi; mở ngày xem giờ và trạng thái chi tiết. Đúng giờ xanh, trễ cam, vắng đỏ, không lịch xám, đang mở chưa điểm danh vàng, tương lai nền trắng, hủy có gạch. Chưa chọn người thì lịch không cho mở dữ liệu, không suy đoán kết quả.
- Tìm kiếm nhiều từ AND, không cần đúng thứ tự; tên HS tìm được không dấu. Debounce 250 ms, abort request cũ, metadata ảnh tránh tải ảnh không tồn tại. Phạm vi và giới hạn đầy đủ ở tài liệu thuật toán.

## Bằng chứng kiểm thử

- **66/66 .NET tests đạt**: hồi quy cũ và ca mới xóa liên đới, giữ tài khoản, chặn ngoài phạm vi, fingerprint thay đổi, không sinh lại buổi, màu/trạng thái, ba buổi/ngày, lưới trùng ô, version và quyền widget, tìm kiếm và metadata ảnh.
- `tests/attendance-ui-release.mjs`: preview/conflict/commit xóa, khung/con/năm, API lịch, lưới không hợp lệ; 8 route × 4 viewport (375/768/1440/1920), list/card, checkbox/xác nhận/hủy, báo cáo mở được, lưu/ẩn/khôi phục bố cục, điều khiển kéo và chọn vị trí, chọn tháng/năm. Không có lỗi JavaScript hay tràn ngang toàn trang trong lượt đạt.
- Cùng bộ API đã đạt trên **SQL Server/Redis QA riêng**, bên cạnh SQLite; xóa child/parent/year và số buổi liên quan đúng.
- `tests/search-ui.mjs`: gõ nhanh 10 ký tự chỉ phát 1 request tìm, không gọi ảnh thiếu, tìm nhiều từ và thay truy vấn không gây lỗi JavaScript.
- Toàn bộ 8 migration, gồm `CatalogSearchIndexes`, chạy thành công trên database mới `CheckInAppUiMigrationQA`. Năm chỉ mục tương đương cũng được áp dụng lên database benchmark QA có dữ liệu.
- Probe ui-ux có đăng nhập tại 375/1440/1920: vòng cuối không còn mục P “Hỏng”. Hai ghi chú còn lại là số dòng của lịch hôm nay (probe tính cả ô tháng/ngày); các trường này được giữ vì cần nhận diện buổi. Phép Tab, modal và tương tác được kiểm tra trong bộ browser riêng; probe cuối dùng `--quick`.
- Ảnh/báo cáo cục bộ: `.local/ui-expansion-qa/`; số đo trước/sau: `performance-before.json`, `performance-after.json`. File phiên trình duyệt QA trong thư mục này là dữ liệu riêng, không đưa vào Git.

## Đối chiếu thị giác

Giữ flat, Inter và đỏ đô/trắng/be. Ảnh người dùng là design reference cho lịch nhiều màu; không sao chép nghiệp vụ ngày công. Bố cục tổng quan là lưới hai cột desktop, một cột mobile; slot giữ kích thước để kéo không làm các widget khác nhảy vị trí. Không bổ sung dark mode. Ô nhập giữ dạng không icon trái.

Đã xem ảnh desktop và mobile: card cùng loại có cấu trúc thống nhất; nút có hình thức tương tác; lịch cha/con dùng một khung mỗi nhóm; desktop dùng được hai cột; hai nút điểm danh vẫn là hành động nổi bật. Khoảng trống của slot tổng quan được giữ có chủ đích cho lưới, và lịch chưa chọn người vẫn hiển thị khung tháng vô hiệu để giải thích phần không gian dành cho widget.

Màu nhấn ở `apps/web/src/production.css` (`--primary`), font ở `apps/web/src/styles.css` (`--app-font` và import Inter). Màu trạng thái lịch ở phần `.attendance-color` trong production.css. Dữ liệu/ảnh QA là fixture thử nghiệm; không thay dữ liệu vận hành bằng dữ liệu mẫu.

## Chạy và nâng cấp

1. Build API mới và web cùng đợt. SQL Server vận hành cần migration `20261102143750_CatalogSearchIndexes`; migration chỉ thay chỉ mục, không xóa hồ sơ/điểm danh. Sao lưu theo quy trình đang dùng trước nâng cấp.
2. Khởi động API mới với cấu hình database/Redis và ContentRoot hiện có. **Giữ thư mục avatars và Data Protection keyring hiện hành**. Không trỏ môi trường vận hành sang database QA.
3. Khởi động web preview theo `scripts/Start-WebBuild.ps1` với API mới; tải lại trình duyệt. Không chỉ thay web trong khi API vẫn là bản cũ vì các endpoint bulk-delete/calendar và cấu trúc grid mới phải đi cùng nhau.
4. Môi trường QA có `scripts/Start-UiQa.ps1` (SQLite cổng 5095), `-Sql` (SQL cổng 5096), dùng build tại `.local/ui-verified-build`. Web QA cổng 5177 trỏ API 5095. Các script test mặc định trỏ QA; không đổi sang production để chạy test xóa.

## Giới hạn đã biết

- Chưa kiểm kéo cảm ứng/camera trên điện thoại vật lý hoặc đóng APK mới. Web responsive được kiểm tra bằng Chrome tự động.
- Chưa có load test đồng thời hay benchmark hàng chục nghìn hồ sơ. Số đo API không cải thiện đồng đều; chỉ khẳng định mức giảm request đã đo, không hứa tất cả API nhanh hơn.
- Tên GLV/lớp/năm vẫn theo collation database; không dấu độc lập provider chỉ có ở tên HS. Bộ chọn người widget giới hạn 100 kết quả và yêu cầu tìm để thu hẹp; danh mục quản lý vẫn phân trang.
- Worker dọn ảnh chạy sau commit mỗi 30 giây, thử lại khi I/O lỗi và giữ tệp còn thuộc hồ sơ tồn tại. Audit bảo mật được giữ. Thẻ in cũ không còn hợp lệ khi hồ sơ/thẻ bị xóa; cache lô in không được trả người đã mất.
- Không tự thay cờ năm hiện hành, sửa lịch sử điểm danh hoặc xóa dữ liệu vận hành để làm đẹp số liệu.
