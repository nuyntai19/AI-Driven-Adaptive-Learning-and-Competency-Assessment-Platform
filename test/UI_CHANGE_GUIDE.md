# Lưu ý khi thay đổi giao diện và bảo trì Cypress

Tài liệu này dành cho developer sửa React component, bố cục, điều hướng hoặc luồng thao tác. Mục tiêu là giữ test theo hành vi người dùng và contract nghiệp vụ khi giao diện được thiết kế lại. Giữ tên component không bảo đảm giữ được test: Cypress thao tác trên DOM đã render.

## Cấu trúc và trách nhiệm

```text
test/cypress/
├── e2e/api/       # Validation, quyền, tenant, lưu dữ liệu, xử lý nghiệp vụ qua API thật
├── e2e/ui/        # Đăng nhập trình duyệt, điều hướng, hiển thị dữ liệu và chống XSS
│   └── validation/ # Mở trang/modal thật, nhập/chọn/bấm và kiểm tra input không hợp lệ
├── screens/       # Selector, thao tác UI và routes.js cho điều hướng
├── fixtures/      # builders.js: payload/ID tổng hợp cho database test
├── support/       # Commands, assertion API/security và multipart transport
└── plugins/       # Node tasks, xác minh owner, truy cập database test, báo cáo
```

[Ma trận validation UI](UI_VALIDATION_COVERAGE.md) ghi rõ component đang dùng, quy tắc có case và phần chưa được bao phủ. `fixtures/ui-validation-cases.js` khai báo các field required/giới hạn cho form tổ chức và tài khoản; `support/register-visible-forms.js` tạo scenario, còn `screens/forms.screen.js` chứa cách mở form và tìm field. Khi đổi quy tắc nghiệp vụ, cập nhật khai báo và kỳ vọng tương ứng, không chỉ đổi selector để test chạy qua.

Assertion về kết quả nghiệp vụ nằm trong spec: HTTP status, error code, URL, dữ liệu hiển thị, dữ liệu không được lưu. Helper `screens/` chỉ cung cấp thao tác và cách tìm phần tử; không tự bỏ qua bước nếu thiếu phần tử hoặc nuốt lỗi để test tiếp tục chạy. `support/browser-safety.js` giữ assertion kiểm tra DOM nguy hiểm dùng chung.

Các test API import payload từ `fixtures/builders.js`, không lấy dữ liệu bằng selector UI. Import/upload có thể dùng browser `fetch` để gửi multipart cùng origin; đây vẫn là kiểm tra API, không phụ thuộc bố cục trang đăng nhập dùng để mở origin. Tài khoản tổng hợp/JWT/mật khẩu vẫn lấy từ Node task đã kiểm tra owner, không chuyển vào fixture public hay selector.

## Quy ước data-cy

Đặt `data-cy` trên phần tử có chức năng được test. Tên mô tả mục đích, chẳng hạn `login-submit`; tránh vị trí như `left-button-2`, màu sắc, tên CSS hoặc tên component. Giữ thuộc tính khi thay kiểu hiển thị, chia/tách component hoặc đổi nội dung nhãn mà chức năng vẫn giữ nguyên.

```tsx
<input data-cy="login-username" aria-label="Tên đăng nhập" />
<button data-cy="login-submit" type="submit">Đăng nhập</button>
```

`data-cy` bổ sung hook kiểm thử, không thay thế `label`, `aria-*`, `id`, `required`, keyboard interaction hoặc kiểm tra phân quyền. Không dùng thuộc tính này để điều khiển nghiệp vụ, CSS hay đưa thông tin nhạy cảm ra DOM.

| Hook đang dùng | Vị trí / ý nghĩa |
|---|---|
| `login-center-code` | Ô mã trung tâm trong `LoginPage` |
| `login-username` | Ô tên đăng nhập |
| `login-password` | Ô mật khẩu |
| `login-submit` | Nút gửi form đăng nhập |
| `student-workspace` | Vùng nội dung layout học sinh sau đăng nhập |
| `teacher-question-text` | Bề mặt nhập nội dung đề bài trong editor giáo viên |
| `manager-class-name` | Tên lớp hiển thị trong danh sách quản lý |
| `student-assignment-title` | Tiêu đề bài tập trong danh sách học sinh |
| `question-topic`, `question-topic-option` | Mở và chọn chủ đề thật trong KnowledgeTopicPicker |
| `grading-score`, `grading-feedback`, `grading-reason` | Các trường trong form chấm bài |
| `notification-reply`, `notification-retract-reason` | Nội dung phản hồi và lý do thu hồi thông báo |
| `goal-skill-${skillId}` | Checkbox kỹ năng, kể cả nhóm chỉ có một kỹ năng được hiển thị gộp |

Đây là các ví dụ; toàn bộ hook được dùng nằm trong `screens/` và các spec validation. Với form đã có `id` ổn định, nhiều hook giữ cùng giá trị để dễ tra cứu.

Các thông báo lỗi kỳ vọng của trường quá ngắn nằm trong `shortErrors` của `fixtures/ui-validation-cases.js`. Cập nhật chúng cùng quy tắc validation khi sản phẩm đổi nội dung lỗi hoặc giới hạn field.

Hook đơn như `login-submit` chỉ có một phần tử tương ứng đang hiển thị trên trang; đừng đặt trên cả bản desktop và mobile đang ẩn rồi dùng `.first()` để né lỗi. Hook của danh sách như `manager-class-name` được lặp theo item; helper tìm item bằng nội dung fixture riêng. Không gắn cả hàng vào hook tên lớp, vì test cần xác nhận chính vùng tên hiển thị dữ liệu đúng.

Component bọc phải truyền hook xuống đúng DOM thao tác. Ví dụ `RichMathEditor` nhận `dataCy="teacher-question-text"` và truyền xuống `contenteditable` qua `data-cy={dataCy}`. Khi thay editor, cập nhật adapter nếu cách nhập thay đổi, đồng thời giữ kiểm tra nội dung được hydrate và công thức thực sự được render.

## Selector và thao tác tập trung trong screens

```javascript
// screens/login.screen.js
const loginScreen = {
  username: () => cy.get('[data-cy="login-username"]'),
  submit: () => cy.get('[data-cy="login-submit"]').click(),
};

// Trong spec: assertion về hành vi vẫn rõ ràng
loginScreen.username().clear().type('student01');
loginScreen.submit();
studentScreen.workspace().should('be.visible');
```

Giữ selector UI trong `screens/*.screen.js`; tránh rải selector theo class, tag, thứ tự `nth-child` hoặc `.first()` trong spec. Dùng `screens/routes.js` khi điều hướng UI thay đổi; đường dẫn API ở spec API vẫn được kiểm tra trực tiếp như một phần contract.

Các selector trong assertion bảo mật là ngoại lệ có chủ đích: `img[src="cypress-xss"]`, `script` và `href^="javascript:"` kiểm tra DOM thực thi, không phải bố cục. Không đổi chúng thành việc chỉ kiểm tra có `data-cy`. Selector `.katex` trong helper editor xác nhận công thức hợp lệ thực sự được render; thay thư viện toán cần thay assertion tương đương, không xóa kiểm tra.

## Khi sửa UI

1. Xác định hành vi có đổi hay chỉ bố cục/CSS. Nếu hành vi giữ nguyên, giữ hook và các assertion nghiệp vụ hiện có.
2. Nếu chuyển phần tử sang component khác, truyền hook xuống DOM đúng chức năng; nếu đổi điều hướng, cập nhật `routes.js` và kỳ vọng theo thiết kế mới.
3. Nếu thêm bước bắt buộc như xác nhận gửi, cập nhật thao tác trong screen helper và spec tương ứng. Thay đổi contract API phải cập nhật cả spec API và tài liệu contract.
4. Với dữ liệu người dùng, giữ test nội dung đã lưu và kiểm tra sau reload; không chỉ kiểm tra một chuỗi render tĩnh hoặc mock response thành công.
5. Chạy build và Cypress với image mới. Không tăng timeout tùy tiện, thêm `cy.wait(5000)`, bỏ assertion, dùng skip hoặc bỏ qua exception để làm test xanh.

Từ repository root:

```powershell
docker compose build api web
docker compose up -d mysql
cd test
npm run test:unit
npm run e2e:ui
# Quan sát phần validation nhập/chọn/bấm:
npm run e2e:ui:validation -- --browser chrome --headed
# Chạy toàn bộ trước khi bàn giao/merge:
npm run e2e
```

Chạy API riêng hoặc chọn một UI spec:

```powershell
npm run e2e:api
npm run e2e -- --spec cypress/e2e/ui/01-authentication.cy.js
```

Mỗi lệnh runner mặc định tự tạo và dọn stack riêng. Không chạy chúng đồng thời vì cùng dùng owner manifest/port. Không đổi `baseUrl` sang ứng dụng/database chính. Không đưa API key AI vào Cypress; đánh giá model thật thuộc runner riêng trong `ai-prompts/`.

Các case UI mới không stub response thành công. Fixture API chỉ chuẩn bị điều kiện như graph Draft, bài đã nộp hoặc thông báo đã được worker giao; thao tác cần kiểm tra vẫn thực hiện trên trình duyệt. Với input số controlled của React, chọn toàn bộ giá trị rồi nhập giá trị mới và kiểm tra `have.value`; `.clear().type('4')` có thể thành `40` nếu component khôi phục `0`. Với `minlength`, kiểm tra lỗi người dùng nhìn thấy vì render lại có thể làm mất cờ `validity.tooShort`. Chỉ dùng `force: true` để chọn file qua input ẩn theo thiết kế file picker.

Khi bàn giao, ghi rõ hook/hành vi đã thay đổi, helper/spec đã cập nhật và kết quả đã chạy. `results/cypress/summary.json` là lần chạy gần nhất, có thể chỉ là một spec; ghi nhận lượt toàn bộ trong [VERIFICATION.md](VERIFICATION.md) và không tính pending/skipped là đạt. Hướng dẫn cài đặt đầy đủ nằm trong [README.md](README.md).
