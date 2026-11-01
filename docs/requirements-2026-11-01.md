# CheckInApp — đặc tả hợp nhất và brief thay đổi 01/11/2026

> **Đợt bổ sung sau phát hành:** [Brief tổng quan, widget, năm học, lịch, báo cáo giờ, tài khoản, avatar/in lô và lazy load](design-dashboard-expansion-2026-11-01.md). Đây là tài liệu chờ duyệt, chưa triển khai; trạng thái hoàn thành bên dưới chỉ áp dụng đợt trước.

## Trạng thái và thứ tự áp dụng

Đã triển khai theo xác nhận “Toàn bộ tài liệu 01/11”. Phần A là kiểm kê trước thay đổi để bảo toàn nghiệp vụ; giới hạn một buổi/ngày và màn quét chung được thay bằng phần B. Phần B/C là đặc tả hiện hành.

42 kiểm thử .NET đạt; build API/web thành công; kiểm thử tích hợp SQL Server và Chrome đạt. Migration có sao lưu và đối chiếu dữ liệu. Xem [hướng dẫn, bằng chứng và giới hạn](release-2026-11-01.md). Camera và cài đặt APK trên điện thoại thật chưa kiểm thử.

## A. Kiểm kê chức năng đã có — không được làm mất

| Nhóm | Hành vi hiện có và giới hạn |
| --- | --- |
| Công nghệ | Web React/Vite JavaScript + Tailwind 4; app React Native/Expo; API .NET 10, MediatR, JWT, EF Core, SQL Server, Redis; DI qua Installers/InsertServiceInAssembly; AspNetRole và bitmask quyền |
| Năm học | Tạo/sửa, khoảng bắt đầu–kết thúc, năm hiện hành; lớp liên kết năm học |
| Lớp | Tạo/sửa, mã lớp duy nhất trong năm học, tìm tên/mã; lớp vừa tạo hiện ngay bằng cách chuyển đúng năm học, trang 1, tìm theo mã mới |
| Học sinh | Tên Thánh tùy chọn, Họ và tên đệm, Tên, ngày sinh; tự cấp ID/mã; FullName được ghép từ hai phần tên; ghi danh/chuyển lớp có hiệu lực, không tự đổi lịch sử buổi đã mở |
| Giáo lý viên | Tên Thánh, Họ và tên đệm, Tên; FullName; liên kết tài khoản, phân công lớp theo ngày hiệu lực |
| Tài khoản | Đăng ký/admin tạo đều chờ duyệt; admin duyệt/từ chối/khóa, chọn vai trò và liên kết GLV; tài khoản chưa duyệt không sử dụng được |
| Đổi mật khẩu | Đổi mật khẩu chính mình, xác minh mật khẩu hiện tại, xác nhận mật khẩu mới; 12–128 ký tự có chữ/số và khác mật khẩu cũ; hash, audit, thu hồi các phiên, đăng nhập lại; không ghi mật khẩu mới vào file secrets |
| Phân quyền | Quyền xem/quản lý năm, lớp, HS, GLV; lịch, điểm danh, điều chỉnh, báo cáo, cấp thẻ, phân công, audit, tài khoản/vai trò; nhập/xuất riêng từng loại; API luôn kiểm tra cả quyền lẫn phạm vi lớp |
| Lịch lặp | Chọn thứ, khoảng từ/đến, giờ mở/đóng; chọn HS, GLV hoặc cả hai; hiện tại mỗi lớp chỉ có một cấu hình lịch |
| Ngoại lệ | Ngày nghỉ/bổ sung, lý do, HS/GLV; hiện tại ngoại lệ theo lớp + ngày |
| Buổi học | Hiện tại duy nhất lớp + ngày; snapshot thời gian, HS/GLV và roster; chỉ các ngày cần điểm danh mới có buổi; buổi hủy không tính chuyên cần |
| Chỉnh lịch hiện tại | Sửa giờ trong cùng ngày cho buổi chưa bắt đầu/chưa hủy/chưa có điểm danh; giờ mới lớn hơn hiện tại, đóng sau mở; quyền ScheduleManage, phạm vi lớp, lý do và version; lưu ngoại lệ để worker không ghi đè |
| Thẻ HS/GLV | Cấp/in/lưu PDF khổ 86×50 mm, chọn QR hoặc Code128; mã ngẫu nhiên, chỉ lưu hash, hiển thị một lần, cấp lại thu hồi thẻ cũ; quyền CredentialManage |
| Điểm danh | Hiện dùng chung màn quét; camera/đầu đọc/mã thẻ; nhận diện HS/GLV, kiểm tra đối tượng buổi và roster/phân công; QR/Barcode, chống quét trùng, idempotency khi retry |
| Quyền quét | CheckIn; quét GLV thêm CatechistAttendance. Kiểm tra tại server, không chỉ ẩn nút |
| Giờ vào/ra thực tế | HS đã có bản ghi mới chỉnh giờ; GLV có thể ghi nhận/điều chỉnh với quyền tương ứng; giờ Việt Nam, giờ ra có thể trống, lý do, version và audit; không đồng nhất với giờ mở/đóng buổi |
| Báo cáo | Chưa điểm danh, chuyên cần các lớp/từng lớp; tính theo roster đủ điều kiện của các buổi HS đã kết thúc, bỏ buổi mở/tương lai/hủy/GLV-only; tổng có trọng số, không trung bình các tỷ lệ lớp; CSV hiện có |
| Excel học sinh | Đúng 4 cột: Tên Thánh, Họ và tên đệm, Tên, Ngày sinh; tự ID/mã, lớp từ lựa chọn; nhận dd/MM/yyyy, yyyy-MM-dd hoặc ngày Excel; đối chiếu họ tên + ngày sinh trong lớp, nhiều kết quả thì báo lỗi |
| Excel GLV | Đúng 3 cột: Tên Thánh, Họ và tên đệm, Tên; ID tự quản lý; đối chiếu họ tên + tên Thánh trong phạm vi được phép, báo lỗi nếu nhiều kết quả |
| Excel lớp | Đúng 2 cột: Mã lớp, Tên lớp; tự ID, năm học từ lựa chọn; đối chiếu mã lớp trong năm học |
| Nhập Excel | Modal chọn file + checkbox chép đè mặc định tắt + chọn lớp với HS; chỉ gửi khi bấm Nhập dữ liệu; dòng tồn tại bỏ qua hoặc cập nhật, ID/mã giữ nguyên; từng dòng độc lập, bảng lưu/bỏ qua/lỗi; không nhập lại toàn file mới nếu đã có dòng thành công |
| Giới hạn Excel | .xlsx tối đa 5 MB/1.000 dòng khi nhập, không nhận công thức/.xls/macro; nhập cần quyền nhập + quản lý, xuất cần xuất + xem; tải mẫu cần nhập hoặc xuất; xuất phạm vi được phép, không chỉ trang đang xem |
| Thanh công cụ | Tìm kiếm/lọc và 3 nút Xuất/Tải mẫu/Nhập cùng hàng trên desktop, có FileOutput/Download/Upload; mobile tự xuống hàng; chưa có lớp thì không khóa im lặng, hướng dẫn chọn lớp/năm hoặc tạo lớp |
| Phân trang | Lớp/HS/GLV: 10,25,50,100,150,200… theo tổng, tối đa 1.000; Trước, số trang, Sau cùng hàng số dòng; trang hiện tại nổi bật, có tổng; đổi số dòng/phạm vi về trang 1 |
| DatePicker | Portal ngoài vùng cuộn modal, tự mở trên/dưới trong viewport, bàn phím/Escape giữ form; dropdown lớp/năm dùng select không bị cắt |
| Bảo toàn dữ liệu | SQL migrations tách tên HS/GLV theo từ cuối là Tên; giữ ID, code, tài khoản liên kết, thẻ và lịch sử. Tên ghép đặc biệt có thể chỉnh trên form |
| Bảo mật phiên | JWT + refresh token hash/rotate, trạng thái tài khoản và phiên kiểm tra mỗi yêu cầu; khóa tài khoản/đổi mật khẩu thu hồi phiên; mật khẩu và token thẻ không ghi vào audit |
| Mobile/Android | Quét online, SecureStore cho refresh, camera, retry cùng request ID; có bản APK preview đã ký trước đây; mã mobile đã bổ sung GLV nhưng chưa đóng gói lại APK sau mọi thay đổi web gần đây; không có hàng đợi điểm danh offline |
| Triển khai | SQL/Redis Docker; secrets/khóa ký chỉ cục bộ; Vite preview cho phép chính xác longle993.io.vn; tên miền không thay thế xác thực/phân quyền/HTTPS |

## B. Yêu cầu đích của đợt thay đổi mới

### B1. Đổi cả ngày và giờ học

- Form Chỉnh lịch có Ngày học, Giờ bắt đầu, Giờ kết thúc, HS/GLV, lý do; hiển thị mốc giờ Việt Nam và version hiện tại.
- Buổi nguồn vẫn phải chưa bắt đầu, chưa hủy và chưa có bất kỳ bản ghi điểm danh nào. Không dùng đổi ngày để mở lại hoặc xóa lịch sử buổi đã học.
- Ngày–giờ bắt đầu mới phải **lớn hơn** thời gian server hiện tại khi lưu; bằng hiện tại cũng từ chối. Kiểm tra lại khi lưu, kể cả form mở từ trước.
- Ngày mới nằm trong năm học của lớp; kết thúc sau bắt đầu trong cùng ngày. Chưa mở rộng lịch qua đêm.
- Kiểm tra trùng giờ với mọi buổi không hủy khác của chính lớp đó, loại trừ buổi đang sửa; giữ quyền/phạm vi, kiểm tra version và lưu audit trước/sau.
- Giữ SessionId của buổi được chuyển. Chuyển ngày, ngoại lệ, roster và version trong một transaction; nếu lỗi thì không chuyển dở dang.
- Ghi ngoại lệ riêng cho lần xuất hiện nguồn và đích: worker không sinh lại buổi cũ và không ghi đè buổi mới. Không hủy cả ngày vì có thể còn các buổi khác.
- Roster học sinh được lập lại theo hiệu lực ghi danh tại ngày mới; GLV được lấy theo phân công hiệu lực ngày mới. Cảnh báo nếu ngày mới không có HS/GLV phù hợp, không âm thầm dùng danh sách ngày cũ.
- Sau lưu, danh sách chuyển sang tháng/ngày mới và làm nổi buổi vừa sửa; người dùng không tưởng buổi vừa biến mất.

### B2. Nhiều lịch trong một ngày

- Một lớp có nhiều khung lịch lặp và nhiều buổi/ngày; mỗi khung có từ/đến, thứ, giờ, HS/GLV.
- Vẫn khai báo HS/GLV trong cùng chức năng Lịch, không tạo cấu hình lịch GLV tách biệt.
- Mỗi buổi có định danh riêng. Quét, retry, roster, giờ vào/ra, audit và báo cáo phải dùng SessionId, không suy ra từ lớp + ngày.
- Không lồng/giao giờ trong **cùng lớp**, bất kể buổi HS-only, GLV-only hay cả hai. Đây là phạm vi mặc định đề xuất; các lớp khác nhau được chạy song song.
- Dùng khoảng nửa mở [bắt đầu,kết thúc): 08:00–09:00 và 09:00–10:00 hợp lệ; 08:30–09:30, trùng hoàn toàn hoặc bao chứa đều bị chặn. Buổi hủy không chiếm khung giờ.
- Lịch lặp phải kiểm tra các ngày thực sự giao nhau về khoảng hiệu lực và thứ; xét cả ngoại lệ/buổi đã dời. Không chỉ so giờ chuông hoặc chỉ so ngày bắt đầu lịch.
- Server kiểm tra xung đột trong transaction được tuần tự hóa theo lớp để hai yêu cầu đồng thời không cùng tạo lịch chồng giờ. Trả lỗi xung đột có ngày/khung giờ liên quan trong phạm vi được phép xem.
- Sửa/hủy một khung hoặc một lần xuất hiện không làm mất các khung còn lại; mặc định chỉ sửa một buổi khi bấm Chỉnh lịch. Sửa cả chuỗi phải là thao tác rõ ràng, chỉ áp dụng tương lai.

### B3. Tách màn điểm danh

- Tổng quan có hai hành động rõ: **Điểm danh giáo lý viên** và **Điểm danh thiếu nhi** (HS).
- Mỗi màn có tiêu đề, danh sách buổi, camera/đầu đọc, kết quả và thống kê riêng cho đối tượng. Chỉ liệt kê buổi bật đối tượng đó.
- Thẻ GLV vẫn QR/Barcode CG1; HS vẫn QR/Barcode CI1. Quét nhầm loại tại màn nào thì báo đúng loại thẻ cần dùng, không âm thầm ghi sang màn kia.
- Không tạo thêm bản ghi vì tách màn. Một người/buổi chỉ một kết quả có mặt, retry giữ nguyên idempotency.
- GLV dùng quyền CheckIn + CatechistAttendance, HS dùng CheckIn; các quyền xem/điều chỉnh và phạm vi lớp giữ nguyên, kiểm tra server cho cả camera/đầu đọc/request trực tiếp.
- Lịch vẫn bật HS/GLV/cả hai. Buổi cả hai xuất hiện ở hai màn nhưng dùng chung SessionId, thời gian và cấu hình; số liệu từng đối tượng độc lập.

### B4. Năm học, lớp mặc định và ghi nhớ

- Danh sách lớp/buổi ở hai màn điểm danh phải giao của: năm học đang chọn, phạm vi được giao, ngày xem và đối tượng HS/GLV. Không rơi về truy vấn tất cả năm khi thiếu điều kiện.
- ClassRoom hiện không có From/Until riêng; mặc định hiểu ngày từ/đến là khoảng năm học, lịch và hiệu lực phân công/ghi danh đã có. Không thêm hai cột ngày của lớp khi chưa có nhu cầu nghiệp vụ rõ.
- Mặc định năm học chứa ngày hôm nay; ưu tiên IsActive nếu có nhiều năm thỏa, tiếp đến StartDate mới nhất. Không có năm hợp lệ thì yêu cầu chọn, không tự dùng năm đã kết thúc.
- Lớp mặc định: lựa chọn đã nhớ còn hợp lệ; nếu không, lớp có buổi đang mở hôm nay đúng đối tượng; tiếp đến buổi sắp mở; nếu có nhiều lớp tương đương, hiển thị để chọn rõ. Không tự quét sang buổi kế tiếp khi buổi hiện tại kết thúc.
- Sau khi chọn lớp, hỏi một lần: **Lưu lựa chọn** hoặc **Chỉ dùng lần này**. Mặc định chưa có sự đồng ý thì chỉ lưu trong phiên đang dùng; không ghi cookie tùy chọn trước khi đồng ý.
- Web lưu cookie tùy chọn tối đa 30 ngày (đề xuất), chỉ chứa định danh năm/lớp/loại màn và phiên bản cấu trúc; không chứa mật khẩu, JWT, refresh token, thông tin học sinh. Host-only, Path=/, SameSite=Lax; Secure trên HTTPS.
- Cách ly tùy chọn theo người đăng nhập, kiểm tra quyền và hiệu lực lại mỗi lần khôi phục. Cookie bị sửa không tạo quyền; lớp mất quyền/năm hết hạn bị bỏ qua và người dùng được chọn lại. Có chức năng xóa/đổi lựa chọn, tắt ghi nhớ.
- Native app không có cookie web: dùng lưu tùy chọn theo tài khoản trên thiết bị (SecureStore đã có), cùng nguyên tắc đồng ý và hết hạn. Đổi tài khoản không áp dụng lựa chọn của người trước.
- Cookie localhost và tên miền là hai vùng lưu riêng; không hứa đồng bộ lựa chọn giữa thiết bị hoặc tên miền.

### B5. Tên hiển thị

- Dữ liệu vẫn tách SaintName, FamilyName, GivenName; FullName chỉ là Họ và tên đệm + Tên để tương thích và đối chiếu Excel.
- Tên hiển thị đọc: **Tên Thánh + FullName**, chuẩn hóa khoảng trắng, bỏ phần Tên Thánh khi trống. Ví dụ: Maria Nguyễn Văn An; không đổi nội dung trường FullName thành tên có Tên Thánh.
- Áp dụng thống nhất cho tổng quan, phân công, chọn GLV/HS, danh sách/kết quả quét, giờ vào/ra, chi tiết, thẻ, báo cáo và các thông báo có tên người ở cả web/native.
- Form và bảng quản lý dữ liệu tiếp tục có các cột tên riêng; không nhập lại tên Thánh vào cột Họ và tên đệm. Các bản xuất theo đúng schema đã duyệt, không tự thêm cột phá mẫu import.
- Không tự loại bỏ tiền tố trùng tên Thánh trong dữ liệu cũ bằng suy đoán; giữ dữ liệu và cho đối soát/chỉnh trên form.

### B6. Báo cáo khi có nhiều buổi/ngày — mặc định cần duyệt

- Chuyên cần chính tính theo **buổi đủ điều kiện**, có tên đơn vị rõ: dự kiến/có mặt/chưa điểm danh theo buổi; tổng trọng số theo roster; buổi GLV-only không vào mẫu số HS.
- Vẫn giữ các chỉ tiêu số ngày của báo cáo cũ nhưng không đếm một ngày hai lần. Đề xuất: ngày đủ = có mặt mọi buổi HS bắt buộc trong ngày; ngày chưa đủ = thiếu ít nhất một buổi; có thể ghi rõ ngày có mặt một phần trong chi tiết.
- Chỉ kết luận ngày sau khi mọi buổi HS không hủy của ngày đó đã kết thúc. Ngày còn buổi chưa kết thúc ở trạng thái đang diễn ra, không tính là ngày vắng.
- Chi tiết ngày liệt kê từng khung giờ và trạng thái. Không gộp buổi sáng đã có mặt để che buổi chiều còn thiếu.
- Đây là mặc định suy ra để tránh sai số do nhiều buổi, chưa phải kết quả kiểm thử hoặc nghiệp vụ đã triển khai.

### B7. Admin Console và thông tin web

- Gom hai menu **Tài khoản** và **Phân quyền** vào một menu cấp cao duy nhất có tên chính xác **Admin Console**. Không còn hai mục độc lập ở thanh điều hướng chính.
- Admin Console có ba mục con: **Tài khoản**, **Vai trò & phân quyền**, **Thông tin web**. Đây là sắp xếp lại điều hướng, không gộp hoặc bỏ các quyền/nghiệp vụ hiện có.
- Tài khoản giữ đầy đủ tạo tài khoản, danh sách chờ duyệt, duyệt/từ chối, khóa tài khoản, liên kết GLV và chọn vai trò. Vai trò & phân quyền giữ bitmask, phạm vi toàn đơn vị/theo lớp và quy tắc không cấp vượt quyền.
- Thông tin web cho phép sửa **Tên web** và tải lên/thay thế/xóa **Logo**. Phạm vi đợt này không tự mở rộng sang đổi domain, cấu hình mail, khóa JWT, dữ liệu kết nối hoặc bảng màu tùy ý.
- Tên web mặc định CheckInApp, bắt buộc nhập sau khi bỏ khoảng trắng đầu/cuối, đề xuất tối đa 100 ký tự; lưu dạng văn bản, không nhận HTML. Cập nhật tên ở nhận diện web, màn đăng nhập và tiêu đề tab trình duyệt.
- Logo đề xuất nhận PNG/JPEG/WebP tối đa 2 MB, kiểm tra nội dung ảnh thật ở server, giới hạn kích thước giải mã, sinh tên lưu trữ an toàn; không nhận SVG/HTML hoặc đường dẫn tệp tùy ý. Giữ tỷ lệ trong vùng hiển thị, có xem trước ảnh và tên web trước khi lưu.
- Xóa logo tùy chỉnh thì quay về logo mặc định; chưa có cấu hình hoặc ảnh không tải được cũng phải có nhận diện dự phòng. Nút Hủy không thay đổi cấu hình đã lưu.
- Cấu hình lưu bền ở server, không chỉ trong trình duyệt của người sửa; API công khai chỉ trả tên web và URL logo phục vụ màn đăng nhập. Thông tin nội bộ, đường dẫn tệp và lịch sử chỉnh sửa không được công khai.
- Bổ sung quyền riêng đề xuất **WebSettingsView / WebSettingsManage**; Admin hệ thống được cấp quyền phù hợp khi nâng cấp. Quyền RoleManage không tự đồng nghĩa với quyền sửa thông tin web.
- Menu Admin Console xuất hiện khi người dùng có ít nhất một quyền của các mục con; mỗi mục con và API tương ứng kiểm tra quyền riêng. Người có quyền xem tài khoản không vì thế được duyệt tài khoản, sửa vai trò hoặc đổi logo.
- Lưu tên/logo kiểm tra version để tránh ghi đè, thao tác nguyên tử: tải ảnh lỗi không làm mất logo hiện hành hoặc lưu dở cấu hình. Ghi nhật ký người sửa, thời gian, giá trị trước/sau và tham chiếu logo; giữ tài khoản/phân quyền hiện có.
- Trên mobile web, Admin Console truy cập qua trang **Khác** của bottom navigation, không thêm một tab chính riêng và không khôi phục drawer điều hướng cũ. Native app chỉ dẫn tới các chức năng được triển khai trên nền tảng đó; yêu cầu này chưa mặc định thêm bộ quản trị đầy đủ vào app native.
- Tên và logo web không tự đổi Android package ID, tên ứng dụng đã cài, launcher icon hoặc tên file APK. Các thay đổi native đó cần build/phát hành riêng, nằm ngoài mục Thông tin web của đợt này.
- Trạng thái: đã triển khai menu, API, quyền và màn cấu hình; kiểm thử SQL/Chrome đạt theo phần kết quả.

## C. Brief UI/UX và hướng triển khai

### Brief

- Sản phẩm: quản lý lớp giáo lý và điểm danh QR/Barcode, giữ các chức năng phần A (đọc code).
- Người dùng: admin, GLV được duyệt, người thao tác điểm danh trong phạm vi phân quyền (đọc code).
- Việc chính: chọn đúng năm/lớp/buổi; quét đúng HS hoặc GLV; khai báo/chỉnh nhiều lịch mà không chồng giờ (yêu cầu người dùng).
- Web desktop ưu tiên quản trị; web ở mobile và app native ưu tiên thao tác điểm danh (yêu cầu và code).
- Khác biệt cần nổi trên Tổng quan: hai lối điểm danh riêng và danh sách buổi theo năm/lớp hiện hành, không giấu trong menu.

### Bảng màu và điều hướng

- Chủ đạo đỏ đô; bề mặt card/input trắng; nền tổng thể trắng be. Giữ Inter và bộ component hiện có, phong cách flat, không thêm gradient/glass hoặc dark mode mặc định.
- Đỏ đô là màu thương hiệu/nút chính/trang đang chọn; màu lỗi là vai riêng có nhãn và icon, không chỉ dựa vào màu. Kiểm tra tương phản trước khi chốt token màu cụ thể ở wireframe.
- Desktop giữ sidebar phù hợp quản trị. Dưới 768px đề xuất bottom navigation cố định, tối đa 5 mục: Tổng quan, Thiếu nhi, GLV, Lịch, Khác; cùng cấu trúc trên app. Mục bị thiếu quyền không hiện hoặc không dẫn vào màn cấm.
- Khác là trang danh sách chức năng có quyền (lớp, HS, GLV, năm, báo cáo, **Admin Console**, hồ sơ/đổi mật khẩu/đăng xuất), không mở drawer/dropdown thay cho điều hướng chính. Admin Console chứa Tài khoản, Vai trò & phân quyền, Thông tin web; không lặp hai mục tài khoản/phân quyền ngoài menu này. Chỉ liệt kê chức năng đã triển khai trên nền tảng đang dùng.
- Dành padding đáy và safe-area cho thanh dưới; không che nút nhập, kết quả quét, số trang, bàn phím hoặc nội dung cuối danh sách. Ẩn thanh điều hướng tại đăng nhập; nhãn + icon, trạng thái hiện tại rõ.
- Thiết kế/test desktop 1440, tablet 768, mobile web 375 và native với safe-area. Web đã kiểm tra tại 1440/768/375; native đã triển khai, cần kiểm thử safe-area/bàn phím trên thiết bị thật.

### U2 — Việc chính từng màn

| Màn | Đến để làm gì | Dữ liệu giúp quyết định | Hành động chính |
| --- | --- | --- | --- |
| Tổng quan | Vào đúng loại điểm danh | Năm học, buổi đang mở/sắp mở, lớp được phép | Điểm danh GLV / Điểm danh thiếu nhi |
| Điểm danh thiếu nhi | Ghi nhận HS đúng buổi | Lớp, ngày, khung giờ, roster, tên hiển thị có tên Thánh | Quét QR/Barcode, xem kết quả/giờ vào-ra |
| Điểm danh GLV | Ghi nhận GLV đúng phân công | Lớp, buổi bật GLV, phân công, tên Thánh + họ tên | Quét thẻ GLV, xem kết quả/giờ vào-ra |
| Lịch | Tạo nhiều khung và dời ngày/giờ | Từng buổi/khung, đối tượng, xung đột, trước/sau chỉnh | Thêm khung / Chỉnh một buổi / Nghỉ-bổ sung |
| Chọn năm/lớp | Chọn và nhớ ngữ cảnh đúng quyền | Khoảng hiệu lực và buổi phù hợp hiện tại | Chọn lớp, đồng ý ghi nhớ hoặc chỉ dùng phiên |
| Báo cáo | Xem thiếu theo buổi/ngày | Năm, lớp, trạng thái chốt, khung giờ | Lọc, xem chi tiết, xuất |
| Danh mục và Excel | Giữ CRUD/import/export hiện có | Tên tách riêng, phạm vi năm/lớp, kết quả từng dòng | Thêm/sửa/nhập/xuất, phân trang |
| Admin Console / Tài khoản | Quản lý tài khoản theo quyền | Trạng thái duyệt/khóa, GLV liên kết, vai trò | Tạo, duyệt/từ chối, khóa và liên kết |
| Admin Console / Vai trò & phân quyền | Cấp đúng chức năng/phạm vi | Bitmask, toàn đơn vị/theo lớp, giới hạn người cấp | Tạo/sửa vai trò theo quyền hiện có |
| Admin Console / Thông tin web | Cập nhật nhận diện web | Tên hiện tại, logo hiện tại, xem trước thay đổi | Sửa tên, thay/xóa logo, lưu hoặc hủy |
| Khác/Hồ sơ | Tìm chức năng quản trị trên mobile | Quyền hiện tại, tài khoản đang dùng | Mở chức năng, đổi mật khẩu, đăng xuất |

Bảng trên là bố cục từ nghiệp vụ/code, không phải kết quả nghiên cứu bên ngoài. Người dùng đã yêu cầu dựng luôn; triển khai bằng bộ component hiện có theo skill ui-ux.

### Phạm vi kỹ thuật triển khai theo tài liệu/thiết kế

1. Bỏ ràng buộc duy nhất Session(ClassRoomId,Date), Schedule(ClassRoomId), Exception(ClassRoomId,Date) theo migration có backfill; thiết kế định danh khung/lần xuất hiện và ngoại lệ theo khung, không ghi đè ngoại lệ toàn ngày.
2. Khóa giao dịch theo lớp để kiểm tra khoảng giờ; xử lý đồng bộ, hủy, dời ngày và API cùng quy tắc; thêm kiểm tra YearId/đối tượng cho truy vấn buổi ở server.
3. Giữ ID buổi/roster/attendance/idempotency cũ; mỗi lịch cũ trở thành một khung, không tạo thêm buổi hoặc tăng mẫu số. Chạy báo cáo trước/sau migration để đối chiếu.
4. Thêm DisplayName nhất quán trong DTO/helper, không đổi key đối chiếu import; tách màn quét và bỏ kết quả/thẻ đang chờ khi đổi loại/năm/lớp/buổi.
5. Bottom navigation web/native và luồng đồng ý ghi nhớ; dùng component sẵn, không cài thư viện UI mới khi chưa cần.
6. Cập nhật test cũ còn giả định một ngày/một buổi, màn quét chung, nhãn FullName hoặc drawer. Build web/API/native, đóng gói APK mới và kiểm thử thiết bị sau khi phần mobile hoàn thành; không coi export Hermes là APK.
7. Gom route/menu tài khoản và vai trò dưới Admin Console; các URL cũ cần điều hướng tương thích và vẫn kiểm tra quyền. Thêm lưu trữ cấu hình web/logo, migration cấp quyền mới cho Admin, DTO công khai tối thiểu, cache invalidation và audit/version. Giữ nguyên tài khoản, quyền và phiên hiện hữu khi sắp xếp lại menu.

### Tiêu chí nghiệm thu đợt mới

| Mã | Ca bắt buộc |
| --- | --- |
| NEW-01 | Dời cả ngày/giờ sang tương lai trong năm học thành công; nguồn không bị worker tái sinh |
| NEW-02 | Bằng hiện tại, quá khứ, kết thúc <= bắt đầu, ngoài năm, buổi đã mở/hủy/có điểm danh bị chặn |
| NEW-03 | Nhiều buổi cùng ngày nối tiếp hợp lệ; giao nhau/trùng/bao chứa bị chặn qua API lẫn UI |
| NEW-04 | Hai yêu cầu đồng thời không vượt kiểm tra chồng giờ; version cũ bị chặn, không sửa dở dang |
| NEW-05 | Hủy/dời một buổi không ảnh hưởng buổi khác cùng ngày; sửa chuỗi không làm đổi lịch sử |
| NEW-06 | Hai màn quét đúng loại, đúng quyền, đúng năm/lớp/ngày; quét sai loại, sai buổi/phân công bị chặn |
| NEW-07 | Cùng thẻ ở hai buổi tạo hai kết quả; lặp/retry trong cùng buổi chỉ một kết quả |
| NEW-08 | Năm học hết hạn, đổi tài khoản, mất phân công, cookie giả/sai/expired đều không mở rộng dữ liệu |
| NEW-09 | Từ chối ghi nhớ không ghi cookie bền vững; đồng ý nhớ đúng tài khoản/ngữ cảnh, có xóa/tắt |
| NEW-10 | Tên Thánh + họ tên hiển thị đủ cả hai nền tảng, không lỗi với tên Thánh trống/tên dài; import key giữ nguyên |
| NEW-11 | Báo cáo nhiều buổi/ngày chính xác; buổi GLV-only/hủy/tương lai không làm tăng vắng HS |
| NEW-12 | Tất cả chức năng phần A được hồi quy: auth/đổi mật khẩu, quyền, CRUD/chuyển lớp, Excel, thẻ, thời gian, phân trang, tên miền |
| NEW-13 | Không tràn ngang ở 375px; bottom nav không che form/kết quả/camera, hoạt động với bàn phím/safe-area |
| NEW-14 | Migration không mất ID/thẻ/lịch sử; web/API/native build, kiểm thử APK mới và camera QR/Barcode trên thiết bị |
| NEW-15 | Chỉ còn menu Admin Console, đủ ba mục con theo quyền; desktop/sidebar và mobile/Khác truy cập được; URL cũ không bỏ qua phân quyền |
| NEW-16 | Lưu tên/logo thành công, nhận diện web/đăng nhập/tab cập nhật và vẫn đúng sau tải lại/khởi động lại; Hủy, xóa logo và ảnh lỗi có trạng thái dự phòng đúng |
| NEW-17 | Từ chối người thiếu quyền sửa cấu hình, tên trống/quá dài, HTML, tệp giả ảnh/quá dung lượng/SVG; version cũ và tải ảnh lỗi không ghi dở hoặc làm mất logo cũ |
| NEW-18 | API công khai chỉ có tên/URL logo; thay đổi có audit; nâng cấp cấp quyền Admin không làm mất vai trò/tài khoản; không tự thay package/icon/tên native đã cài |

### Trình tự đã được phê duyệt

1. Duyệt brief và các mặc định nêu rõ trong tài liệu.
2. Skill ui-ux: 2–3 wireframe có nội dung thật để chọn (Tổng quan, hai màn quét, Lịch, bottom navigation, Admin Console và Thông tin web), rồi chốt bộ component/token chung. Nếu người dùng yêu cầu dựng luôn thì bỏ bước chọn wireframe theo skill.
3. Triển khai migration/quy tắc lịch, API/phạm vi/tên, web/native, ghi nhớ, báo cáo; cập nhật từng tiêu chí từ chưa chạy sang kết quả thực có bằng chứng.
4. Cập nhật hướng dẫn chạy, tên miền, APK và hướng dẫn sử dụng; không xóa lịch sử kiểm thử cũ.

## Kết quả nghiệm thu 01/11

| Nhóm | Kết quả và giới hạn |
| --- | --- |
| NEW-01…05 | Unit và SQL integration đạt: dời ngày giữ ID/nguồn, khung nối tiếp, chặn chồng giờ và lưu đồng thời. Quy tắc thời gian, phạm vi, version kiểm tra tại server. |
| NEW-06…10 | SQL đạt quét đúng/sai loại, tên Thánh, lọc năm. Chrome đạt đồng ý ghi nhớ và reload. Native chưa thử camera vật lý. |
| NEW-11 | Unit kiểm tra hai buổi/ngày tham dự một phần; báo cáo tách số buổi/ngày. |
| NEW-12 | 42 ca .NET và bộ tích hợp mới đạt. Giữ chức năng cũ; chưa chạy lại mọi thao tác thủ công Excel/in thẻ đợt này. |
| NEW-13 | Chrome 1440/768/375 không tràn ngang; bottom nav hoạt động. Native bàn phím/safe-area cần thử thiết bị. |
| NEW-14 | Backup, migration và đối chiếu dữ liệu đạt; API/web/APK build. Cài và quét thiết bị thật còn chờ. |
| NEW-15…18 | Ba tab Admin Console theo quyền; SQL/Chrome đạt đổi tên/logo, version, public DTO tối thiểu, từ chối quyền/tên/ảnh giả. Lưu cấu hình có audit. |

Cấu hình web là bản ghi AuditLog có phiên bản và tệp PNG riêng trong API; không có bảng cấu hình riêng. Migration mới thay cấu trúc lịch. Roster lịch sử giữ nguyên; roster buổi tương lai tính lại theo ngày đích.
