# Đổi mật khẩu tài khoản đang đăng nhập

1. Nhấn icon chìa khóa **Đổi mật khẩu** cạnh tên tài khoản, phía cuối thanh bên. Trên điện thoại mở menu để thấy mục này.
2. Nhập mật khẩu hiện tại, mật khẩu mới và xác nhận mật khẩu mới. Mật khẩu mới cần 12–128 ký tự, có chữ và số, khác mật khẩu hiện tại.
3. Nhấn **Đổi mật khẩu**. Thành công sẽ quay lại đăng nhập; tất cả phiên cũ bị thu hồi. Đăng nhập bằng mật khẩu mới.

Chức năng chỉ đổi mật khẩu của chính người đang đăng nhập; không phải luồng đặt lại mật khẩu cho tài khoản khác. Mật khẩu mới không được lưu vào file bootstrap/secrets. File bootstrap chỉ chứa thông tin khởi tạo ban đầu, không phản ánh mật khẩu sau khi đã đổi trên web. Khởi động lại API không đặt lại mật khẩu đã đổi.
