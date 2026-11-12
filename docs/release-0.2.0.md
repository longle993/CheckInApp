# CheckInApp 0.2.0 — tổng quan A và mở rộng chức năng

## Sử dụng

Mở https://longle993.io.vn/ hoặc http://127.0.0.1:5175/. API cổng5083, SQL14333, Redis16379. Tải lại trang sau cập nhật. Các script chạy vẫn giữ như hướng dẫn `release-2026-11-01.md`.

- Tổng quan bố cục A: hai nút điểm danh cố định; Lịch bên trái, thông tin cá nhân và lối tắt bên phải. **Tùy chỉnh tổng quan** cho phép xóa, thêm lại/thay widget hoặc lối tắt, đổi thứ tự trong danh sách, khôi phục mặc định, Lưu/Hủy. Lưu riêng tài khoản ở server, có kiểm tra phiên bản; không thay tổng quan tài khoản khác. Widget lịch giữ cột chính theo bố cục A; các widget phụ theo thứ tự đã lưu. Native bật/tắt widget và dùng cấu hình cùng tài khoản; thứ tự chi tiết chỉnh trên web.
- Điều hướng có nhóm Điểm danh → Thiếu nhi/GLV và Báo cáo → Chuyên cần/Đi muộn về sớm. Mobile có tab Điểm danh mở hai lựa chọn, không mở drawer.
- Tạo/sửa năm học chặn mọi khoảng giao nhau, bao gồm năm không đánh dấu hiện hành. Ngày bắt đầu/kết thúc tính cả hai đầu; năm kế tiếp bắt đầu ngày hôm sau. Không thu hẹp năm làm lịch/buổi nằm ngoài khoảng mới. Mặc định chỉ chọn năm khi đúng một năm bao phủ hôm nay Việt Nam; có thể đổi năm thủ công.
- Lịch mỗi buổi một dòng, hai buổi chạm giờ được phép. **Thêm khung lịch** tạo mới; Sửa khung sửa đúng khung đang chọn. Không cần chèn1phút giữa hai buổi.
- Báo cáo đi muộn/về sớm lọc từ/đến, năm, lớp, GLV phụ trách theo ngày, đối tượng và vi phạm; phân trang và xuất CSV toàn kết quả. Chỉ tính buổi đã kết thúc, không hủy; muộn/sớm so với giờ buổi, phần phút dương làm tròn lên. Thiếu giờ ra không kết luận về sớm. Chưa có giờ học chuẩn độc lập với giờ mở/đóng nhận điểm danh.
- Admin Console: sửa họ tên/email và tùy chọn đổi vai trò/GLV theo quyền UserManage/RoleManage. Tài khoản khóa/từ chối có Xem xét duyệt lại, nhập lý do → Chờ duyệt → duyệt lại. Không tự hoạt động khi chỉ sửa thông tin; phiên đăng nhập cũ bị thu hồi khi sửa hồ sơ. Bảo vệ admin cuối và kiểm quyền người cấp. Phiên bản web/API hiển thị tại Thông tin web, native tại Khác.
- HS/GLV: nút Ảnh đại diện chọn ảnh PNG/JPEG/WebP ≤2MB, tối đa2048×2048; cắt vuông giữa ảnh, xem trước, thay/xóa. Ảnh lưu riêng phía API và được bảo vệ quyền xem hồ sơ. Excel không thêm cột ảnh. Thẻ có avatar, thiếu ảnh dùng chữ cái trên web.
- In thẻ hàng loạt: chọn người trong trang hoặc toàn kết quả lọc tối đa100/lô, bỏ chọn, xác nhận cấp lại rồi chọn QR/Code128 và in/lưu PDF A4. Mỗi thẻ86×50mm, tỷ lệ100%. Cấp lại vô hiệu thẻ cũ; không cấp mã khi chỉ mở hộp thoại. Lô dùng requestID, giao dịch toàn lô, gửi lại cùng yêu cầu trả mã cũ trong15phút. Nếu hết hạn cần tạo lô mới có xác nhận. Không có kết quả thành công một phần: một người sai phạm vi khiến cả lô bị từ chối.

## Dữ liệu và bảo trì

Không đổi schema trong đợt này: bố cục, ảnh và kết quả lô lưu metadata qua AuditLog; tệp ảnh tại `backend/CheckInApp.Api/.local/avatars`. Giữ thư mục này khi sao lưu. Kết quả mã thẻ lô lưu **mã hóa Data Protection** để retry15phút; không ghi mã thẻ dạng rõ vào audit. Giữ keyring Data Protection của tài khoản chạy API nếu chuyển máy. Ciphertext audit vẫn tồn tại sau hết hạn, API không trả kết quả hết hạn; cần cơ chế lưu trữ/dọn dữ liệu khi vận hành dài hạn.

Đọc dữ liệu thật ngày01/11: Thiếu Nhi2B chỉ có một cấu hình12:01–17:00, version12; chưa có cấu hình08:00–12:00 riêng. Không tạo bù hoặc sửa lịch sử. Dữ liệu có nhiều năm QA cũ chồng lên năm thật: hiển thị cảnh báo và yêu cầu chọn, không tự xóa/gộp. Các kiểm thử mới dùng năm tương lai không giao nhau, có tên Expansion QA; không sửa lớp/HS thật.

## Kiểm thử và giới hạn

- 42/42 kiểm thử .NET hồi quy đạt; API/web build thành công.
- `tests/expansion-release.mjs`: bố cục/phiên bản, năm chồng, hai buổi nối tiếp, ảnh giả, retry lô, báo cáo phạm vi và xem xét lại/sửa tài khoản đạt trên SQL thật.
- `tests/expansion-ui.mjs`: modal tùy chỉnh và21 tổ hợp route/viewport1440/768/375 đạt, không lỗi JavaScript; sai số đo chiều rộng1px. Ảnh kiểm tra ở `.local/expansion-qa/`.
- Web tách lazy module Tổng quan, quét, danh mục, lịch, báo cáo, Admin Console và in lô; camera tải khi bật, avatar tải gần viewport. Gói vào chính khoảng256KB so với392KB trước tách. Đây là kích thước build, không phải cam kết thời gian tải trên điện thoại.
- Native giữ phạm vi điểm danh/lịch/tổng quan; các chức năng quản trị, báo cáo và in dùng web. Chưa kiểm camera vật lý, bản in QR/Barcode hoặc safe-area/bàn phím trên điện thoại thật. Danh sách native vẫn cần đo thực tế khi dữ liệu lớn, chưa chuyển toàn bộ sang FlatList.
- Ảnh hiện crop giữa tự động, chưa có kéo/zoom căn ảnh. Báo cáo có dấu Đã điều chỉnh theo audit AdjustTimes. Xuất báo cáo giờ hiện là CSV, không phải XLSX; các Excel danh mục cũ giữ nguyên.

Màu/font giữ bộ nhận diện đỏ đô/trắng/be và Inter; không có dark mode. Các tiện ích mới vẫn áp dụng quyền và phạm vi tại server, không chỉ ẩn nút.

APK đã build thành công, xác minh chữ ký/package/bundle/endpoint; tệp `.local/android-preview/CheckInApp-LAN.apk` (0.2.0, versionCode3). Bản sao tải `.local/android-download/CheckInApp-LAN.apk`, checksum tại SHA256SUMS.txt.
