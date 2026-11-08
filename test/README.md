# Kiểm thử validation, sanitization và prompt AI

Thư mục `test/` chứa hai bộ chạy độc lập:

- **Cypress**: kiểm tra ứng dụng/API thật, dữ liệu không hợp lệ, phân quyền, tenant/ownership, nội dung nguy hiểm và cách trình duyệt hiển thị dữ liệu đã lưu.
- **AI prompt evaluation**: gọi trực tiếp model bằng API key do developer cung cấp, dùng lại mã dựng prompt và kiểm tra đầu ra hiện tại. Không cần chạy ứng dụng hay database.

Không thay đổi mã nghiệp vụ của ứng dụng. Các kiểm thử có thể phát hiện lỗi hiện hữu; một response `500`, test bị bỏ qua hoặc thiếu fixture đều không được tính là đạt.

Khi thay đổi giao diện, đọc [lưu ý cho developer UI](UI_CHANGE_GUIDE.md) về `data-cy`, screen helper và cách giữ các kiểm tra nghiệp vụ/security.

Spec được chia thành `cypress/e2e/api/` và `cypress/e2e/ui/`. Payload tổng hợp nằm trong `cypress/fixtures/`, selector/thao tác UI trong `cypress/screens/`, assertion dùng chung trong `cypress/support/`. Hiện có 23 spec với 353 case: 187 API và 166 UI. Trong đó, 159 case mới ở `ui/validation/` mở trang/modal và nhập, chọn, bấm trên giao diện thật. Xem [ma trận validation UI](UI_VALIDATION_COVERAGE.md) để biết từng nhóm chức năng, field và giới hạn coverage.

## 1. Cài đặt Cypress

Cần Node.js 22/24/26 phù hợp yêu cầu Cypress 16, Docker Desktop đang chạy, và cấu hình `.env` local của repository.
Package Cypress được pin riêng trong `test/package.json` và `test/package-lock.json`; không thêm dependency vào frontend.

```powershell
cd test
npm ci
npm run test:unit
cd ..
```

Các test Node kiểm tra cơ chế từ chối môi trường không an toàn, quyền sở hữu tài nguyên test, dọn dẹp khi trình duyệt lỗi và việc từ chối một lần chạy không thực thi test nào.

## 2. Chạy E2E trên môi trường riêng

Từ repository root, build đúng source cần kiểm tra:

```powershell
docker compose build api web
docker compose up -d mysql
cd test
npm run e2e
```

Runner sử dụng `scripts/e2e/acceptance_stack.cjs`:

- API/Web riêng tại `http://127.0.0.1:5001` và `http://127.0.0.1:3002`.
- Tạo schema/user MySQL `edutwin_e2e_<runId>`; chỉ dùng MySQL local làm host chứa schema test.
- Mật khẩu và JWT riêng cho tài khoản tổng hợp; không đăng nhập bằng tài khoản dữ liệu thật.
- Xác minh manifest, tên schema, origin và nhãn owner Docker trước khi chạy.
- Dọn hai container và schema do lần chạy này tạo, kể cả khi Cypress lỗi.
- Không cấp Gemini key cho acceptance stack; các test xử lý bất đồng bộ kiểm tra luồng fallback. Chấm AI thật thuộc bộ chạy ở mục 4.
- Worker thông báo vẫn chạy thật; chỉ đặt chu kỳ poll 1 giây trong runtime test để xử lý các sự kiện fixture trước khi mở inbox.

Harness hiện tại dùng network `edutwin_default` và image `edutwin-api:latest` / `edutwin-web:latest`. Không chạy trên image cũ để kết luận về source mới.
Nếu đã tồn tại manifest acceptance, runner dừng để tránh ghi đè tài nguyên của lần chạy khác. Kiểm tra và phục hồi bằng harness hiện có trước khi chạy lại.

Chọn một spec hoặc trình duyệt:

```powershell
npm run e2e:api
npm run e2e:ui
npm run e2e:ui:validation -- --browser chrome --headed
npm run e2e -- --spec cypress/e2e/ui/01-authentication.cy.js
npm run e2e -- --browser chrome --headed
```

Để giữ môi trường cho debug, khởi tạo từ repository root:

```powershell
node scripts/e2e/acceptance_stack.cjs start
cd test
npm run e2e:existing
# Hoặc mở Cypress tương tác: npm run e2e:open
cd ..
node scripts/e2e/acceptance_stack.cjs stop
```

`--reuse-stack` không dọn môi trường đã tồn tại. Chỉ chạy một bộ E2E tại một thời điểm vì các test cùng dùng fixture seed và một manifest.
Không đổi `baseUrl` sang runtime chính: config sẽ từ chối.

## 3. Phạm vi và bằng chứng Cypress

| Phạm vi | Kiểm tra |
|---|---|
| Toàn bộ protected HTTP surface | Catalog lấy từ controller hiện tại; request chưa xác thực phải bị từ chối trước nghiệp vụ. Route không hiểu được làm catalog lỗi, tránh âm thầm bỏ sót. |
| Đăng nhập | Trường bắt buộc, độ dài, whitespace, identity injection, đăng nhập hợp lệ và browser required fields. |
| Tổ chức | Giáo viên/học sinh/lớp: giới hạn dữ liệu, khối lớp, tenant giả, duplicate và kiểm tra dữ liệu đã lưu. |
| RBAC và platform | Account type, quyền quản trị, tenant/ownership, dữ liệu role và concurrency. |
| Nội dung học thuật | Subject, graph, curriculum, loại câu hỏi/chế độ đáp án, rubric và positive controls. |
| Giao/nộp bài | Deadline, thời lượng, target/question IDs, answer telemetry, versioned draft, nộp lại, worker terminal state và ownership. |
| Import/ảnh | CSV preview/confirm, actor/revision binding, nội dung file sai, DOCX hỏng, PNG giả và upload sai account type. |
| Thông báo | Chính sách nội dung, preview token, sửa nội dung sau preview, audience và quyền gửi. |
| Học thích ứng | Các tham số phiên luyện tập và mục tiêu/lộ trình; thiếu schema là lỗi điều kiện chạy. |
| Trình duyệt | Đường dẫn không đủ quyền và hiển thị HTML/KaTeX độc hại trong dữ liệu đã lưu. |
| Validation trên UI thật | Form tổ chức, tài khoản, phân quyền, platform, soạn nội dung, giao/chấm/làm bài, mục tiêu/lộ trình, luyện tập và thông báo: required, giới hạn chuỗi/số, mật khẩu, file picker, lỗi trả về và trạng thái nút. [Ma trận chi tiết](UI_VALIDATION_COVERAGE.md). |

Các case cụ thể nằm trong `cypress/e2e/`. Kiểm tra auth toàn bộ route không thay thế validation từng field; đây là ma trận các biên nghiệp vụ trọng tâm, không phải chứng minh mọi tổ hợp input đều an toàn.
Một số trường được phép lưu HTML dưới dạng **văn bản**. Test kiểm tra không thực thi script khi hiển thị, không mặc định đòi server phải xóa mọi dấu `<`/`>`.

Kết quả local:

- `test/results/cypress/summary.json`: số test và trạng thái từng test/spec, không dump credential/request/response.
- `test/results/cypress/screenshots/`: ảnh khi lỗi; chỉ dùng dữ liệu tổng hợp.
- CLI trả nonzero nếu có failure, pending, skipped, lỗi config hoặc không thực thi test.

`summary.json` luôn là lần chạy gần nhất, kể cả khi chỉ chọn một spec. Xem [kết quả kiểm chứng](VERIFICATION.md) để phân biệt lượt toàn bộ với lượt kiểm tra bổ sung.

Không tắt assertion để biến validation đang hỏng thành PASS. Ghi nhận kết quả rồi sửa nghiệp vụ trong một task riêng.

## 4. Đánh giá prompt AI độc lập

Cần **.NET SDK 10**. Chạy từ repository root:

```powershell
./test/scripts/run-ai-prompts.ps1 -Mode SelfTest
./test/scripts/run-ai-prompts.ps1 -Mode Prepare
./test/scripts/run-ai-prompts.ps1 -Mode Live -Model '<model-dang-trien-khai>' -PromptForApiKey
```

`SelfTest` kiểm chứng bằng dữ liệu trong bộ nhớ. `Prepare` xuất input đã kiểm tra mà không gọi model và không tạo output giả.
`Live` mới gọi provider thật; API key nhập ẩn hoặc lấy từ environment do developer cấp. Script không tự đọc `.env` và không nhận key trên command line.

Xem [hướng dẫn AI chi tiết](ai-prompts/README.md) để chọn provider, dùng biến môi trường trong CI, thêm fixture, chỉnh giới hạn và gọi CLI đa nền tảng.

**File live chỉ gồm các trường `parameters`, `prompt`, `output`**:

- `parameters`: dữ liệu/thiết lập câu hỏi, rubric và telemetry mà giao diện/client giáo viên, học sinh có thể cung cấp.
- `prompt`: đề, lời nhập và bài làm/ảnh do hai vai trò cung cấp. Prompt hệ thống thực tế được dựng bằng code hiện tại nhưng không được chép vào artifact này.
- `output`: phản hồi thực tế của model, kể cả đầu ra không đạt contract; không sửa thành kết quả giả/fallback.

Không xuất API key, system instructions, schema máy chủ, synthetic IDs, điểm sơ bộ nội bộ, repair hints, quota hay telemetry vận hành.
Oracle/expected observations nằm riêng trong `ai-prompts/expectations.json`; không gửi oracle cho model và không chép oracle vào file kết quả.

Kết quả nằm mặc định ở `test/ai-prompts/results/` và được Git bỏ qua. Một lần gọi provider lỗi trước khi có output không được thêm thành một câu trả lời AI giả.
Không dùng số test PASS để thay thế đánh giá nội dung chuyên môn của giáo viên/developer.

## 5. Tài liệu kỹ thuật

- [Cypress configuration](https://docs.cypress.io/app/references/configuration)
- [Cypress environment variables](https://docs.cypress.io/app/guides/environment-variables)
- [Quy trình acceptance hiện có](../scripts/e2e/README.md)
