# Kiểm chứng phạm vi tri thức theo khối, giáo trình và lớp

Ngày hoàn tất: 23/10/2026. Đối chiếu với [kế hoạch đã duyệt](../plans/KNOWLEDGE-GRADE-CLASS-SCOPE-FIX-PLAN-2026-10-22.md) và [manifest phân loại đã duyệt](KNOWLEDGE-GRADE-PROPOSALS-2026-10-22.md).

## Kết quả triển khai

| Bước | Kết quả |
| --- | --- |
| 1. Contract và phân loại | Đã cập nhật yêu cầu, nguyên tắc, schema, API và UI. Manifest được áp dụng đúng phạm vi Published đã duyệt; `MATH10_PTBH` / NodeId `20006` vẫn chưa phân loại. |
| 2. Database và guard | Hai migration bổ sung (`20261022153531`, `20261023080000`) tạo phiên bản đồ thị theo môn/khối, mapping node/edge/parent, pin phiên bản giáo trình và trigger/FK/check bảo vệ grade, chu trình, Published, lịch sử và application. Không đổi định danh node/edge. |
| 3. API | Graph, node picker và giáo trình resolve ở server theo khối, phiên bản, giáo trình hoặc lớp/năm học/lịch sử; trả phạm vi đã giải, nguồn ứng dụng và prerequisite ngoài phạm vi. Request sai grade, tenant, lớp hoặc quyền bị từ chối. |
| 4. Giao diện | Teacher views đang dùng đã có lọc khối/lớp/năm học, editor báo lựa chọn xung đột khi đổi khối, graph theo lớp và nhãn nguồn, công cụ fork/publish phiên bản. Query key và invalidation theo scope. |
| 5. Luồng học sinh | Câu hỏi, luyện tập, adaptive, V4, graph dashboard và chỉ số risk lấy phạm vi lớp đang áp dụng hoặc thư viện khối đã duyệt. Prerequisite ngoài phạm vi dùng evidence hoặc đánh giá bổ sung; policy V4 mới yêu cầu tạo lại đề xuất cũ. |
| 6. Kiểm thử/chuyển đổi | Migration trên bản sao từ cả schema trước bước 1 và schema đang chạy, database mới chạy đủ migration và seed. Đã áp dụng local, API/Web hoạt động. |

## Bằng chứng nghiệm thu

- Trước hardening có backup riêng `storage/backups/knowledge-scope-2026-10-23-before-hardening.sql`; SHA-256 `3775e142ee8ca18ff06cd72adcfedcd0a9dacd24231914b56bfb64e79bae9c20`. Backup trước migration đầu cũng còn trong `storage/backups/knowledge-scope-2026-10-22-before-migration.sql`. Bản sao restore được và cả đường nâng cấp 50→52, 51→52 đều thành công; 29 bảng dữ liệu được bảo vệ giữ nguyên fingerprint.
- Database local `edutwin` đang ở 52 migration; 26 phiên bản gồm 13 legacy giữ lịch sử, 12 thư viện khối đã duyệt và 1 Draft có sẵn; 57 mapping node, 36 mapping edge; NodeId `20006` có 0 mapping. Chạy lại migration báo không còn migration cần áp dụng.
- Sau nâng cấp và khởi động API, so với backup ngay trước hardening, 29 bảng gồm giáo trình, lớp/application, câu hỏi, bài làm, evidence, Twin và lịch sử có **0 bảng thay đổi**. API `/api/v1/health/ready` trả `Healthy`, MySQL `Healthy`; Web trả HTTP 200.
- Image API từ commit trước thay đổi (`abf8199`) đã được build riêng và chạy trên bản sao database đã nâng cấp 52 migration: health, đăng nhập và đọc graph đều HTTP 200. Vì image cũ chỉ biết graph theo môn, đây là đường quay lui khẩn cấp ngắn hạn với thao tác học thuật tạm khóa; không vận hành lâu dài bằng UI cũ sau khi đã bật phân loại khối. Phép thử dùng `scripts/e2e/knowledge_scope_rollback_check.cjs`, container/user tạm đã được dọn.
- Kiểm tra HTTP thực trên database tách biệt: **16/16 đạt** (`storage/verification/knowledge-scope-api-results.json`). Bao gồm cách ly khối 10/12, năm học/lớp, quyền, thay grade đồng thời sửa node/lớp, pin Published, fork và bất biến phiên bản cũ.
- Trình duyệt thực trên stack tách biệt đạt 10 kiểm tra: đăng nhập giáo viên, đổi khối và xử lý node xung đột, graph lớp 12/năm học, lọc danh sách và bố cục điện thoại. Ảnh và kết quả ở `storage/verification/knowledge-scope-browser/`.
- Kiểm thử MySQL thật: 2/2 đạt, gồm ràng buộc grade/chu trình/published và ghi cạnh đồng thời. Frontend: 819 bài đạt; build TypeScript/Vite và Docker API/Web đạt. Backend đầy đủ sau triển khai: **4592 đạt, 0 lỗi, 106 bỏ qua** (`tests/EduTwin.BLL.Tests/TestResults/knowledge-scope-postdeploy.trx`). Các bài MySQL cần kết nối riêng được chạy trong lượt 2/2 ở trên.

## Điểm cần hiểu khi sử dụng

- Các giáo trình Published trước chuyển đổi được chốt phiên bản legacy theo đúng cấu trúc đã lưu, nhằm giữ lịch sử. Thư viện mới chỉ dùng manifest khối đã duyệt. Bản nháp V2 đang chứa node Toán từ khối khác cần giáo viên sửa phạm vi trước khi xuất bản; hệ thống hiển thị xung đột và không tự bỏ node.
- Node `MATH10_PTBH` chờ phân loại chuyên môn riêng; tên/mã không được dùng để suy khối.
- Câu hỏi và evidence quá khứ không bị viết lại. Các câu ENGLISH cũ có grade 10 vẫn giữ lịch sử; selector hiện tại chỉ dùng câu hợp lệ với ngữ cảnh grade. Chưa tái tính hàng loạt risk/Twin từ evidence cũ, đúng phạm vi kế hoạch.
- Backup là đường khôi phục riêng; khi cần quay lui khẩn cấp bản chạy, dùng build cũ với schema bổ sung và tạm khóa ghi học thuật cho đến khi build mới trở lại. Không chạy `Down` xóa mapping hoặc phục hồi đè lên evidence mới phát sinh.

