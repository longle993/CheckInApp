# CheckInApp — thiết kế đăng nhập web desktop/mobile

Ngày: 02/11/2026. Trạng thái: **đã triển khai giao diện web theo tài liệu được duyệt**. Chỉ thay React/CSS, không đổi API hoặc APK. Yêu cầu này chỉ áp dụng màn xác thực web ở kích thước đầy đủ và mobile web, không phải thay giao diện app React Native.

## 1. Brief và nguồn tham khảo

- Sản phẩm: CheckInApp quản lý lớp giáo lý và điểm danh; người dùng là quản trị viên/GLV có tài khoản được duyệt. Nguồn: mã hiện tại và yêu cầu trước đó.
- Việc chính: nhận diện đúng ứng dụng của giáo xứ, nhập tài khoản, đăng nhập; có đường chuyển sang đăng ký hiện có. Nguồn: yêu cầu và mã.
- Tham khảo: ảnh người dùng cung cấp có desktop chia mảng nhận diện bên trái, form bên phải; bản hẹp đưa nhận diện lên trên, form dưới, nối bằng đường cong nhiều lớp. Đây là **design reference**, không phải ảnh lỗi hoặc wireframe của CheckInApp.
- Hai kiểu người dùng yêu cầu được hiểu là **hai bố cục responsive của cùng trang**, không phải hai theme tùy chọn hay hai bước chào/đăng nhập.
- Hướng nhận diện đề xuất: giữ đỏ đô/trắng/trắng be và Inter đã duyệt; lấy bố cục, sự phân vùng và đường cong từ ảnh. Không chuyển toàn ứng dụng sang xanh theo mẫu. Đây là mặc định cần đọc cùng brief.

Audit: React19/Vite7, Tailwind4; component nội bộ `Brand`, `Button`, trạng thái lỗi/đang xử lý có sẵn. `Login` ở `apps/web/src/main.jsx` hiện một cột, dùng chung container cho đăng nhập/đăng ký. `Brand` ở `apps/web/src/ui.jsx` đọc tên/logo công khai từ `/api/v1/web-settings`, có nhận diện dự phòng. Token màu ở `apps/web/src/production.css`; font và nền tảng CSS ở `apps/web/src/styles.css`. Phong cách hiện tại flat, nền sáng.

## 2. Nội dung nhận diện bắt buộc

Thứ tự: **logo app → tên đơn vị bên dưới**. Nội dung chính xác:

> THIẾU NHI THÁNH THỂ GIÁO XỨ BÙI PHÁT<br>
> XỨ ĐOÀN THIÊN THẦN RAPHAEL

- Hai dòng trên là hai khối nội dung; trên màn hẹp cho phép khối đầu tự xuống thêm dòng ở khoảng trắng để đọc được, không ép một hàng bằng chữ quá nhỏ, không cắt dấu `…`.
- Giữ đầy đủ dấu tiếng Việt và chữ in hoa. Dòng đầu mô tả đơn vị, dòng hai là tên xứ đoàn và có mức nhấn cao hơn.
- Logo dùng logo app đang cấu hình tại **Admin Console → Thông tin web**; không dùng tên lửa/“Spacer” trong ảnh mẫu, không tự vẽ lại logo giáo xứ. Hiển thị `object-fit: contain`, không crop hoặc làm méo; logo nhiều màu được giữ nguyên.
- Nếu logo khó đọc trên nền đỏ, đặt trên đệm trắng có khoảng trống, không tự đổi màu logo. Logo thiếu/tải lỗi dùng biểu tượng app dự phòng hiện có cùng tên đơn vị; không làm form mất khả năng đăng nhập.
- Tên web động tiếp tục dùng cho tiêu đề tab và tên ứng dụng hỗ trợ truy cập; không lặp “CheckInApp” nhiều lần cạnh logo và tên đơn vị nếu không cần.
- Hai dòng tên đơn vị là nội dung cố định của màn xác thực trong phạm vi này. Không tự thêm trường sửa tên giáo xứ/xứ đoàn vào Admin Console hoặc đổi API cấu hình.

## 3. Desktop — từ 960px trở lên

Khung đăng nhập căn giữa vùng nội dung, rộng tối đa khoảng1000–1080px, lề ngoài tối thiểu24–32px. Chiều cao theo nội dung, tối thiểu khoảng560px khi màn đủ cao. Màn thấp cho phép cuộn toàn trang; không khóa chiều cao làm mất nút.

```text
Nền trắng be
┌─────────────────────────┬──────────────────────────────┐
│                         │                              │
│        LOGO APP         │  Đăng nhập                    │
│                         │  Dùng tài khoản đã được       │
│ THIẾU NHI THÁNH THỂ      │  admin duyệt.                 │
│ GIÁO XỨ BÙI PHÁT         │                              │
│ XỨ ĐOÀN THIÊN THẦN       │  Email                       │
│ RAPHAEL                 │  [________________________]  │
│                         │  Mật khẩu                    │
│ Nền đỏ đô               │  [____________________ mắt]  │
│                 đường   │  [       Đăng nhập        ]  │
│                 cong    │  Chưa có tài khoản? Đăng ký  │
│                         │  Quên mật khẩu: liên hệ admin│
└─────────────────────────┴──────────────────────────────┘
```

Sơ đồ chỉ mô tả vị trí, chưa phải wireframe đã kiểm thử.

- Mảng nhận diện khoảng44%, form khoảng56%; logo và hai khối tên căn giữa trong mảng trái, toàn nhóm cân theo chiều cao form.
- Logo khoảng96–120px; chữ đơn vị16–18px, xứ đoàn18–22px, line-height1.4–1.5. Cho phép xuống dòng tự nhiên theo độ rộng thực.
- Form có chiều rộng đọc khoảng360–400px, padding40–48px; tiêu đề26–28px, nhãn14px, chữ nhập16px, ô nhập/nút cao44–48px.
- Chuyển tiếp giữa hai mảng bằng đường cong trắng với tối đa2 lớp đỏ nhạt trung gian, rộng khoảng32–48px; giữ ngoài vùng chữ/ô nhập. Dùng SVG/CSS nhẹ, `aria-hidden`, `pointer-events:none`.
- Đề xuất mảng màu phẳng thay gradient xanh, viền nhẹ và bo16px; không cần bóng đậm như ảnh. Giữ cảm giác chia mảng mềm, không áp thay đổi này lên dashboard.

## 4. Mobile web và tablet hẹp — dưới 960px

```text
┌────────────────────────────┐
│       Nền đỏ đô            │
│          LOGO APP          │
│ THIẾU NHI THÁNH THỂ        │
│ GIÁO XỨ BÙI PHÁT           │
│ XỨ ĐOÀN THIÊN THẦN RAPHAEL │
╰────── đường cong mềm ──────╯
│ Đăng nhập                  │
│ Email                      │
│ [________________________] │
│ Mật khẩu                   │
│ [____________________ mắt] │
│ [       Đăng nhập        ] │
│ Chưa có tài khoản? Đăng ký │
│ Quên mật khẩu: liên hệ…   │
└────────────────────────────┘
```

- Nhận diện ở trên, form ở dưới trong cùng luồng cuộn; không có màn chào riêng như hình điện thoại ở giữa ảnh tham khảo.
- Trang rộng theo viewport, lề16px, form tối đa440px; tablet768px có thể tăng vùng nhận diện/form đến560px nhưng độ rộng ô nhập vẫn gọn.
- Logo64–80px, tên đơn vị13–14px, tên xứ đoàn15–16px. Header cao theo nội dung, dự kiến200–260px; không đặt chiều cao cố định cắt chữ khi tăng font.
- Đường cong chuyển sang mép dưới header, cao20–32px; form padding20–24px, tiêu đề22–24px. Nút Đăng nhập rộng toàn form.
- Không ẩn logo hoặc hai dòng tên ở mobile. Không có sidebar/bottom navigation khi chưa đăng nhập.
- Khi bàn phím hiện: cuộn tới ô đang focus, nút gửi còn truy cập bằng cuộn/Enter; dùng `min-height:100dvh` có fallback, không `height:100vh` + khóa cuộn. Không tự autofocus trên mobile để tránh bàn phím che nhận diện ngay lần mở; desktop có thể giữ autofocus email.
- Màn ngang thấp dùng bố cục cuộn; không cố căn giữa dọc khiến phần trên nằm ngoài viewport. Tôn trọng safe-area và zoom chữ200%.

## 5. Form và hành vi giữ đúng nghiệp vụ

| Thành phần | Thiết kế/hành vi |
| --- | --- |
| Tiêu đề | Đăng nhập |
| Mô tả | Dùng tài khoản đã được admin duyệt. |
| Email | Nhãn rõ phía trên, `type=email`, `autocomplete=username`, ví dụ `ban@example.com`, không tự viết hoa |
| Mật khẩu | Nhãn rõ, `autocomplete=current-password`, mặc định che; nút hiện/ẩn ở bên phải, nhãn truy cập thay đổi tương ứng |
| Nút chính | Đăng nhập → Đang đăng nhập…; chặn gửi lặp trong lúc xử lý |
| Đăng ký | Chưa có tài khoản? Đăng ký; chuyển form trong cùng khung nhận diện |
| Quên mật khẩu | Giữ hướng dẫn liên hệ admin hiện có; không tạo liên kết tới trang chưa tồn tại |
| Lỗi | Lỗi trường ngay dưới trường; lỗi API/đăng nhập gần nút gửi, `role=alert`, không chỉ báo bằng màu |
| Thông báo | Kết quả đăng ký chờ duyệt, thông báo đổi mật khẩu/phiên hết hạn giữ đúng luồng hiện tại |

Ô nhập nền trắng, viền rõ; không có icon trang trí bên trái. Nút mắt là hành động hỗ trợ mật khẩu, không phải icon trang trí. Không dùng gạch chân mảnh làm ranh giới duy nhất như ảnh vì cần dễ nhận biết trên điện thoại.

Ảnh tham khảo là mẫu đăng ký, nhưng màn chính trong yêu cầu là **đăng nhập**: không đưa trường Họ tên, checkbox điều khoản hay nút Sign Up làm nút chính ở màn login. Giữ đăng ký hiện có trong cùng thiết kế khi chuyển chế độ: Họ và tên, Email, Mật khẩu; câu chờ admin duyệt và nút Gửi đăng ký. Không tự thêm OAuth/Google, ghi nhớ đăng nhập, CAPTCHA hoặc reset mật khẩu tự phục vụ. Đây là giữ phạm vi và API dự án, không áp mặc định nút Google của skill vào một chức năng chưa có backend.

## 6. Trạng thái và khả năng truy cập

Các bản thiết kế/triển khai phải có: bình thường; focus; thiếu trường; email sai; thông tin đăng nhập không hợp lệ; tài khoản chưa duyệt/khóa theo thông báo server; đang gửi; mất mạng; logo lỗi; thông báo đăng ký thành công và trở về đăng nhập.

- Không tiết lộ thêm tình trạng tài khoản ngoài hợp đồng lỗi API hiện tại.
- Không nhấp nháy/nhảy vị trí cả form khi logo tới muộn; giữ sẵn vùng logo. Lỗi nhiều dòng được phép tăng chiều cao khung và cuộn.
- Label liên kết input bằng `for/id`, thứ tự Tab từ email → mật khẩu → mắt → gửi → đăng ký; focus nhìn rõ, Enter gửi form.
- Nút mắt `type=button`, vùng chạm tối thiểu44×44px; không làm mất giá trị/caret khi đổi trạng thái.
- Chữ thường đạt tương phản tối thiểu4.5:1; tên đơn vị trên đỏ dùng trắng, không dùng đỏ nhạt làm chữ chính. Không thêm animation liên tục; tôn trọng `prefers-reduced-motion`.
- Chỉ một `h1` cho Đăng nhập/Đăng ký; hai dòng đơn vị là nội dung thương hiệu, không tranh thứ bậc với tiêu đề nhiệm vụ. Logo có mô tả phù hợp, tránh đọc lặp tên đơn vị hai lần với screen reader.

## 7. Kế hoạch triển khai sau duyệt

1. Duyệt brief này; thiết kế desktop/mobile theo cùng hướng tham khảo. Nếu cần so sánh wireframe, chỉ so biến thể tỷ lệ/đường cong, không đổi hướng nhận diện đã nêu.
2. Tách phần hiển thị nhận diện xác thực hoặc thêm variant cho `Brand`; không làm logo/sidebar toàn app phình theo kích thước login.
3. CSS giới hạn trong vùng auth; tái dùng Button/input hiện có; giữ nguyên hợp đồng auth/JWT/cookie/duyệt tài khoản. Không thêm dependency hình ảnh/chuyển động hoặc API mới chỉ để dựng layout.
4. Dùng logo đã cấu hình, SVG trang trí nội bộ; không tải ảnh bên ngoài, không nhúng ảnh screenshot làm nền form. Hai dòng tiếng Việt là text thật, đọc và chọn được.
5. Kiểm tra trình duyệt/probe và ảnh chụp trước bàn giao; chỉ build web nếu không phát sinh thay đổi ngoài phạm vi. Không cần build APK cho yêu cầu mobile web này.

## 8. Tiêu chí nghiệm thu dự kiến — chưa chạy

| Mã | Kết quả cần đạt |
| --- | --- |
| LOGIN-01 | Logo app và chính xác hai khối tên đơn vị hiện cả desktop/mobile, không cắt chữ/méo logo |
| LOGIN-02 | Desktop nhận diện trái/form phải; mobile nhận diện trên/form dưới; không thêm bước chào |
| LOGIN-03 | Không tràn ngang tại320/375/390/768/960/1440px; màn thấp/landscape và zoom200% vẫn dùng được |
| LOGIN-04 | Đăng nhập/đăng ký/chờ duyệt/lỗi/phiên hết hạn giữ hành vi; gửi lặp bị chặn, Enter hoạt động |
| LOGIN-05 | Tab/focus/label/nút mắt và bàn phím mobile đúng; không che nội dung cuối |
| LOGIN-06 | Logo tải chậm/lỗi không chặn đăng nhập; đổi logo từ Admin Console được áp dụng sau tải lại |
| LOGIN-07 | Đăng ký dùng cùng khung, đủ trường và trạng thái; sidebar/dashboard không bị CSS auth ảnh hưởng |
| LOGIN-08 | Không tải camera, báo cáo, công cụ in hay ảnh ngoài chỉ để hiển thị login; ảnh trang trí nhẹ và không tương tác |

Chưa có wireframe/render mới hoặc số liệu kiểm thử trong đợt tài liệu này. Không có dữ liệu người dùng giả được đưa vào ứng dụng. Mặc định: giữ đỏ đô/trắng/be, Inter, không dark mode; ô nhập trơn, logo từ cấu hình hiện có, hai dòng đơn vị cố định. Các quy ước bố cục được suy từ ảnh, code và skill, không phải nghiên cứu người dùng bên ngoài.

## 9. Kết quả triển khai

Đã dựng desktop hai cột và mobile một cột, logo động từ cấu hình, hai dòng tên đơn vị, chuyển tiếp cong, nút hiện/ẩn mật khẩu, ngăn gửi lặp và giữ nguyên API xác thực. CSS giới hạn trong `.auth-redesign`; Brand có variant auth, sidebar dùng kiểu cũ.

Build web thành công. Chrome kiểm tra320/375/390/768/960/1440px đạt: không tràn ngang, bố cục đổi đúng, tên đơn vị đầy đủ, hiện/ẩn mật khẩu, chuyển đăng ký/thành công chờ duyệt và lỗi đăng nhập. Các phản hồi auth được mock trong ca UI, không tạo tài khoản hoặc dùng mật khẩu thật. Ảnh đã xem tại `.local/login-qa/login-375.png` và `login-1440.png`, có logo cấu hình thực tế sau khi tải. Chưa thử bàn phím trên điện thoại vật lý hoặc chứng nhận toàn bộ zoom/screen reader.

Probe ui-ux ở375/1440px không có mục xếp Hỏng. Cảnh báo401 là refresh của khách chưa đăng nhập; nút chuyển đăng ký dạng ghost có nền/viền trắng là chủ ý, không phải nút chính thiếu ranh giới. Report ở `.local/login-qa/probe/report.json`.
