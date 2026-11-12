# Triển khai vận hành 02/11/2026

- Đã khởi động Docker Desktop, SQL Server và Redis hiện có; giữ database CheckInAppIntegration.
- Backup COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY thành công trước migration. Bản lưu: `.local/backups/pre-ui-20261102/database.bak`; SHA256 `93075C809E656145E29DC435FFB19C520678AE6AB3877305AA88F0AE010853F3`.
- Đã sao lưu API .local (ảnh/cấu hình) và keyring nếu có vào cùng thư mục. Đây là dữ liệu riêng, không đưa Git.
- Publish Release API tại `.local/production-ui-build`, build Vite thành công; chạy API5083 với ContentRoot cũ, web5175. Migration CatalogSearchIndexes đã có trong lịch sử đủ 8 migration.
- Web và API web-settings trả HTTP200 cả localhost lẫn https://longle993.io.vn.
- Smoke đăng nhập chưa đạt: cả thông tin Integration và bootstrap cục bộ trả401; không reset mật khẩu hoặc thay tài khoản. Vì vậy chưa xác nhận các chức năng yêu cầu đăng nhập trên vận hành. Bộ QA trước triển khai đã đạt66tests và32viewport nhưng không thay thế nghiệm thu vận hành.
- Không chạy bộ thử xóa trên dữ liệu vận hành. Log khởi động trong thư mục backup. Dịch vụ API/web đang chạy nền; chưa thiết lập tự khởi động lại sau reboot cho hai tiến trình này.

## Điều tra 401 và kiểm thử tiếp

- Đối chiếu PBKDF2 Identity với hash SQL: tài khoản trong Integration secrets có tồn tại/Active nhưng mật khẩu lưu không khớp; email bootstrap development không tồn tại trong database SQL hiện hành. Không phải lỗi migration hoặc lỗi quyền (tài khoản chưa duyệt trả403).
- Tài khoản mobile UAT lưu tại `.local/mobile-uat/credentials-and-fixture.json` tồn tại/Active, mật khẩu khớp. Không đổi mật khẩu hoặc tài khoản, không ghi bí mật vào tài liệu. Không tự ghi đè secrets Integration vì đây là cấu hình bootstrap, không phải nguồn đồng bộ mật khẩu sau thay đổi.
- Đăng nhập và API đọc danh mục HS/GLV/lớp/năm, tìm kiếm, báo cáo chuyên cần, báo cáo giờ, lịch màu và dashboard đạt. Browser10route: tổng quan/lớp/HS/GLV/năm/lịch/hai báo cáo/hai màn điểm danh, giữ phiên đăng nhập, không lỗi JavaScript. Đây là smoke chỉ đọc: không phát sinh điểm danh, không sửa/xóa dữ liệu vận hành. Chức năng ghi/xóa đã kiểm trên QA; chưa được xem là nghiệm thu ghi/xóa trên production.
- Node cần `--use-system-ca` để dùng CA Windows khi kiểm HTTPS; không tắt xác minh chứng chỉ.
- Cùng bộ smoke đăng nhập/API/10route cũng đạt qua HTTPS https://longle993.io.vn, xác minh đường public proxy/cookie. Không chỉ kiểm HTTP200 trang đăng nhập.

## Cập nhật 05/11/2026
Đã triển khai bản sửa UI/phân quyền tại .local/production-nov05-build sau backup VERIFYONLY; xem [chi tiết và kiểm thử](ui-refinement-2026-11-05.md). Migration SeparateCatalogPermissions đã xác nhận trên database vận hành; smoke HTTPS10route đạt.
