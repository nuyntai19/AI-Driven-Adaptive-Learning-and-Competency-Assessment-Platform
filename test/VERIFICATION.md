# Kết quả kiểm chứng và sửa lỗi ngày 06/11/2026

Base HEAD: `4b20708c2714b9603c97959bf2c840842afc9171`, cùng các thay đổi chưa commit trong working tree. Lượt này sửa nguyên nhân của các lỗi test theo model, capability catalog và thiết kế giao diện hiện có.

## Kết quả sau sửa

| Kiểm tra đã chạy | Kết quả |
|---|---|
| Build API từ source đã sửa | Thành công, exit 0; frontend không đổi |
| Regression .NET toàn project | **4.699 đạt, 0 lỗi, 106 bỏ qua**, tổng 4.805 |
| Node tests: isolation, route catalog, PNG fixture, runner cleanup | **34/34 đạt** |
| AI prompt self-test, .NET SDK 10 | **61 kiểm tra đạt**, 17 fixture, 0 model call thật |
| Cypress toàn bộ 10 spec trên schema MySQL mới | **194/194 đạt**, 0 lỗi, 0 pending, 0 skipped; exit 0 |
| Adaptive Practice/lộ trình, nằm trong Cypress ở trên | **30/30 đạt**; các ca trước đây bị chặn đã chạy thực tế |
| Protected API surface, nằm trong một ca Cypress | **210 endpoint / 31 controller** đều trả 401 khi chưa xác thực |
| Migration discovery / model snapshot | Regression xác nhận migration được khám phá và **không còn pending model changes** |
| AI Prepare | 17 input, chỉ có `parameters` + `prompt`, không có `output` giả |

106 test .NET có điều kiện cần môi trường/fixture tích hợp riêng bị bỏ qua trong lượt container này; chúng không được tính là PASS. Cypress đã chạy HTTP/browser/MySQL thật cho các luồng thuộc bộ E2E trên database riêng.

## Nguyên nhân và thay đổi

1. **Migration Adaptive Practice không được khám phá.** File cũ thiếu metadata `[Migration]`/`[DbContext]`, snapshot cũng thiếu ba entity. Thêm [migration khắc phục](../src/EduTwin.DAL/Persistence/Migrations/20261105174213_RestoreMissingModulePersistence.cs) có designer và snapshot đồng bộ. Forward migration chỉ tạo ba bảng Adaptive Practice cùng index/FK theo cấu hình DAL hiện có; không sửa migration lịch sử hay tạo bảng qua fixture SQL.
2. **Snapshot ghi chú giáo viên bị thiếu, nhưng bảng đã có migration hợp lệ.** Đồng bộ mapping `TeacherStudentNote` vào snapshot/target model; không tạo hoặc xóa lại `teacher_student_notes`. Regression kiểm tra cả migration ghi chú cũ và migration mới đều được khám phá để tránh tạo bảng trùng.
3. **Validation tạo phiên chưa đầy đủ.** Contract/BLL kiểm tra thời lượng đúng ba lựa chọn UI 10/15/20 phút, mastery 0–100, enum, ID và môn/chủ đề active cùng tenant/môn. Kiểm tra chạy trước nhánh tiếp tục phiên cũ, bảo đảm request lỗi không tạo hoặc mở lại phiên tạm dừng.
4. **Controller ghi chú trả 500 vì thiếu DI registration.** Đăng ký đủ sáu use case vào `AddOrganization`; thêm regression dựng controller bằng DI thật và Cypress đọc bảng ghi chú trên schema mới.
5. **Endpoint ghi chú thiếu dynamic permission policy.** Bổ sung composite read/manage policy từ capability catalog hiện có; giữ tenant/ownership/author checks. Cypress xác nhận Student/PlatformAdmin bị từ chối và Teacher hợp lệ đọc được.
6. **Các kỳ vọng test cũ lệch contract đang chạy.** Sửa helper xử lý 204 khi chưa có active session; tính cả policy khai báo ở controller trong coverage test; cập nhật số entity/filter và confidence nullable. Các regression về fallback dùng lại chính sách deterministic đã có từ commit `70a6c75` và kiểm tra reason code tương ứng. Không đổi `EvidenceGate`, thuật toán mastery hay trọng số chấm điểm để làm test xanh.

Các lượt thử ban đầu và lỗi thiết lập đã được phân tích trước khi chạy lại toàn bộ suite. Kết quả 194/194 ở trên là một lượt hoàn chỉnh sau sửa, không cộng các lần chạy lặp.

## Artifact local và phạm vi triển khai

### Kiểm chứng lại và áp dụng database chính ngày 06/11/2026

Đã hoàn tất kiểm chứng sau triển khai lúc **20:02 ngày 06/11/2026 (Asia/Saigon)**. Database local chính là `edutwin`; không có database từ xa nào được cập nhật.

| Kiểm tra lại trước triển khai | Kết quả |
|---|---|
| Build API từ working tree hiện tại | Thành công |
| Regression .NET | **4.699 đạt, 0 lỗi, 106 bỏ qua** |
| Node | **34/34 đạt** |
| AI prompt offline | **61 đạt**, 17 fixture, 0 model call thật |
| Cypress trên schema riêng | **194/194 đạt**, 0 pending/skipped; dọn tài nguyên thành công |
| Khôi phục backup database chính vào schema UUID riêng | **44/44 bảng cũ khớp checksum** |
| Nâng cấp bản sao dữ liệu chính bằng EF | Thành công; không mất khóa bản ghi cũ |
| API trên bản sao sau nâng cấp | Readiness/MySQL Healthy, liveness 200; endpoint cần đăng nhập trả 401 |

**Lỗi môi trường chính được xác nhận:** lịch sử migration dừng tại `20261004074050_AddClassLifecycleAndAcademicGradeLevelConstraints`, thiếu cả các migration trung gian; không chỉ thiếu ba bảng Adaptive Practice. Bảng `teacher_student_notes` cũng chưa tồn tại. Vì vậy cần áp dụng **25 migration đang chờ**, theo đúng thứ tự EF, đến `20261105174213_RestoreMissingModulePersistence`. Không tạo bảng thủ công và không đánh dấu migration giả trong history.

Đã sao lưu toàn bộ database trước khi sửa, kiểm tra khôi phục, thử đường nâng cấp trên bản sao, rồi xác nhận database chính vẫn khớp backup trước khi áp dụng. API được tạm dừng khi không có job chấm bài Pending/Processing. Migration chính thực thi thành công; API mới được khởi động với `Seed__Enabled=false` qua override local [rollout.compose.yml](results/verification/rollout.compose.yml), giữ nguyên các volume và dữ liệu hiện có. Không reset mật khẩu, chạy lại demo seed, hoặc chạy script sửa điểm.

Sau triển khai đã xác nhận:

- Database chính có đủ ba bảng Adaptive Practice và bảng ghi chú giáo viên.
- Cột, khóa chính/unique, khóa ngoại và index khớp bản sao đã nâng cấp.
- Toàn bộ khóa bản ghi thuộc 44 bảng ban đầu còn tồn tại; dữ liệu định danh tài khoản và password hash được giữ nguyên.
- Các thay đổi trong migration trên bảng cũ khớp lần thử: migration history, audit, phạm vi lớp, catalog quyền và ánh xạ loại tài khoản. Sau startup, chỉ các bảng audit/quyền/vai trò/phiên bản xác thực có thay đổi theo bootstrap hiện có; dữ liệu học tập không đổi sau migration.
- Qua web chính `http://localhost:3000`: trang đăng nhập 200, readiness 200 với MySQL Healthy, liveness 200; hai endpoint ghi chú/Adaptive trả 401 khi chưa xác thực.
- Log API sau khởi động không có heading fail/critical; container đang chạy, restart count 0 tại thời điểm kiểm tra.

**Giới hạn đã ghi nhận:** mật khẩu seed trong `.env` hiện tại trả 401 với các tài khoản cũ được thử trên bản sao. Đây chưa phải bằng chứng lỗi đăng nhập của chương trình; mật khẩu seed không phải nguồn xác thực cho tài khoản đã tồn tại. Không sửa hoặc reset tài khoản để làm kiểm tra đạt. Đăng nhập bằng mật khẩu đã đạt trong Cypress trên tài khoản tổng hợp, nhưng **chưa xác nhận đăng nhập bằng mật khẩu thật của tài khoản đang dùng trên database chính**. Các kiểm tra runtime trên database chính chỉ dùng endpoint public và request chưa xác thực. Phương án tự ký JWT bị auto-review từ chối vì bỏ qua xác thực mật khẩu; không được thực thi.

Hai lần chặn trước khi áp dụng đã được ghi nhận riêng với lỗi ứng dụng: bộ đọc log kiểm chứng cần nhận diện UTF-16 của PowerShell, và một lần auto-review hết thời gian chờ. Cả hai xảy ra trước khi migration chạy. Sau khi sửa bộ đọc và thử lại được chấp thuận, migration và kiểm chứng sau triển khai đều hoàn tất.

Backup local, được Git bỏ qua: [SQL trước triển khai](../storage/backups/2026-11-06-before-system-repair-4c35948aedea4e1c8eee87014b05106d.sql), **319.281 byte**, SHA-256 `e7dbeba81dfe9f0f6fb4263944ec5d8b4752d732dbcef362bcfdb943044cd17c`. Bản sao dùng để thử nâng cấp được dọn sau khi đối chiếu xong; backup gốc được giữ lại.

Bằng chứng lượt triển khai, chỉ lưu local:

- [Build](results/verification/main-rollout-build.log), [.NET](results/verification/main-rollout-backend.log), [TRX](results/verification/backend/main-rollout.trx), [Node](results/verification/main-rollout-node.log), [AI offline](results/verification/main-rollout-ai-selftest.log).
- [Cypress toàn bộ](results/verification/main-rollout-cypress.log), [SQL xuất từ EF để review](results/verification/main-rollout.sql).
- [Khôi phục backup](results/verification/main-rollout-restore.log), [kết quả nâng cấp bản sao](results/verification/main-rollout-clone-result.log), [API bản sao](results/verification/main-rollout-clone-smoke.json).
- [Áp dụng EF trên database chính](results/verification/main-rollout-apply.log), [kiểm tra runtime chính](results/verification/main-rollout-smoke.json), [đối chiếu cuối cùng](results/verification/main-rollout-final.json).

Các artifact dưới đây được Git bỏ qua:

- [Build API đã sửa](results/verification/api-repair-build.log)
- [Regression .NET](results/verification/backend-repair-tests.log)
- [Kết quả .NET TRX](results/verification/backend/backend-repair.trx)
- [Cypress sau sửa, toàn bộ suite](results/verification/cypress-repair-full.log)
- [Summary Cypress mới nhất](results/cypress/summary.json)
- [17 input AI chuẩn bị để review](ai-prompts/results/prepared-review.json)

Mỗi lượt Cypress đã dọn container/schema theo owner manifest. Lượt kiểm chứng ban đầu chỉ áp dụng migration trên schema test sạch; lượt triển khai bổ sung ở trên đã áp dụng thành công lên database ứng dụng chính sau khi kiểm tra schema/history và backup.

Acceptance stack cố ý không có API key AI để kiểm tra fallback; log thiếu cấu hình provider trong luồng đó là điều kiện test. Chưa chạy `Live` hoặc tạo phản hồi model thật. File live khi developer cung cấp key vẫn chỉ xuất dữ liệu người dùng và output thực tế theo [hợp đồng export](ai-prompts/README.md).
