# QA chuyên sâu chức năng học sinh — 20/11/2026

Kết luận: **chưa đạt để xác nhận toàn bộ trải nghiệm student ổn định**. Đã kiểm thử trực tiếp trong trình duyệt và với API/MySQL thật trên môi trường QA độc lập. Ghi nhận **13 vấn đề**, gồm 1 P1 và 12 P2. Đây là báo cáo phát hiện; đợt này chưa sửa mã nghiệp vụ.

## Môi trường và cách tính kết quả

- Mã nguồn kiểm tra: `424a755b3a8b3c25ff1e9be4cba5002c1c34a8f0`.
- Web/API Docker tạm trên cổng 3002/5001; schema và tài khoản giả lập độc lập. Không dùng dữ liệu học sinh thật để tạo/nộp/chốt bài hoặc đổi mật khẩu.
- Trình duyệt ChatGPT in-app browser; tương tác UI thực, quan sát DOM/ảnh. Không dùng các script Chrome lịch sử để kết luận đạt.
- 70 kịch bản UI được ghi sổ: **57 PASS, 13 FAIL**. Một kịch bản có thể gồm nhiều bước; không tính mỗi lần click thành một test.
- Frontend: **948/948 PASS**, 26 suites.
- Backend student-related: **743 PASS, 36 SKIP, 0 FAIL**; không coi 36 test bỏ qua là đạt.
- HTTP/MySQL thật: **112 kiểm tra đạt** = 24 assignment acceptance + 72 student API + 6 account security + 7 scheduled/publication checks + 3 teacher API setup assertions. Ba kiểm tra cuối chuẩn bị kết quả thật cho luồng student yêu cầu xem lại; không đại diện cho QA toàn bộ giảng viên.
- Độ rộng CSS đo thực tế: desktop 1366, tablet 768, hẹp 406 px. Yêu cầu viewport 320 bị công cụ giới hạn thành 406: **chưa kiểm chứng 320 px**. Kiểm tra sáng/tối và che khuất tại các trạng thái ghi trong ledger, không khẳng định mọi tổ hợp thiết bị.
- AI provider không được cấu hình trong stack độc lập: đã kiểm tra fallback, thông báo và lưu bài; chưa kiểm chứng chất lượng AI trực tiếp.

## Các vấn đề cần sửa

### 1. P1 — UI-CONTENT-01: quy tắc xét dấu tam thức bậc hai bị đảo ngược

**Tái hiện:** Toán → Lộ trình → chặng Hàm số và phương trình bậc hai → mở “Ý chính của kỹ năng”.

**Thực tế:** nội dung nói ngoài khoảng hai nghiệm trái dấu với a, trong khoảng hai nghiệm cùng dấu với a.

**Đúng:** khi có hai nghiệm thực phân biệt, ngoài khoảng hai nghiệm **cùng dấu a**, trong khoảng **trái dấu a**. Ví dụ x²−1 dương ngoài (−1,1), âm trong khoảng. Nội dung sai có thể trực tiếp làm học sinh học sai kiến thức.

**Vị trí:** `src/EduTwin.DAL/Seeding/EduTwinSeedFactory.cs:251`. Cần kiểm tra cả dữ liệu đã seed, không chỉ sửa chuỗi cho DB mới. Bằng chứng: `quadratic-lesson-visible-text.txt`, ảnh 25.

### 2. P2 — UI-PRACTICE-05: mất bài chưa nộp khi rời phiên luyện tập

**Tái hiện:** bắt đầu phiên tự luận → nhập bài giải → Tạm dừng → Rời khỏi trang → Tiếp tục cùng phiên, cùng câu.

**Thực tế:** ô bài giải trống, không có cảnh báo mất phần chưa nộp. **Mong đợi:** khôi phục bản nháp hoặc cảnh báo rõ trước khi rời. Phân biệt với bài tập/đầu vào: các luồng đó đã khôi phục bản nháp thành công.

**Vị trí điều tra:** `web/edutwin-web/src/pages/AdaptivePracticeSessionPage.tsx`, trạng thái bài giải và luồng pause/leave/resume. Quan sát trực tiếp đã ghi trong ledger; ảnh phiên đầu không còn trên đĩa, không dẫn ảnh không tồn tại.

### 3. P2 — UI-DATE-01: ngày đích chứa cả chuỗi giờ ISO

**Tái hiện:** mở lộ trình có deadline đầy đủ ISO. **Thực tế:** `Ngày đích 19T16:59:59Z/01/2027` (lần trước cũng thấy `01T16:59:59Z/03/2027`). **Mong đợi:** ngày được định dạng nhất quán theo múi giờ ứng dụng, không lộ đoạn `T…Z`.

**Vị trí:** `StudentLearningPathPage.tsx:352` đang `split("-").reverse().join("/")` trên toàn timestamp. Bằng chứng: ảnh 24 và 25.

### 4. P2 — UI-FLOW-01: nhãn nút cuối phần đầu vào không đúng đích đến

**Tái hiện:** kiểm tra hai phần Hàm số → Bậc hai; hoàn thành phần đầu. **Thực tế:** nút ghi “Xem kết quả và lộ trình đề xuất” nhưng bấm lại mở phần kiểm tra thứ hai. **Mong đợi:** “Sang phần tiếp theo”; chỉ nói xem kết quả khi hết các phần.

**Vị trí:** `StudentLearningPathPage.tsx:665`, tính nhãn dựa vào phần phụ thuộc còn đang khóa trong agenda cũ. Ràng buộc thứ tự vẫn hoạt động; lỗi nằm ở kỳ vọng điều hướng.

### 5. P2 — UI-A11Y-01: focus thoát khỏi modal kiểm tra

**Tái hiện:** mở kiểm tra toàn màn hình, dùng Tab từ “Để làm sau”. **Thực tế:** focus tới nút “Mở góc tập trung” ở nền, nằm ngoài dialog. Modal mục tiêu/mở rộng cũng chưa chuyển focus vào khi mở. **Mong đợi:** focus vào dialog và giữ vòng Tab trong dialog, trả focus khi đóng.

**Vị trí điều tra:** `MissionPlayerModal` và các modal learning path. Menu mobile và modal bảo mật/góc tập trung đã có các hành vi Escape/focus tốt hơn; không gộp tất cả modal là lỗi.

### 6. P2 — UI-DASH-LOGIC-01: chưa có bằng chứng vẫn hiển thị dự đoán 0 và rủi ro 0%

**Tái hiện:** chuyển sang Tiếng Anh chưa có lượt ghi nhận. **Thực tế:** 0 lần ghi nhận, chưa có dữ liệu nhưng dự đoán `0.0/8`, cần thêm 8 điểm, nguy cơ chưa đạt `0%`. **Mong đợi:** “Chưa đủ dữ liệu dự báo”, không biến trạng thái chưa biết thành điểm 0 hoặc khẳng định rủi ro bằng 0.

Trang hồ sơ năng lực đã phân biệt thiếu dữ liệu tốt hơn. Bằng chứng: `dashboard-no-evidence.txt`, ảnh 23.

### 7. P2 — UI-GOAL-04: tự tính thời lượng mâu thuẫn với kiểm tra deadline cuối

**Tái hiện:** trong popup điều chỉnh mục tiêu, chọn hạn cùng ngày và ngân sách thấp → tự tính thời lượng. **Thực tế:** thông báo đã tính 140 phút/ngày để xong trước 20/11; kết quả ước lượng lại cần 153 phút, hai ngày học, sớm nhất 23/11 và không cho tiếp tục. Có thông báo tăng từ 8 lên 8 điểm gây khó hiểu.

**Mong đợi:** dùng cùng ràng buộc/ngân sách giữa bộ tự tính và kết quả cuối; chưa khả thi thì không thông báo như đã đáp ứng. Cơ chế chặn và gợi ý ngày khả thi vẫn hoạt động.

### 8. P2 — UI-CARDS-LOGIC-01: thẻ ôn trắc nghiệm không có lựa chọn nhưng đáp án chỉ là C

**Tái hiện:** Sổ tay → Thư viện thẻ ôn → Ôn thẻ → Mở đáp án. **Thực tế:** mặt trước chỉ có đề, thiếu các lựa chọn; mặt sau “ĐÁP ÁN ĐỐI CHIẾU: C”. Lời giải có x=1 và x=3 nhưng ký tự C không thể đối chiếu với lựa chọn nào.

**Mong đợi:** hiển thị nội dung đáp án hoặc đầy đủ các lựa chọn. Bằng chứng: `flashcard-visible-text.txt`, ảnh 26.

### 9. P2 — UI-NOTIFY-LOGIC-01: tiêu đề AI hoàn thành sai với kết quả fallback

**Tái hiện:** nộp bài khi AI unavailable → xem hộp thông báo. **Thực tế:** tiêu đề “AI đã xử lý toàn bài”, nội dung lại 0 AI/2 fallback, dùng thuật ngữ `fallback`, `terminal`. **Mong đợi:** thông báo rõ bài đã lưu, chấm tự động/dự phòng và chờ giáo viên; không khẳng định AI đã phân tích khi chưa có kết quả AI.

### 10. P2 — UI-NOTIFY-02: khoảng ngày đảo ngược được báo thành lỗi tải

**Tái hiện:** hộp thư chọn từ 21/11 đến 20/11. **Thực tế:** “Không tải được hộp thư. Hãy thử làm mới”, danh sách bị thay bằng lỗi. Nhật ký hoạt động cũng báo lỗi chung về kết nối/quyền/khoảng ngày.

**Mong đợi:** kiểm tra ngày bắt đầu ≤ ngày kết thúc, chỉ rõ trường sai và giữ nội dung đang có. Gộp hai màn hình vào một vấn đề validation/UX.

### 11. P2 — UI-EXAM-STATE-01: kết quả chưa công bố bị trình bày như AI đang xử lý

**Tái hiện:** nộp kỳ thi, chưa công bố → mở trình xem lại bài làm. **Thực tế:** đầu trang nói chờ giảng viên công bố, phần tổng quan lại `0/1` đúng và AI đang phân tích 1 câu. **Mong đợi:** trạng thái “Kết quả chưa được công bố”, không suy ra 0 câu đúng hay trạng thái tiến trình AI từ dữ liệu đã bị ẩn.

Kiểm tra bảo mật kết quả vẫn đạt: trước công bố không lộ đúng/sai, điểm và lời giải; sau công bố xem được. Ảnh 20 là trang chi tiết chờ công bố, không phải ảnh tổng quan có lỗi; kết luận tổng quan dựa trên quan sát UI ghi trong ledger.

### 12. P2 — UI-REVIEW-COPY-01: nội dung dự phòng chưa theo quyết định giáo viên

**Tái hiện:** giáo viên dùng API override hợp lệ từng câu, rồi approve-result toàn bài → học sinh mở bài đã chốt. **Thực tế trước khi gửi yêu cầu xem lại:** tổng quan chính thức 10/10, chờ GV 0 và đã chốt; khối dự phòng vẫn nói lập luận tạm thời cần giáo viên xem xét. Popup yêu cầu xem lại gọi đây là “Kết quả AI” dù không có đánh giá AI thành công.

**Mong đợi:** ưu tiên trạng thái/nhận xét giáo viên hiện tại; giữ lịch sử AI chưa hoàn tất dưới dạng lịch sử, không khiến học sinh hiểu bài vẫn đang chờ chấm. Sau khi học sinh gửi yêu cầu mới thì trạng thái chờ xem xét lại là hợp lý.

Bài dùng để kết luận này đi qua API giáo viên đầy đủ. Không dùng fixture kỳ thi chỉ seed final approval để kết luận tính nhất quán của quy trình chốt.

### 13. P2 — UI-EMPTY-SCOPE-01: tài khoản chưa có lớp bị xử lý không nhất quán

**Tái hiện:** tạo tài khoản giả lập chưa gán lớp → đăng nhập → Tổng quan → Lộ trình → Luyện tập.

**Thực tế:** Tổng quan báo chưa có môn/lớp. Lộ trình tự chuyển sang Toán và cho tạo, bắt đầu kiểm tra đầu vào. Luyện tập lại báo “Lớp đã lưu trữ hoặc bạn đã rời lớp”, vô hiệu hóa bắt đầu nhưng vẫn hiện “Đang tải đề xuất…/Đang tải lịch sử…”. Sổ tay yêu cầu chọn lớp có giáo trình.

**Mong đợi:** xác định rõ có cho tự học ngoài lớp không và áp dụng nhất quán; tài khoản mới cần thông báo chưa được xếp lớp. Query bị disable không nên hiển thị như đang tải. `AdaptivePracticeOverviewPage.tsx` chỉ enable query khi `classResolved`, nhưng trạng thái UI vẫn dùng pending/loading.

Bằng chứng: `empty-account-practice.txt`, ảnh 22. Không thấy lộ tên, điểm hoặc bài của tài khoản trước.

## Những luồng đã kiểm chứng đạt

| Nhóm | Phạm vi đã thực hiện |
|---|---|
| Đăng nhập, quyền, phiên | Rỗng/sai mật khẩu/sai trung tâm, trim thông tin hợp lệ; student bị chặn teacher/admin; logout rồi Back không lấy lại trang bảo vệ; tài khoản mới không nhận dữ liệu tài khoản cũ. HTTP đổi mật khẩu trên tài khoản giả lập, token cũ vô hiệu, khôi phục mật khẩu fixture, revoke-all. |
| Phạm vi môn/lớp | Chuyển Toán/Tiếng Anh; liên kết bài tập/thông báo đi đúng phạm vi; rời trang toàn cục và quay về vẫn khôi phục phạm vi. |
| Kiểm tra đầu vào | Không chọn phần thì chặn; chỉ chọn phần phụ thuộc có popup xác nhận bổ sung nền tảng; hủy giữ lựa chọn, xác nhận mới bắt đầu; thứ tự frontend/backend khóa phần kế tiếp; đáp án nháp sửa được trước nộp phần, đóng/mở khôi phục; thiếu đáp án/lý do có cảnh báo; bỏ câu không coi đủ bằng chứng. |
| Mục tiêu/lộ trình | Điểm vượt 10, ngày không hợp lệ/quá khứ, lịch học rỗng, thời lượng thấp, tuyến rỗng bị chặn; gợi ý ngày khả thi; accept tạo tuyến; hủy mở rộng giữ tuyến. |
| Học và công cụ | Ví dụ mẫu/ghi chú/hoàn thành hoạt động, trạng thái AI unavailable; máy tính 1+2, chia 0; đồ thị biểu thức sai; nháp vẽ/undo/redo/lưu/fullscreen/Escape; file sai định dạng bị từ chối. |
| Bài tập/thi | Tìm kiếm có/không kết quả; lưu nháp, đổi đáp án, next/prev/reload; chưa đủ câu/lý do không nộp; nộp chỉ đọc; giới hạn giờ không reset sau reload, hết giờ tự nộp dữ liệu đã lưu; kỳ thi chưa tới giờ giấu câu và chặn start; trước/sau công bố giấu/mở kết quả đúng. |
| Luyện tập | Tiếp tục phiên, chặn tạo phiên khác khi đang hoạt động; gợi ý, đáp án đúng/sai, pause timer, hủy/xác nhận kết thúc, tổng kết, liên kết luyện tiếp đúng chủ đề/thời lượng. Có ngoại lệ mất nháp như lỗi 2. |
| Hồ sơ/sổ tay | Lọc năng lực chưa có dữ liệu, phân trang lịch sử; mục tiêu hợp lệ lưu; thống kê lượt/câu riêng; tìm kiếm chuỗi markup hiển thị vô hại; thẻ reveal/requeue/remember/kết thúc; tự đánh giá thẻ không tự tăng mastery. |
| Thông báo/nhật ký | Mở, đánh dấu đã đọc, không còn chưa đọc, lọc nguồn/loại, liên kết đúng bài, clear filter. Có ngoại lệ khoảng ngày và tiêu đề AI như trên. |
| Xem xét lại | Bài được giáo viên chốt qua API thật; whitespace và lý do dưới 10 ký tự bị chặn; lý do hợp lệ tạo yêu cầu pending, UI không còn cho gửi trùng. Biểu mẫu báo lỗi đề bài đã thử validation/hủy, chưa kiểm chứng vòng xử lý hoàn chỉnh. |
| Hiển thị | Desktop/tablet/hẹp 406, sáng/tối, menu mobile, modal công cụ; không thấy tràn ngang tại các đo đạc lưu. Thanh điều hướng nổi có thể đè nội dung lúc cuộn giữa trang, nhưng phần cuối vẫn cuộn ra và thao tác được; chưa đủ căn cứ coi là lỗi không thể truy cập. |

## Giới hạn và phần chưa xác nhận

- Không tuyên bố đã phủ mọi tổ hợp chức năng. Chưa kiểm thử offline thực/khôi phục mạng và hai tab cùng sửa qua UI; API có kiểm tra xung đột revision nhưng không thay thế QA trình duyệt đa tab.
- Chưa kiểm thử máy thật 320px, Safari/iOS/Android, screen reader, tải đồng thời hoặc độ trễ sản xuất.
- Chưa xác nhận OCR thành công/ảnh bài làm hợp lệ và chất lượng AI thật. Không dùng fallback làm bằng chứng AI thành công.
- Chưa chạy đủ vòng giáo viên giải quyết yêu cầu xem lại/báo lỗi đề rồi học sinh nhận kết quả cuối; đã thử phía student tạo yêu cầu và trạng thái pending.
- Chưa phủ đầy đủ dữ liệu lớp đã lưu trữ/rời lớp thật. Không coi tài khoản chưa có lớp là tương đương trường hợp lịch sử lớp.
- 36 test backend bị skip vẫn cần môi trường/tham số riêng để chạy; 112 kiểm tra HTTP không thay thế tất cả chúng.
- Ảnh phiên đầu 01–18 và hai script HTTP bổ sung ban đầu không còn trong workspace sau gián đoạn phiên. Log kết quả và ledger đã được khôi phục/lưu; ảnh 19–26 đã kiểm tra tồn tại. Các lỗi không có ảnh được ghi đúng giới hạn này, không dẫn ảnh giả.
- Fixture kiểm tra hiển thị sau công bố có seed trạng thái final approval, nên không dùng sự mâu thuẫn per-question của fixture đó làm lỗi sản phẩm. Luồng xem lại dùng API override/chốt thật riêng.

## Bằng chứng và chạy lại

Raw evidence nằm trong `docs/verification/evidence/student-deep-qa-2026-11-20/` (đã ignore khỏi Git): `ui-results.json`, các log test, visible text, fixtures và thư mục screenshots. Báo cáo này không chứa mật khẩu/token.

- Frontend: `npm test` tại `web/edutwin-web`.
- Backend: `dotnet test tests/EduTwin.BLL.Tests/EduTwin.BLL.Tests.csproj --no-restore --filter "FullyQualifiedName~Recommendations|FullyQualifiedName~Assignments|FullyQualifiedName~Bookmarks|FullyQualifiedName~ReviewRequest|FullyQualifiedName~StudentWorkspace|FullyQualifiedName~AccountSecurity|FullyQualifiedName~StudentSubjectGoal|FullyQualifiedName~Notification|FullyQualifiedName~GetAttemptFeedback" --verbosity quiet`.
- Stack/API cơ sở: xem `scripts/e2e/README.md`; start acceptance stack rồi chạy `node scripts/e2e/assignment_api_acceptance.cjs`.
- Fixture bổ sung: `node scripts/e2e/student_qa_remaining.cjs`; đầu ra ghi ID kỳ thi và tài khoản trống trong evidence. Lịch thi tương đối tính tại thời điểm tạo, cần kiểm tra ngay.
- Chuẩn bị bài đã chốt: nộp bài giả lập trước, rồi `node scripts/e2e/student_qa_review.cjs <disposable-assignment-id>`. Script chỉ cho title prefix acceptance và dùng stack owner manifest.
- Dọn bằng `node scripts/e2e/acceptance_stack.cjs stop`, có xác minh owner và chỉ xóa schema/container QA của lần chạy đó. Không dùng script Chrome lịch sử để thay thế các bước UI trong ledger.

**Dọn sau kiểm thử đã xác minh:** container/schema/user của lần QA này đã được xóa bằng harness; database chính không bị thay đổi. Hai container acceptance cũ đang dừng được trả lại tên ban đầu, không khởi động hoặc xóa. Tab kiểm thử đã đóng và viewport override đã reset. Script QA bổ sung qua kiểm tra cú pháp `node --check`; không sửa mã nghiệp vụ.

## Sổ kịch bản UI

Danh sách dưới giữ mô tả quan sát gốc bằng tiếng Anh để đối chiếu với JSON. PASS là đạt trong điều kiện/kịch bản đã ghi, không có nghĩa cả tính năng không còn lỗi.

| ID | Kết quả | Kịch bản quan sát |
|---|---|---|
| UI-AUTH-01 | PASS | Empty login: required fields block submission |
| UI-AUTH-02 | PASS | Incorrect credentials show a clear error without navigation |
| UI-AUTH-03 | PASS | Valid student signs into workspace |
| UI-NAV-01 | PASS | Switch English to Math updates subject and class scope |
| UI-NAV-02 | PASS | Mobile drawer opens; Escape closes and restores focus to menu button |
| UI-LAYOUT-01 | PASS | Dashboard and navigation fit measured 406px CSS viewport without horizontal overflow |
| UI-FOCUS-01 | PASS | Focus timer selects 10 minutes, starts, disables duration switches and persists across navigation |
| UI-NOTE-01 | PASS | Notebook separates answer attempts, distinct graded questions and empty incorrect filter |
| UI-NOTE-02 | PASS | Search markup is inert; no results has working reset to all answers |
| UI-NOTE-03 | PASS | Empty flashcard library disables review and explains how to add cards |
| UI-FOCUS-02 | PASS | Pause, reset to original duration and Escape close work on mobile |
| UI-PATH-01 | PASS | No sections selected disables start with explanatory message |
| UI-PATH-02 | PASS | Selecting only quadratic functions prompts for unselected foundation Hàm số; cancel retains only chosen checkbox |
| UI-PATH-03 | PASS | Confirmed placement starts foundation first and disables dependent section |
| UI-PATH-04 | PASS | Empty written answer blocked with explanation |
| UI-PATH-05 | PASS | Save then return to question restores answer; edited draft survives close and resume |
| UI-PATH-06 | PASS | Section confirmation names unanswered question numbers and permits returning to edit |
| UI-TOOLS-01 | PASS | Calculator 1+2 gives 3; divide by zero shows an error and no insert result |
| UI-TOOLS-02 | PASS | Graph tool rejects abc( with Vietnamese syntax guidance |
| UI-PATH-07 | PASS | Multiple-choice missing choice and required explanation are blocked |
| UI-TOOLS-03 | PASS | Scratchpad opens fullscreen, supports drawing, saves, undo/redo and Escape shrink |
| UI-A11Y-01 | FAIL | Placement modal keyboard focus escapes to background focus launcher |
| UI-FLOW-01 | FAIL | After section 1/2, button says view results but opens section 2/2 |
| UI-PATH-08 | PASS | Submitting with a skipped question keeps submissions and explains insufficient evidence without claiming mastery |
| UI-UPLOAD-01 | PASS | Text file rejected by photo input with supported formats and 20MB limit |
| UI-PATH-09 | PASS | Completing all placement sections opens the route preview with both section results |
| UI-CONTENT-01 | FAIL | Quadratic-sign lesson reverses the sign rule |
| UI-GOAL-01 | PASS | Score 11 blocked by native max=10 validation |
| UI-GOAL-02 | PASS | Impossible 31/02 date disables preview |
| UI-GOAL-03 | PASS | Past date rejected with explicit earliest valid date |
| UI-GOAL-04 | FAIL | Auto study-time message contradicts same-day feasibility result |
| UI-GOAL-05 | PASS | Suggested date recovers from deadline warning; minimum slider remains 10 minutes; at least one study day is retained |
| UI-CUSTOM-01 | PASS | Customization blocks empty section scope and persists separate question counts in the preview |
| UI-DATE-01 | FAIL | Active route renders raw ISO time inside the target date |
| UI-STUDY-01 | PASS | Worked example reveals steps; written summary completes a study activity without claiming mastery; unavailable AI feedback gives a recoverable message |
| UI-ASSIGN-01 | PASS | Unknown search term produces no-match state; clearing restores list; empty exam list has contextual guidance |
| UI-TIMER-01 | PASS | Timed assignment hides question before explicit start; server timer starts at 59s and reload continues at 10s with selected answer and saved reasoning restored |
| UI-TIMER-02 | PASS | At 00:00 the saved draft auto-submits, recorded duration is 1 minute, selected answer and reasoning become read-only |
| UI-REVIEW-01 | PASS | AI outage settles to teacher review; no fabricated AI reasoning score; published teacher solution can be expanded |
| UI-REPORT-01 | PASS | Error report rejects whitespace, requires a type and sufficient detail; valid form enables send; cancel returns safely |
| UI-ASSIGN-02 | PASS | Manual submission identifies unanswered questions and blocks missing mandatory reasoning |
| UI-ASSIGN-03 | PASS | Previous/next navigation retains drafts; prior selected choice can be changed; final submission keeps both answers read-only with final answer 5 |
| UI-TWIN-01 | PASS | Competency filters show two unknown topics; history next page shows different dated records |
| UI-TWIN-02 | PASS | Goal rejects score 11 and horizon 0; valid 7.5 and 60 days saves visibly |
| UI-PRACTICE-01 | PASS | Existing active session disables creating another; resume keeps question progress |
| UI-PRACTICE-02 | PASS | Empty answer cannot submit; hint levels reveal guidance; correct roots accepted |
| UI-PRACTICE-03 | PASS | Pause freezes timer and resumes; wrong discriminant 100 shows correct 0 with a worked solution |
| UI-PRACTICE-04 | PASS | Cancel stop keeps session; confirm stop opens summary with 1/2 correct, 50% accuracy, one error and continuation choices |
| UI-PRACTICE-05 | FAIL | Unsubmitted adaptive written work disappears after pause, leave and resume |
| UI-NOTIFY-LOGIC-01 | FAIL | Notification claims AI processed the whole assignment although zero questions have AI analysis |
| UI-NOTIFY-02 | FAIL | Reversed notification date range appears as an inbox loading failure |
| UI-NOTIFY-01 | PASS | Opening a notification marks it read; related link preserves assignment, class and subject; marking visible unread notifications read reaches an explicit empty-unread state |
| UI-ACCOUNT-01 | PASS | Security profile belongs to student01, empty password form is invalid, Escape closes mobile modal and revoke text explains current session logout |
| UI-ACTIVITY-01 | PASS | Activity source/type filters show only own matching receipts; clear restores history |
| UI-NAV-03 | PASS | Returning from global inbox/journal restores Math subject and class context |
| UI-CARDS-LOGIC-01 | FAIL | Multiple-choice flashcard hides all choices but reveals only the answer letter |
| UI-CARDS-01 | PASS | Recall hides answer until reveal, repeat-later requeues the card and remembered completes with one card and no mastery claim |
| UI-DASH-LOGIC-01 | FAIL | No-evidence subject is assigned a zero prediction and zero risk |
| UI-RBAC-01 | PASS | Student direct navigation to teacher question bank redirects to no-permission page without exposing teacher content |
| UI-AUTH-04 | PASS | Unknown center gives generic credential error; whitespace around valid center and username is trimmed |
| UI-EXAM-01 | PASS | Future exam shows scheduled date/time, hides the stem and provides no start link; HTTP start is also rejected |
| UI-EXAM-02 | PASS | Submitted unpublished exam exposes only own answer, with no correctness, score or teacher solution |
| UI-EXAM-STATE-01 | FAIL | Unpublished exam summary presents hidden results as analysis still running |
| UI-EXAM-03 | PASS | Published exam shows official score 10, correct count and solutions; answer remains read-only. Final approval was seeded for visibility coverage only. |
| UI-REVIEW-01 | PASS | Review request rejects whitespace and fewer than 10 characters; valid synthetic request is submitted and pending state prevents duplicate UI action |
| UI-REVIEW-COPY-01 | FAIL | Genuinely teacher-reviewed and finalized result retains fallback paragraph saying teacher review is needed, and review modal calls it AI result despite no AI assessment |
| UI-AUTH-05 | PASS | Logout returns to login; browser Back cannot restore protected student content; subsequent login shows new account with zero assignments and no previous identity |
| UI-EMPTY-01 | PASS | New account has zero assignments, attempts and bookmarks; notebook explains missing curriculum instead of showing previous account records |
| UI-EMPTY-SCOPE-01 | FAIL | No-class account: dashboard says no subject/class, but route silently selects Math and permits placement; adaptive page instead says class archived or student left class |
| UI-ASSIGNMENT-ERROR-01 | PASS | Nonexistent assignment shows safe unavailable-or-no-access message with a scoped link back to assignment list |
