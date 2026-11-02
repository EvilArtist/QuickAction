# QuickAction

Dock shortcut tiếng Việt cho Windows 11 x64, viết bằng WPF / .NET 10. Không cần chạy dock bằng quyền Administrator.

## Chạy

Giải nén `dist/QuickAction-win-x64.zip` vào thư mục cố định và mở `QuickAction.exe`. Di chuột lên thanh trắng ở cạnh trái để mở dock. Nút ⚙ mở Settings; ▦ mở tất cả ứng dụng. Khi chưa có shortcut, vào Settings hoặc kéo EXE/BAT/CMD/PS1/MSI vào dock để thêm.

Lần đầu chưa có shortcut, Settings tự mở. Xem `VALIDATION.md` để biết các kiểm tra đã thực hiện và các tình huống còn cần thử trên máy đích.

- Kéo thanh idle hoặc khoảng nền trống để đổi màn hình/cạnh. Kéo icon ghim lên icon khác để sắp xếp.
- Mỗi shortcut có tên, chương trình, parameters, thư mục làm việc và lựa chọn Admin riêng; có thể thêm cùng EXE nhiều lần với parameters khác nhau.
- Settings → **Chọn ứng dụng đã cài…** mở lưới ứng dụng có icon, tên và tìm kiếm, bao gồm ứng dụng Microsoft Store. App desktop điền đường dẫn chương trình thật cùng tham số khởi chạy; app Store hoặc mục không có file chạy trực tiếp dùng định danh `shell:AppsFolder\…` (không hỗ trợ admin/tham số riêng). Icon được lưu cùng shortcut. Chọn ứng dụng rồi bấm **Lưu shortcut**. Ứng dụng portable chưa đăng ký với Windows vẫn có thể thêm bằng đường dẫn.
- File icon tùy chỉnh được chép vào dữ liệu ứng dụng; có thể chọn icon dựng sẵn hoặc tự lấy từ EXE.
- Chỉ ghim tối đa 5 app. Escape hoặc click ra ngoài đóng All Apps. Tray có Hiện/ẩn, Settings và Thoát.
- Dock mở rộng/thu gọn chiều rộng và chiều cao trong 400 ms, giảm tốc nhẹ ở cuối; giữ sát cạnh màn hình. Animation tắt khi Windows tắt hiệu ứng chuyển động.
- Animation dùng vùng hiển thị bên trong cửa sổ cố định, không resize cửa sổ native ở từng frame. Kích thước cửa sổ được chốt sau khi chuyển động kết thúc.
- Startup mặc định tắt. Nếu di chuyển thư mục ứng dụng, lưu lại thiết lập startup.

## Build / kiểm thử / đóng gói

CI/CD Microsoft Store: xem [hướng dẫn thiết lập](docs/MICROSOFT-STORE.md). Mỗi push/PR chạy build và tests; push `main` gửi gói MSIX sau khi đã cấu hình Partner Center và GitHub secrets.

Yêu cầu .NET SDK 10 và Visual Studio Build Tools với workload C++ desktop (C++20, Windows 11 SDK) trên Windows x64. Bước build nền native dùng Windows PowerShell có sẵn theo đường dẫn đầy đủ, không yêu cầu PowerShell 7 trong PATH của Visual Studio. Thành phần nền native được build tự động và đóng kèm DLL; máy chạy bản phát hành không cần cài build tools.

```powershell
dotnet build QuickAction/QuickAction.csproj -c Release
dotnet run --project QuickAction.Tests -c Release
./publish.ps1
```

Kiểm thử lõi dùng executable harness, không cần tải framework test bên ngoài. `QuickAction.Core` chứa models, validation, tạo lệnh chạy, pinning, geometry và persistence. `QuickAction` chứa ViewModel, WPF views và tích hợp Windows. Logic chạy process tách khỏi việc tạo lệnh để có thể kiểm tra mà không thực thi chương trình.

## Dữ liệu và hành vi

Cấu hình nằm ở `%LocalAppData%\QuickAction\config.json`, icon trong `icons`. Save giữ `.bak`; file hỏng được giữ lại trước khi phục hồi. Export/import ZIP chứa cấu hình và icon, không chứa file chương trình. Parameters được lưu nguyên văn và có thể chứa thông tin nhạy cảm; log chỉ ghi loại lỗi và HRESULT, tối đa hai file khoảng 1 MB.

EXE chạy trực tiếp; BAT/CMD qua `cmd.exe /d /s /c`; PS1 qua Windows PowerShell `-NoProfile -File`; MSI qua `msiexec /i`. Parameters là chuỗi native theo quy tắc của chương trình đích. BAT/CMD có shell semantics: các ký tự như `&`, `|`, `%` có ý nghĩa của CMD. QuickAction không chỉnh ExecutionPolicy, không bypass UAC và không ẩn cửa sổ console. Thông báo “đã gửi yêu cầu chạy” không có nghĩa chương trình đã hoàn tất. Script PowerShell có exit code khác 0 sẽ có thông báo riêng.

Dock dùng cửa sổ WPF layered với alpha theo từng pixel. Windows Composition vẽ blur và màu phủ trong cùng một lớp nền với clip bo góc; vị trí, kích thước và bán kính được cập nhật cùng tiến độ mở/thu của dock. Không dùng accent blur toàn cửa sổ vì hiệu ứng đó bỏ qua vùng cắt của cửa sổ layered. Lớp màu phủ tuân theo thiết lập độ đục (100% sẽ che kín blur). Khi transparency tắt, bật high contrast hoặc Windows không hỗ trợ blur, phần bên trong dock chuyển sang nền đục. Tọa độ docking dùng pixel vật lý của màn hình, kích thước giao diện dùng DIP. Nền luôn nổi mặc định bật; có thể tắt trong Settings.

Kiểm tra hồi quy góc trong suốt (cần phiên desktop Windows):

```powershell
dotnet build QuickAction.VisualTests -c Release -m:1
dotnet run --project QuickAction.VisualTests -c Release --no-build
dotnet run --project QuickAction.VisualTests -c Release --no-build -- --blur
```

Harness dùng cấu hình tạm riêng, xác minh HWND có cờ layered và render 36 trường hợp (4 cạnh × 3 trạng thái × 3 mức DPI), kiểm tra alpha ở góc bằng 0 và phần giữa còn hiển thị.

Chế độ `--blur` tạm mở nền sọc để so sánh độ tương phản pixel trên desktop khi bật/tắt blur, đồng thời kiểm tra các pixel ngoài vùng bo góc ở trạng thái idle/all và các frame mở/thu 10%, 35%, 70% tại cả bốn cạnh. Cần Windows bật transparency và không dùng high contrast. Kiểm tra này đo cả lớp Windows Composition mà RenderTargetBitmap của WPF không ghi lại được.

## Kiểm tra thủ công trước phát hành rộng

- Bốn cạnh dock, hover 150 ms, thu gọn 600 ms, mở/đóng All Apps và thao tác bàn phím.
- Hai màn hình khác DPI (100/150/200%), tọa độ âm, tháo màn hình, khởi động lại để xác nhận vị trí.
- Tạo shortcut cùng ứng dụng với parameters khác nhau; thêm icon tùy chỉnh; pin 5 app rồi thử pin app thứ 6; kéo đổi thứ tự.
- Chạy mẫu vô hại từng loại file với khoảng trắng/Unicode; quyền admin, hủy UAC và PowerShell bị policy chặn.
- Tắt transparency, bật high contrast, kiểm tra góc ngoài không nhận chuột; kiểm tra CPU khi idle.
- Sao lưu/khôi phục, cấu hình hỏng, tray, startup và mở ứng dụng lần hai.

Không có tự cập nhật, đồng bộ cloud hoặc telemetry. V1 hỗ trợ một dock, tối đa 5 app ghim, và số shortcut còn lại trong All Apps.
