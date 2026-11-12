# CheckInApp — đặc tả chỉnh giao diện và điểm danh 02/11/2026

## 1. Trạng thái, nguồn và thứ tự áp dụng

**Người dùng đã yêu cầu dựng luôn; đã triển khai web/API và kiểm thử QA.** Kết quả, cách chạy và giới hạn thực tế xem [bản cập nhật](release-ui-2026-11-02.md); thuật toán/tối ưu bổ sung xem [tìm kiếm và hiệu năng](search-performance-2026-11-02.md). Nội dung bên dưới giữ đặc tả để đối chiếu. Tài liệu này được viết trước kế hoạch thực thi theo yêu cầu người dùng. Đây không phải thông báo phát hành. Các mặc định đề xuất dưới đây cần được phân biệt với yêu cầu đã nêu.

Nguồn: yêu cầu ngày 02/11/2026, ảnh tham khảo Bảng chấm công có ô ngày nhiều màu, mã web/API và tài liệu bản 0.2.0. Ảnh dùng làm design reference cho lịch tháng, ô ngày chia màu và chú giải; không đưa các nghiệp vụ công tác/nghỉ lễ/ngày phép của ảnh vào hệ thống giáo lý.

Tài liệu bổ sung [đặc tả hợp nhất](requirements-2026-11-01.md) và thay thế đúng các mục tương ứng của [mở rộng 01/11](design-dashboard-expansion-2026-11-01.md): mặc định năm học, tùy chỉnh widget, cấu trúc lịch và thao tác xóa. Các quyền, quét thẻ, Excel, roster lịch sử, nhiều buổi/ngày và quy tắc báo cáo còn lại giữ nguyên.

Áp dụng ui-ux nhánh U: tài liệu/brief trước, wireframe trước giao diện thật. Việc sửa tài liệu trong repo được người dùng yêu cầu trực tiếp. Ở bước tài liệu ban đầu chưa thay mã/dữ liệu; sau đó đã triển khai theo xác nhận dựng luôn của người dùng.

## 2. Brief và việc chính từng màn

- Sản phẩm: quản lý giáo lý và điểm danh HS/GLV (code và yêu cầu).
- Người dùng: quản trị viên, GLV trong quyền và phạm vi được cấp (code).
- Việc chính: vào đúng lịch để điểm danh; quản lý danh mục/lịch nhanh; đọc chuyên cần và cá nhân hóa tổng quan (yêu cầu).
- Nền tảng: web desktop và web điện thoại; native giữ phạm vi hiện có, không mặc nhiên mở rộng toàn bộ quản trị sang APK (mặc định).
- Trọng tâm: thao tác rõ trên từng bản ghi, xóa theo lựa chọn có xác nhận, lịch điểm danh trực quan theo từng buổi (yêu cầu).

| Màn | Việc chính | Thông tin quyết định | Hành động cuối |
| --- | --- | --- | --- |
| Điều hướng/điểm danh | Mở đúng nhóm và đúng loại điểm danh | Trạng thái mở/đóng, năm học, lớp, giờ, HS/GLV | Chọn buổi và điểm danh |
| Học sinh/GLV | Tìm đúng người, đổi cách xem | Tên Thánh + họ tên, mã, lớp/phân công, ảnh | Sửa dòng/card hoặc chọn để xóa |
| Năm học | Quản lý năm và năm hiện hành | Tên, ngày bắt đầu/kết thúc, hiện hành | Thêm/sửa/chọn xóa |
| Lịch | Xem khung rồi các lịch chi tiết bên trong | Lớp, khoảng hiệu lực, thứ, giờ, đối tượng, ngày từng buổi | Thêm/sửa đúng cấp hoặc chọn xóa |
| Báo cáo chuyên cần | Xem được kết quả đúng phạm vi | Năm, lớp, khoảng ngày, số buổi đủ điều kiện | Đọc tổng/chi tiết/xuất báo cáo |
| Tổng quan | Sắp widget vào vị trí còn trống | Lưới, kích thước widget, vùng đặt hợp lệ | Lưu bố cục cá nhân |
| Bảng điểm danh | Nhìn trạng thái từng buổi trong tháng | Người, lớp, ngày, giờ và màu từng buổi | Mở chi tiết ngày |

## 3. Audit và nguyên tắc giao diện

React 19/Vite, Tailwind 4, Lucide; chưa có thư viện chuyên dụng lịch/lưới kéo thả trong package web. Có Button, Select, DatePicker, Modal, Table, State, Badge trong `apps/web/src/ui.jsx` và PersonAvatar. Tái sử dụng, không tạo bộ control thứ hai, chưa tự thêm dependency.

Phong cách flat: kiểm tra file JSX/CSS không thấy tín hiệu glass, gradient hoặc utility shadow lớn. Giữ Inter, đỏ đô/trắng/be; màu nhấn hiện được ghi đè tại `apps/web/src/production.css`, font và token nền ở `apps/web/src/styles.css`. Đã có CSS scrollbar. Không bổ sung dark mode. Copy tiếng Việt; icon-only phải có tên truy cập, tooltip và trạng thái chọn bằng hình/viền ngoài màu.

Hiện trạng đọc từ code (không thay cho kiểm thử runtime):

- `main.jsx`: nhóm Điểm danh/Báo cáo dùng details/summary chỉ có chữ ở cấp cha.
- `ui.jsx`: DatePicker chỉ có nút tháng trước/sau, tiêu đề tháng chưa chọn trực tiếp.
- `DashboardPage.jsx`: cấu hình bằng danh sách lên/xuống và ẩn/hiện, chưa có tọa độ lưới; `DashboardController.cs` hiện giới hạn bốn loại widget.
- `ReportScreen.jsx`: có màn/API chuyên cần; khoảng ngày khởi tạo từ năm đang chọn cần kiểm tra khi năm tải bất đồng bộ/đổi năm. **Chưa xác định đây là nguyên nhân lỗi người dùng gặp.**
- Quan hệ dữ liệu trong `AppDbContext.cs` dùng nhiều DeleteBehavior.Restrict: xóa liên đới cần xử lý phía server, không chỉ thêm nút UI hoặc bật cascade toàn bộ.

## 4. Yêu cầu chi tiết

### R01 — Điều hướng mở/đóng và icon

Nhóm Điểm danh có icon nhận diện điểm danh, nhóm Báo cáo có icon biểu đồ; có chevron rõ ràng ở cạnh cuối, đổi hướng khi mở/đóng. Icon nhóm và chevron là hai vai riêng. Click/Enter/Space vào tiêu đề đóng/mở, có `aria-expanded`; mục con vẫn điều hướng. Nhóm chứa route hiện tại mở khi vào trang, mục đang xem có trạng thái active. Không hiện nhóm rỗng vì thiếu quyền. Hai nút Điểm danh thiếu nhi/GLV giữ riêng và rõ.

### R02 — Năm học hiện hành ở điểm danh

Yêu cầu: khi vào điểm danh, mặc định lấy năm học hiện hành. **Mặc định đề xuất mới:** ưu tiên năm duy nhất có IsActive trong phạm vi được phép; nếu không có, chọn năm duy nhất chứa hôm nay theo Asia/Ho_Chi_Minh. Nhiều năm được đánh dấu hiện hành, hoặc nhiều năm chứa hôm nay mà không có năm hiện hành duy nhất: hiển thị yêu cầu chọn và cảnh báo cấu hình. Không có năm phù hợp: hiển thị trạng thái chọn năm, không tải toàn bộ năm.

Quy tắc này thay thứ tự “chứa hôm nay trước, IsActive sau” ở B4 tài liệu hợp nhất. Không tự sửa cờ năm trong database. Lựa chọn cũ từ cookie không được ghi đè năm hiện hành khi khởi tạo mới; chỉ khôi phục lớp còn thuộc năm hợp lệ. Người dùng vẫn được chủ động đổi năm để xem lịch cũ trong phiên. Chờ tải năm xong mới truy vấn buổi; đổi năm phải xóa lớp/buổi không còn hợp lệ và bỏ qua phản hồi truy vấn cũ. Áp dụng cả HS và GLV.

### R03 — Danh sách/card HS và GLV

Hai nút chỉ icon List/LayoutGrid cạnh thanh tìm/lọc; có tooltip “Dạng danh sách”, “Dạng thẻ”, tên truy cập và `aria-pressed`. Mặc định danh sách như hiện tại. Chuyển kiểu xem giữ bộ lọc, trang, số dòng và lựa chọn đang có; hai kiểu dùng cùng nguồn dữ liệu và tổng số.

Card có ảnh hoặc chữ cái dự phòng, Tên Thánh + họ tên, mã và thông tin lớp/phân công hiện có. Không tải dữ liệu ngoài phạm vi chỉ để làm đầy card. Nút sửa nằm trên từng dòng/card; checkbox chế độ xóa có ở cả hai cách xem. Không trộn checkbox xóa với chọn in thẻ. Năm học không bắt buộc thêm dạng card.

### R04 — Lịch cha/con

Cha là **khung lịch lặp**: lớp, khoảng hiệu lực, thứ, giờ, HS/GLV, số buổi trong phạm vi đang xem. Mở chevron mới hiện con là **buổi/lịch điểm danh chi tiết**: ngày, giờ, đối tượng, trạng thái, SessionId để thao tác đúng buổi.

Mỗi cha có Sửa khung lịch, Thêm lịch chi tiết; mỗi con có Sửa lịch điểm danh. Nút Thêm khung lịch ở đầu trang. Lịch bổ sung không có cha được đặt trong nhóm “Lịch bổ sung”, không tạo quan hệ giả. Buổi được dời ngày vẫn giữ định danh và quan hệ nguồn nếu có. Lọc tháng/năm giới hạn danh sách con, chỉ rõ số buổi đang hiển thị; không tuyên bố là tổng toàn khung.

Sửa cha tác động lịch tương lai theo quy tắc cũ, không viết lại lịch sử; sửa con chỉ tác động một buổi và vẫn kiểm tra thời gian/xung đột/version. Xóa cha là thao tác riêng có phạm vi ở R06. Đóng/mở nhóm không làm mất lựa chọn xóa. Cha không có con hiện thông báo và nút thêm nếu có quyền.

### R05 — Sửa tại dòng, chọn xóa hàng loạt

Áp dụng HS, GLV, năm học, khung lịch và lịch chi tiết. Hiểu “giáo lý” trong yêu cầu là giáo lý viên theo ngữ cảnh; chưa tự mở rộng sang xóa lớp học độc lập.

1. Chế độ thường: nút Sửa trên từng dòng/card; nút Xóa cạnh Thêm trên thanh công cụ.
2. Nhấn Xóa chỉ vào chế độ chọn: hiện checkbox, số mục đã chọn, Hủy và “Xóa (n)”; n=0 thì nút thực thi không khả dụng.
3. Checkbox đầu danh sách chọn các dòng của trang đang xem, không ngầm chọn mọi kết quả ở trang khác. Đổi bộ lọc/trang xóa lựa chọn và báo rõ; đổi list/card giữ lựa chọn.
4. Lịch có checkbox riêng cha và con. Chọn cha nghĩa là xóa cả khung và dữ liệu con trong toàn khoảng hiệu lực, kể cả ngoài tháng đang xem; hộp xác nhận phải nói rõ. Cha được chọn thì con biểu thị được bao gồm, không đếm/xóa hai lần. Chọn vài con không ngầm chọn cả cha; dùng trạng thái mixed khi thích hợp.
5. Nhấn Xóa (n) mở hộp xác nhận, chưa xóa. Hiện tên/số mục, dữ liệu liên quan bị ảnh hưởng và cảnh báo nguyên văn ở R06. Hủy giữ dữ liệu; xác nhận mới gửi yêu cầu.
6. Đang xử lý chặn gửi lặp; thất bại giữ lựa chọn và lỗi có thể xử lý; thành công cập nhật danh sách, trang hợp lệ và số liệu liên quan.

### R06 — Xác nhận và phạm vi xóa liên đới

Mọi thao tác xóa dữ liệu có câu nguyên văn:

> Thực hiện xóa dữ liệu sẽ xóa luôn cả các dữ liệu đã phát sinh, bạn có chắc chắn muốn xóa?

Nút Hủy và Xóa dữ liệu, mặc định focus vào Hủy. Escape tương đương Hủy. Bổ sung tóm tắt ảnh hưởng bên dưới, không thay câu cảnh báo bằng câu khác.

| Đối tượng xóa | Dữ liệu liên quan cần xử lý | Không xóa theo dây chuyền |
| --- | --- | --- |
| Học sinh | Ghi danh, roster và điểm danh của người đó, thẻ, ảnh riêng | Lớp, năm, buổi và người khác |
| GLV | Phân công, điểm danh GLV, thẻ, ảnh riêng; gỡ liên kết tài khoản | Tài khoản đăng nhập, vai trò, lớp và người khác |
| Năm học | Lớp thuộc năm, khung/ngoại lệ/buổi, roster/điểm danh, ghi danh và phân công gắn các lớp đó | Hồ sơ HS/GLV dùng chung, tài khoản, dữ liệu năm khác |
| Khung lịch | Buổi thuộc khung, ngoại lệ thuộc khung, roster và điểm danh của các buổi đó | Khung khác cùng lớp/ngày, HS/GLV gốc |
| Lịch chi tiết | Buổi được chọn, roster và điểm danh thuộc buổi | Khung cha và các buổi khác |

Đây là phạm vi thực thi đề xuất để đáp ứng yêu cầu xóa dữ liệu phát sinh; khác thao tác hủy buổi cũ. Xóa một lần xuất hiện phải có dấu chặn sinh lại, để worker không tái tạo buổi vừa xóa. Audit tối thiểu ghi người thao tác, phạm vi và số lượng; không xóa audit bảo mật, không ghi token thẻ/mật khẩu. Ẩn widget chỉ thay bố cục, không phải xóa dữ liệu nghiệp vụ, dùng nhãn “Ẩn widget”.

Server kiểm tra quyền quản lý của loại dữ liệu và quyền/phạm vi với toàn bộ dữ liệu bị tác động; không cho xóa năm nếu chỉ được quản lý một phần lớp. Tính ảnh hưởng lại khi xác nhận; nếu version/phạm vi đã đổi thì trả xung đột để xem lại. Một lô là một transaction, lỗi bất kỳ mục nào không được xóa dở. Làm mới báo cáo/cache và thu hồi thẻ liên quan. Dọn tệp ảnh sau commit có cơ chế retry, không xóa ảnh dùng chung. Kiểm tra metadata in thẻ/lưu cấu hình có tham chiếu tới ID bị xóa.

### R07 — Sửa lỗi báo cáo chuyên cần

Ghi nhận lỗi người dùng: hiện chưa xem được. Bước triển khai phải tái hiện trên đúng route/tài khoản/quyền/phạm vi, kiểm tra lỗi JavaScript, request/status/response, bộ lọc năm/ngày và hợp đồng API; ghi nguyên nhân thật trước khi sửa.

Giữ đủ ba chế độ toàn bộ lớp, từng lớp, chưa điểm danh. Không truy vấn với năm chưa sẵn sàng. Khi đổi năm cập nhật khoảng ngày mặc định hợp lệ; không giữ SessionId/lớp từ năm cũ. Loading, lỗi có Thử lại, không có dữ liệu, không có quyền phải khác nhau. Lỗi ở truy vấn buổi/roster phải hiện rõ ở tab tương ứng.

Giữ mẫu số theo roster buổi HS đã kết thúc, loại tương lai/đang mở/hủy/GLV-only; tổng có trọng số, nhiều buổi/ngày không bị gộp sai. Xuất báo cáo cùng bộ lọc và phạm vi. Chỉ đánh dấu sửa xong sau kiểm thử dữ liệu thật hoặc fixture kiểm soát được; không dùng số giả che lỗi.

### R08 — Chọn nhanh tháng và năm

Nâng DatePicker dùng chung và mọi màn có điều hướng lịch: chọn tháng trực tiếp, chọn/nhập năm trực tiếp; giữ tháng trước/sau và về hôm nay nếu phù hợp. Chọn năm xa không cần bấm qua từng tháng. DatePicker ngày sinh không bị giới hạn bởi năm học; trường ngày lịch/năm học giữ giới hạn nghiệp vụ tương ứng.

Đổi tháng/năm chưa ghi giá trị ngày đến khi chọn ngày; xử lý tháng 2/năm nhuận và ngày 31 đúng. Popup trong modal không bị cắt, có bàn phím, Escape chỉ đóng lịch, focus quay về nút mở; cập nhật focus trap để bao gồm control chọn tháng/năm. Múi giờ ngày nghiệp vụ là Việt Nam.

### R09 — Tổng quan có lưới kéo thả

Chế độ “Tùy chỉnh tổng quan” hiện lưới vị trí trống như màn hình điện thoại. Mỗi widget có kích thước theo số ô, tay nắm kéo và nút Ẩn. Khi kéo có bóng vị trí dự kiến, trạng thái đủ chỗ/không đủ chỗ; thả vào vùng thiếu chỗ, ngoài lưới hoặc đè widget khác thì trả vị trí cũ. Không chồng widget hoặc làm mất widget khác.

Mặc định đề xuất: desktop 12 cột, tablet 6, điện thoại 2; kích thước theo loại widget, chưa có resize tự do. Bảng điểm danh tối thiểu 6 cột desktop, 6 tablet, 2 mobile; chiều cao đủ lịch 6 tuần và chú giải. Quy tắc chiếm ô phải tính cả chiều rộng/chiều cao; hiển thị khung đúng kích thước thực. Bố cục từng breakpoint lưu riêng để chuyển màn hình không phá bố cục đã lưu.

Có Lưu/Hủy, thêm lại widget đã ẩn, khôi phục mặc định. Hỗ trợ cảm ứng và phương án di chuyển bằng bàn phím/nút chọn vị trí cho người không kéo thả được. Hai hành động điểm danh vẫn dễ truy cập ngoài vùng widget. Lưu theo tài khoản phía server, có version chống ghi đè; chuyển cấu hình cũ từ thứ tự sang vị trí mặc định không mất tùy chọn. API phải xác thực loại widget, tọa độ, kích thước, trùng ô và quyền, không chỉ tin trình duyệt.

### R10 — Widget “Bảng điểm danh”

Lịch tháng T2–CN, chọn trực tiếp tháng/năm, số ngày và chú giải dưới lịch như ảnh. **Mặc định đề xuất:** trạng thái theo một người (HS hoặc GLV) trong phạm vi năm/lớp đang chọn; GLV có liên kết tài khoản có thể mặc định chính mình, người quản lý chọn người được phép xem. Chưa chọn người thì hiện yêu cầu chọn; không gom nhiều người thành một màu không có nghĩa.

| Trạng thái | Màu đề xuất | Quy tắc |
| --- | --- | --- |
| Đúng giờ | Xanh lá | Có giờ vào không sau giờ bắt đầu chuẩn đang dùng |
| Trễ | Cam | Giờ vào sau giờ chuẩn; dùng chung quy tắc phút muộn với báo cáo giờ |
| Vắng | Đỏ | Buổi đã kết thúc, người thuộc roster/phân công hợp lệ nhưng không có điểm danh |
| Không có lịch | Xám nhạt | Không có buổi áp dụng cho người đó trong ngày |
| Chưa điểm danh | Vàng | Buổi đang mở chưa ghi nhận; không tự kết luận vắng |
| Chưa đến giờ | Nền trắng/viền xám | Buổi tương lai; phân biệt với không có lịch |
| Đã hủy | Xám kèm ký hiệu gạch | Buổi hủy không tính vắng/mẫu số |

Giờ chuẩn tạm theo opensAtUtc như báo cáo giờ bản 0.2.0, không tự thêm thời gian ân hạn. Có mặt nhưng dữ liệu giờ thiếu/không hợp lệ: hiện “Chưa xác định giờ”, không đoán đúng giờ. Màu là mặc định thiết kế cần thống nhất token khi dựng; có nhãn/tooltip và chi tiết để không chỉ dựa vào màu.

Một ngày có hai buổi: chia ô thành hai phần **bằng nhau** theo thứ tự giờ tăng dần, mỗi phần một màu; ba buổi trở lên chia thành số dải tương ứng, không dùng trung bình màu hoặc chỉ lấy buổi cuối. Số ngày vẫn đọc được; chạm/click ngày xem danh sách từng buổi, lớp, giờ dự kiến/thực tế và trạng thái. Không hiểu tỷ lệ diện tích màu là thời lượng công như ảnh chấm công. Buổi thuộc nhiều lớp của cùng người vẫn có định danh riêng.

Tải theo tháng/phạm vi trong một truy vấn phù hợp, tránh mỗi ô gọi API riêng. Chống phản hồi tháng cũ ghi đè tháng mới; làm mới khi điểm danh/điều chỉnh/xóa có ảnh hưởng. Widget ẩn không tải dữ liệu; quyền xem người/điểm danh kiểm tra ở server. Số tổng nếu hiển thị phải ghi đơn vị buổi, không sao chép số ngày công thập phân trong ảnh.

## 5. Kế hoạch triển khai sau tài liệu

1. Chốt brief và mặc định; vẽ các hướng wireframe cho lưới tổng quan và lịch cha/con, kèm list/card và chế độ chọn xóa. Dùng nội dung minh họa, không ghi dữ liệu thật.
2. Tái hiện lỗi năm hiện hành và chuyên cần; lập ca hồi quy cho nguyên nhân tìm được.
3. Chốt hợp đồng dữ liệu: xóa lô/ảnh hưởng, quan hệ khung–buổi, lưới dashboard phiên bản mới, dữ liệu điểm danh tháng theo người. Kiểm kê khóa ngoại và metadata trước khi viết xử lý xóa.
4. Sửa component dùng chung: điều hướng icon/chevron, chọn tháng/năm, công cụ list/card, chọn xóa và dialog; nối nghiệp vụ thật theo yêu cầu, không giao handler rỗng.
5. Hoàn thiện lịch cha/con, danh mục, năm học, báo cáo; triển khai lưới và widget lịch sau khi API đủ dữ liệu.
6. Kiểm thử .NET/API/web theo phần thay đổi; kiểm tra giao diện 375/768/1440/1920, bàn phím/cảm ứng, quyền và lỗi; cập nhật hướng dẫn/phát hành bằng kết quả thực tế.

## 6. Tiêu chí nghiệm thu

| Mã | Ca kiểm tra bắt buộc |
| --- | --- |
| A01 | Hai nhóm menu có icon và chevron đúng chiều; mở/đóng bằng chuột/bàn phím; quyền hạn không tạo nhóm rỗng |
| A02 | Vào mới HS/GLV chọn đúng năm hiện hành; thử cookie năm cũ, năm tải chậm, không có/nhiều năm hiện hành, mất quyền, đổi năm chủ động |
| A03 | List/card cùng dữ liệu, giữ lọc/trang khi chuyển; tên dài, thiếu ảnh, rỗng, lỗi tải; sửa đúng người |
| A04 | Lịch nhiều khung/ngày, cha không con, con dời ngày, lịch bổ sung; sửa cha không đổi buổi đã học, sửa con không đổi buổi khác |
| A05 | Xóa một/nhiều mục, chọn trang, bỏ chọn/hủy, chọn cha+con, cha có con ngoài tháng; đếm và tóm tắt không trùng |
| A06 | Câu cảnh báo đúng nguyên văn ở mọi xóa dữ liệu; hủy không đổi dữ liệu; gửi lặp/concurrency/lỗi giữa lô không xóa dở |
| A07 | Xóa trên fixture cô lập đủ khóa ngoại/thẻ/ảnh; không mất người/năm khác; worker không sinh lại buổi; truy cập trái quyền bị chặn server |
| A08 | Chuyên cần mở và xem được cả ba tab; mẫu số/tổng có trọng số đúng với hai buổi/ngày, roster thay đổi, hủy, tương lai, GLV-only và dữ liệu vừa xóa |
| A09 | Chọn thẳng tháng/năm xa, năm nhuận/ngày 31, ngày sinh, popup trong modal ở 375px; không mất form hoặc lệch ngày do múi giờ |
| A10 | Kéo đủ chỗ/thiếu chỗ/ngoài lưới/trùng ô; hủy/lưu/ẩn/thêm lại; tải lại/đổi thiết bị/đổi tài khoản; nâng cấu hình cũ và xung đột version |
| A11 | Widget: 0/1/2/3+ buổi/ngày, đúng giờ/trễ/vắng/chưa đến giờ/đang mở/hủy; không có người liên kết, quyền hạn, tháng 6 tuần, cập nhật giờ/xóa |
| A12 | Không tràn ngang toàn trang ở 375px; icon có tên truy cập, focus rõ; trạng thái màu có giải thích; không cài thư viện hoặc đổi brand ngoài phạm vi |

Kết quả thực tế sau triển khai được ghi trong release-ui-2026-11-02.md; không suy các tiêu chí bên trên thành bằng chứng cho ca chưa chạy. Kiểm thử xóa dùng dữ liệu thử nghiệm cô lập, không dùng dữ liệu vận hành. Chỉ cập nhật release khi có bằng chứng build/test và ghi rõ giới hạn còn lại.
