# CGCLauncher

> Launcher chính thức cho máy chủ Minecraft **CrystalGemCraft** — dành riêng cho cộng đồng người chơi Việt Nam.

[![Open Source](https://img.shields.io/badge/Open%20Source-MIT-orange)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-blue)]()
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)]()
![GitHub all releases](https://img.shields.io/github/downloads/RageOfFire/CGCLauncher/total)
![Discord](https://img.shields.io/discord/752171524919918672)
![GitHub commit activity](https://img.shields.io/github/commit-activity/m/RageOfFire/CGCLauncher)
![GitHub last commit](https://img.shields.io/github/last-commit/RageOfFire/CGCLauncher)
![GitHub repo size](https://img.shields.io/github/repo-size/RageOfFire/CGCLauncher)
![GitHub followers](https://img.shields.io/github/followers/RageOfFire)
![GitHub Repo stars](https://img.shields.io/github/stars/RageOfFire/CGCLauncher)

---

## Mã nguồn mở

CGCLauncher là **phần mềm mã nguồn mở** được phát hành theo giấy phép **MIT**.

Bạn có thể tự do:
- Xem toàn bộ mã nguồn
- Fork và tùy chỉnh theo nhu cầu
- Đóng góp tính năng hoặc báo lỗi qua Issues/Pull Request
- Sử dụng lại code cho dự án của bạn (miễn là giữ nguyên thông tin bản quyền)

> 🔗 GitHub: `https://github.com/RageOfFire/CGCLauncher`

---

## Yêu cầu để build

| Công cụ | Link tải |
|---------|---------|
| .NET 8 SDK | https://dotnet.microsoft.com/download/dotnet/8.0 |
| Java 17+ | Cần để chạy Minecraft (không cần để build launcher) |

## Build & chạy

```
dotnet restore
dotnet build
dotnet run
```

---

## Cấu hình trước khi phát hành

### Địa chỉ máy chủ

Mở `MainWindow.xaml.cs` và tìm đoạn này gần đầu class:

```csharp
private static readonly DefaultServerEntry[] DefaultServers =
{
    new("CrystalGemCraft", "CHANGE_ME.example.net"),
};
```

Thay `"CHANGE_ME.example.net"` bằng địa chỉ máy chủ thật của bạn:

```csharp
private static readonly DefaultServerEntry[] DefaultServers =
{
    new("CrystalGemCraft", "mc.crystalgemcraft.com"),
};
```

Launcher sẽ tự động ghi `servers.dat` lần đầu người chơi khởi chạy, máy chủ CrystalGemCraft sẽ có sẵn trong danh sách Multiplayer. Chỉ ghi một lần — người chơi quay lại giữ nguyên danh sách server của họ.

---

## Cấu trúc dự án

```
CGCLauncher/
├── Models/
│   ├── AuthModels.cs         Các model cho Xbox Live / XSTS / Minecraft auth
│   ├── MsaTokens.cs          Microsoft OAuth token + stored session
│   ├── LauncherSettings.cs   Lưu tuỳ chọn người chơi (phiên bản, thư mục, v.v.)
│   ├── VersionManifest.cs    Danh sách phiên bản từ Mojang
│   ├── VersionDetails.cs     Chi tiết từng phiên bản (thư viện, args, assets)
│   ├── Rule.cs               Đánh giá rule của Mojang (theo OS/arch)
│   └── AssetIndexFile.cs     Bảng hash → URL cho tài nguyên game
│
├── Services/
│   ├── AuthService.cs        Chuỗi xác thực Microsoft → Xbox → Minecraft
│   ├── LoginWindow.xaml(.cs) Cửa sổ trình duyệt nhúng để đăng nhập Microsoft
│   ├── MojangApiService.cs   Tải manifest và JSON chi tiết từ Mojang
│   ├── DownloadService.cs    Tải client jar, thư viện, native, tài nguyên
│   ├── GameLauncher.cs       Xây dựng JVM args và khởi chạy Minecraft
│   ├── ServerListService.cs  Ghi servers.dat lần đầu khởi chạy
│   └── SettingsService.cs    Lưu/tải settings vào %AppData%\CGCLauncher
│
├── Utils/
│   ├── OsUtils.cs            Phát hiện OS/arch + đánh giá rule
│   └── UuidUtils.cs          Tạo UUID offline (dùng khi chưa đăng nhập)
│
├── Styles/
│   └── Theme.xaml            Giao diện pixel-art: gạch đá, gỗ, nút gem
│
├── Assets/
│   ├── logo.png              Icon server (dùng trên title bar và taskbar)
│   ├── logo.ico              .ico đa độ phân giải tạo từ logo.png
│   └── VT323-Regular.ttf     Font pixel nhúng sẵn (không cần cài)
│
├── MainWindow.xaml(.cs)      Cửa sổ chính: chọn phiên bản, nút launch, console, auth
└── App.xaml(.cs)             Entry point + merge theme dictionary
```

---

## Tính năng

- **Chọn phiên bản** — tải và chạy bất kỳ bản Minecraft chính thức nào
- **Snapshot toggle** — hiện/ẩn bản snapshot bằng checkbox
- **Máy chủ tích hợp sẵn** — CrystalGemCraft tự động có trong danh sách Multiplayer
- **Đăng nhập Microsoft** — chuỗi OAuth đầy đủ, refresh token lưu giữa các phiên
- **Chế độ ngoại tuyến** — nhập tên bất kỳ và chơi không cần đăng nhập (Offline Mode)
- **Ghi nhớ phiên** — phiên bản, thư mục và tên người dùng được khôi phục khi mở lại
- **Ẩn khi chơi** — launcher tự ẩn khi Minecraft chạy, hiện lại khi đóng game
- **Giao diện pixel-art** — chủ đề gạch đá / gỗ phong cách CrystalGemCraft

---

## Xác thực

Sử dụng client ID Xbox của chính Microsoft (`00000000402b5328`) — cách tiếp cận tương tự Prism Launcher, ATLauncher và GDLauncher. **Không cần đăng ký Azure app.**

```
Đăng nhập Microsoft (cửa sổ trình duyệt)
  → MS access token
    → Xbox Live token
      → XSTS token
        → Minecraft access token
          → Lấy thông tin profile (tên + UUID)
```

Refresh token được lưu tại `%AppData%\CGCLauncher\auth\session.json`.
Xoá file đó để buộc đăng nhập lại.

---

## Cài đặt người chơi

Tuỳ chọn được lưu tại `%AppData%\CGCLauncher\settings.json`:

| Trường | Nội dung |
|--------|---------|
| `LastVersionId` | Phiên bản Minecraft đã chọn lần cuối |
| `InstallDir` | Thư mục cài đặt game |
| `ShowSnapshots` | Trạng thái checkbox Snapshot |
| `HideLauncherOnLaunch` | Ẩn launcher khi game chạy |
| `OfflineUsername` | Tên người chơi chế độ ngoại tuyến |

---

## Roadmap

- [x] Version Manager — tải và khởi chạy bất kỳ bản Minecraft chính thức
- [x] Máy chủ tích hợp sẵn — servers.dat được seed với IP của bạn lần đầu chạy
- [x] Giao diện pixel-art — phong cách CrystalGemCraft
- [x] Microsoft OAuth — chuỗi auth đầy đủ, lưu refresh token, đăng xuất
- [x] Lưu phiên — phiên bản và thư mục được khôi phục khi mở lại
- [x] Ẩn khi chơi — launcher ẩn trong lúc game chạy, hiện lại khi đóng
- [x] Mã nguồn mở — giấy phép MIT
- [ ] Hardcode phiên bản server (bỏ picker, luôn dùng đúng phiên bản server)
- [ ] Tự cập nhật từ CDN/GitHub releases

---

## Các endpoint mạng

| Endpoint | Mục đích |
|----------|---------|
| `launchermeta.mojang.com` | Danh sách phiên bản |
| `resources.download.minecraft.net` | Tải tài nguyên |
| `login.live.com` | Microsoft OAuth tokens |
| `user.auth.xboxlive.com` | Xbox Live auth |
| `xsts.auth.xboxlive.com` | XSTS token |
| `api.minecraftservices.com` | Minecraft token + profile |

---

## Đóng góp

Pull Request và Issues luôn được chào đón! Nếu bạn muốn thêm tính năng hoặc báo lỗi, hãy mở một Issue trên GitHub trước để thảo luận.

Giấy phép: **MIT** — xem file `LICENSE` để biết chi tiết.
