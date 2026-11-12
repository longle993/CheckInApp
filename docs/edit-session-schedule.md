# Chỉnh lịch buổi học chưa bắt đầu

> Đã cập nhật 01/11: đổi cả ngày/giờ tương lai trong năm học; nhiều khung không chồng nhau. Xem [bản cập nhật](release-2026-11-01.md).

Trong **Lịch**, chọn lớp/tháng và bấm **Chỉnh lịch** cạnh buổi học. Chọn ngày mới, nhập giờ bắt đầu/kết thúc (giờ Việt Nam), chọn HS/GLV, lý do và bấm **Lưu lịch**. ID buổi được giữ nguyên; thay đổi chỉ áp dụng riêng buổi đó, được lưu ngoại lệ để đồng bộ lịch không ghi đè.

Cần quyền ScheduleManage và phạm vi lớp tương ứng. Buổi phải chưa bắt đầu, chưa hủy, chưa có dữ liệu điểm danh. Giờ bắt đầu mới phải **lớn hơn** thời gian hiện tại; giờ kết thúc phải sau giờ bắt đầu trong cùng ngày. Chọn ít nhất HS hoặc GLV. Nếu buổi đã tới giờ bắt đầu trong lúc đang mở form, API vẫn từ chối khi lưu. Nếu người khác đã sửa lịch, tải lại trước khi lưu lại.

Thay đổi được ghi nhật ký trước/sau; danh sách HS của buổi tương lai được đồng bộ với đối tượng HS/GLV đã chọn. Không sửa lịch sử của buổi đã mở.
