# BÁO CÁO NGHIỆM THU UI/E2E POST-R09 — TOÀN VẸN KHỐI HỌC THUẬT & VÒNG ĐỜI LỚP HỌC (CLASS-GRADE-CURRICULUM-QUESTION-ASSIGNMENT INTEGRITY)

> **Dự án:** AI-Driven Adaptive Learning and Competency Assessment Platform (EduTwin)  
> **Thời điểm nghiệm thu:** 2026-10-04 / 2026-10-05  
> **Branch thực thi:** `student/answer`  
> **Môi trường xác thực:** Live Docker Stack (MySQL :3307, ASP.NET Core API :5000, Vite React Web :3000)  
> **Database Migration:** `20261004074050_AddClassLifecycleAndAcademicGradeLevelConstraints`  
> **Trạng thái chính thức:** **100% PASS — 18/18 KỊCH BẢN CHROME UI HOÀN THÀNH XÁC THỰC THỰC TẾ & BẢO VỆ TOÀN VẸN DỮ LIỆU**  
> **Cam kết giới hạn:** Tuyệt đối KHÔNG có mã Bulk Import; KHÔNG push commit lên remote trước khi kiểm tra; KHÔNG rò rỉ thông tin đăng nhập/mật khẩu trong tài liệu và mã nguồn.

---

## 1. TỔNG QUAN VÀ PHẠM VI NGHIỆM THU

Milestone nghiệm thu giao diện người dùng và bảo mật tầng dữ liệu cho tính năng **Class Lifecycle & Academic Grade Level Constraints** đã được hoàn thành trọn vẹn thông qua bộ kiểm thử tự động Headless Chrome CDP tương tác trực tiếp với hệ thống đang chạy thực tế trên cổng 3000 (Web Frontend), 5000 (Backend API) và 3307 (MySQL Database).

### Các khía cạnh cốt lõi được nghiệm thu:
1. **Quản lý Vòng đời & Khối học thuật Lớp học (Center Manager):**
   - Lớp học bắt buộc chỉ định `GradeLevel` (Khối 10, 11, 12).
   - Giao diện danh sách hiển thị rõ ràng Badge khối và trạng thái hoạt động.
   - Thêm học sinh cùng khối: Thành công tức thì mà không yêu cầu lý do ngoại lệ.
   - Thêm học sinh khác khối: Hệ thống cảnh báo amber, bắt buộc nhập lý do ngoại lệ (`gradeMismatchReason`), xác thực độ dài tối đa 500 ký tự.
   - Trạng thái chọn và bộ nhớ cache ngoại lệ được bảo toàn xuyên suốt các trang phân trang (pagination persistence).
   - Xóa lớp học: Lớp trống không có học sinh cho phép xóa an toàn kèm modal xác nhận. Lớp có học sinh đang theo học bị chặn xóa kèm thông điệp nghiệp vụ rõ ràng, không làm mất tính toàn vẹn dữ liệu.
2. **Toàn vẹn Giáo trình & Ngân hàng Câu hỏi (Teacher - Teacher English):**
   - Giáo trình bắt buộc chọn `GradeLevel` khi khởi tạo.
   - Gán lớp học vào giáo trình: Chỉ cho phép gán các lớp có cùng khối học với giáo trình. Gán lớp khác khối bị từ chối bằng xung đột trạng thái HTTP 409 (`InvalidStateTransition`).
   - Ngân hàng câu hỏi: Hiển thị nhãn khối học thuật (`KHỐI 10`, `KHỐI 11`), hỗ trợ kích hoạt và lọc theo khối.
3. **Phân phối & Ràng buộc Bài tập (Teacher & Student):**
   - Bài tập tạo từ câu hỏi khác khối bắt buộc bật xác nhận ngoại lệ và nhập lý do hợp lệ; kiểm tra chặn lý do vượt quá 500 ký tự với bộ đếm ký tự trực quan.
   - Chế độ giao bài theo danh sách chỉ định (`SelectedStudents`) chặn hoàn toàn việc để trống danh sách học sinh (không rơi ngầm về `WholeClass`).
   - Giao bài cho lớp đã lưu trữ/ngừng hoạt động bị từ chối fail-closed kèm banner thông báo từ chối và định danh rõ ràng lớp trong dropdown.
   - Xác thực học sinh đích (`student05` - Bảo Lễ Hồ) nhìn thấy bài tập được giao, trong khi học sinh ngoài danh sách (`student01` - Duy Bảo Trịnh) hoàn toàn không thấy bài tập, đảm bảo cách ly dữ liệu học tập 100%.
4. **Bảo mật Tầng Dữ liệu & Chuẩn hóa Lỗi (Security & RFC 7807):**
   - Kiểm thử 5 véc-tơ tấn công trực tiếp qua HTTP API: Toàn bộ lỗi trả về tuân thủ chuẩn RFC 7807 ProblemDetails, tuyệt đối 0 rò rỉ stack trace và 0 rò rỉ từ khóa/mã lỗi SQL/MySQL.

---

## 2. MA TRẬN 18 KỊCH BẢN CHROME UI & BẰNG CHỨNG XÁC THỰC

Toàn bộ 18 ảnh chụp màn hình bằng chứng thực tế được lưu trữ tuần tự tại thư mục:  
`docs/verification/evidence/class-grade-integrity-2026-10-04/`

| STT | Mã Kịch Bản | Vai Trò (Persona) | Tuyến Đường / Modal | Hành Động & Kiểm Thử | Kết Quả Mong Đợi | Kết Quả Thực Tế | Trạng Thái | File Bằng Chứng |
|:---:|---|---|---|---|---|---|:---:|---|
| **01** | `CM-CLS-01` | Center Manager | `/quan-ly/lop-hoc` | Kiểm tra hiển thị danh sách lớp học mới tạo khối 10 và 11 | Hiển thị Badge Khối 10, Khối 11 trực quan | Huy hiệu Khối 10 (emerald) và Khối 11 (amber) hiển thị nổi bật | **PASS** | [`01_class_grade_level_displayed.png`](./evidence/class-grade-integrity-2026-10-04/01_class_grade_level_displayed.png) |
| **02** | `CM-CLS-02` | Center Manager | Modal Thêm Học Sinh | Chọn học sinh Khối 10 (`student01`) vào lớp Khối 10 | Cho phép thêm trực tiếp, không yêu cầu lý do ngoại lệ | Thêm thành công, không hiện cảnh báo lệch khối | **PASS** | [`02_add_student_same_grade_success.png`](./evidence/class-grade-integrity-2026-10-04/02_add_student_same_grade_success.png) |
| **03** | `CM-CLS-03` | Center Manager | Modal Thêm Học Sinh | Chọn học sinh Khối 11 (`student05` - Bảo Lễ Hồ) vào lớp Khối 10 | Xuất hiện hộp cảnh báo vàng thông báo lệch khối học thuật | Hộp cảnh báo màu hổ phách hiển thị với thông tin khối lớp vs khối học sinh | **PASS** | [`03_add_student_cross_grade_warning.png`](./evidence/class-grade-integrity-2026-10-04/03_add_student_cross_grade_warning.png) |
| **04** | `CM-CLS-04` | Center Manager | Modal Thêm Học Sinh | Nhấn "Xác nhận thêm" khi chưa nhập lý do ngoại lệ | Chặn submit và hiển thị thông báo lỗi validate màu đỏ | Viền đỏ xuất hiện quanh ô lý do, thông báo yêu cầu lý do ngoại lệ | **PASS** | [`04_add_student_cross_grade_validation.png`](./evidence/class-grade-integrity-2026-10-04/04_add_student_cross_grade_validation.png) |
| **05** | `CM-CLS-05` | Center Manager | Modal Thêm Học Sinh | Nhập lý do hợp lệ và nhấn xác nhận thêm học sinh | Thêm học sinh khác khối thành công, ghi nhận audit trail | Thành công thêm vào lớp; kiểm tra MySQL có đầy đủ `grade_level_at_enrollment=11`, `grade_mismatch_reason` | **PASS** | [`05_add_student_cross_grade_success.png`](./evidence/class-grade-integrity-2026-10-04/05_add_student_cross_grade_success.png) |
| **06** | `CM-CLS-06` | Center Manager | Modal Thêm Học Sinh | Chuyển trang phân trang qua lại khi đã chọn và nhập lý do | Bộ nhớ cache lưu giữ lựa chọn và lý do ngoại lệ giữa các trang | Lựa chọn và nội dung lý do ngoại lệ được bảo toàn 100% khi back/forward | **PASS** | [`06_pagination_cross_grade_cache.png`](./evidence/class-grade-integrity-2026-10-04/06_pagination_cross_grade_cache.png) |
| **07** | `CM-CLS-07` | Center Manager | `/quan-ly/lop-hoc` | Xóa lớp học rỗng (0 học sinh) | Hiển thị modal xác nhận xóa và xóa thành công | Lớp học được xóa khỏi danh sách mà không gặp xung đột | **PASS** | [`07_delete_empty_class_success.png`](./evidence/class-grade-integrity-2026-10-04/07_delete_empty_class_success.png) |
| **08** | `CM-CLS-08` | Center Manager | `/quan-ly/lop-hoc` | Thử xóa lớp học đang có học sinh theo học | Bị chặn bởi thông báo lỗi nghiệp vụ rõ ràng, không xóa | Thông báo lỗi "Không thể xóa lớp học đang có học sinh hoạt động" xuất hiện | **PASS** | [`08_delete_class_with_students_blocked.png`](./evidence/class-grade-integrity-2026-10-04/08_delete_class_with_students_blocked.png) |
| **09** | `TC-CUR-01` | Teacher (English) | Modal Tạo Giáo Trình | Tạo giáo trình `UIACC-ENG-G10` với Khối 10 | Bắt buộc chọn khối, tạo thành công giáo trình Khối 10 | Giáo trình khởi tạo thành công với nhãn Khối 10 | **PASS** | [`09_curriculum_create_grade10.png`](./evidence/class-grade-integrity-2026-10-04/09_curriculum_create_grade10.png) |
| **10** | `TC-CUR-02` | Teacher (English) | Chi tiết Giáo Trình | Gán lớp học cùng Khối 10 (`UIACC-GRADE10-CLASS`) | Gán thành công, hiển thị banner thông báo lưu thành công | Lớp học được liên kết với giáo trình, lưu thành công không có lỗi | **PASS** | [`10_curriculum_same_grade_assigned.png`](./evidence/class-grade-integrity-2026-10-04/10_curriculum_same_grade_assigned.png) |
| **11** | `TC-CUR-03` | Teacher (English) | Chi tiết Giáo Trình | Thử gán lớp học Khối 11 (`UIACC-GRADE11-CLASS`) vào giáo trình Khối 10 | Hệ thống từ chối với lỗi xung đột trạng thái (HTTP 409) | Banner cảnh báo xung đột khối học thuật xuất hiện, ngăn chặn lưu sai khối | **PASS** | [`11_curriculum_cross_grade_blocked.png`](./evidence/class-grade-integrity-2026-10-04/11_curriculum_cross_grade_blocked.png) |
| **12** | `TC-QBK-01` | Teacher (English) | `/giao-vien/cau-hoi` | Tạo câu hỏi Khối 10 (`#20030`) và kiểm tra trong ngân hàng | Hiển thị nhãn khối tương ứng và ở trạng thái kích hoạt | Câu hỏi `#20030` hiển thị nhãn `KHỐI 10`, trạng thái Hoạt động | **PASS** | [`12_question_grade_level_compat.png`](./evidence/class-grade-integrity-2026-10-04/12_question_grade_level_compat.png) |
| **13** | `TC-ASN-01` | Teacher (English) | Tạo Bài Tập | Tạo bài tập lớp Khối 10 có câu hỏi Khối 11; thử nhập lý do > 500 ký tự | Hiển thị bộ đếm ký tự và lỗi chặn submit khi lý do vượt quá 500 ký tự | Bộ đếm `540/500 ký tự` màu đỏ, cảnh báo vượt 500 ký tự, chặn chuyển bước | **PASS** | [`13_assignment_cross_grade_reason_required.png`](./evidence/class-grade-integrity-2026-10-04/13_assignment_cross_grade_reason_required.png) |
| **14** | `TC-ASN-02` | Teacher (English) | Tạo Bài Tập | Chọn TargetMode `SelectedStudents` nhưng không chọn học sinh nào | Bị chặn xuất bản với bảng thông báo lỗi, không fallback ngầm về toàn lớp | Bảng thông báo lỗi hiển thị rõ: "Vui lòng chọn ít nhất 1 học sinh" | **PASS** | [`14_assignment_empty_selected_students_blocked.png`](./evidence/class-grade-integrity-2026-10-04/14_assignment_empty_selected_students_blocked.png) |
| **15** | `TC-ASN-03` | Teacher (English) | Tạo Bài Tập | Chọn gán bài tập cho lớp học đã lưu trữ/ngừng hoạt động | Hệ thống hiển thị lớp trong select và từ chối giao bài bằng banner đỏ | Lớp hiển thị nhãn `[Ngừng hoạt động]`, banner từ chối giao cho lớp không hoạt động | **PASS** | [`15_assignment_inactive_class_blocked.png`](./evidence/class-grade-integrity-2026-10-04/15_assignment_inactive_class_blocked.png) |
| **16** | `ST-DST-01` | Student Target (`student05`) | `/hoc-tap/bai-tap` | Đăng nhập tài khoản học sinh đích (Bảo Lễ Hồ) | Nhìn thấy bài tập `UIACC-ENG-HW-G10` với nút "Bắt đầu ->" | Bài tập xuất hiện ngay trên đầu danh sách, sẵn sàng làm bài | **PASS** | [`16_student_target_assignment_visible.png`](./evidence/class-grade-integrity-2026-10-04/16_student_target_assignment_visible.png) |
| **17** | `ST-DST-02` | Student Non-Target (`student01`) | `/hoc-tap/bai-tap` | Đăng nhập tài khoản học sinh không được giao (Duy Bảo Trịnh) | Không nhìn thấy bài tập `UIACC-ENG-HW-G10` | Danh sách bài tập hoàn toàn không chứa bài tập chỉ định cho học sinh khác | **PASS** | [`17_student_non_target_assignment_hidden.png`](./evidence/class-grade-integrity-2026-10-04/17_student_non_target_assignment_hidden.png) |
| **18** | `SEC-AUD-01` | Security Auditor | Direct API Test Suite | Chạy 5 véc-tơ tấn công vi phạm toàn vẹn dữ liệu qua HTTP API | Phản hồi đúng mã lỗi HTTP, chuẩn ProblemDetails, 0 rò rỉ stack trace/SQL | 5/5 véc-tơ đạt chuẩn an toàn tuyệt đối (0 Leak detected) | **PASS** | [`18_backend_security_validation_safe.png`](./evidence/class-grade-integrity-2026-10-04/18_backend_security_validation_safe.png) |

---

## 3. CẢI TIẾN HIỆU NĂNG & SỬA LỖI MÃ NGUỒN PHÁT HIỆN QUA CHROME UI (FORWARD CORRECTIVE FIX)

### 3.1. Sự cố phát hiện & Tối ưu hóa khả năng mở rộng (Scalability)
1. **Lỗi dịch biểu thức LINQ trên MySQL:** Khi thực hiện kịch bản `02_add_student_same_grade_success` trên môi trường thực tế kết nối trực tiếp với MySQL qua Pomelo MySQL Provider (`src/EduTwin.BLL/Organization/AddStudentsToClassUseCase.cs`), cấu trúc `Contains` trên danh sách GUID không thể dịch trực tiếp thành SQL IN expression.
2. **Khả năng mở rộng (Scalability Guard):** Ban đầu, việc kiểm tra học sinh đã có trong lớp tải toàn bộ tập `targetClass.ClassStudents` vào bộ nhớ RAM (`AsNoTracking()`), gây áp lực I/O và memory khi sĩ số lớp đạt hàng trăm/hàng nghìn học sinh. Đồng thời, cần áp đặt giới hạn kích thước mẻ nhập (`MaxBatchSize = 100`) để bảo vệ cơ sở dữ liệu chống DoS/OOM.

### 3.2. Giải pháp Forward Corrective
Tuân thủ nguyên tắc **Forward Corrective Fix Only** (không can thiệp bừa bãi, không rollback):
1. **Giới hạn mẻ nhập an toàn:** Khai báo hằng số `public const int MaxBatchSize = 100;` trong `AddStudentsToClassUseCase.cs`. Khi số lượng học sinh yêu cầu vượt quá 100, hệ thống từ chối ngay lập tức với lỗi `ValidationFailed` và thông điệp chuẩn mực.
2. **Cây biểu thức logic tham số hóa:** Sử dụng phương thức trợ giúp `BuildIdEqualityFilter<T>` để xây dựng cây biểu thức logic `Expression.OrElse` với hằng số tham số hóa chuẩn xác, hỗ trợ cả `Student` và `ClassStudent`.
3. **Truy vấn SQL vị từ trực tiếp:** Kiểm tra học sinh đã thuộc lớp bằng cách truy vấn trực tiếp bảng `ClassStudents` trong SQL mà không nạp toàn bộ lớp vào bộ nhớ:

```csharp
// src/EduTwin.BLL/Organization/AddStudentsToClassUseCase.cs
public const int MaxBatchSize = 100;

// ...

if (request.StudentIds.Count > MaxBatchSize)
{
    return AddStudentsToClassResult.Failure(
        ErrorCodes.ValidationFailed,
        $"Số lượng học sinh thêm vào lớp không được vượt quá {MaxBatchSize} học sinh mỗi lượt.");
}

// ...

var requestedStudentIds = request.StudentIds.ToList();
var studentFilter = BuildIdEqualityFilter<Student>(nameof(Student.StudentId), requestedStudentIds);

var validStudents = await _context.Students
    .Include(s => s.User)
    .Where(studentFilter)
    .Where(s => s.CenterId == centerId && !s.IsDeleted && !s.User.IsDeleted && s.User.RoleName == UserRole.Student)
    .ToListAsync(cancellationToken);

// Kiểm tra trùng lặp trực tiếp trên bảng ClassStudents mà không tải toàn bộ lớp
var membershipFilter = BuildIdEqualityFilter<ClassStudent>(nameof(ClassStudent.StudentId), requestedStudentIds);
var existingMemberships = await _context.ClassStudents
    .Where(cs => cs.CenterId == centerId && cs.ClassId == classId)
    .Where(membershipFilter)
    .ToListAsync(cancellationToken);
```

### 3.3. Hiệu quả
- Sửa dứt điểm lỗi dịch thuật SQL của EF Core trên MySQL.
- Ngăn chặn triệt để OOM và nghẽn bộ nhớ khi lớp học có quy mô lớn.
- Toàn bộ 3.619 bài kiểm thử unit/integration backend giữ nguyên trạng thái xanh (0 bài kiểm thử bị ảnh hưởng).

---

## 4. XÁC MINH KIỂM TOÁN TẦNG CƠ SỞ DỮ LIỆU (DATABASE AUDIT TRAIL)

Sau khi thực hiện các kịch bản thêm học sinh khác khối, dữ liệu thực tế tại MySQL container (port 3307) đã được kiểm tra trực tiếp qua SQL query:

```sql
SELECT 
    class_student_id,
    class_id,
    student_id,
    grade_level_at_enrollment,
    grade_mismatch_reason,
    exception_approved_by,
    exception_approved_at,
    status
FROM class_students
WHERE class_id = '11756baf-f415-4d56-a3b8-a6989c246a51';
```

**Kết quả ghi nhận:**
1. Bản ghi học sinh cùng khối (`student01` - Duy Bảo Trịnh, Khối 10):
   - `grade_level_at_enrollment`: `10`
   - `grade_mismatch_reason`: `NULL`
   - `exception_approved_by`: `NULL`
   - `exception_approved_at`: `NULL`
   - `status`: `Active`
2. Bản ghi học sinh khác khối (`student05` - Bảo Lễ Hồ, Khối 11):
   - `grade_level_at_enrollment`: `11`
   - `grade_mismatch_reason`: `'Học sinh có năng khiếu học vượt cấp'`
   - `exception_approved_by`: `'d0000000-0000-0000-0001-000000000002'` (Center Manager ID)
   - `exception_approved_at`: `2026-10-04 18:58:40` (Thời điểm phê duyệt thực tế)
   - `status`: `Active`

Dữ liệu kiểm toán lưu trữ đầy đủ, chính xác, phục vụ công tác thanh tra học thuật định kỳ.

---

## 5. KẾT QUẢ KIỂM THỬ HỒI QUY TOÀN DIỆN (REGRESSION SUITE QUALITY GATES)

Trước khi đóng băng báo cáo nghiệm thu, toàn bộ các bộ kiểm thử tự động của hệ thống đã được thực thi lại từ đầu:

| STT | Bộ Kiểm Thử | Lệnh Thực Thi | Kết Quả Thực Tế | Thời Gian | Đánh Giá |
|:---:|---|---|:---:|:---:|:---:|
| **1** | **Backend .NET Regression** | `dotnet test tests/EduTwin.BLL.Tests/` | **Passed: 3,619**, Failed: 0, Skipped: 60 | 39 giây | **PASS (100%)** |
| **2** | **AddStudentsToClass Suite** | `dotnet test tests/EduTwin.BLL.Tests/ --filter "AddStudentsToClass"` | **Passed: 44**, Failed: 0, Skipped: 1 | 5 giây | **PASS (100%)** |
| **3** | **Live MySQL Integration** | `dotnet test` (với `EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING`) | **Passed: 1**, Failed: 0 | 24 giây | **PASS (100%)** |
| **4** | **Frontend Unit/Component** | `npm test` (edutwin-web) | **Passed: 422**, Failed: 0, Skipped: 0 | 4.7 giây | **PASS (100%)** |
| **5** | **Frontend Static Lint** | `npm run lint` (edutwin-web) | **0 Errors**, 33 Warnings (ESLint) | 14 giây | **PASS (100%)** |
| **6** | **Production Bundle Build** | `npm run build` (edutwin-web) | **0 Errors** (`dist/` generated cleanly) | 11.4 giây | **PASS (100%)** |
| **7** | **Acceptance Test Harness (CDP)** | `node tests/acceptance/class_grade_integrity/run_all.cjs` | **18/18 Passed (Run 1: 166.0s, Run 2: 165.8s)** | ~166 giây/lượt | **PASS (100% Idempotent)** |

> [!NOTE]
> - Bài kiểm thử `AddStudentsToClass_LiveMySql_TranslatesGuidFiltersAndEnforcesBatchLimits` kế thừa thuộc tính `[MySqlIntegrationFact]` nên được chủ động bỏ qua (`SKIP`) trong các lượt chạy test mặc định nếu không cấu hình biến môi trường kết nối MySQL Admin. Khi được cung cấp chuỗi kết nối MySQL cục bộ (`localhost:3307`), bài kiểm thử đã khởi tạo cơ sở dữ liệu tạm thời trên MySQL thật, di chuyển schema và chạy xác thực thành công 100% (`Passed: 1, Failed: 0`).
> - Bộ nghiệm thu Chrome CDP (`run_all.cjs`) đã được chạy liên tiếp 2 lần trên cùng một cơ sở dữ liệu đang chạy để kiểm chứng tính chất **Idempotent** (khả năng tự dọn dẹp fixture với tiền tố `UIACC` an toàn, không rò rỉ dữ liệu và không làm sập kịch bản khi chạy lại). Cả 2 lượt chạy liên tiếp đều đạt **18/18 kịch bản PASS**.

---

## 6. BẢO MẬT & CAM KẾT TUÂN THỦ (COMPLIANCE VERIFICATION)

1. **Tuyệt đối KHÔNG có Bulk Import:**
   - Trong toàn bộ quá trình nghiệm thu, không có bất kỳ dòng mã hay API nào liên quan đến Import hàng loạt được kích hoạt hoặc triển khai.
2. **Không rò rỉ thông tin nhạy cảm:**
   - Mật khẩu hạt giống được đọc trực tiếp từ biến môi trường runtime (`.env`), không bị hardcode trong tài liệu hay commit.
   - Bảng nhật ký kiểm thử bảo mật tầng dữ liệu (`18_backend_security_validation_safe.png`) chứng minh chuẩn RFC 7807 ProblemDetails không làm lộ stack trace hay cú pháp câu lệnh SQL.
3. **Tính chân thực của bằng chứng:**
   - Toàn bộ 18 ảnh chụp màn hình được chụp từ Chrome Headless tương tác thực tế với DOM của ứng dụng EduTwin trên cổng 3000, không sử dụng ảnh giả lập (mocking) hay ảnh ghép.
4. **Trạng thái Git:**
   - Mã nguồn được bảo toàn trên branch `student/answer`.
   - Chưa thực hiện push lên remote server, sẵn sàng cho công tác kiểm tra của quản trị viên.

---
*Báo cáo được khởi tạo và xác thực tự động bởi hệ thống nghiệm thu EduTwin.*
