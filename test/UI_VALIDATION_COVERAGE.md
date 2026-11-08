# Ma trận validation thao tác thật trên UI

Bộ test nằm trong `cypress/e2e/ui/validation/`. Mỗi case mở trang/modal thật, nhập hoặc chọn dữ liệu, bấm nút khi phù hợp, rồi kiểm tra constraint trình duyệt, trạng thái nút hoặc thông báo từ chối được render. Đây là phần bổ sung cho các spec API và UI sanitization hiện có.

Có **159 case trong 11 spec** mới, đưa toàn bộ Cypress lên **353 case trong 23 spec**. Đây là số case được khai báo; kết quả chạy thực tế nằm trong [VERIFICATION.md](VERIFICATION.md).

## Các phần đã có case

| Phần / component đang dùng | Thao tác và quy tắc được kiểm tra | Spec |
|---|---|---|
| LoginPage | Điền các ô còn lại rồi bỏ trống mã trung tâm, tên đăng nhập hoặc mật khẩu; browser chặn request | `16-login-fields` |
| TeacherListPage | Mở tạo/sửa/reset; tên/username bắt buộc, giới hạn chuỗi, mật khẩu ngắn, lý do ngắn, xác nhận không khớp | `10-organization-forms` |
| StudentListPage | Mở tạo/sửa/reset; họ tên/username bắt buộc, giới hạn chuỗi, mật khẩu và lý do reset | `10-organization-forms` |
| SubjectListPage | Mở tạo/sửa môn; mã/tên bắt buộc; độ dài mã, tên, mô tả | `10b-center-rbac-forms` |
| ClassListPage | Mở tạo/sửa lớp; tên/năm học/môn/giáo viên bắt buộc; độ dài tên và năm học | `10b-center-rbac-forms` |
| CenterProfilePage | Tên trung tâm bắt buộc và tối đa 200 ký tự | `10b-center-rbac-forms` |
| AuthorizationManagementPage | Tạo role, mã bắt đầu sai định dạng; sửa role và ma trận quyền thiếu lý do hợp lệ | `10b-center-rbac-forms`, `13-review-rbac-platform` |
| PlatformCentersPage | Tạo trung tâm và tài khoản quản lý ban đầu thiếu input; sửa metadata thiếu tên/lý do; đổi trạng thái có lý do quá ngắn | `10c-platform-security-forms`, `13-review-rbac-platform` |
| CenterManagersModal | Tạo quản lý thiếu input; không reset mật khẩu quá ngắn | `10c-platform-security-forms`, `13-review-rbac-platform` |
| PlatformSecurityModal | Mở từ menu tài khoản học sinh/nền tảng; trường bắt buộc, mật khẩu quá ngắn, xác nhận không khớp | `10c-platform-security-forms` |
| TeacherQuestionEditorView, KnowledgeTopicPicker, các editor đáp án | Chọn môn/khối/chủ đề, nhập đề; đề trống, điểm 0, đáp án chuẩn trống, phương án MCQ thiếu nội dung | `11-academic-authoring` |
| QuestionImageEditor | Chọn SVG không được hỗ trợ, nhìn thấy lỗi | `11-academic-authoring` |
| QuestionImportModal | Mở import, đổi nguồn Excel/DOCX/ZIP, chọn file sai phần mở rộng; preview bị chặn | `11-academic-authoring` |
| TeacherCurriculumEditorView | Tên/khối chưa hợp lệ không tạo request lưu; giới hạn tên | `11-academic-authoring` |
| TeacherAssignmentEditorView | Wizard thiếu tiêu đề/lớp, thời lượng 0, chưa chọn câu hỏi | `11-academic-authoring` |
| TeacherKnowledgeGraphView | Mở phiên bản Draft thật; node thiếu tên/mã, trọng số/thời lượng ngoài biên, giới hạn mã | `11-academic-authoring` |
| Graph relationship editor, KnowledgeVersionPanel | Mở Draft sao chép từ khung đã duyệt, chọn hai endpoint trùng nhau, trọng số liên kết ngoài 0–1; giới hạn tên phiên bản, không publish khung trống dù đã xác nhận rà soát | `17-content-components` |
| GradingCriteriaEditor | Thêm rubric trong editor câu hỏi; thiếu tên, điểm 0, tổng điểm sai và yêu cầu bằng chứng ảnh trùng dòng | `17-content-components` |
| Clone curriculum, CurriculumArchiveDialog, CurriculumApplicationPanel | Clone/publish fixture bằng API thật; tên clone trắng/quá dài, lý do lưu trữ trắng/quá dài, giới hạn lý do thay/ngừng áp dụng | `17-content-components` |
| NotificationComposer | Teacher/Manager/Platform mở soạn; nội dung/người nhận thiếu thì không preview; tiêu đề giới hạn; gửi HTML để thấy phản hồi từ chối thật | `11-academic-authoring` |
| GoalAndProposalModal, SkillScopeSelector | Chọn học ngay để mở form mục tiêu; phạm vi trống, tên quá dài, điểm ngoài 0–10, ngày đã qua | `12-student-learning` |
| StudentTwinPage | Điểm mục tiêu và thời hạn ngoài biên trong editor thật | `12-student-learning` |
| AdaptivePracticeOverviewPage, AdaptivePracticeSessionPage | Chỉ có 10/15/20 phút; mở phiên thật; nút nộp giữ disabled khi chưa nhập/chọn đáp án | `12-student-learning` |
| TeacherStudentManagementView | Nhập ghi chú có markup như văn bản, reload và mở lại; không thực thi HTML | `12-student-learning` |
| AssignmentGradingWorkspace | Chờ bài nộp/worker thật, mở form chấm; điểm ngoài thang, thiếu nhận xét/lý do | `13-review-rbac-platform` |
| NotificationReceiptActions, NotificationSentActions | Phản hồi trắng, giới hạn phản hồi, HTML bị server từ chối và hiển thị lỗi; lý do thu hồi trắng/quá dài | `14-notification-actions` |
| StudentAssignmentDetailPage, LearningPlayerPage | Mở bài tập đã xuất bản, nhập đáp án; thiếu lập luận bắt buộc thì không thể xác nhận nộp, bổ sung lập luận thì tiếp tục được | `15-player-and-proposal` |
| ProposalCustomizationForm, TimeBudgetSlider | Chọn ngày đích tương lai, mốc 2 giờ/ngày và ngày học trong chế độ học ngay; mở preview thật, nhập số câu mỗi bộ dưới 3 hoặc trên 20 | `15-player-and-proposal` |
| NotificationOperations | Mở form thử lại của dispatch DeadLetter tổng hợp; lý do trắng giữ nút disabled, giới hạn 500 ký tự | `18-runtime-form-states` |
| MissionPlayerModal, luồng placement | Chọn chủ đề seed và bắt đầu kiểm tra; các trường mục tiêu được ẩn, request có `clearDeadline=true`; nộp trống trong nhiệm vụ thật bị native validation chặn | `18-runtime-form-states` |

## Phạm vi cần hiểu đúng

Ma trận kiểm tra các quy tắc nêu ở trên cho từng nhóm chức năng, không chứng minh mọi tổ hợp input hoặc mọi trạng thái runtime của tất cả component đều an toàn. Component dùng chung được kiểm tra thông qua trang đang dùng nó, không tạo route test hoặc bỏ qua quyền để mount component.

- Layout, biểu đồ, dashboard, summary, báo cáo, nhật ký, lịch sử, cảnh báo và widget chỉ đọc không có form nghiệp vụ để kiểm tra input sai. Bộ này không gán nhãn validation PASS cho việc chỉ mở chúng.
- Các bộ lọc/search, công cụ toán, ảnh nháp, từng mode editor và mọi biến thể hoạt động học có quy tắc riêng; ma trận trên chỉ ghi rõ phần nào đã có scenario. Không xem một case cho form cha là đã kiểm hết mọi field/component con.
- `SubjectGoalsModal` dưới màn quản lý học sinh bị chặn theo catalog quyền hiện tại: CenterManager không có `twin.student.update_scoped`. Test xác nhận không hiện nút legacy, không cấp thêm quyền để mở form. Việc validation trực tiếp của modal này qua UI là **chưa áp dụng được trên luồng hiện tại**.
- Các bản legacy không được router/actor hiện tại render không được tính là đã test chỉ vì có trong cùng file TSX.
- `KnowledgeNodeCreatePanel`/`KnowledgeEdgeCreatePanel` thuộc view đồ thị legacy; view giáo viên hiện hành có case node/edge riêng. `TeacherOverrideModal` không có nơi import/render hiện tại. `MissionPlayerModal` kiểm tra hoạt động trống của nhiệm vụ thực sự được tạo, chưa kiểm tra mọi loại hoạt động hoặc mọi trạng thái pause/retry/conflict.
- Form đổi mật khẩu là component dùng chung; hai entry point Student/Platform có case trực tiếp. Các entry point không tồn tại trong layout hiện hành không được tạo thêm chỉ để test.
- Riêng giáo trình hiện gom lỗi client thành thông báo chung `Không thể lưu giáo trình. Vui lòng kiểm tra lại thông tin.`. Test kiểm tra đúng thông báo UI này và không có request tạo giáo trình, không nhầm chuỗi exception nội bộ với nội dung người dùng nhìn thấy.

## Fixture và tính độc lập

Fixture dùng database acceptance riêng và luôn kiểm tra owner. API chỉ dùng chuẩn bị điều kiện như role tùy chỉnh, graph Draft, bài tập có câu hỏi/lập luận bắt buộc, bài đã nộp hoặc thông báo đã phân phối. Không stub response thành công cho thao tác validation. File picker dùng `selectFile` trên input file thật; `{ force: true }` chỉ dùng để truy cập input file ẩn do thiết kế nút chọn file, không sửa DOM hay bỏ qua validation nghiệp vụ.

Review fixture thực hiện đúng `start → submit → chờ job terminal`; notification fixture thực hiện `preview → send → chờ delivery`; fixture bài tập lập luận tạo và activate câu hỏi qua API của giáo viên Tiếng Anh. Không gọi model AI thật trong acceptance stack.

Worker thông báo của acceptance được cấu hình poll 1 giây để xử lý hàng đợi fixture; fixture chờ đúng thông báo của lượt chạy tối đa 90 giây, không ghi delivery trực tiếp hoặc gọi dispatcher thay worker. Timeout vẫn làm test thất bại.

Riêng form phục hồi `NotificationOperations` dùng Node task ghi một event/dispatch **DeadLetter tổng hợp** vào schema test sau khi xác minh owner. Đây là điều kiện dựng sẵn để kiểm tra lý do và trạng thái nút; không phải bằng chứng worker tự thất bại đủ 5 lần hoặc redrive phân phối thành công. Test không bấm xác nhận redrive và không tạo delivery giả. Các form clone/archive/application dùng giáo trình được clone và publish bằng API thật, không lưu trữ giáo trình seed đang có lớp dùng.

Từ base `b47c314`, placement ẩn phần tùy chỉnh mục tiêu và gửi `clearDeadline=true`. Case nhiệm vụ kiểm tra chính các điều kiện này rồi xác nhận accept 200 trước khi mở nhiệm vụ Hàm số. Các case validation mục tiêu chọn học ngay để mở đúng form. Lượt chẩn đoán deadline seed 01/04/2026 thuộc base cũ và được giữ trong VERIFICATION để đối chiếu lịch sử.

Fixture tùy chỉnh lộ trình nhập rõ ngày tương lai trong chế độ học ngay để tạo preview khả thi khi chạy spec độc lập. Điều này tách điều kiện dựng preview khỏi lỗi số câu mỗi bộ mà case cần kiểm tra.

`maxlength` được kiểm tra bằng việc gõ quá giới hạn và xác nhận giá trị hiển thị bị chặn. `minlength` với React controlled input có thể không giữ cờ `validity.tooShort` sau render; case kiểm tra thông báo từ chối thật trên UI. Validation có thể diễn ra ở browser, handler client hoặc backend; trường hợp backend trả lỗi phải hiển thị được lỗi trên trang.

Chạy từ `test/` sau khi build image API/web theo [README](README.md):

```powershell
npm run e2e:ui:validation -- --browser chrome --headed
```

Không chạy đồng thời nhiều runner vì cùng port/manifest. Chạy `npm run e2e` để kiểm chứng cả API và UI. Kết quả thực tế và các lỗi phát hiện được cập nhật trong [VERIFICATION.md](VERIFICATION.md); case bị lỗi, pending hoặc skipped không tính là đạt.
