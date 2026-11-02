# Kết quả kiểm tra — 2026-10-28

## Sửa blur tràn góc và lệch animation — 2026-11-02

- Video người dùng cho thấy accent blur vẫn phủ hình chữ nhật ngoài phần WPF bo góc. Kiểm tra cũ đo blur ở giữa và alpha WPF nên đã bỏ sót lỗi; alpha WPF không chứng minh được vùng blur của compositor.
- Thay accent blur/SetWindowRgn bằng Windows Composition HostBackdropBrush. Blur và tint dùng chung container với CompositionRoundedRectangleGeometry; cập nhật offset, size và radius từ chính SetReveal của animation. Giữ cửa sổ native cố định giữa các frame.
- Thành phần C++ nhỏ dùng Windows SDK và C++/WinRT có sẵn trong SDK, build tự động và đóng kèm QuickAction.Native.dll; không tải thư viện bên thứ ba. Không khởi tạo được compositor thì dùng nền WPF đục.
- Build Release đạt, 0 lỗi và 0 cảnh báo. Kiểm tra pixel desktop: nền sọc có độ tương phản 127 khi tắt lớp nền và 0 khi bật blur ở độ đục 30%.
- 36 ca kiểm tra vùng ngoài clip trên desktop đạt: 4 cạnh × (all, 3 frame thu, idle, 3 frame mở, all). Mỗi frame lấy từ 4 tới 4486 điểm ngoài hình bo góc/vùng đang hiển thị; chênh lệch pixel tối đa khi bật lớp nền là 0.
- 36 kiểm tra alpha WPF, 8 ca không resize native giữa frame và các ca hồi quy đảo chiều, mất focus, ẩn/hiện tiếp tục đạt.
- Chưa thay thế kiểm tra nhiều màn hình vật lý khác DPI hoặc kiểm tra FPS bằng profiler trên máy người dùng.

## Khôi phục blur nền — 2026-11-02

**Bản thử đầu tiên, đã được thay thế bởi mục sửa blur tràn góc phía trên.**

- Bật compositor accent blur trên cửa sổ layered; native region bám theo geometry bo góc trong mỗi frame mở/thu. Không resize HWND thêm giữa các frame.
- Giữ tùy chọn độ đục; nền đục khi Windows tắt transparency, dùng high contrast hoặc không hỗ trợ blur.
- Build Release: 0 lỗi, 0 cảnh báo. 36 ca alpha góc và các ca hồi quy animation, đảo chiều, mất focus, ẩn/hiện đều đạt.
- Kiểm tra `--blur` lấy pixel desktop trên nền sọc thật: độ tương phản giảm từ 89.50 khi tắt blur xuống 0.81 khi bật blur ở độ đục 30%. Đây là kiểm tra compositor thực tế, không chỉ RenderTargetBitmap của WPF.
- Chưa kiểm tra trực tiếp nhiều màn hình vật lý khác DPI và fallback high contrast trên phiên người dùng.

## Sửa nhấp nháy / kẹt giữa mở–thu — 2026-10-31

- Animation được cập nhật bởi DispatcherTimer 16 ms và thời gian thực đã trôi qua, không phụ thuộc sự kiện CompositionTarget.Rendering. Sau khi UI chậm, tick tiếp theo chốt trạng thái đích nếu đã đủ 400 ms.
- Yêu cầu đổi sang trạng thái hiện tại không tạo lại giao diện; mất focus khi đang ở dạng viên thuốc không render lại. Chặn render lồng nhau do sự kiện native khi đặt vị trí cửa sổ.
- Hover được kiểm tra bằng tọa độ chuột với geometry đang hiển thị, thay cho IsMouseOver của cửa sổ bao lớn hơn dock. Ẩn dock dừng timer; hiện lại chốt đúng trạng thái.
- Kiểm thử đạt: mất focus, 20 yêu cầu mở lặp, chặn luồng UI 550 ms rồi tiếp tục, ẩn/hiện giữa transition, 20 lần đảo chiều nhanh, 8 ca không resize native giữa frame và 36 ca alpha góc.
- Đây là kiểm tra hồi quy trong harness Windows; chưa tái hiện trực tiếp chuỗi thao tác gây lỗi trên phiên debug của người dùng.

## Sửa giật khi animation — 2026-10-31

- Thay resize HWND từng frame bằng một cửa sổ bao cố định trong transition, chỉ nội suy geometry hiển thị. Kích thước native được chốt ở đầu/cuối; không truy vấn monitor/DPI hoặc thay đổi bố cục nội dung ở mỗi frame.
- Tạm chặn hover và thao tác lên nội dung khi đang chuyển động; xử lý parent của phần tử text khi bắt đầu drag để tránh truyền ContentElement vào VisualTreeHelper.
- Build Release đạt; 8 ca lấy mẫu xác nhận không có native resize qua 15 frame, 36 kiểm tra alpha góc vẫn đạt. Không dùng kết quả này để khẳng định FPS thực tế trên mọi máy.

## Animation kích thước — 2026-10-31

- Mở rộng/thu gọn width và height trong 400 ms, cubic ease-out; cập nhật vị trí ghim và clipping theo từng frame.
- Tôn trọng thiết lập animation của Windows; hủy animation khi kéo hoặc đóng, đổi hướng/màn hình không nội suy qua desktop.
- Kiểm thử lấy mẫu frame 25% xác nhận kích thước trung gian ở 8 trường hợp (pill/all × 4 cạnh); xác nhận animation kết thúc, 36 kiểm tra góc trong suốt tiếp tục đạt.

## Sửa nền ngoài dock — 2026-10-31

- Bỏ DWM Acrylic và clipping bằng region native; chuyển sang `AllowsTransparency=true`, nền cửa sổ transparent, cắt nội dung theo hình bo góc và bỏ đường viền trắng.
- Build Release: 0 lỗi, 0 cảnh báo.
- 36 kiểm tra render đạt ở Left/Right/Top/Bottom, idle/pill/all và 100/150/200%: cả bốn góc có alpha=0, điểm giữa vẫn hiển thị. Xác nhận HWND thật có `WS_EX_LAYERED`.
- Các mức DPI trên là kích thước render trong test, không thay thế kiểm tra nhiều màn hình vật lý khác DPI.
- Đánh đổi rõ ràng: nền dock bán trong suốt, không còn backdrop blur. Mô tả kính mờ bên dưới là kết quả của bản cũ.

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
