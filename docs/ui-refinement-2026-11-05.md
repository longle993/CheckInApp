# Điều chỉnh UI và phân quyền 05/11/2026

Yêu cầu đã duyệt thực hiện trực tiếp, giữ brand Inter/đỏ đô/trắng, flat.

- Lịch điểm danh giới hạn chiều rộng, ô ngày gọn; số ngày/số buổi không có nền riêng; màu nền là trạng thái của từng buổi. Mặc định GLV liên kết từ catechistId của phiên, không phụ thuộc trang tìm kiếm100người. Chọn người khác vẫn được giữ khi tải lại dữ liệu.
- Liên kết GLV độc lập đổi vai trò, kể cả Admin; không lấy quyền của GLV thay quyền tài khoản. Kiểm tra quyền quản lý, tranh chấp liên kết và version trước lưu.
- Form tài khoản có label tách dòng, input đủ rộng, lý do textarea.
- Quyền nhóm theo sidebar; checkbox nhóm chọn/bỏ tất cả và có trạng thái chọn một phần. Danh mục năm/lớp/HS/GLV: xem, sửa (gồm thêm), xóa, export, import. Tách quyền xóa khỏi sửa tại API và UI, nâng quyền tương thích vai trò cũ bằng migration.
- Bảng HS/GLV: ảnh và checkbox cột cố định gọn, tên Thánh trước họ tên. Checkbox có focus rõ, hit area đủ thao tác.
- Kéo widget chỉ cập nhật khi đổi ô đích và giới hạn theo animation frame, tránh render lại toàn bộ nội dung widget. Không đánh đổi kiểm tra va chạm.
- Tối ưu báo cáo theo số liệu thực đo; giữ phạm vi quyền và không cache chéo tài khoản. Kiểm thử nghiệp vụ/ủy quyền tại QA, không xóa hồ sơ vận hành.

Kết quả thực thi và kiểm thử được bổ sung sau khi hoàn tất; các dòng trên là yêu cầu, không phải bằng chứng đạt.

## Kết quả 05/11/2026

- Đã thực hiện các thay đổi trên web/API, giữ font Inter và token màu hiện hành. Lịch tối đa440px, ô46px desktop/40px mobile, chữ nằm trực tiếp trên các dải màu. Bố cục cũ tự thu chiều cao widget lịch, giữ tọa độ nếu còn hợp lệ.
- Form sửa tài khoản tách label/input, textarea lý do3dòng. Gắn GLV qua UpdateCatechistLink độc lập RoleId; có kiểm tra409 khi GLV thuộc tài khoản khác. Không tự liên kết hồ sơ thật dựa vào trùng tên.
- Quyền được gom8nhóm tương ứng sidebar, checkbox nhóm có trạng thái một phần. Thứ tự danh mục: Xem, Sửa/thêm, Xóa, Export, Import rồi quyền phụ. Tất cả kiểm tại API. Import vẫn yêu cầu quyền sửa/thêm; export cần quyền xem dữ liệu trong phạm vi hiện hành. Đã thêm Excel năm học (ngày yyyy-MM-dd, Hiện hành0/1), xóa nhiều lớp giữ hồ sơ người/tài khoản.
- Migration20261105100000_SeparateCatalogPermissions chuyển quyền xóa tương đương từ quyền quản lý cũ một lần, Admin có đủ quyền mới; sau đó sửa và xóa độc lập. Không dùng phép suy quyền xóa động từ quyền sửa. Không đổi schema hồ sơ hoặc mật khẩu.
- Báo cáo chuyên cần: sessionById O(1), rosterByClass/rosterByStudent Lookup, enrollmentByClass và pendingSet thay các vòng Any/Single/Where lặp trên toàn tập mỗi học sinh/ngày. Xây lookup O(S+R+E+P); xử lý chi tiết theo số roster liên quan, vẫn có group/sort theo ngày. Giữ công thức chuyên cần, buổi chưa chốt, phạm vi và cache hiện hành. Chưa đổi thuật toán báo cáo giờ.
- Kéo widget: gom cập nhật preview qua requestAnimationFrame, bỏ setState khi ô đích không đổi, useMemo giữ nội dung widget khi preview đổi; cleanup frame khi drop/cancel/unmount. Không gửi API trong quá trình kéo; lưu khi bấm Lưu bố cục. Chưa đo FPS trên điện thoại vật lý nên không khẳng định hết lag mọi thiết bị.
- Sửa thêm retry API sau refresh token: gửi lại đúng yêu cầu một lần, tôn trọng AbortSignal, tránh kết quả401 cũ sau refresh thành công.
- 70/70 backend tests đạt, gồm Admin liên kết GLV không đổi vai trò, chống chiếm liên kết, sửa không cấp xóa, xóa lớp giữ người/tài khoản. Bộ QA32route/viewport, APIpreview/conflict/commit và thao tác lưới đạt; test refinement đạt lịch mặc định, chữ trong suốt, nhóm quyền và kích thước form. Đã xem ảnh calendar/permissions/profile. Ảnh tại `.local/ui-nov05-qa/`.
- Đã sao lưu SQL COPY_ONLY/CHECKSUM và VERIFYONLY thành công tại `.local/backups/pre-refine-20261105/`, triển khai API `.local/production-nov05-build`, xác nhận migration trong database vận hành. Smoke chỉ đọc qua tên miền đạt đăng nhập/API và10trang không lỗi JavaScript, giữ phiên. Không xóa/điểm danh thử trên dữ liệu vận hành.
- Đo localhost5lượt trên dữ liệu vận hành hiện có: tìm HS19–23ms; chuyên cần20–28ms; báo cáo giờ26–31ms. Mẫu nhỏ, có thể có cache; không phải benchmark tải đồng thời hoặc bằng chứng tỷ lệ nhanh hơn trước. Kết quả tại `.local/runtime/nov05-performance.json`.

## Bổ sung vị trí điều khiển, đổi chỗ và lịch

- Tùy chỉnh tổng quan render qua portal lên cùng thanh năm học; mobile dùng icon có aria-label/tooltip. Lối tắt chỉ icon, giữ tên truy cập.
- placeOrSwap: đặt vùng trống hoặc đổi tọa độ với đúng một widget bị chạm; kiểm tra lại kích thước và va chạm cả hai với các widget còn lại. Hai kích thước không phù hợp bị từ chối. Preview theo rAF giữ nguyên.
- Lịch quản lý lọc mục cancelled khỏi danh sách. Synchronize xóa vật lý buổi tương lai bị hủy khi không có dữ liệu HS/GLV, xóa roster tương ứng; giữ ngoại lệ để không sinh lại. Buổi có dữ liệu lịch sử được giữ.
- Đổi ngày buổi sang ngày có điểm danh HS hoặc GLV của cùng lớp trả409 TARGET_DATE_HAS_DATA trước khi lưu. Không ghi đè hoặc chuyển dữ liệu đã phát sinh. Đồng bộ khung lịch cũng chặn thay đổi buổi tương lai đã có dữ liệu.
- 72/72 .NET tests đạt, gồm cảnh báo ngày đích và xóa buổi hủy không sinh lại. tests/dashboard-swap.mjs đạt: topbar/icon/drag-swap/mobile không tràn, không JSerror. Web build và API publish .local/production-nov05-swap-build thành công.
- Triển khai API bổ sung CHƯA hoàn tất: công cụ chặn lệnh backup/restart với blocked by policy. API vận hành vẫn là production-nov05-build của lần trước; không coi cảnh báo ngày đích đã hoạt động trên vận hành. Web dist mới được build và dùng chung preview hiện hành nên giao diện được cập nhật, API mới cần triển khai đồng bộ.
