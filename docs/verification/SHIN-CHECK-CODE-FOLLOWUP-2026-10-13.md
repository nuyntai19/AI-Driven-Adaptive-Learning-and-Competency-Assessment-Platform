# Kiểm tra nhánh shin/check-code và sửa bổ sung — 13/10/2026

> Ghi chú bàn giao: các dòng “chưa commit/push” bên dưới mô tả thời điểm kiểm chứng từng lượt, không phải trạng thái Git sau bàn giao. Gói commit tiếp theo gồm các sửa trong báo cáo này và [kiểm thử import/nháp, sửa ký hiệu tập hợp và chuyển vào bàn phím MathLive](IMPORT-DRAFT-BROWSER-RETEST-2026-10-13.md). Frontend cuối: 754 test đạt, 0 lỗi/bỏ qua; TypeScript/Vite build và lint đạt. Không có migration, thay mode hàng loạt hoặc chấm lại dữ liệu cũ trong gói này. File fixture, ảnh bằng chứng và backup runtime bị ignore, không đưa lên Git.

Gate trước push: fetch remote xác nhận nền HEAD/remote cùng `93ca621`; staged 18 file, kiểm tra whitespace đạt, không phát hiện mẫu private key/API key trong nội dung staged. Chạy lại nhóm backend ArchivedLearningScopeTests/EduTwinSeedFactoryTests: **34 pass, 0 fail/skip** trên binary đã build ở lượt sửa. Lần chạy đầu trong sandbox bị timeout kết nối testhost trước khi chạy test; lần chạy lại ngoài sandbox với timeout kết nối 180 giây đạt (test duration 7 giây). Đây không phải assertion lỗi sản phẩm. Không chạy lại toàn bộ backend hoặc AI provider thật trong gate push.

## Phạm vi và mốc Git

- Nền bàn giao: `d6688b4fb6ebcf9c79229006661e1462fb68d8c1` trên `student/answer`.
- Nhánh đã fetch và checkout: `shin/check-code`, HEAD `93ca621`; hai commit mới là `07699e8` và `93ca621`.
- Diff của người cộng tác so với nền: 107 file, 6.036 dòng thêm / 2.237 dòng xóa. Các sửa bổ sung dưới đây nằm trong working tree, chưa stage/commit/push; không merge vào `student/answer`.
- Đối chiếu toàn danh sách thay đổi, đọc sâu luồng đáp án, session, import và các báo cáo. Không tuyên bố đã nghiệm thu mọi dòng code hoặc mọi tình huống vận hành.

## Những phần người cộng tác đã làm

| Nhóm | Nội dung có trong hai commit |
| --- | --- |
| Làm bài | Chờ hydrate đúng scope; hợp nhất draft server/local; giữ key và payload khi retry mất response; chặn submit nếu upload ảnh thất bại; reset trạng thái giữa câu; thông báo AI free practice không hứa teacher review |
| Lộ trình | GET session; class/owner/topic/session scope; path ID bền vững khi recommendation thay phiên bản; optimistic concurrency; điều kiện hoàn thành và checkpoint; câu Active; ưu tiên/mode/pace ảnh hưởng schedule |
| Import V1 | CSV UTF-8/XLSX, workbook theo môn, topic từng dòng, preview/revision/token, chọn tập hợp hợp lệ, Draft-only, validation nội dung/mode/score, giới hạn giải nén/XML/buffer/cache/concurrency, transaction rollback |
| Báo cáo/UX | Curriculum application hiện hành cho dashboard; trạng thái điểm CSV; chống CSV formula injection; ghi chú local theo actor/center; bỏ số dashboard hardcode; math delimiter/escaped dollar; giữ language/misconception |
| Hạ tầng/kiểm thử | Proxy body limit 11 MiB; SQL generator cho seed applicability deletion; bundle/chunk checks; test và hướng dẫn chạy lại. Script run có đổi mật khẩu demo hiển thị cứng, không phải thay mật khẩu database |

Import hiện chỉ là V1 text/inline math: Excel/CSV (TXT legacy), chưa Word/PDF, ảnh/Equation, OCR hay media package. Báo cáo chức năng cũ có mục DOCX nhưng trạng thái mới nhất là V1 theo [report 13/10](QUESTION-IMPORT-V1-REPORT-2026-10-13.md), không dùng mục lịch sử đó để hứa hỗ trợ hiện tại.

## Hai lỗi được xác nhận và sửa trong lượt đầu

### 1. ShortAnswer TextExact thiếu Chèn công thức

Bản `93ca621` chưa đổi `ModeAwareAnswerEditor` hoặc seed tập xác định so với `d6688b4`. ShortAnswer TextExact vẫn đi qua input chữ thuần; MathEquivalent/NumericRational/Coordinate2D và Manual đã có composer. Khi đổi loại sang ShortAnswer, biểu mẫu vẫn mặc định TextExact.

- Student answering TextExact nay dùng cùng composer chữ/công thức với Manual, không đổi evaluation mode hoặc backend parser. Có chú thích chấm khớp văn bản, không xét tương đương toán học.
- Giữ raw source (kể cả `$...$`), `displayLatex`/`answerDisplayLatex` rỗng; không tự strip ký hiệu, đổi mode hoặc coi biểu thức khác cú pháp là tương đương. Với câu toán cần tương đương, giáo viên phải chủ động chọn mode toán phù hợp. Lượt đầu chưa đổi authoring (được bổ sung theo yêu cầu mới bên dưới); MCQ vẫn chọn phương án, submitted/read-only không có composer chỉnh sửa.
- Riêng template `MATH-FUNC-02` (tập xác định `y=1/(x-2)`) có mode MathEquivalent rõ ràng cho database tạo mới. Regression kiểm tra cả center A/B và dạng `D=R\\{2}`. Không đổi default của tất cả ShortAnswer.
- Không chạy maintenance/regrading hoặc cập nhật câu đang có: sau rollout, câu `10001` vẫn MathEquivalent, `20001` vẫn TextExact như trước.

### 2. Câu bị lưu trữ giữa buổi làm session bị kẹt

Probe trước sửa: session gồm `900,901`, câu `900` đã làm đúng, `901` Archived; next-question không tìm được câu, Evaluate trả false, trạng thái InProgress.

`LearningPathSessionAccess` nay lọc tập scheduled ID theo câu còn Active, không xóa, cùng center/subject/topic, không Essay/Manual. GET session, next-question và Evaluate dùng cùng tập. Tỷ lệ hoàn thành không tính câu đã rút; việc đọc chỉ lọc JSON detached, không SaveChanges hoặc tăng row-version. Chỉ một update session được phép mới lưu schedule đã revalidate; không sửa attempts/điểm/mastery.

Probe sau sửa: cùng dữ liệu giả, Evaluate được chấp nhận, trạng thái Completed, tập còn `900`. Regression bao gồm Archived, Draft, soft-deleted, GET không ghi dữ liệu, checkpoint, số attempts giữ nguyên. Nếu không còn câu, Evaluate không tự Complete; phải xác nhận hoạt động tường minh, không tạo điểm/mastery. UI giải thích điều này.

## Kiểm chứng thực hiện trong lượt này

| Gate | Kết quả |
| --- | --- |
| Reproduce trước sửa | Student TextExact routing test fail; bốn regression session fail đúng lỗi; probe InMemory kẹt InProgress |
| Backend cuối | 4.252 pass, 0 fail, 103 integration skip; tổng 4.355. Các skip không được tính là pass |
| Nhóm scope/session | 22 pass, 0 fail trước khi thêm hai test seed; full cuối bao gồm tất cả |
| Frontend cuối | 726 pass, 0 fail/skip; nhóm answer editor 38 pass |
| Seed | Hai center dùng mode toán cho riêng mẫu tập xác định; đáp án tương đương được nhận; câu chữ khác vẫn giữ TextExact |
| MySQL review nhánh trước sửa bổ sung | Import V1 1 pass, 0 skip/fail: persistence/rollback/retry/500 Draft; schema UUID được dọn, kiểm tra còn 0 `edutwin_import_*` |
| Lint/build/bundle | Lint quiet exit 0; TS/Vite build thành công; 4 bundle checks pass; warning MathLive chunk lớn vẫn còn |
| API/web Docker | Build/publish thành công; API 0 warnings/errors; chỉ recreate API/web, MySQL và volumes giữ nguyên |
| Browser | Component production source trên fixture Vite cách ly: TextExact/MathEquivalent/Essay có nút; TextExact mở popup, nhập và chèn `x=3`, raw `$x=3$` / displayLatex rỗng. Không nộp hoặc chấm bài thật |
| Live localhost | Web HTTP 200; học tập không auth HTTP 401; container chạy; bundle `ModeAwareAnswerEditor-D1-htFc6.js`; DB vẫn 48 migrations, không áp migration mới |
| Bảo toàn | Backup trước rollout; SHA-256 19 bảng bảo vệ trước/sau khớp, changedTables rỗng; kiểm tra riêng hai mode câu có sẵn không đổi |

Backup `storage/backups/2026-10-13-before-answer-session-fix.sql`, SHA256 `985f2b9ddb0fcceac13e3240d6e5b6e7d05966e913b53bedb24aa22cdadad0bf`; không restore live. Probe và ảnh `storage/verification/shin-check-code-review/student-text-exact-formula.png` được Git ignore. Trang/tsx QA tạm đã xóa, server Vite và tab QA đã đóng; không thêm route QA vào sản phẩm.

## File sửa trong lượt này

1. `src/EduTwin.BLL/Recommendations/UseCases/LearningPathSessionAccess.cs`
2. `src/EduTwin.DAL/Seeding/QuestionSeedTemplates.cs`
3. `tests/EduTwin.BLL.Tests/Recommendations/ArchivedLearningScopeTests.cs`
4. `tests/EduTwin.BLL.Tests/Seeding/EduTwinSeedFactoryTests.cs`
5. `web/edutwin-web/src/components/math/answer-editor/ModeAwareAnswerEditor.tsx`
6. `web/edutwin-web/src/pages/LearningPlayerPage.tsx`
7. `web/edutwin-web/tests/proseAnswerFormulaEditor.test.ts`
8. `UI_UX_SPEC.md`
9. Báo cáo này.
10. `web/edutwin-web/src/components/math/answer-editor/InlineMathAnswerInput.tsx` (lượt bổ sung authoring).
11. `web/edutwin-web/src/pages/centerManagerQuestionEditorHelpers.ts` (lượt bổ sung authoring).
12. `web/edutwin-web/tests/modeAwareAnswerEditorHelpers.test.ts` (lượt bổ sung authoring).

## Bổ sung: Chèn công thức khi tạo/sửa câu hỏi — yêu cầu tiếp theo 13/10/2026

Người dùng xác nhận phần soạn câu hỏi vẫn hiển thị các input cũ trong TextExact/NumericRational/Coordinate2D/MathEquivalent. Nguyên nhân là nhánh UI `profile="authoring"` chưa nằm trong phạm vi sửa answering ở lượt đầu; không phải mất nút do chọn nhầm loại câu hoặc chưa push.

### Thay đổi

- `ModeAwareAnswerEditor` dùng composer Chèn công thức cho mọi profile chỉnh sửa hợp lệ: 5 mode ShortAnswer và Essay/Manual. Không còn render PlainTextAnswerInput, NumericRationalMathInput hoặc Coordinate2DInput ở orchestrator; giữ các file cũ để không phá hợp đồng của consumer độc lập.
- `InlineMathAnswerInput` nhận variant teacher/center-manager/neutral/student, giữ focus, ref Casio, clear, external draft, disabled/read-only. Label authoring là Đáp án chuẩn. Không tự đoán hoặc đổi evaluation mode.
- TextExact/Manual giữ source chữ và `$...$`, displayLatex rỗng. Ba mode toán dùng giá trị toán đã serialize; một công thức chèn độc lập được bỏ delimiter trình bày, không bỏ nội dung hoặc chuẩn hóa ý nghĩa ở client.
- Bộ kiểm tra `validateAnswerMathFormulas` trước đây chỉ coi MathEquivalent là toàn công thức; NumericRational báo unwrapped-latex với `\frac{3}{4}`, Coordinate2D có thể báo sai cú pháp ở `\left`. Nay kiểm tra toàn công thức cho cả ba mode toán. Vẫn chặn placeholder, cú pháp sai, delimiter chưa đóng; TextExact/Manual vẫn kiểm tra theo nội dung chữ/công thức. Không thay bộ so khớp backend.
- Form tạo và form sửa Teacher dùng cùng `TeacherQuestionEditorView`; consumer CenterManager cũng dùng cùng orchestrator. MCQ vẫn chọn đáp án đúng trong danh sách phương án; mỗi nội dung phương án đã có Chèn công thức. Dropdown 5 mode chỉ dành ShortAnswer; Essay dùng Manual, MCQ dùng chọn phương án, không mở tổ hợp bất hợp lệ.

### Ý nghĩa các chế độ chấm hiện có

| Mode | Đối chiếu đáp án sơ bộ | Vai trò AI |
| --- | --- | --- |
| TextExact | Trim đầu/cuối, so chuỗi không phân biệt hoa/thường. Không tự coi `1/2` là `0.5`; khoảng trắng nội bộ và source ký hiệu vẫn có ý nghĩa | Phân tích lập luận, nêu bất đồng nếu có, không tự thay grade xác định |
| NumericRational | Chuẩn hóa số nguyên/thập phân/phân số thành số hữu tỉ; `1/2` và `0.5` tương đương | Nhận mode/canonical answer và kết quả sơ bộ để phân tích/đề xuất |
| Coordinate2D | Đối chiếu hai thành phần hữu tỉ; chấp nhận ký hiệu tọa độ và dạng số tương đương trong parser hỗ trợ | Như trên, phân tích lập luận riêng |
| MathEquivalent | Bộ parser giới hạn cho số hữu tỉ, tọa độ, tập hợp/tập số thực trừ tập hữu hạn; ví dụ `D=R\{2}` và `R \ {2}`. Không phải CAS tương đương mọi biểu thức | Nhận canonical values, phân tích ý nghĩa/lập luận và nêu giới hạn |
| Manual | Không tự cho đúng/sai hoặc điểm sơ bộ bằng so khớp chuỗi | AI đề xuất theo lời giải/rubric; giáo viên duyệt/chốt |

Mode được gửi trong AI request; không chuyển Gemini model/provider theo mode. Kết quả phân tích, chất lượng lập luận và điểm đề xuất là thông tin riêng; điểm giáo viên chốt vẫn là nguồn cuối cùng. Định dạng toán ngoài grammar được chuyển review, không tự coi sai. Việc có nút nhập căn/biến không có nghĩa mọi mode chấp nhận chúng khi so khớp.

### Kiểm chứng và rollout bổ sung

- TDD: 12 trong 15 regression authoring fail trước sửa (chỉ Manual đã có composer); sau sửa đủ 15 trường hợp Teacher/CenterManager/neutral pass.
- Nhóm answer/editor/payload/formula: **121 pass, 0 fail**; gồm tạo payload, giữ nháp riêng, công thức phân số/tọa độ/tập hợp, readonly và cấu hình bất hợp lệ.
- Toàn frontend mới: **751 pass, 0 fail/skip**. Lint quiet exit 0; TS/Vite build thành công, 976 modules; bundle checks **4 pass**. Warning MathLive 825,52 kB vẫn còn.
- Nhóm backend kiểm tra grading/prompt/request factory: **76 pass, 0 fail/skip**. Không chạy AI provider thật hoặc chấm lại bài cũ; không coi test prompt là đo chất lượng AI thực tế.
- Docker build web thành công; chỉ `up -d --no-deps web`, không restart API/MySQL, không migration, maintenance hoặc cập nhật database. Web chunk deployed `ModeAwareAnswerEditor-2Voti5ja.js`; container inspect xác nhận image `sha256:93aa64ffbe0314027b49a459b901bda9c39999085f41e97d9417895759b41a54`.
- Browser thật Teacher Math trên localhost: form tạo mới có nút trong đủ 5 mode; Essay có nút, MCQ có nút cho từng phương án. NumericRational mở popup, nhập/chèn `3/4`, xem mã `$3/4$`; chuyển sang Coordinate2D/MathEquivalent/Manual không lẫn nháp; quay về NumericRational vẫn giữ công thức. Không bấm lưu, không tạo câu thật, không submit/chấm/đổi điểm. Kiểm tra form sửa qua shared component và regression, không lưu câu cũ qua browser.
- Ảnh bằng chứng: `storage/verification/shin-check-code-review/teacher-authoring-formula.png` (ignored). Tab QA mới được đóng, tab Teacher đang dùng của người dùng giữ nguyên.
- Chưa stage/commit/push; HEAD vẫn `93ca621`, giữ toàn bộ sửa bổ sung trước đó. Không có script sửa mode đại trà hoặc chuyển đổi đáp án cũ.

## Giới hạn còn lại

- Những phần người cộng tác làm thêm có các gate trên đạt, không có nghĩa toàn hệ thống tuyệt đối không lỗi. Chưa chạy lại toàn bộ relational suite hoặc browser E2E nhiều vai trò trong lượt này; lọc session mới đã chạy InMemory và full backend, chưa có ca MySQL mới riêng cho tình huống đó.
- Chưa kiểm chứng 1.000 học sinh đồng thời, nhiều replica, provider AI thật hoặc mọi lỗi mạng/offline/reload. Preview import/cache và generation lock vẫn process-local như report của người cộng tác; path chưa độc lập theo mọi curriculum application.
- Docker npm audit còn 11 advisories (9 high, 2 moderate); không chạy audit fix hoặc thay dependency ngoài phạm vi. MathLive còn warning chunk lớn.
- Không tự thay mode hoặc chấm lại các câu/attempts cũ để làm UI trông đồng nhất. Nếu cần sửa dữ liệu câu cũ, phải xác định scope và duyệt riêng, giữ lịch sử.
