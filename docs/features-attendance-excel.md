> **Cập nhật 01/11:** Đã triển khai toàn bộ đợt thay đổi. Xem [hướng dẫn và bằng chứng mới](release-2026-11-01.md). Nội dung bên dưới ghi lại các mốc cũ; đặc tả 01/11 được ưu tiên.

# Giờ vào/ra, thẻ và Excel

> Hiện trạng trước đợt 01/11/2026. [Đặc tả hợp nhất](requirements-2026-11-01.md) ghi yêu cầu đích; tách màn quét, nhiều buổi và tên hiển thị có tên Thánh đang chờ triển khai.

## Sử dụng

- **Giáo lý viên** → **Cấp thẻ** → chọn QR hoặc Barcode, in/lưu PDF khổ 86×50 mm. Quyền cấp thẻ dùng `CredentialManage`; cấp lại thu hồi mã cũ. Thẻ GLV có mã ngẫu nhiên riêng (CG1), chỉ lưu hash ở server.
- Quét HS/GLV trong cùng màn Điểm danh bằng camera hoặc đầu đọc Barcode; server nhận diện loại thẻ. Người thao tác cần `CheckIn` và, khi quét GLV, thêm `CatechistAttendance`. Buổi cần bật GLV và giáo lý viên phải có phân công hiệu lực trong ngày đó. Quét lặp/retry không tạo bản ghi thứ hai; điểm danh GLV không thay số có mặt của HS.
- Mã nguồn mobile đã cập nhật nhãn kết quả HS/GLV và danh sách GLV; đã export bundle Android/Hermes thành công. APK phát hành trước đó chưa được đóng gói lại trong lần bổ sung này.

- **Điểm danh** → chọn ngày/buổi → khối **Giờ vào / ra và điểm danh giáo lý viên** bên dưới danh sách quét. Học sinh phải được điểm danh trước khi sửa giờ. Giáo lý viên lấy từ phân công có hiệu lực ngày học. Giờ hiển thị theo Việt Nam; có thể để trống giờ ra. Mỗi lần lưu cần lý do và được ghi nhật ký; dữ liệu thay đổi đồng thời trả lỗi để tải lại.
- **Lịch** vẫn cho phép cấu hình giờ mở/đóng của lịch điểm danh. Giờ vào/ra thực tế được lưu riêng, không sửa giờ mở/đóng của buổi.
- **Học sinh** → **Cấp thẻ** → chọn QR hoặc Barcode → **In thẻ** → máy in hoặc **Lưu PDF**. Kích thước trang 86 × 50 mm, tỷ lệ 100%, tắt đầu/chân trang. Cấp thẻ mới thu hồi mã cũ; lưu/in ngay trước khi đóng.
- **Học sinh / Giáo lý viên / Lớp học** → **Tải mẫu Excel**, **Nhập Excel**, **Xuất Excel**. File `.xlsx`, tối đa 5 MB/1.000 dòng khi nhập. Không hỗ trợ `.xls`, macro hoặc ô công thức. File xuất chỉ chứa dữ liệu trong phạm vi lớp được phép; học sinh và lớp theo bộ lọc hiện tại, không giới hạn ở trang đang xem.

## Quyền

Admin có các quyền mới sau khi API khởi động. Trong **Phân quyền**, cấp riêng `StudentImport/Export`, `CatechistImport/Export`, `ClassImport/Export`, `CatechistAttendance`. Nhập cần thêm quyền quản lý tương ứng; xuất cần quyền xem tương ứng. Điều chỉnh giờ học sinh dùng `AttendanceCorrect`; xem bảng dùng `AttendanceView`. Tải file mẫu cần quyền nhập hoặc xuất.

## Quy ước Excel

### Chép đè dữ liệu đã có

Ô **Chép đè dữ liệu đã có** nằm trong cửa sổ Nhập dữ liệu Excel, mặc định bỏ chọn mỗi lần mở. Nếu không chọn, dòng đã có được bỏ qua và đếm riêng; nếu chọn, dòng được cập nhật sau khi kiểm tra quyền/phạm vi và dữ liệu hợp lệ. Dòng mới vẫn được thêm ở cả hai chế độ.

Học sinh đối chiếu theo họ tên đầy đủ + ngày sinh trong lớp đang chọn; nếu nhiều hồ sơ cùng khớp thì báo lỗi để đối soát. Khi chép đè, ID và mã học sinh được giữ nguyên. Muốn sửa họ tên/ngày sinh của hồ sơ cũ, dùng Chỉnh sửa trên web vì đây là thông tin dùng để nhận diện trùng trong file không có ID. Lớp đối chiếu Mã lớp trong năm học đang chọn; giáo lý viên đối chiếu họ tên đầy đủ + Tên Thánh trong phạm vi được phép, báo lỗi nếu trùng nhiều hồ sơ. Muốn đổi họ tên/Tên Thánh của giáo lý viên cũ, dùng Chỉnh sửa trên web.

### Đối tượng điểm danh theo lịch

Trong **Cấu hình lịch** và **Ngày nghỉ / bổ sung**, chọn **HS**, **GLV** hoặc cả hai. Buổi không hủy phải chọn ít nhất một đối tượng. API từ chối điểm danh/điều chỉnh giờ cho đối tượng không được bật; giao diện ẩn phần điểm danh tương ứng. Buổi chỉ GLV không có roster học sinh và không tính vào chuyên cần học sinh.

Cấu hình được chụp lại trên mỗi buổi. Thay lịch chỉ cập nhật buổi tương lai; buổi đã bắt đầu giữ nguyên lịch sử. Lựa chọn trong ngoại lệ áp dụng riêng ngày đó. Các lịch/buổi cũ giữ cả HS và GLV sau migration để bảo toàn hành vi đang có.

Giữ nguyên hàng tiêu đề. Ngày sinh học sinh dùng `dd/MM/yyyy`; cũng nhận `yyyy-MM-dd` hoặc ngày Excel dạng số. Chọn đúng lớp trước khi tải mẫu, xuất hoặc nhập học sinh. Hệ thống tự lấy lớp đang chọn; không nhập ID, mã học sinh hay mã lớp trong file học sinh. Với file lớp học, ID tự quản lý và năm học lấy từ lựa chọn hiện tại, không có hai cột này trong file. Không nhập tài khoản, mật khẩu, mã QR/Barcode hay thông tin phân quyền qua Excel.

- Học sinh: **Tên Thánh, Họ và tên đệm, Tên, Ngày sinh** (đúng bốn cột; tải lại mẫu mới).
- Giáo lý viên: **Tên Thánh, Họ và tên đệm, Tên**. ID do hệ thống quản lý; tải lại mẫu mới.
- Lớp học: **Mã lớp, Tên lớp**. Năm học tự lấy từ năm học đang chọn trên web; ID tự quản lý. Tải lại mẫu mới.

Học sinh mới được tự cấp ID và mã; hồ sơ có sẵn được đối chiếu tự động trong lớp. Danh sách và form học sinh có cột Họ và tên đệm, Tên riêng; các màn xem thông tin, thẻ, điểm danh, báo cáo vẫn ghép thành họ tên đầy đủ. Dữ liệu cũ được chuyển theo từ cuối là Tên, phần còn lại là Họ và tên đệm; các trường hợp tên ghép đặc biệt có thể sửa lại trên form.

Đối với lớp, cập nhật theo Mã lớp trong năm học đang chọn; mã chưa có tạo lớp mới. Không dùng import để chuyển lớp; dùng chức năng Chuyển lớp để giữ lịch sử. Ghi danh mới có hiệu lực ngày hiện tại, trong năm học đang còn hiệu lực.

Mỗi dòng được lưu độc lập. Bảng kết quả nêu số dòng Excel và lỗi cụ thể. Không nhập lại toàn bộ file nếu có dòng thành công: xuất lại dữ liệu hoặc chỉ giữ các dòng lỗi để tránh tạo trùng dữ liệu mới.

## Khởi động bản mới

Dừng API cũ bằng Ctrl+C trong cửa sổ đã chạy nó, rồi:

```powershell
cd D:\LongLe\Code\CheckInApp
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Integration.ps1
```

Migration `AttendanceTimesAndCatechists` tự chạy khi API dùng SQL Server khởi động. Web build mới đã nằm trong `apps/web/dist`; tải lại trang bằng Ctrl+F5. APK Android cũ chưa có các màn quản trị mới; các chức năng trên hiện được triển khai trên web.

## Thanh Excel và cửa sổ nhập

Trong danh sách Học sinh, Giáo lý viên và Lớp học, ba nút Xuất Excel, Tải mẫu Excel, Nhập Excel nằm cùng hàng với ô tìm kiếm/bộ lọc trên máy tính, kèm icon kết xuất/tải xuống/tải lên. Màn hình nhỏ tự xuống hàng.

Bấm **Nhập Excel** để mở cửa sổ nhập: chọn lớp (với học sinh), chọn file `.xlsx`, tick **Chép đè dữ liệu đã có** nếu cần rồi bấm **Nhập dữ liệu**. Chọn file chưa tự gửi lên server. Checkbox chép đè chỉ xuất hiện trong cửa sổ này, mặc định bỏ chọn mỗi lần mở. Mẫu lớp học tiếp tục lấy năm học hiện tại. Bảng kết quả được mở sau khi server xử lý xong.

## Tải mẫu khi chưa chọn lớp

Trong màn Học sinh, nếu đang xem Toàn bộ lớp, bấm **Tải mẫu Excel** hoặc **Xuất Excel** để chọn lớp rồi tiếp tục; **Nhập Excel** mở cửa sổ có chọn lớp, chọn file, checkbox chép đè và nút Nhập dữ liệu. Ứng dụng chọn lớp đó trên bộ lọc và tiếp tục tải file hoặc mở hộp chọn file `.xlsx`. Nếu năm học chưa có lớp, hộp thoại hướng dẫn chọn năm học khác hoặc tạo lớp. Nút chỉ khóa trong lúc xử lý yêu cầu. Lỗi nhập file được giữ rõ trên màn hình và lỗi từng dòng nằm trong bảng kết quả.

Trong màn Giáo lý viên không cần chọn lớp để nhập Excel. Dữ liệu và form quản lý tách Họ và tên đệm/Tên; phân công, thẻ và điểm danh vẫn hiển thị họ tên đầy đủ. Migration SplitCatechistNames chuyển dữ liệu cũ theo từ cuối là Tên và giữ nguyên tài khoản liên kết, thẻ, phân công và lịch sử.

## Phân trang danh mục

Danh sách lớp, học sinh, giáo lý viên có bộ chọn số dòng: 10, 25, 50, 100 và các mức tăng 50 theo tổng bản ghi (tối đa 1.000 dòng/trang). Đổi số dòng hoặc phạm vi năm học/lớp sẽ trở về trang 1.
