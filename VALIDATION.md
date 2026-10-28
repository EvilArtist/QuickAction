# Kết quả kiểm tra — 2026-10-28

## Đã thực hiện

- Build Release WPF/.NET 10: 0 lỗi, 0 cảnh báo.
- 31 kiểm thử lõi và tích hợp đạt: tạo lệnh EXE/BAT/CMD/PS1/MSI, quoting, Unicode, runas, pin/reorder, atomic save/backup/recovery, import/export, ZIP traversal, phiên bản JSON, vị trí bốn cạnh, tọa độ âm và kích thước DPI.
- Chạy EXE harness và script CMD vô hại thực tế: xác nhận chương trình nhận đúng đối số Unicode và có khoảng trắng.
- Kiểm tra trực tiếp Windows UI: Settings hiển thị tiếng Việt; tạo và lưu shortcut Notepad; ghim shortcut; All Apps hiển thị tên, chương trình, Admin, Run và Bỏ ghim; icon EXE được trích xuất.
- Kiểm tra trực tiếp dock bên trái: kính mờ, Settings phía trên, All Apps phía dưới, app ghim ở giữa; đóng All Apps trở về viên thuốc; tự thu về thanh idle 10 × 100.
- Publish self-contained win-x64 hoàn tất; runtimeconfig liệt kê runtime .NET và WindowsDesktop được đóng kèm.

## Chưa kiểm chứng trực tiếp

- Hiển thị đồng thời các tình huống màn hình phụ khác DPI, tháo màn hình khi đang chạy; các phép tính đã có kiểm thử.
- Luồng UAC admin/chọn Cancel, MSI cài đặt thực tế và lỗi chính sách PowerShell trên máy người dùng.
- Startup sau đăng nhập Windows, transparency bị tắt và high contrast, kéo thả/sắp xếp bằng chuột trong mọi hướng dock.

Không coi các mục trên là đã đạt chỉ từ kết quả build hoặc kiểm thử logic. Checklist đầy đủ nằm trong README.

## Chẩn đoán cửa sổ

`QuickAction.exe --diagnostics <đường-dẫn-log>` ghi các bước khởi động, đưa dock lên taskbar và mở All Apps để hỗ trợ kiểm tra bằng công cụ UI. Chế độ chạy thông thường không đưa dock lên taskbar. Không bật chế độ này khi thiết lập startup.
