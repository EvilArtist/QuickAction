# Tự động cập nhật Microsoft Store

Workflow `.github/workflows/store.yml` chạy build và core/launch tests khi push vào mọi nhánh hoặc mở pull request. Push vào `main` (hoặc chạy thủ công trên `main`) còn đóng gói bản Windows 11 x64 self-contained và gửi MSIX lên Store. PR không có quyền truy cập secrets phát hành. Bản MSIX được lưu trong Actions artifacts 14 ngày.

## Thiết lập một lần

1. Đăng ký Windows developer tại Partner Center; tạo sản phẩm **MSIX**, đặt tên QuickAction. Pipeline này dành cho sản phẩm miễn phí theo giới hạn hiện tại của Store CLI.
2. Trong Product identity, lấy chính xác Package/Identity/Name, Package/Identity/Publisher và Package/Properties/PublisherDisplayName. Không dùng tên hiển thị thay cho Identity Name.
3. GitHub repository → Settings → Secrets and variables → Actions → **Variables**: tạo các biến sau ở cấp repository (build job cần đọc chúng).

| Variable | Giá trị |
| --- | --- |
| `STORE_IDENTITY_NAME` | Package/Identity/Name |
| `STORE_PUBLISHER` | Package/Identity/Publisher, ví dụ `CN=...` |
| `STORE_PUBLISHER_DISPLAY_NAME` | PublisherDisplayName đúng trong Partner Center |
| `STORE_PRODUCT_ID` | Store product ID của ứng dụng |

4. Build package đầu tiên bằng lệnh bên dưới rồi upload thủ công trong Partner Center. Hoàn thành mô tả, ảnh chụp, phân loại độ tuổi, thông tin quyền riêng tư và giải trình `runFullTrust` (WPF desktop launcher). Bản đầu tiên phải được duyệt và đang live trước khi dùng API cập nhật. Pipeline không tạo listing mới hay tự điền thông tin pháp lý.
5. Liên kết Microsoft Entra tenant với Partner Center, tạo app registration, thêm app này trong Partner Center → Account settings → User management → Microsoft Entra applications với vai trò **Manager**.
6. GitHub → Settings → Environments: tạo `microsoft-store`, giới hạn deployment branch là `main`. Không bật required reviewers nếu muốn phát hành hoàn toàn tự động.
7. Thêm **Secrets** dưới environment `microsoft-store` (hoặc repository):

| Secret | Giá trị |
| --- | --- |
| `AZURE_AD_TENANT_ID` | Entra tenant ID |
| `AZURE_AD_APPLICATION_CLIENT_ID` | Application/client ID |
| `AZURE_AD_APPLICATION_SECRET` | Client secret **value**, không phải secret ID |
| `SELLER_ID` | Seller ID của Partner Center |

Không gửi secret vào chat, commit hay file cấu hình. Đổi secret trong GitHub khi hết hạn.

8. Push code lên `main`. Xem tab Actions và trạng thái submission trong Partner Center. Workflow thành công nghĩa là lệnh gửi đã hoàn thành; ứng dụng chỉ xuất hiện với người dùng sau khi Microsoft chứng nhận/phát hành. Nếu có submission đang xử lý hoặc bản nháp sửa thủ công, xử lý trong Partner Center trước khi chạy lại; workflow không tự xóa submission.

## Build MSIX tại máy

Cần .NET SDK 10 và Windows SDK có MakeAppx x64:

```powershell
./scripts/Build-StorePackage.ps1 `
  -IdentityName 'YOUR.IDENTITY.NAME' `
  -Publisher 'CN=YOUR-PUBLISHER-ID' `
  -PublisherDisplayName 'Your publisher name' `
  -Version 1.0.1.0
```

File nằm trong `dist/store`. Đây là gói chưa ký dành cho Store (Store ký khi phát hành); không dùng trực tiếp để sideload. CI dùng phiên bản UTC dạng `1.<số ngày từ 2020-01-01>.<số khoảng 2 giây trong ngày>.0`; mỗi lần build mới tăng phiên bản, revision luôn bằng 0. Nếu ứng dụng hiện có phiên bản lớn hơn, sửa công thức trước khi bật pipeline. Hai build trong cùng khoảng 2 giây có thể trùng phiên bản; các lần phát hành main được chạy tuần tự và build mất lâu hơn khoảng này.

## Kiểm tra bản đóng gói trước submission đầu tiên

Build CI kiểm tra logic, khởi chạy mẫu và MakeAppx validation. Nó không thay thế Windows App Certification Kit hoặc kiểm tra desktop tương tác. Chạy WACK và thử bản cài MSIX trên Windows 11, đặc biệt tray, UAC, chạy script, lưu cấu hình, cập nhật và gỡ cài đặt.

Startup hiện dùng registry Run cho bản portable; chưa được chuyển sang StartupTask của MSIX. Không coi startup của bản Store là đã được hỗ trợ/kiểm chứng. Cần kiểm tra và chuyển tích hợp này trước khi quảng bá tính năng startup trên Store. Việc đóng gói cũng có thể làm thay đổi vị trí dữ liệu do chuyển hướng của Windows; không giả định tự di chuyển cấu hình portable.

## Tài liệu chính thức

- [Microsoft: GitHub Actions publishing, điều kiện tài khoản và bản live đầu tiên](https://learn.microsoft.com/en-us/windows/apps/publish/msstore-dev-cli/github-actions)
- [Microsoft Store Developer CLI, giới hạn sản phẩm miễn phí](https://learn.microsoft.com/en-us/windows/apps/publish/msstore-dev-cli/overview)

Chưa có secrets/identity của tài khoản trong repository. Chỉ thêm workflow không tự kết nối được Partner Center. Thiếu cấu hình trên `main` sẽ báo lỗi rõ ràng; các nhánh khác vẫn có thể kiểm tra đóng gói với identity CI.
