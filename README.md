# QuickAction

Dock shortcut tiếng Việt cho Windows 11 x64, viết bằng WPF / .NET 10. Không cần chạy dock bằng quyền Administrator.

## Chạy

Giải nén `dist/QuickAction-win-x64.zip` vào thư mục cố định và mở `QuickAction.exe`. Di chuột lên thanh trắng ở cạnh trái để mở dock. Nút ⚙ mở Settings; ▦ mở tất cả ứng dụng. Khi chưa có shortcut, vào Settings hoặc kéo EXE/BAT/CMD/PS1/MSI vào dock để thêm.

Lần đầu chưa có shortcut, Settings tự mở. Xem `VALIDATION.md` để biết các kiểm tra đã thực hiện và các tình huống còn cần thử trên máy đích.

- Kéo thanh idle hoặc khoảng nền trống để đổi màn hình/cạnh. Kéo icon ghim lên icon khác để sắp xếp.
- Mỗi shortcut có tên, chương trình, parameters, thư mục làm việc và lựa chọn Admin riêng; có thể thêm cùng EXE nhiều lần với parameters khác nhau.
- File icon tùy chỉnh được chép vào dữ liệu ứng dụng; có thể chọn icon dựng sẵn hoặc tự lấy từ EXE.
- Chỉ ghim tối đa 5 app. Escape hoặc click ra ngoài đóng All Apps. Tray có Hiện/ẩn, Settings và Thoát.
- Startup mặc định tắt. Nếu di chuyển thư mục ứng dụng, lưu lại thiết lập startup.

## Build / kiểm thử / đóng gói

Yêu cầu .NET SDK 10 trên Windows:

```powershell
dotnet build QuickAction/QuickAction.csproj -c Release
dotnet run --project QuickAction.Tests -c Release
./publish.ps1
```

Kiểm thử lõi dùng executable harness, không cần tải framework test bên ngoài. `QuickAction.Core` chứa models, validation, tạo lệnh chạy, pinning, geometry và persistence. `QuickAction` chứa ViewModel, WPF views và tích hợp Windows. Logic chạy process tách khỏi việc tạo lệnh để có thể kiểm tra mà không thực thi chương trình.

## Dữ liệu và hành vi

Cấu hình nằm ở `%LocalAppData%\QuickAction\config.json`, icon trong `icons`. Save giữ `.bak`; file hỏng được giữ lại trước khi phục hồi. Export/import ZIP chứa cấu hình và icon, không chứa file chương trình. Parameters được lưu nguyên văn và có thể chứa thông tin nhạy cảm; log chỉ ghi loại lỗi và HRESULT, tối đa hai file khoảng 1 MB.

EXE chạy trực tiếp; BAT/CMD qua `cmd.exe /d /s /c`; PS1 qua Windows PowerShell `-NoProfile -File`; MSI qua `msiexec /i`. Parameters là chuỗi native theo quy tắc của chương trình đích. BAT/CMD có shell semantics: các ký tự như `&`, `|`, `%` có ý nghĩa của CMD. QuickAction không chỉnh ExecutionPolicy, không bypass UAC và không ẩn cửa sổ console. Thông báo “đã gửi yêu cầu chạy” không có nghĩa chương trình đã hoàn tất. Script PowerShell có exit code khác 0 sẽ có thông báo riêng.

Dock dùng DWM desktop Acrylic khi hệ thống cho phép, với vùng cửa sổ bo tròn để góc ngoài không chặn chuột. Khi transparency tắt hoặc API không hỗ trợ, dùng nền đục. Tọa độ docking dùng pixel vật lý của màn hình, kích thước giao diện dùng DIP. Nền luôn nổi mặc định bật; có thể tắt trong Settings.

## Kiểm tra thủ công trước phát hành rộng

- Bốn cạnh dock, hover 150 ms, thu gọn 600 ms, mở/đóng All Apps và thao tác bàn phím.
- Hai màn hình khác DPI (100/150/200%), tọa độ âm, tháo màn hình, khởi động lại để xác nhận vị trí.
- Tạo shortcut cùng ứng dụng với parameters khác nhau; thêm icon tùy chỉnh; pin 5 app rồi thử pin app thứ 6; kéo đổi thứ tự.
- Chạy mẫu vô hại từng loại file với khoảng trắng/Unicode; quyền admin, hủy UAC và PowerShell bị policy chặn.
- Tắt transparency, bật high contrast, kiểm tra góc ngoài không nhận chuột; kiểm tra CPU khi idle.
- Sao lưu/khôi phục, cấu hình hỏng, tray, startup và mở ứng dụng lần hai.

Không có tự cập nhật, đồng bộ cloud hoặc telemetry. V1 hỗ trợ một dock, tối đa 5 app ghim, và số shortcut còn lại trong All Apps.
