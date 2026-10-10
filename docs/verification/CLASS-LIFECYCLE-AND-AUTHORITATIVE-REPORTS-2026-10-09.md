# Sửa vòng đời lớp và báo cáo điểm — 09/10/2026

## Kết quả

Đã cập nhật API/web localhost. Không push. Không xóa/chuyển bài làm hoặc chốt điểm thay giáo viên/quản lý.

- Bỏ suy diễn “không có khối = lớp lịch sử”. Status là nguồn chuẩn: Active + Current; Archived + History.
- Quản lý trung tâm lưu trữ/mở lại bằng biểu mẫu sửa lớp, bắt buộc lý do, kiểm tra RowVersion. Một SaveChanges transaction lưu cả lớp và nhật ký before/after; tác nhân/thời điểm lấy từ phiên server.
- Nhật ký phân trang 20 sự kiện, chỉ giáo viên phụ trách hoặc quản lý cùng trung tâm được đọc. Tái sử dụng AuthorizationAuditLog append-only, không tạo một hệ thống thông báo/nhật ký tổng quát mới.
- Lưu trữ ngừng tạo/phát hành bài và nhận bài/nội dung nháp mới. Không cắt quyền xem bài/chấm điểm cũ. Giáo trình còn cấu hình được hiển thị “tạm ngừng — lớp đã lưu trữ”; mở lại không tạo lại ứng dụng đã kết thúc.
- Một bộ tính điểm xác định dùng chung cho học sinh và báo cáo giáo viên, thang 10. Hoàn thành/nộp đủ không đồng nghĩa 10 điểm. Điểm tạm được ghi rõ, chỉ điểm Final đã chốt tham gia trung bình/min/max và đánh giá học lực theo điểm.
- Chưa giao bài/chưa có điểm đã chốt không được tự kết luận nguy cơ cao. Bài quá hạn là dấu hiệu cần nhắc nhở, khác với lỗi kiến thức/điểm thấp.
- Báo cáo đọc một snapshot nhất quán cho lớp (repeatable read), chỉ bài đã phát hành và đúng AssignmentTargets. Không gọi AI và không N+1 theo học sinh/bài. Không tải câu trả lời/ảnh hay văn bản reasoning để tính điểm.
- Không còn lấy trang 100 học sinh/50 bài rồi giả là đầy đủ; báo cáo nạp toàn bộ snapshot lớp, UI render 25 học sinh/trang, CSV/Excel/bản in dùng đầy đủ snapshot. Nếu tải lỗi thì không giả NotStarted/0 điểm và khóa xuất/in.
- Danh sách/badge học sinh thống nhất guard Current: lớp Active/Current, thành viên Active, môn phù hợp. Lớp cụ thể không phù hợp trả not-found; chế độ lịch sử giữ truy cập bài được giao cũ.

## Dữ liệu localhost

Backup riêng (Git-ignored): storage/backups/2026-10-09-before-lifecycle.sql và .json.
SHA-256 SQL: be3575a00f9025fecd303513452e0ded8eb65537492be1bdbbc3a1f344a5b690.

Migration mới: 20261008184057_AlignClassLifecycleScope. Không chỉnh lại migration cũ đã áp dụng.

Bốn lớp Active/History bị phân loại sai trước đó (hai EDUTWIN_A, hai EDUTWIN_B) được sửa thành Active/Current. Status, khối, thành viên, phân công vẫn giữ nguyên; RowVersion tăng, updated_by NULL. Có bốn sự kiện ClassScopeCorrected: ActorUserId/CreatedBy NULL, AfterData.Source System/Migration. Không giả rằng quản lý đã lưu trữ hoặc mở lại lớp.

Sau migration: 10 lớp Active/Current. Không tự lưu trữ lớp “Dữ liệu kiểm thử cũ” chỉ vì tên của nó. Quyết định lưu trữ lớp thật phải do quản lý thực hiện.

Đối chiếu SHA-256 toàn bộ hàng theo primary key của 19 bảng bảo vệ: **không thay đổi**. Trong đó assignments 4; assignment_questions 54; targets/progress 20/20; attempts 204; reasoning_analyses 204; attachments 2; curriculums 7; curriculum_nodes 84; curriculum_classes 4; curriculum application ledger 4; memberships 30; student twins 4; knowledge twins 14; twin history 362.

MySQL DDL có implicit commit; phần sửa dữ liệu + audit có transaction riêng trước thêm check. EF cảnh báo thao tác suppressTransaction; thao tác thực tế hoàn thành thành công. Khi triển khai môi trường khác phải backup, dừng ghi lớp và kiểm tra migration status; không sửa hàng loạt bằng SQL ngoài migration. Down chỉ bỏ constraint, không xóa nhật ký hay đảo lại phân loại sai.

## Kiểm chứng

- Toàn bộ backend: 3.961 pass, 0 fail, 77 integration tests skipped khi không bật môi trường SQL.
- Frontend: 596 pass, 0 fail; TypeScript/Vite và Docker API/web build thành công.
- Bật riêng MySQL trên database tạm: 10/10 pass (7 workspace unit + 3 integration). Bao gồm migration hệ thống, canonical check, teacher không được archive, manager archive/reopen có audit, bài trả lời đủ nhưng sai = 0/10, báo cáo lớp lưu trữ vẫn đọc được, curriculum ledger không bị kết thúc bởi manager, ứng dụng đã kết thúc không sống lại.
- Test báo cáo 101 học sinh/55 bài đã phát hành + một bài Draft: không bị cắt trang, chỉ học sinh được nhắm đích có nghĩa vụ bài, Draft không được tính.
- CSV 125 học sinh: giữ dòng cuối. Excel số điểm vẫn là numeric để tính toán; cột phân loại ghi “Chưa chốt điểm” cho điểm tạm.
- Chrome Teacher Math: trang báo cáo nạp thành công. Lớp Toán 12 chưa giao bài: không có điểm/không gắn nguy cơ cao, học sinh “Chưa đủ dữ liệu đánh giá”. Lớp kiểm thử cũ: student01 nộp 3/3 nhưng điểm trung bình bài đã chốt 8,5 (không còn bị suy ra 10); nhật ký hiện nguồn System/Migration.
- Viewport 900px: tiêu đề rộng 837px, cao 36px; root scrollWidth 885px/viewport 900px. Viewport 375px: scrollWidth 360px/viewport 375px. Không tràn ngang cả trang hay ép tiêu đề thành cột hẹp.
- Chrome Center Manager EDUTWIN_A: biểu mẫu chuyển Active→Archived hiện textarea required=true/maxLength=500. Chỉ mở rồi hủy, không bấm lưu; lớp cũ vẫn Active và RowVersion=6. Drawer quản lý hiển thị đúng sự kiện System/Migration. Server/source cũng đã kiểm tra cả chiều mở lại; không tạo một lớp Archived thật chỉ để kiểm tra UI.

Bằng chứng: docs/verification/evidence/class-lifecycle/teacher-report-900.jpg, teacher-report-375.jpg, manager-archive-reason.jpg (biểu mẫu đã hủy, không lưu), manager-class-history.jpg. Dữ liệu ảnh kiểm chứng là dữ liệu localhost, không tự push.

Sau người dùng đăng nhập Center Manager để kiểm tra: users thay đổi một tài khoản ở đúng ba trường last_login_at/updated_at/row_version; script verify_lifecycle_login_delta.cjs đối chiếu riêng SQL backup và xác nhận không thay đổi các trường khác. 18 bảng bảo vệ còn lại vẫn nguyên hash. Đây là metadata đăng nhập hợp lệ, không phải sửa vai trò/mật khẩu hay dữ liệu nghiệp vụ.

## Giới hạn/phần không mở rộng

- Không triển khai hệ thống thông báo/nhật ký đa tác nhân tổng quát (vẫn là hạng mục để sau).
- Không sửa điểm lịch sử, gọi AI chấm lại, đổi lộ trình/Twin hoặc xuất bản các giáo trình Draft Toán 10/11/12.
- Báo cáo full snapshot theo lớp; không phải endpoint tổng hợp tất cả trung tâm. Với lớp có khối lượng dữ liệu rất lớn có thể bổ sung export job/server paging sau; hiện không dùng giới hạn trang để bỏ dữ liệu.
- Không thay đổi luồng worker/provider/quota/microbatch AI trong ca này. Các file AI dirty trước lượt này không thay đổi hash so với baseline.
- Quan sát thêm (chưa tự mở rộng sửa): “nguy cơ” tại tổng quan quản lý đang lấy Digital Twin, còn báo cáo học sinh giáo viên mới có tiêu chí điểm đã chốt hoặc bài quá hạn. Một lớp có thể có 0 nguy cơ Twin nhưng 4 học sinh quá hạn. Nên đặt tên riêng “rủi ro năng lực” và “cần nhắc nộp bài/hỗ trợ”, hoặc thống nhất định nghĩa sau khi người dùng chọn; không tự coi hai chỉ số là cùng nhau.
- Lớp legacy vẫn có nhiều khối và khối lớp NULL vì giữ nguyên dữ liệu cũ. Không thể suy ra kết thúc lớp; quản lý cần quyết định lưu trữ hoặc xử lý kế hoạch học. UI vẫn có nút thêm thành viên, nhưng server yêu cầu khối hợp lệ: nên bổ sung cảnh báo/disabled cho legacy chưa phân khối trong ca UX tiếp theo, không tự ép grade hoặc rút học sinh ở ca này.

## Tệp thay đổi riêng ca này

So hash ở đầu ca với cuối ca: 39 tệp bên dưới, cộng báo cáo này và một dòng dẫn cập nhật trong báo cáo audit cũ = 41 tệp. Hai tệp EF Designer/ModelSnapshot chứa khoảng 7.000 dòng mỗi tệp là model sinh tự động; không phải 14.000 dòng nghiệp vụ mới. Snapshot hiện hữu chủ yếu thêm một constraint. Các thay đổi cũ trong working tree được giữ riêng, không tính là vừa sửa toàn bộ ở lượt này.

- `docs/plans/CLASS-LIFECYCLE-AND-REPORTS-WORKING-2026-10-09.md`
- `scripts/ops/academic_scope_integrity.cjs`
- `scripts/ops/run_lifecycle_checks.cjs`
- `scripts/ops/verify_lifecycle_login_delta.cjs`
- `src/EduTwin.API/Controllers/ClassesController.cs`
- `src/EduTwin.BLL/AssessmentAndReasoning/AttemptSubmissionValidator.cs`
- `src/EduTwin.BLL/Assignments/AssignmentResultCalculator.cs`
- `src/EduTwin.BLL/Assignments/CreateAssignmentUseCase.cs`
- `src/EduTwin.BLL/Assignments/ListStudentAssignmentsUseCase.cs`
- `src/EduTwin.BLL/Assignments/PublishAssignmentUseCase.cs`
- `src/EduTwin.BLL/Assignments/SaveAssignmentDraftUseCase.cs`
- `src/EduTwin.BLL/Assignments/StartStudentAssignmentUseCase.cs`
- `src/EduTwin.BLL/Assignments/StudentAssignmentScope.cs`
- `src/EduTwin.BLL/Assignments/SubmitStudentAssignmentUseCase.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/CurriculumApplicationUseCase.cs`
- `src/EduTwin.BLL/Dashboards/GetStudentWorkspaceSummaryUseCase.cs`
- `src/EduTwin.BLL/Dashboards/StudentAcademicScopeReader.cs`
- `src/EduTwin.BLL/Organization/ClassReportsUseCase.cs`
- `src/EduTwin.BLL/Organization/DependencyInjection.cs`
- `src/EduTwin.BLL/Organization/UpdateClassUseCase.cs`
- `src/EduTwin.Contracts/CurriculumAndQuestions/ApplyCurriculumRequest.cs`
- `src/EduTwin.Contracts/Organization/ClassAcademicReportDto.cs`
- `src/EduTwin.Contracts/Organization/UpdateClassRequest.cs`
- `src/EduTwin.DAL/Persistence/Configurations/ClassConfiguration.cs`
- `src/EduTwin.DAL/Persistence/Migrations/20261008184057_AlignClassLifecycleScope.Designer.cs`
- `src/EduTwin.DAL/Persistence/Migrations/20261008184057_AlignClassLifecycleScope.cs`
- `src/EduTwin.DAL/Persistence/Migrations/EduTwinDbContextModelSnapshot.cs`
- `tests/EduTwin.BLL.Tests/Dashboards/DashboardMySqlIntegrationTests.cs`
- `tests/EduTwin.BLL.Tests/Organization/ClassReportsTests.cs`
- `tests/EduTwin.BLL.Tests/Organization/UpdateClassUseCaseTests.cs`
- `web/edutwin-web/src/api/organizationApi.ts`
- `web/edutwin-web/src/components/ClassHistoryPanel.tsx`
- `web/edutwin-web/src/components/teacher/CurriculumApplicationPanel.tsx`
- `web/edutwin-web/src/pages/ClassListPage.tsx`
- `web/edutwin-web/src/pages/teacher/TeacherStudentManagementView.tsx`
- `web/edutwin-web/src/pages/teacher/teacherExcelReports.ts`
- `web/edutwin-web/src/pages/teacher/teacherReportsHelpers.ts`
- `web/edutwin-web/src/types/organization.ts`
- `web/edutwin-web/tests/classLifecycleReports.test.ts`
