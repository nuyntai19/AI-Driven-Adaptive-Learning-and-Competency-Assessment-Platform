# Rà soát ca lớp/giáo trình và responsive giáo viên — 09/10/2026

Cập nhật sau khi người dùng đồng ý: các phát hiện dưới đây đã được xử lý trong ca tiếp theo. Xem `CLASS-LIFECYCLE-AND-AUTHORITATIVE-REPORTS-2026-10-09.md` để biết sửa đổi, dữ liệu migration và kiểm chứng mới. Báo cáo này giữ nguyên làm bằng chứng của thời điểm rà soát, không phải trạng thái hiện tại.

Kết luận: đã sửa lỗi bố cục, nhưng **chưa nên push ca nghiệp vụ lớp**. Kiểm thử kỹ thuật trước đó đạt không chứng minh quy tắc nghiệp vụ đã đầy đủ. Lượt này không tự đổi trạng thái/phạm vi lớp, không sửa điểm, không rollback database.

## 1. Lỗi responsive đã sửa

Nguyên nhân trong `TeacherPageHeader`: chuyển sang hàng ngang từ 640px, cụm actions có `shrink-0`, trong khi tiêu đề cho phép co tới gần 0. Ở viewport 900px tiêu đề chỉ còn rộng 34,7px, cao 324px, tạo cột chữ dọc trong ảnh người dùng.

Sửa nhỏ, không đụng dữ liệu:

| Tệp | Sửa trong lượt này |
| --- | --- |
| `web/edutwin-web/src/components/teacher/TeacherPrimitives.tsx` | +3/-3: header wrap theo không gian thực tế; khối tiêu đề có flex basis 28rem; actions có min-width 0/max-width 100% và được co/wrap |
| `web/edutwin-web/src/pages/teacher/TeacherStudentManagementView.tsx` | +2/-2: chọn lớp vừa màn hình; metric cards 1 cột trên điện thoại, 2/4 cột ở màn hình lớn |
| `web/edutwin-web/tests/teacherHeaderResponsive.test.ts` | Mới: 2 ca regression cho header và màn hình học sinh |

Build Docker web/TypeScript/Vite đạt; toàn bộ frontend 592 đạt, 0 lỗi. Đã cập nhật **chỉ web**, không khởi động lại API/migration. Backend không đổi trong lượt này; kết quả 3.953 đạt/76 skipped là của lượt trước, không dùng nó để che các thiếu sót nghiệp vụ dưới đây.

Chrome Teacher Math, `/giao-vien/hoc-sinh`:

| Viewport | Chiều rộng tiêu đề sau sửa | Chiều cao | Tràn ngang trang |
| ---: | ---: | ---: | --- |
| 375 | 328px | 64px | Không; document/client width cùng 360px (trừ thanh cuộn) |
| 768 | 705px | 36px | Không; document/client width cùng 753px |
| 900 | 837px | 36px | Bố cục/toolbar hiển thị bình thường |
| 1280 | 944px | 36px | Không; document/client width cùng 1280px |

Smoke test header dùng chung tại ngân hàng câu hỏi 375px: tiêu đề rộng 328px/cao 64px, document/client width cùng 360px. Đã trả viewport về kích thước ban đầu và để lại trang quản lý học sinh.

Bằng chứng tại `docs/verification/evidence/class-scope-review/teacher-students-before-900.jpg` và `teacher-students-after-{375,768,900,1280}.jpg`.

## 2. Lớp bị chuyển History do đâu?

Dòng 139 migration `20261008162602_AddAcademicClassScopeAndCurriculumApplications`:

```sql
UPDATE classes SET learning_scope='History' WHERE grade_level IS NULL;
```

Đây là **migration tự phân loại**, không phải thao tác một quản lý đăng nhập trên giao diện. Nó không có điều kiện giới hạn trung tâm/ID lớp, không ghi lý do/người/thời điểm chuyển phạm vi, và không đổi `status`, `updated_by`, `updated_at`, `row_version` của lớp.

Mục đích ban đầu là giữ hai lớp mẫu cũ của EDUTWIN_A ngoài tổng quan hiện tại, bảo toàn các bài test. Nhưng suy luận `chưa có khối = lớp lịch sử` không đúng như một quy tắc tổng quát. Không được dùng tình trạng thiếu dữ liệu để tự quyết định vòng đời lớp.

Truy vấn MySQL chỉ đọc xác nhận:

| Trung tâm | Lớp | Status | LearningScope | Khối |
| --- | --- | --- | --- | --- |
| EDUTWIN_A | Lớp Toán — Dữ liệu kiểm thử cũ | Active | History | NULL |
| EDUTWIN_A | Lớp Tiếng Anh — Dữ liệu kiểm thử cũ | Active | History | NULL |
| EDUTWIN_B | Lớp Toán | Active | History | NULL |
| EDUTWIN_B | Lớp Tiếng Anh | Active | History | NULL |

Hai lớp B còn `updated_by=NULL`, `updated_at=2026-01-01`, `row_version=1`: không có dấu vết người dùng chuyển History. Sáu lớp mới tại A vẫn Active/Current, phân khối 10–12.

Ca tách **dữ liệu mẫu** trước đó được người dùng cho phép. Script `split_seed_grade_classes_local.cjs` giới hạn hai lớp EDUTWIN_A, tạo sáu lớp, giữ thành viên/bài cũ và ghi `DemoGradeClassesSplit`. Tuy nhiên script dùng ID quản lý mẫu hard-code: đó là thao tác vận hành được ủy quyền, **không phải bằng chứng người quản lý đã đăng nhập và thực hiện chuyển History**. Cần phân biệt dấu vết System/Seed/Migration với hành động người dùng thực tế, không mạo danh tác nhân.

`Status` và `LearningScope` hiện là hai cột khác nhau. Vì giao diện quản lý chỉ hiển thị Status, nó vẫn ghi “Hoạt động”. Đây không phải lỗi cập nhật cache đơn thuần: thiếu quy tắc phối hợp hai trạng thái và thiếu luồng tác nhân.

## 3. Các phát hiện phải xử lý hoặc duyệt hướng sửa

### F1 — P1 — Migration tự đưa lớp chưa phân khối của mọi trung tâm sang History (ca mới)

Nguồn: migration dòng 139. Đã ảnh hưởng bốn lớp, gồm hai lớp B không thuộc ca sửa dữ liệu mẫu A. Lớp bị loại khỏi tổng quan/lộ trình Current và khỏi điều kiện ghi dữ liệu học thuật dù vẫn Active.

Đề xuất: không sửa ngược migration đã áp dụng và không chạy UPDATE diện rộng lần nữa. Tạo phương án sửa bổ sung, chỉ rõ từng lớp và nguồn thực hiện; lớp chưa khai báo khối phải được đánh dấu cần hoàn thiện dữ liệu, không mặc định kết thúc việc học.

### F2 — P1 — Chưa có thao tác tác nhân và audit vòng đời/phạm vi lớp (ca mới)

Nguồn: `UpdateClassRequest` không có LearningScope/lý do; `UpdateClassUseCase` chỉ gán Status/Grade/Teacher và thông tin updated, không ghi sự kiện chuyển phạm vi. UI `ClassListPage` chỉ có Active/Archived, không quản lý cột scope mới. Vì vậy không thể giải thích “ai đã chuyển lớp này sang History?” bằng một hành động ứng dụng hợp lệ.

Đề xuất luồng tối giản: quản lý trung tâm chọn **Lưu trữ/ngừng sử dụng lớp**, nhập lý do; server kiểm tra quyền, row version, ghi before/after + tác nhân + thời gian. Status Archived phải thống nhất với phạm vi lịch sử. Nếu giữ LearningScope thì mọi thay đổi phải đi cùng luồng này, không là một trạng thái bí mật độc lập. Học sinh/giáo viên không tự thay vòng đời lớp; giáo viên vẫn quản lý áp dụng giáo trình trong lớp mình phụ trách.

Khi mở lại lớp cần một hành động có lý do/kiểm tra riêng. Không xóa/chuyển bài cũ, không reset Twin. Lưu trữ lớp phải bảo toàn quyền đọc/chấm bài đã nộp; quyền xem lịch sử khác quyền giao bài mới. Việc kết thúc ứng dụng giáo trình cần thống nhất actor được phép với trigger hiện tại (đang giới hạn giáo viên), không dùng ID giáo viên thay cho người quản lý.

### F3 — P1 — History đang vừa bị khóa vừa tiếp tục nhận bài mới (ca mới + luồng cũ chưa được nối)

Nguồn: `AddStudentsToClassUseCase`/`CreateStudentUseCase` yêu cầu Current; `CreateAssignmentUseCase` dòng 133 và `PublishAssignmentUseCase` dòng 138 chỉ yêu cầu Status Active, không xét LearningScope. Lớp Active+History vẫn có thể tạo/phát hành bài mới nhưng không thêm học sinh/gán giáo trình mới.

Đề xuất: định nghĩa vòng đời thống nhất rồi áp dụng vào mọi luồng ghi. Chỉ chặn tạo/phát hành/tham gia mới; không cắt quyền đọc bài đã giao/đã nộp và chấm bài đang chờ xử lý. Chưa thay các guard trong lượt rà soát này.

### F4 — P1 — Bảng điểm giáo viên lấy tỷ lệ hoàn thành làm điểm (lỗi cũ, không do ca scope)

Nguồn: `TeacherStudentManagementView.tsx` dòng 193–202:

```ts
const ratio = totalQuestions > 0 ? completedQuestions / totalQuestions : 1;
calculatedScore = Math.round(ratio * 10 * 10) / 10;
```

Nếu nộp đủ 50 câu thì phép tính trả 10, bất kể học sinh đúng/sai. Giá trị này đi vào điểm trung bình, phân loại và xuất CSV/Excel/báo cáo. Đây là điểm UI suy diễn, không phải điểm chấm server. Lượt responsive không sửa công thức này.

Đề xuất: dùng kết quả chấm tổng hợp server, phân biệt điểm tạm/đã duyệt/chưa chấm; null không được thay bằng điểm hoàn thành. Cần test nộp đủ nhưng làm sai, gần đúng, câu bị hủy, chưa chốt và điểm giáo viên sửa.

### F5 — P2 — Chưa giao bài mà đã “Nguy cơ cao” (lỗi cũ, tái hiện trực tiếp)

Nguồn: dòng 223 đặt completionRate=0 khi totalAssigned=0; dòng 293/331 xét `<50` không kiểm tra đã có bài/bằng chứng. Chrome lớp Toán 12: tổng 0 bài tập, vẫn 1/1 học sinh “Nguy cơ cao” và “Cần hỗ trợ sớm”.

Đề xuất: chưa có dữ liệu => “Chưa đánh giá”, không suy ra nguy cơ thấp/cao hoặc cần đôn đốc. Tránh đánh giá dựa vào dữ liệu chưa tải xong/lỗi mạng.

### F6 — P2 — API huy hiệu/danh sách không hoàn toàn cùng guard với academic context (ca mới)

Nguồn: `GetStudentWorkspaceSummaryUseCase` dòng 48–49 và `ListStudentAssignmentsUseCase` dòng 67–70: khi có classId thì bỏ qua nhánh lọc Current. Academic context/dashboard lại từ chối lớp History trong mode Current. Một URL/query chỉ định lớp cũ có thể cho header lỗi context nhưng danh sách/huy hiệu vẫn trả bài cũ trong mode Current.

Đây không phải bằng chứng lộ bài người khác: dữ liệu assignment vẫn lọc StudentId của người đăng nhập. Tuy vậy cần hợp đồng scope thống nhất, với ngoại lệ đọc assignment-target lịch sử sau khi rời lớp được nêu rõ và test riêng.

### F7 — P2 — Báo cáo lớp mới đọc trang đầu và biến lỗi tải tiến độ thành “chưa nộp” (lỗi cũ)

Nguồn: TeacherStudentManagementView chỉ lấy 100 học sinh/50 bài tập; phần Promise.all catch lưu mảng rỗng. Sau đó tiến độ thiếu mặc định NotStarted. Chưa thấy phân biệt học sinh không nằm trong assignment-target với học sinh được giao nhưng chưa làm.

Đề xuất: tổng hợp/pagination phía server, có trạng thái dữ liệu chưa tải/lỗi; tôn trọng từng đối tượng nhận bài. Không cho xuất báo cáo như dữ liệu đã xác nhận khi thiếu trang hoặc tải lỗi.

## 4. Những gì đã kiểm tra và đang đúng

- API áp dụng giáo trình có permission policy; BLL kiểm tra giáo viên sở hữu lớp và quyền đọc nguồn giáo trình. Client không tự quyết định actor/giờ duyệt.
- DB giữ khóa ngoại cùng trung tâm, unique một ứng dụng chính mỗi lớp, ledger trước được giữ khi thay giáo trình, và trigger chặn sửa/xóa lịch sử đã kết thúc.
- Học sinh không có UI/API gán giáo trình; context đọc thành viên lớp và không lấy toàn đồ thị môn làm fallback khi thiếu ứng dụng.
- Cache query được xóa khi login/logout trong các layout/LoginPage; không có bằng chứng hồi quy dùng cache học sinh trước sau đổi tài khoản ở luồng này.
- Bài làm/điểm/năng lực không bị reset. So sánh 18 bảng với backup: 17 bảng giữ nguyên hoàn toàn; bảng users khác đúng một hàng Teacher Math tại các cột `last_login_at`, `updated_at`, `row_version` sau khi người dùng đăng nhập. Không đổi hash mật khẩu, vai trò hoặc trạng thái tài khoản.
- Vẫn 204 attempts, 204 reasoning_analyses, 4 assignments, 54 assignment_questions, 20 targets/progress, 2 ảnh nháp, 362 twin_update_history. Lượt này không sửa dữ liệu lớp.

Kiểm tra bảo toàn 18 bảng không bao gồm cột mới của classes và ledger mới. Vì vậy **bài làm giữ nguyên không đồng nghĩa migration vòng đời lớp đã đúng nghiệp vụ**.

## 5. Danh mục tệp và bước tiếp theo

Danh mục đầy đủ, nhóm tệp ca scope, tệp dùng chung và schema sinh tự động: `docs/verification/CHANGE-INVENTORY-2026-10-09.md`.

Không thể quy mọi dòng git diff so HEAD cho lượt mới nhất vì nhiều ca chưa commit riêng. Snapshot danh mục có 188 tệp văn bản/code, +20.536/-320; trong đó +14.138 là Designer/model snapshot sinh tự động. 71 tệp liên quan ca scope (nhiều tệp dùng chung với các ca trước), 3 tệp responsive lượt này; số dòng mỗi tệp là delta toàn working tree.

Chưa push. Đề nghị người dùng duyệt hướng sửa vòng đời lớp qua quản lý trung tâm + sửa dữ liệu đã phân loại sai có dấu vết, rồi xử lý các lỗi điểm/nguy cơ/báo cáo. Không tự chạy rollback/reseed hoặc gán hành động vận hành thành hành động đăng nhập của tác nhân.
