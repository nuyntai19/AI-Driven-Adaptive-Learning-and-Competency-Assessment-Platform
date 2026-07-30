# Hướng dẫn cài đặt và chạy dự án EduTwin

Tài liệu này mô tả cách cài đặt, cấu hình và chạy phiên bản hiện tại của dự án **AI-Driven-Adaptive-Learning-and-Competency-Assessment-Platform** trên máy Windows.

## 1. Tổng quan dự án

Dự án gồm 3 phần chính:

- **Backend**: .NET Web API ở `src/EduTwin.API`
- **Business / Data layer**: `src/EduTwin.BLL`, `src/EduTwin.DAL`, `src/EduTwin.Contracts`
- **Frontend**: React + Vite ở `web/edutwin-web`
- **Cơ sở dữ liệu**: MySQL, có thể chạy bằng Docker Compose

## 2. Yêu cầu môi trường

Bạn cần cài sẵn:

- **.NET SDK 10.0** hoặc mới hơn tương thích với `net10.0`
- **Node.js 22** và `npm`
- **Docker Desktop** nếu muốn chạy toàn bộ hệ thống bằng container
- **Git** để lấy mã nguồn

## 3. Cấu trúc cổng và đường dẫn mặc định

Theo cấu hình hiện tại:

- **Frontend dev server**: `http://localhost:3000`
- **Backend API**: `http://localhost:5000`
- **Adminer**: `http://localhost:8080`
- **MySQL**: `localhost:3307` khi chạy bằng Docker Compose

Các API chính dùng tiền tố `/api/v1`.

## 4. Chuẩn bị biến môi trường

Trong thư mục gốc có file mẫu `.env.example`. Hãy sao chép thành `.env` và chỉnh lại giá trị phù hợp.

Các biến quan trọng:

- `ConnectionStrings__Default`: chuỗi kết nối MySQL
- `Jwt__SigningKey`: khóa ký JWT cho môi trường phát triển
- `Jwt__Issuer`, `Jwt__Audience`: thông tin phát hành và audience của JWT
- `Gemini__ApiKey`: khóa API Gemini nếu dùng tính năng liên quan
- `Seed__CenterManagerPassword`: mật khẩu seed dữ liệu ban đầu

Lưu ý:

- Frontend hiện **không cần** cấu hình `VITE_API_BASE_URL`
- Khi chạy local, frontend Vite sẽ proxy các request `/api` sang `http://localhost:5000`

## 5. Cài đặt phụ thuộc

### 5.1. Backend .NET

Mở PowerShell tại thư mục gốc dự án rồi chạy:

```powershell
dotnet tool restore --tool-manifest .config/dotnet-tools.json
dotnet restore EduTwin.sln
dotnet build EduTwin.sln
```

Lưu ý: file manifest của `dotnet tool` nằm trong thư mục `.config`, nên nếu chạy `dotnet tool restore` ở thư mục gốc mà không chỉ rõ manifest thì lệnh sẽ báo không tìm thấy file manifest.

Nếu bạn muốn kiểm tra nhanh trước khi cài tiếp, có thể dùng:

```powershell
dotnet tool restore --tool-manifest .config/dotnet-tools.json
dotnet ef --version
```

### 5.2. Frontend React/Vite

```powershell
cd web\edutwin-web
npm ci
```

## 6. Chạy nhanh bằng Docker Compose

Đây là cách đơn giản nhất nếu bạn muốn chạy đủ backend, frontend, MySQL và Adminer cùng lúc.

### 6.1. Chuẩn bị

1. Đảm bảo file `.env` đã tồn tại ở thư mục gốc.
2. Kiểm tra `ConnectionStrings__Default` trỏ tới service `mysql` như trong file mẫu.
3. Kiểm tra các biến JWT và seed đã được điền hợp lệ.

### 6.2. Khởi động hệ thống

Tại thư mục gốc dự án:

```powershell
docker compose up -d --build
```

### 6.3. Truy cập dịch vụ

- Frontend: `http://localhost:3000`
- Backend health liveness: `http://localhost:5000/api/v1/health/live`
- Backend health readiness: `http://localhost:5000/api/v1/health/ready`
- Adminer: `http://localhost:8080`

### 6.4. Dừng hệ thống

```powershell
docker compose down
```

## 7. Chạy cục bộ không dùng Docker

Nếu bạn muốn debug từng phần riêng lẻ, chạy backend và frontend độc lập như sau.

### 7.1. Khởi động MySQL

Bạn cần một máy MySQL cục bộ hoặc một container MySQL riêng.

Nếu dùng đúng cấu hình mặc định của dự án, chuỗi kết nối có dạng:

```text
Server=localhost;Port=3306;Database=edutwin;User=edutwin_user;Password=edutwin_password;
```

### 7.2. Chạy backend

Tại thư mục gốc dự án, chạy lần lượt các bước sau:

1. Khôi phục tool và thư viện backend:

```powershell
dotnet tool restore --tool-manifest .config/dotnet-tools.json
dotnet restore EduTwin.sln
```

2. Thiết lập biến môi trường cho phiên bản local:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__Default = "Server=localhost;Port=3306;Database=edutwin;User=edutwin_user;Password=edutwin_password;"
$env:Jwt__SigningKey = "YOUR_DEVELOPMENT_SIGNING_KEY_HERE"
$env:Jwt__Issuer = "EduTwinAuth"
$env:Jwt__Audience = "EduTwinUsers"
$env:Seed__Enabled = "true"
$env:Seed__CenterManagerPassword = "change_me_to_a_secure_seed_password"
```

3. Chạy API backend:

```powershell
dotnet run --project src/EduTwin.API/EduTwin.API.csproj
```

Backend sẽ lắng nghe trên cổng do ASP.NET Core chọn mặc định hoặc theo cấu hình môi trường của bạn. Các API dùng đường dẫn `/api/v1/...`.

Nếu bạn chỉ muốn kiểm tra backend đã build được chưa, có thể chạy thêm:

```powershell
dotnet build src/EduTwin.API/EduTwin.API.csproj
```

### 7.3. Chạy frontend

Mở terminal mới:

```powershell
cd web\edutwin-web
npm run dev
```

Vite mặc định chạy ở `http://localhost:3000` và proxy `/api` sang `http://localhost:5000`.

## 8. Kiểm tra sau khi chạy

Bạn có thể kiểm tra nhanh bằng các URL sau:

- `http://localhost:5000/api/v1/health/live`
- `http://localhost:5000/api/v1/health/ready`
- `http://localhost:3000`

Nếu chạy bằng Docker, có thể mở thêm Adminer tại `http://localhost:8080` để kiểm tra database.

## 9. Chạy kiểm thử

Project test hiện có:

- `tests/EduTwin.BLL.Tests`

Lệnh chạy test:

```powershell
dotnet test tests/EduTwin.BLL.Tests/EduTwin.BLL.Tests.csproj
```

## 10. Ghi chú quan trọng

- Backend dùng **.NET 10** và các project đều bật `TreatWarningsAsErrors`, nên build có thể fail nếu có cảnh báo mới.
- Backend sẽ seed dữ liệu khi `Seed__Enabled=true` và môi trường là `Development`.
- Khi chạy production bằng Docker, web container phục vụ frontend và tự proxy API qua service `api`.
- Nếu bạn đổi cổng hoặc chuỗi kết nối, cần cập nhật đồng bộ cả `.env`, backend, và cấu hình chạy local.
