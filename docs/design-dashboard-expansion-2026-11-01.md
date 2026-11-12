# CheckInApp — brief và đặc tả mở rộng giao diện 01/11/2026

> Đợt tiếp theo: [chỉnh giao diện và điểm danh 02/11/2026](design-attendance-ui-2026-11-02.md), **đã triển khai web/API và kiểm thử QA**, xem [bản cập nhật](release-ui-2026-11-02.md). Đợt mới thay tùy chỉnh thứ tự widget bằng lưới kéo thả, bổ sung Bảng điểm danh, lịch cha/con và thao tác xóa theo lựa chọn; đồng thời cập nhật quy tắc năm mặc định và các lỗi cần sửa. Nội dung dưới đây mô tả đợt 0.2.0, không phải kết quả của đợt mới.

## 1. Trạng thái và phạm vi

**Đã chọn phương án A và triển khai bản 0.2.0.** Kết quả và giới hạn thực tế ở [release-0.2.0.md](release-0.2.0.md); phần bên dưới giữ đặc tả đã duyệt để đối chiếu. Không thay schema SQL trong đợt này. Đây là phần bổ sung sau bản phát hành 01/11; trạng thái từng nhóm cần đối chiếu với bản phát hành, không suy từ đặc tả ra kết quả kiểm thử. Khi có mâu thuẫn, tài liệu này thay thế đúng quy tắc tương ứng của `requirements-2026-11-01.md`; các chức năng khác giữ nguyên.

Nguồn: yêu cầu người dùng, ảnh hiện trạng lớp Thiếu Nhi 2B ngày 01/11 (ảnh hiển thị **12:01–17:00**, người dùng nêu thêm buổi 08:00–12:00), đối chiếu mã web/API hiện tại. Chưa truy vấn dữ liệu thật để kết luận nguyên nhân thiếu buổi.

Skill ui-ux áp dụng nhánh U1/U2: lập brief và nghiệp vụ trước; sau duyệt brief sẽ vẽ 2–3 wireframe để chọn, rồi mới triển khai. Việc ghi tài liệu dự án được người dùng yêu cầu rõ; không áp dụng giới hạn “chưa đụng file dự án” của bước wireframe vào các tệp tài liệu này.

## 2. Brief thiết kế

- Sản phẩm: quản lý lớp giáo lý, học sinh, GLV, điểm danh và báo cáo (người dùng/code).
- Người dùng: quản trị viên và GLV theo quyền/phạm vi lớp (code).
- Việc chính: vào nhanh đúng loại điểm danh; thấy đủ lịch theo năm học; quản lý và kiểm tra chuyên cần/giờ vào-ra (người dùng).
- Nền tảng: web quản trị desktop/mobile và React Native điểm danh; nghiệp vụ quản trị đầy đủ trên web, native nhận tổng quan/điểm danh/lịch/avatar/phiên bản phù hợp phạm vi hiện có (mặc định đề xuất).
- Trọng tâm: hai hành động điểm danh luôn rõ; các khối phụ cá nhân hóa, không lấn át việc điểm danh (người dùng + đề xuất).

Giữ Inter, đỏ đô `#781f36`, trắng và trắng be, flat; chưa bổ sung dark mode. Token hiện tại ở `apps/web/src/production.css`, font trong CSS/entry web; native giữ token tương ứng. Tái sử dụng Button, Modal, Select, DatePicker, Table, State, Badge và Lucide. Ảnh gửi là ảnh hiện trạng để chẩn đoán, không phải mẫu thiết kế mới. Các tên/ví dụ trong tài liệu chỉ minh họa, không thêm vào dữ liệu thật.

## 3. Cấu trúc điều hướng

Desktop:

```text
Tổng quan
Điểm danh
  Điểm danh thiếu nhi
  Điểm danh giáo lý viên
Lớp học / Học sinh / Giáo lý viên / Năm học / Lịch
Báo cáo
  Báo cáo chuyên cần (toàn bộ báo cáo hiện tại)
  Báo cáo đi muộn về sớm
Admin Console
  Tài khoản / Vai trò & phân quyền / Thông tin web
```

Menu cha mở/thu danh sách con, trạng thái hiện tại rõ; ẩn mục thiếu quyền, không hiện cha rỗng. Giữ tương thích URL cũ và kiểm quyền tại API. Báo cáo hiện có giữ đủ các chế độ lớp, từng lớp, chưa điểm danh.

Web mobile: thanh dưới **Tổng quan – Điểm danh – Lịch – Báo cáo – Khác**, chỉ hiện mục hợp quyền; Điểm danh mở trang có hai lựa chọn/nhóm con, không mở drawer. Native dùng Tổng quan – Điểm danh – Lịch – Khác; Báo cáo chỉ thêm khi có màn native tương ứng, không đưa tab chết. Hai nút trên Tổng quan vẫn đi trực tiếp vào đúng màn quét.

## 4. Tổng quan và tùy chỉnh

Đầu trang: tiêu đề Tổng quan, bộ chọn năm học, nút **Tùy chỉnh tổng quan**. Khối chào nhỏ: “Xin chào, Giuse Nguyễn Văn An” + avatar; dùng tên Thánh và họ tên GLV liên kết, nếu chưa liên kết thì tên tài khoản; không tự gán tên Thánh. Tên dài được xuống dòng, thiếu avatar dùng chữ cái.

Hai hành động chính: **Điểm danh thiếu nhi**, **Điểm danh giáo lý viên**, theo quyền. Đây là vùng cố định; widget/lối tắt được chỉnh phía dưới.

Danh mục khối ban đầu, không thêm số liệu/KPI ngoài nhu cầu:

| Khối | Nội dung | Tùy chỉnh |
| --- | --- | --- |
| Xin chào | Avatar và tên hiển thị | Khối nhỏ mặc định bật; được ẩn |
| Thông tin cá nhân | Tên, vai trò, lớp phụ trách trong năm đang chọn | Bật/tắt, thứ tự |
| Lịch điểm danh | Các buổi theo bộ lọc, giờ, lớp, HS/GLV, trạng thái; mỗi buổi một dòng | Bật/tắt, hôm nay/7 ngày, thứ tự |
| Lối tắt | Lớp học, Học sinh, GLV, Lịch, Báo cáo, Admin Console theo quyền | Thêm/bỏ, thứ tự từ danh mục có sẵn |

Modal tùy chỉnh chia “Widget” và “Lối tắt”; checkbox + nút lên/xuống, không bắt buộc kéo thả; Xem trước, Lưu, Hủy, Khôi phục mặc định. Không cho chèn HTML/URL tùy ý. Hủy không ghi thay đổi; lỗi lưu giữ bản nháp. Khôi phục mặc định chỉ ảnh hưởng tài khoản hiện tại.

Đề xuất lưu cấu hình theo tài khoản trên server, có phiên bản; thứ tự desktop/mobile cùng ngữ nghĩa, mobile xếp một cột. Mất quyền thì ẩn khối và không tải dữ liệu của khối đó. Cấu hình widget tách biệt cookie đồng ý ghi nhớ lớp; không tận dụng cookie để chứa thông tin cá nhân. Khi chưa có cấu hình hoặc cấu hình cũ không hợp lệ, dùng mặc định tương thích.

## 5. Năm học: chống chồng ngày và mặc định

- Ngày bắt đầu/kết thúc là ngày địa phương Việt Nam, hai đầu đều bao gồm. Hai năm chồng nếu `new.Start <= existing.End && existing.Start <= new.End`; loại bản ghi đang sửa khỏi phép so sánh. Năm sau bắt đầu sớm nhất ngày kế tiếp ngày kết thúc năm trước.
- Áp dụng tạo/sửa API lẫn UI, kể cả năm không đánh dấu hiện hành. Kiểm tra đồng thời trong giao dịch với cơ chế khóa phù hợp; không chỉ kiểm tra trước khi gửi form.
- Lỗi hiển thị tên và khoảng ngày của năm đang xung đột. Khi sửa khoảng năm phải bảo đảm không đẩy các lịch/buổi và dữ liệu phụ thuộc ra ngoài phạm vi; từ chối với danh sách ảnh hưởng, không tự sửa lịch sử.
- Mặc định đăng nhập/lần mở mới lấy năm chứa **ngày hiện tại Việt Nam**. Đây là quy tắc mới ưu tiên hơn năm được lưu cookie trước đây. Sau đó người dùng có thể đổi năm để xem dữ liệu lịch sử; giữ lựa chọn trong phiên. Cookie lớp chỉ khôi phục khi thuộc năm đang chọn và còn quyền.
- Không có năm phù hợp: hiện “Chưa có năm học phù hợp hôm nay”, yêu cầu chọn năm, không âm thầm lấy năm gần nhất. Trong dữ liệu cũ có nhiều năm cùng bao phủ hôm nay: báo xung đột và yêu cầu chọn, không tự sửa/xóa dữ liệu. Các lần tạo/sửa mới vẫn bị chặn chồng khoảng.
- Đổi năm cập nhật lớp, lịch, điểm danh, widget, báo cáo; bỏ lớp/GLV/buổi không hợp lệ, bỏ kết quả quét đang chờ. Hủy/không nhận response cũ để không hiển thị chéo năm.

## 6. Lịch: nhiều buổi cùng ngày và lỗi đang phản ánh

Mỗi AttendanceSession là một dòng, khóa bằng ID buổi, sắp ngày → giờ bắt đầu → lớp → ID. Nhóm tiêu đề ngày được phép nhưng không gộp dữ liệu theo ngày/lớp. Ngày 01/11 có 08:00–12:00 và 12:00–17:00 phải có **hai dòng**; 12:01–17:00 cũng là khung riêng hợp lệ. Khoảng lịch dùng quy tắc [bắt đầu, kết thúc): hai buổi chạm 12:00 được phép, không cần chèn một phút. Hiển thị cả buổi đã kết thúc khi xem lịch ngày/tháng, không chỉ buổi sắp tới.

Thêm khung và Sửa khung là hai hành động riêng, hiển thị rõ tên/giờ khung đang sửa. Sau lưu phải tải lại đủ danh sách, chuyển tháng phù hợp và đánh dấu buổi mới; phản hồi “Đã thêm khung 08:00–12:00”. Lưu lỗi không báo thành công. Các cấu hình không sinh buổi do thứ/ngày/ngoại lệ phải giải thích nguyên nhân.

**Bằng chứng hiện có:** `Schedule` trong `main.jsx` dùng `items.map` với `key=s.id`, không chỉ lấy một buổi/ngày; nút Cấu hình lịch đang lấy khung đầu bằng `find`, có nguy cơ người dùng sửa khung cũ thay vì thêm khung mới. Đây là giả thuyết UX, chưa phải nguyên nhân đã xác nhận.

Checklist chẩn đoán trước khi sửa: đối chiếu năm/lớp; số Schedule và Session ngày 01/11; payload tạo có ID cũ hay không; thứ/khoảng hiệu lực; ngoại lệ/hủy; response API có hai buổi không; cache/bản build đang chạy; filter trạng thái và thứ tự. Không tạo bù buổi hoặc điểm danh lịch sử để che lỗi. Nếu khai báo hồi tố, phải xác định riêng ảnh hưởng mẫu số báo cáo, không tự tăng số buổi vắng của học sinh.

Widget Lịch dùng cùng nguồn và quy tắc hiển thị; nếu giới hạn số dòng phải có “Xem tất cả N buổi”, không làm buổi thứ hai biến mất im lặng.

## 7. Báo cáo đi muộn về sớm

Menu con độc lập, giữ nguyên báo cáo chuyên cần. Bộ lọc: từ/đến, năm học, lớp, GLV phụ trách, đối tượng HS/GLV/cả hai, loại vi phạm muộn/sớm/cả hai. Đề xuất mặc định tháng hiện tại giao với khoảng năm học, chưa có năm thì chưa tải báo cáo. GLV phụ trách lọc theo phân công có hiệu lực **ngày buổi học**, không chỉ phân công hiện tại; không nhân đôi dòng khi có nhiều GLV.

Một dòng = một người trong một buổi. Cột: ngày, lớp, khung giờ, đối tượng, mã, avatar nhỏ, tên Thánh + họ tên, GLV phụ trách, giờ vào, giờ ra, phút muộn, phút về sớm, tình trạng dữ liệu. Có phân trang, tổng người/buổi vi phạm, tổng phút và xuất theo phạm vi/quyền; dữ liệu xuất không giới hạn ở trang đang xem.

Mặc định đề xuất: dùng giờ bắt đầu/kết thúc đã lưu của buổi làm chuẩn, ngưỡng miễn trừ 0 phút (chưa bổ sung chức năng cấu hình ngưỡng). Muộn nếu vào > bắt đầu; sớm nếu ra < kết thúc. So sánh timestamp đầy đủ trước khi làm tròn; phút hiển thị làm tròn lên phần dương, ghi rõ trong tooltip. Thiếu giờ ra là **Chưa có giờ ra**, không kết luận về sớm; vắng không tính là đi muộn. Buổi hủy không tính; buổi đang mở đánh dấu tạm tính, báo cáo chốt mặc định chỉ lấy buổi kết thúc. Giờ chỉnh sửa hợp lệ là giá trị dùng báo cáo, có dấu đã điều chỉnh và audit.

Giờ đóng cửa nhận quét hiện có có thể khác giờ kết thúc học thực tế: cần nêu rõ quy ước này khi duyệt brief. Nếu muốn tách hai khái niệm, phải bổ sung giờ học chuẩn trước khi triển khai công thức; không tự suy đoán từ lần quét cuối.

## 8. Admin Console, duyệt lại và phiên bản

- Danh sách tài khoản thêm Sửa thông tin và Xem xét duyệt lại cho Suspended/Rejected. Luồng đề xuất: Khóa/Từ chối → Chờ duyệt lại (bắt buộc lý do) → Hoạt động khi duyệt vai trò/phạm vi/GLV hợp lệ. Không tự mở tài khoản khi chỉ lưu thông tin.
- Admin sửa họ tên, email, GLV liên kết, vai trò/phạm vi theo quyền riêng `UserManage` đề xuất; duyệt lại dùng UserApprove, đổi vai trò cần RoleManage. Kiểm email duy nhất, GLV liên kết hợp lệ, chống cấp quyền vượt quyền người thực hiện và bảo vệ admin hoạt động cuối cùng.
- Thu hồi phiên khi thay đổi danh tính đăng nhập/quyền/trạng thái; mở lại không khôi phục token cũ. Audit trước/sau, lý do, người làm, thời điểm, version chống sửa đồng thời. Không xem mật khẩu/hash, không đổi mật khẩu qua form hồ sơ này.
- Thông tin web thêm khu **Phiên bản hệ thống** chỉ đọc: phiên bản phát hành web, API, build ID/thời điểm build. Native hiển thị versionName/versionCode của APK tại Khác; không nhầm version optimistic concurrency của cấu hình với phiên bản sản phẩm. Dữ liệu lấy từ metadata build; không để admin nhập số phiên bản giả. Không hiện đường dẫn máy, secrets hoặc cấu hình kết nối.

## 9. Avatar và in hàng loạt

- HS/GLV thêm ảnh tùy chọn: xem trước, chọn/thay/xóa, crop vuông; PNG/JPEG/WebP tối đa 2 MB, server xác thực nội dung/kích thước và chuẩn hóa, giới hạn đề xuất 2048×2048. Không nhận SVG/HTML, không giữ EXIF. Lưu reference ảnh và thumbnail; ảnh riêng tư theo quyền xem người, không đặt đường dẫn công khai đoán được.
- Thiếu/lỗi ảnh dùng chữ cái; danh sách, chi tiết, kết quả điểm danh và mẫu thẻ đều hỗ trợ ảnh. Lazy-load ảnh ngoài viewport; đặt sẵn kích thước tránh nhảy layout. Excel hiện giữ cột cũ, không thêm ảnh nhúng hoặc bắt buộc URL trong đợt này.
- Thẻ giữ **86×50 mm**, QR hoặc Barcode Code128. Có ảnh, tên Thánh + họ tên, mã, lớp/năm và thông tin đang có trên mẫu; tên dài xuống dòng, không cắt QR/Barcode hoặc vùng trắng quanh mã. Không hy sinh độ đọc mã để phóng ảnh.
- In hàng loạt HS/GLV: checkbox dòng/trang, “Chọn tất cả kết quả lọc” có tổng số rõ, bỏ chọn, xem trước, chọn QR/Barcode, chọn bố trí giấy A4 và in/lưu PDF ở tỷ lệ 100%. Đổi bộ lọc hủy lựa chọn hoặc yêu cầu xác nhận rõ trước khi in; không ngầm chọn ngoài bộ lọc.
- Ràng buộc quan trọng: hệ thống hiện chỉ lưu hash mã thẻ, không thể dựng lại mã gốc. **In hàng loạt cần cấp mới/cấp lại mã**, cảnh báo cụ thể các thẻ cũ sẽ bị vô hiệu khi người dùng xác nhận cấp lại. Xem trước bố cục dùng mã mẫu không hợp lệ, chưa làm mất thẻ cũ. Không âm thầm cấp lại khi mở modal.
- Cấp theo lô có idempotency, kết quả từng người, chống retry sinh nhiều mã, kiểm quyền CredentialManage và phạm vi. Chốt giới hạn đề xuất 100 thẻ/lô; lô lớn chia có tiến độ. Mã gốc chỉ trong kết quả/phiên tạo bản in, không log/cache lâu dài; nếu mất file sau cấp lại phải cấp lại rõ ràng. Không lưu mã gốc lâu dài để tiện in lại nếu chưa có thiết kế bảo mật riêng.

## 10. Lazy load và hiệu năng

- Web tách module theo route: tổng quan, danh mục, điểm danh, lịch, hai báo cáo, Admin Console. React.lazy/Suspense + error boundary; thư viện camera chỉ tải khi bật camera, mã in/QR/Barcode chỉ tải khi mở cấp/in thẻ, export chỉ tải khi dùng.
- Widget ẩn không gọi API; widget ngoài viewport tải khi gần xuất hiện, widget điểm danh/lịch đầu màn tải ưu tiên. Mỗi khối có skeleton, rỗng, lỗi và thử lại riêng; không để lỗi một widget làm trắng trang.
- Server phân trang danh mục/báo cáo, truy vấn theo phạm vi năm/lớp và cột cần thiết; không tải toàn bộ dữ liệu rồi gọi đó là lazy load. Chống request trùng, hủy request khi đổi bộ lọc, cache có key tài khoản/năm/lớp và invalidation sau thay đổi.
- Native dùng danh sách ảo và chỉ tải dữ liệu màn có focus; không hứa chia bundle OTA chỉ bằng React.lazy. Đo trên APK thật; camera chỉ giữ khi màn quét đang hoạt động.
- Ghi baseline rồi so kích thước bundle/request/memory và thời gian chuyển màn ở cùng dữ liệu/thiết bị. Nghiệm thu: không tải camera/print trên lần mở tổng quan, không request widget ẩn, không response chéo năm, không ảnh làm lệch dòng. Chưa có số liệu hiệu năng đo cho đợt này.

## 11. Việc chính từng màn và ý tưởng bố cục

| Màn | Việc chính / dữ liệu quyết định | Hành động cuối |
| --- | --- | --- |
| Tổng quan | Nhận đúng năm, buổi và loại người | Điểm danh HS/GLV; mở lối tắt |
| Tùy chỉnh | Chọn khối cần dùng, thứ tự rõ | Lưu hoặc khôi phục mặc định |
| Năm học | Kiểm khoảng ngày, xung đột | Lưu khoảng hợp lệ |
| Lịch | Thấy mọi buổi theo ngày/giờ/lớp | Thêm khung hoặc sửa đúng khung/buổi |
| Điểm danh | Đúng đối tượng, năm/lớp/buổi | Quét và nhận kết quả |
| Báo cáo giờ | So giờ chuẩn và thực tế | Lọc, kiểm tra dòng, xuất |
| Tài khoản | Thông tin, trạng thái, quyền | Sửa hoặc duyệt lại có lý do |
| Thông tin web | Nhận diện và build đang chạy | Sửa nhận diện; xem phiên bản |
| Hồ sơ/in thẻ | Ảnh, danh tính, mã đúng người | Lưu ảnh; xác nhận cấp/in lô |

Quy ước bố cục ở đây dựa trên component và nghiệp vụ hiện có, chưa khảo sát sản phẩm bên ngoài. Ý tưởng để phát triển ở bước wireframe (chưa phải phương án cần chọn bằng lời):

- **A — Ưu tiên điểm danh (đề xuất):** lời chào nhỏ trên cùng, hai hành động trực tiếp, dưới là lịch chiếm 2/3 và thông tin cá nhân/lối tắt chiếm 1/3; mobile xếp một cột.
- **B — Ưu tiên lịch:** lời chào và hai hành động giữ đầu, lịch trải rộng, widget cá nhân/lối tắt thành dải bên dưới. Phù hợp nhiều buổi trong ngày.
- **C — Tổng quan mô-đun:** hai hành động cố định, các widget còn lại thành lưới có thứ tự tùy chỉnh. Linh hoạt hơn nhưng cần kiểm soát để không biến tổng quan thành nhiều card ngang hàng.

Mọi phương án đều có nội dung/trạng thái tương đương: chưa có lịch, hai buổi cùng ngày, tên dài/thiếu avatar, thiếu quyền, đang tải/lỗi, năm không phù hợp. Probe desktop 1440, tablet 768, mobile 375; bàn phím và safe-area thử native. Giai đoạn tài liệu chưa tạo wireframe hay chạy probe, không ghi nhận giả kết quả UI.

## 12. Triển khai và tiêu chí nghiệm thu dự kiến

Thứ tự: duyệt brief → wireframe 2–3 phương án → chốt phương án → sửa năm/lịch và test → menu/tổng quan → báo cáo → admin/phiên bản → ảnh/in lô → lazy load → hồi quy/build/migration có backup. Không làm migration tự gộp/xóa các năm cũ chồng nhau.

| Mã | Ca nghiệm thu bắt buộc — đều chưa chạy trong đợt này |
| --- | --- |
| EXT-01 | Tạo/sửa năm giao nhau/bao chứa/trùng biên bị chặn, hai request đồng thời không lọt; năm kế tiếp ngày hôm sau hợp lệ |
| EXT-02 | Chọn năm theo hôm nay VN; không có/nhiều năm rõ trạng thái; đổi năm không sót lớp/GLV/response cũ |
| EXT-03 | Ngày 01/11 có hai dòng 08–12 và 12–17, cả hai còn hiện sau reload; chồng giờ bị chặn; thêm không ghi đè khung đầu |
| EXT-04 | Tùy chỉnh widget/lối tắt lưu riêng tài khoản; hủy/khôi phục đúng; thiếu quyền không tải dữ liệu |
| EXT-05 | Menu cha/con và URL cũ đúng quyền; mobile không có drawer hoặc tab chết |
| EXT-06 | Muộn/sớm đúng biên, giây/làm tròn, thiếu giờ ra, vắng, buổi hủy/mở; phân công lịch sử và nhiều GLV không nhân dòng |
| EXT-07 | Khóa/từ chối → chờ duyệt lại → hoạt động; chặn thiếu quyền, giữ admin cuối, token cũ không sống lại, audit/version đầy đủ |
| EXT-08 | Phiên bản web/API/APK đúng metadata thật, phân biệt version cấu hình; không lộ secrets |
| EXT-09 | Avatar upload/thay/xóa, giả ảnh/quá cỡ bị chặn, ảnh được bảo vệ; tên dài/thiếu ảnh đúng trên thẻ |
| EXT-10 | Chọn trang/tất cả kết quả, lô100, cấp lại có xác nhận, retry không cấp trùng, lỗi từng người rõ; giấy đúng86×50; quét QR/Barcode bản in thật |
| EXT-11 | Lazy-load route/camera/print/widget đo được, không request thừa hoặc sai phạm vi; lỗi chunk có thử lại |
| EXT-12 | Hồi quy auth/quyền, Excel, phân trang, đổi lịch, báo cáo cũ, thẻ cũ khi chưa cấp lại; build web/API/APK và kiểm thử thiết bị |

Các mặc định cần duyệt cùng brief: tùy chỉnh riêng từng tài khoản; ngày năm học bao gồm hai đầu; ưu tiên năm theo hôm nay hơn cookie; chuẩn muộn/sớm dùng giờ buổi và ngưỡng0; native chưa mở rộng quản trị/báo cáo;100 thẻ/lô và cấp lại có xác nhận do mã cũ chỉ lưu hash.

## 13. Trạng thái triển khai 0.2.0

Người dùng đã chọn A, yêu cầu xóa/thay widget và lối tắt. Đã triển khai web và API; native có menu Điểm danh, lời chào, bật/tắt widget chung tài khoản, ảnh và phiên bản. APK0.2.0 build3 ký/kiểm tra thành công.

EXT-01…05: có kiểm tra server, layout A và menu nhóm; tích hợp kiểm năm chồng/hai buổi/layout version đạt. EXT-06: báo cáo giờ, lọc và CSV, dấu điều chỉnh đã triển khai; kiểm dữ liệu biên thời gian trên thiết bị và phân công lịch sử phức tạp cần UAT. EXT-07…09: sửa/duyệt lại, metadata phiên bản, ảnh bảo vệ quyền đã có; tích hợp avatar thật/giả/thay/xóa và luồng tài khoản đạt. EXT-10: lô giao dịch toàn bộ hoặc từ chối, tối đa100, retry idempotent15phút; không trả kết quả một phần như đề xuất ban đầu. EXT-11: route lazy, camera động, ảnh gần viewport, build chính~256KB; chưa đo native dữ liệu lớn. EXT-12:42 unit đạt,21 route/viewport đạt, build API/web/APK đạt; in giấy và camera vật lý còn cần thử.

Các khác biệt triển khai: crop ảnh giữa tự động, chưa kéo/zoom; layout chính/phụ của A cố định, đổi thứ tự widget phụ và lối tắt được lưu; native không có các chức năng quản trị web và chưa chuyển toàn bộ danh sách sang FlatList. Không ghi nhận những phần này là đã kiểm thử vật lý.
