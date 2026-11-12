# Tìm kiếm và tải dữ liệu — thuật toán, tối ưu và kiểm chứng

## Phạm vi

Đợt 02/11/2026 bổ sung theo yêu cầu cải thiện tìm kiếm/tải dữ liệu. Áp dụng danh mục web, bộ chọn người của Bảng điểm danh và API danh mục. Không thay thuật toán nhận diện QR/Barcode, không dùng dữ liệu mẫu để thay kết quả thật, không cài search engine mới.

## Thuật toán tìm kiếm

1. Giới hạn từ khóa tối đa 120 ký tự, tối đa 8 từ khác nhau. Chuỗi rỗng chỉ áp dụng bộ lọc. API trả lỗi 400 có giải thích nếu vượt giới hạn; không âm thầm cắt chuỗi.
2. Tách theo khoảng trắng Unicode, bỏ khoảng trắng đầu/cuối và các từ trùng không phân biệt hoa/thường. Ví dụ `  bao   Nguyen bao ` tạo hai từ `bao`, `Nguyen`.
3. Với từng từ, áp dụng phép tìm **chuỗi con** (`Contains` được EF chuyển thành SQL có tham số). Các từ nối bằng **AND**: mọi từ đều phải khớp, thứ tự từ không quan trọng. Bên trong một từ, các trường nối bằng **OR**.
4. Học sinh: mỗi từ được chuẩn hóa Unicode FormD, loại dấu kết hợp, đổi đ/Đ thành d/D, chuyển hoa bằng `CatalogService.Search`; so với `SearchName` đã lưu (họ tên + tên Thánh). Đồng thời tìm mã học sinh bằng từ gốc. Ví dụ `bao nguyen` tìm được Nguyễn Gia Bảo. `SearchName` được cập nhật trong SaveChanges khi tạo/sửa/nhập học sinh, không tính lại toàn bảng khi tìm.
5. GLV: tìm từng từ trong FullName hoặc SaintName. Lớp: Name hoặc Code. Năm học: Name. Các trường này giữ quy tắc so sánh của provider/collation hiện có; **không cam kết tìm không dấu đồng nhất giữa SQLite và SQL Server cho GLV/lớp/năm học**. Chuẩn hóa không dấu độc lập provider hiện chỉ áp dụng tên học sinh.
6. Lọc quyền và phạm vi lớp/năm ở server trước khi trả kết quả. Học sinh tiếp tục dựa trên ghi danh có hiệu lực hôm nay; đây không phải chức năng tra cứu hồ sơ lịch sử toàn thời gian. Tìm kiếm không mở rộng quyền xem.
7. Đếm tổng kết quả trên server, sắp xếp tên rồi ID để các bản ghi trùng tên có thứ tự ổn định; năm học sắp ngày bắt đầu giảm dần rồi ID. Phân trang OFFSET/LIMIT (`Skip/Take`), số dòng 1–1.000; trang được giới hạn để tránh tràn số offset. Không tải toàn bộ danh mục về trình duyệt để lọc.

Không dùng Levenshtein, fuzzy matching, tìm gần đúng, tự sửa chính tả, embedding hoặc full-text. `%`, `_`, dấu nháy trong đầu vào không được ghép vào SQL thủ công. Có kiểm thử tìm nhiều từ đảo thứ tự, không dấu HS, từ không tồn tại, giới hạn độ dài và dữ liệu ngoài phạm vi.

## Tối ưu phía trình duyệt

- Ô tìm danh mục và tìm người trong widget chờ **250 ms** khi có từ khóa. Trong một lượt gõ liên tục nhanh hơn 250 ms/ký tự, chỉ request cuối được gửi. Xóa sạch từ khóa tải lại ngay. Khi đang có từ khóa, chuyển trang/lọc cũng qua khoảng chờ này.
- `useData` dùng AbortController và hủy cả timer khi đổi điều kiện hoặc rời màn. Server nhận CancellationToken để có thể dừng truy vấn; việc hủy không bảo đảm truy vấn đã hoàn thành ở server sẽ được thu hồi.
- Phản hồi cũ không được ghi đè phản hồi mới; dữ liệu chỉ hiển thị khi path của kết quả trùng path đang yêu cầu. Đổi năm không dùng tạm lớp/buổi của năm trước.
- Hủy request sau khi refresh token không bị hiểu nhầm là phiên đăng nhập hết hạn.
- Tải danh sách năm tách khỏi thay đổi lựa chọn năm, tránh gọi lại API năm mỗi lần chuyển năm; tải lớp có abort khi đổi năm.
- `hasAvatar` trong kết quả danh mục cho biết hồ sơ có ảnh hiện hành hay không. `false` dùng chữ cái dự phòng và **không gọi API ảnh**. Ảnh có thật vẫn chỉ tải khi gần viewport, hủy khi unmount/đổi người. Cập nhật/xóa ảnh làm mới revision và metadata.
- Bộ chọn người của widget tải tối đa 100 người theo từ khóa, thay vì 1.000; người dùng tìm để thu hẹp danh sách lớn. Danh mục quản lý vẫn có phân trang riêng.
- Không cache danh mục lâu dài ở localStorage hoặc chia sẻ cache giữa tài khoản. Phân quyền và thay đổi dữ liệu vẫn được kiểm tra ở server.

## Tối ưu truy vấn và chỉ mục

- Truy vấn chỉ đọc dùng AsNoTracking và projection các trường cần hiển thị.
- Danh mục đọc quyền một lần trong request và tái sử dụng kết quả khi tính phạm vi/lọc; không cache quyền qua request hoặc qua tài khoản.
- API năm học bỏ truy vấn lớp/phạm vi không cần thiết trước khi đọc năm, nhưng vẫn Require YearView; thêm tìm/lọc và phân trang năm học.
- GLV chỉ trả phân công của **các GLV trong trang kết quả**, trong phạm vi lớp được phép, thay vì tải toàn bộ phân công rồi để client tìm lại. Khối phân công trên màn cũng theo trang đang xem.
- Metadata ảnh lấy bản ghi Avatar mới nhất theo CreatedAtUtc/Id trong projection; chuỗi Detail rỗng nghĩa là đã xóa ảnh, không dùng một ảnh cũ trước đó.
- Migration `20261102143750_CatalogSearchIndexes` thêm:

| Bảng | Chỉ mục | Mục đích |
| --- | --- | --- |
| Enrollments | ClassRoomId, From, Until, StudentId | Thu hẹp tập ghi danh theo lớp/hiệu lực |
| Assignments | ClassRoomId, From, Until, CatechistId | Lọc phân công theo lớp/hiệu lực |
| Students | FullName, Id | Hỗ trợ thứ tự phân trang ổn định |
| Catechists | FullName, Id | Hỗ trợ thứ tự phân trang ổn định |
| Audit | EntityId, CreatedAtUtc | Tìm metadata mới nhất theo hồ sơ |

Hai chỉ mục đơn ClassRoomId cũ của Enrollments/Assignments được thay bằng chỉ mục ghép có cùng cột đầu; migration Down khôi phục được. Không thay bảng dữ liệu nghiệp vụ hoặc cột tên.

**Giới hạn thuật toán:** chỉ mục B-tree không biến tìm chuỗi con `%từ%` thành index seek trực tiếp trên tên. Chi phí tìm vẫn phụ thuộc số hồ sơ trong phạm vi, số từ và vị trí trang. OFFSET ở trang rất sâu vẫn có chi phí; chưa chuyển sang cursor vì UI có chọn số trang bất kỳ. Nếu dữ liệu tăng lớn, cần đo execution plan rồi cân nhắc full-text/n-gram/cursor; không tuyên bố độ phức tạp O(log n) cho Contains.

## Đo và tái kiểm tra

`tests/search-performance.mjs` đo trên API cục bộ có xác thực: một lượt warm-up, 20 lượt/loại, ghi median, p95 và số byte JSON. Kết quả nằm trong `.local/ui-expansion-qa/performance-before.json` và `performance-after.json`; không chứa token/mật khẩu. Dataset QA lúc baseline: 120 HS, 4 GLV; kết quả này không đại diện cho tải hàng chục nghìn hồ sơ hay mạng điện thoại.

Baseline SQL Server/Redis trước tối ưu:

| API | Median | p95 | JSON |
| --- | --- | --- | --- |
| Tìm HS `Nguyen`, 25 dòng | 34,51 ms | 44,27 ms | 8.535 byte |
| GLV, 25 dòng | 26,97 ms | 35,39 ms | 1.451 byte |
| Lớp, 25 dòng | 23,56 ms | 26,02 ms | 641 byte |

Kết quả sau tối ưu phải ghi theo lần chạy thực tế trong tài liệu phát hành; không suy phần trăm nhanh hơn chỉ từ thay đổi code. Metadata ảnh làm JSON tăng nhẹ, đổi lại giảm request ảnh không tồn tại. Với 25 người không ảnh, danh sách không cần 25 request ảnh trả 404.

Kết quả đo độc lập sau cùng trên cùng 120 HS/4 GLV:

| API | Median | p95 | JSON |
| --- | --- | --- | --- |
| Tìm HS `Nguyen`, 25 dòng | 39,18 ms | 54,37 ms | 8.985 byte |
| GLV, 25 dòng | 24,61 ms | 31,89 ms | 1.523 byte |
| Lớp, 25 dòng | 32,28 ms | 47,26 ms | 641 byte |

GLV giảm thời gian trong lần đo này; HS và lớp chưa giảm. Không khẳng định mọi API nhanh hơn. Lợi ích đã kiểm chứng trực tiếp ở trình duyệt là **10 ký tự gõ nhanh chỉ phát một request tìm kiếm**, và **không có request ảnh** khi cả trang không có ảnh. Đây là giảm tải/chờ đợi do request thừa; chưa phải kiểm thử tải đồng thời hoặc chứng minh latency tốt hơn trên dữ liệu lớn.

Nghiệm thu trình duyệt: gõ nhanh nhiều ký tự chỉ gọi một truy vấn tìm cuối, không có request ảnh cho `hasAvatar=false`; đổi từ khóa/route không hiện lỗi AbortError; request chậm cũ không ghi đè; chọn trang, list/card, sửa/xóa, xuất/nhập giữ kết quả đúng. Kiểm thử chức năng tìm không thay thế kiểm thử tải đồng thời.

## Triển khai

SQL Server cần áp dụng migration cùng bản API mới để có chỉ mục. Sao lưu theo quy trình vận hành hiện có trước khi nâng cấp; không chạy thử xóa trên dữ liệu thật. Database QA tạo bằng EnsureCreated không có lịch sử migration: chỉ áp dụng chỉ mục tương đương trên QA để đo, không giả mạo lịch sử migration của database vận hành. Web và API cần được nâng cấp cùng nhau để tận dụng `hasAvatar`.

## Bổ sung 05/11/2026
Xem [tối ưu lookup báo cáo, kéo widget và số đo vận hành](ui-refinement-2026-11-05.md). Thuật toán tìm kiếm văn bản vẫn theo đặc tả trên; không thay Contains thành full-text hoặc hứa độ phức tạp index-seek.
