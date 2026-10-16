# Prompt giao Antigravity triển khai lộ trình học tập EduTwin V4.0

Sao chép toàn bộ nội dung trong khối dưới đây vào Antigravity, hoặc yêu cầu Antigravity đọc và thực hiện nguyên file này trong repository.

```text
BẠN ĐANG TRIỂN KHAI DỰ ÁN EDUTWIN.

AGENT ROLE: IMPLEMENTER — Full-stack, database, nội dung pilot và kiểm thử.
HUMAN OWNER: Người dùng/chủ dự án đang giao yêu cầu này.
TASK ID: EDUTWIN-LEARNING-PATH-V4.
REQUIREMENT IDS: V4-01 đến V4-38 trong đặc tả V4.0.
REPOSITORY: C:\AI-Driven-Adaptive-Learning-and-Competency-Assessment-Platform
EXPECTED BRANCH khi viết prompt: shin/studytask.
EXPECTED HEAD khi viết prompt: 46670e4b5bc12d22706ff219283209112a81f26f.
BASELINE: Đọc baseline môn học từ PROJECT_TRACKING.md; không tự gán HEAD hiện tại thành baseline môn học.

ĐẶC TẢ CHÍNH:
docs/plans/EDUTWIN-PERSONALIZED-LEARNING-PATH-V4.0.md

MỤC TIÊU VÀ PHẠM VI ĐƯỢC GIAO

Triển khai thực chất thiết kế V4.0 trong repository: mục tiêu cá nhân → xác định điểm xuất phát → preview tuyến → bản đồ năng lực → nhiệm vụ phù hợp thời gian → hoạt động học đa dạng → bằng chứng → điều chỉnh bước tiếp theo.

Người dùng đã cho phép thiết kế lại mạnh tay phân hệ lộ trình. Được thay UI, DTO, service điều phối, schema, migration và cơ chế cũ trong phạm vi V4 để đạt hành vi mới. Không bị giới hạn ở hai bảng hoặc lịch học cũ. Đây là yêu cầu triển khai, không phải chỉ lập kế hoạch, làm mockup hoặc viết báo cáo.

Hoàn thành phạm vi “bản đầu tiên” ở mục 20.1 của V4 theo các gate, đồng thời thực hiện các điều kiện an toàn dữ liệu, chuyển đổi và nghiệm thu liên quan. Các mục phát triển sau và chưa làm ở 20.2–20.3 không mặc nhiên thuộc lần này. Không chỉ chọn phần dễ nhất rồi tuyên bố V4 hoàn thành.

Đặc biệt: dùng kết quả từng câu của bài kiểm tra/bài tập hợp lệ để cập nhật nhận định theo Skill và điều chỉnh tuyến tương lai. Chỉ có điểm tổng thì không được suy ra em yếu kỹ năng cụ thể. AI không được tự ghi mastery; kết quả chờ chấm/duyệt không được coi là đã xác nhận.

1. ĐỌC NGỮ CẢNH VÀ CHỐT BASELINE TRƯỚC KHI SỬA

Đọc AGENTS.md áp dụng nếu có, sau đó đọc theo thứ tự:
1) PROJECT_REQUIREMENTS.md
2) CONSTITUTION.md
3) DATABASE_SCHEMA.md
4) API_CONTRACTS.md
5) UI_UX_SPEC.md
6) MASTER_PLAN.md
7) PROJECT_TRACKING.md và TEAM_ASSIGNMENT.md
8) CODEBASE_CHANGE_PLAN.md, nếu có phần liên quan
9) PROMPT_TEMPLATES.md
10) Toàn bộ đặc tả V4.0, không chỉ phần tóm tắt.

Yêu cầu trong prompt này và yêu cầu trực tiếp của người dùng xác định phạm vi thay đổi đã được giao. Những hạn chế cũ về hình thức lộ trình, giới hạn bảng, kiểu phiên hoặc việc cần lập Change Proposal trước mọi thay đổi không phải lý do xin duyệt lại các thay đổi đã nằm trong V4. Ghi quyết định và cập nhật tài liệu liên quan trước/cùng lúc triển khai. Giữ các invariant về tenant, ownership, quyền, evidence, lịch sử và yêu cầu giảng viên đã xác minh.

Nếu phát hiện xung đột thực sự với yêu cầu giảng viên đã xác minh hoặc cần phá dữ liệu để tiếp tục, nêu chính xác nguồn và phần bị ảnh hưởng; tiếp tục các phần độc lập. Không dùng xung đột chung chung để dừng cả nhiệm vụ.

Đọc git branch, HEAD, status, diff và ghi nhận baseline. Nếu HEAD/branch khác dữ liệu trong prompt, xem lịch sử/diff để hiểu thay đổi; không checkout/reset về SHA cũ. Nếu khác do công việc mới liên quan thì tiếp tục trên trạng thái hiện tại và ghi rõ. Chỉ cần làm rõ khi đó là repository/nhánh không liên quan hoặc có thay đổi đồng thời xung đột không thể xử lý an toàn.

Workspace đã có thay đổi chưa commit ở thời điểm giao việc:
- src/EduTwin.BLL/AssessmentAndReasoning/SubmitAttemptUseCase.cs
- src/EduTwin.BLL/Recommendations/Services/LearningSessionExecutionService.cs
- src/EduTwin.BLL/Recommendations/UseCases/GenerateLearningPathUseCase.cs
- src/EduTwin.BLL/Recommendations/UseCases/UpdateLearningPathSessionUseCase.cs
- web/edutwin-web/src/pages/LearningPlayerPage.tsx
- docs/plans/EDUTWIN-PERSONALIZED-LEARNING-PATH-V4.0.md là file mới.

Đây là thông tin tham chiếu, không phải danh sách đầy đủ thay cho git status thực tế. Đọc và bảo toàn thay đổi hiện có, tích hợp khi liên quan. Không reset, clean, checkout file để bỏ thay đổi hoặc ghi đè mù. Không tự nhận mọi diff cuối là do bạn viết.

Trước khi code, báo ngắn tám điểm theo PROMPT_TEMPLATES: vai trò/owner/task/requirements; branch/HEAD; dependency; phạm vi sửa; invariant; kiểm thử; điều kiện cần dừng; quyền Git. Sau đó tự tiếp tục triển khai, không chờ một lượt xác nhận thường lệ.

2. PHẠM VI FILE VÀ RANH GIỚI

Được sửa/thêm file liên quan trong:
- src/EduTwin.Contracts: contract/enums/DTO phục vụ V4.
- src/EduTwin.DAL: model/configuration/migration/context/seed liên quan V4.
- src/EduTwin.BLL: recommendations/planning/content/run và tích hợp assessment/evidence/review.
- src/EduTwin.API: routes/DI/adapters/authorization phục vụ V4.
- web/edutwin-web/src và tests: trải nghiệm học sinh, authoring giáo viên, API client, renderer và kiểm thử.
- tests: unit, relational integration và E2E liên quan.
- docs/plans, docs/decisions, docs/verification và các tài liệu requirement/schema/API/UI/tracking cần đồng bộ.
- scripts liên quan seed/verify V4 khi cần.

Không thay đổi nghiệp vụ platform admin, billing, vận hành hạ tầng, tài khoản/quyền ngoài phần tích hợp bắt buộc. Không mở quyền học thuật cho CenterManager/PlatformAdmin để tiện demo. Không tự sửa .env/credentials, đổi nhà cung cấp hoặc model AI runtime, cài dependency lớn không cần thiết hay chuyển sang microservice.

Nếu cần file tích hợp ngoài danh sách nhưng trực tiếp phục vụ V4, giải thích trong kế hoạch thay đổi và sửa tối thiểu. Không refactor toàn repository để làm đẹp.

3. KHẢO SÁT MÃ NGUỒN, KHÔNG TIN TÀI LIỆU LÀ CHỨC NĂNG ĐÃ CÓ

Đọc và đối chiếu ít nhất:
- GenerateLearningPathUseCase, GetActiveLearningPathUseCase, GetNextQuestionUseCase.
- LearningSessionExecutionService, UpdateLearningPathSessionUseCase, LearningPathSessionAccess.
- LearningPath, LearningPathSession, LearningPathSessionRun và EF mapping/migrations.
- AttemptSubmissionValidator, SubmitAttemptUseCase, Attempt mapping.
- Evidence Gate, AI worker/post-processing, TeacherApprove/TeacherOverride, Twin pipeline.
- StudentLearningPathPage, StudentDashboardPage, LearningPlayerPage, SessionEvaluationOutcomeModal.
- Các test tương ứng và cách cấu hình MySQL test hiện có.

Những rủi ro cần kiểm tra lại bằng code hiện tại:
- Submit tự đóng Passed theo tỷ lệ đúng tổng rồi evaluate không xét lại.
- Đánh giá cũ tìm Attempt bằng CreatedAt, cùng ghi trạng thái với bảng Session/Run.
- Materialize chỉ tạo một checkpoint và giữ ngưỡng 70/75%.
- Resolve Session thiếu kiểm tra Path owner/subject/class ở một số nhánh.
- Không có Twin bị chuyển thành mastery 0.
- Worker sau Attempt tự supersede/generate Path, phá tính liên tục của tuyến.
- Retry yêu cầu câu mới nhưng checkpoint pool chỉ có một câu.
- UI khẳng định Twin cập nhật/mở khóa khi response chưa có bằng chứng.

Không mặc định các rủi ro trên vẫn còn nếu code đã đổi; xác minh và ghi kết quả. Một lỗi test đã thấy trong lần kiểm tra trước: ArchivedLearningScopeTests.DetailedPathRead_RejectsOutOfScopeSavedSessions_AndNeverBackfillsOnGet. Kiểm tra lại; không xóa/chỉnh test để hợp thức hóa GET có side effect.

4. KẾ HOẠCH THỰC HIỆN BẮT BUỘC

Tạo docs/plans/EDUTWIN-V4-IMPLEMENTATION-PLAN.md từ code thực tế, gồm work package theo thứ tự, file scope, dependency, quyết định schema/API, AC và lệnh kiểm thử. Đây là kế hoạch thực thi; lập xong phải làm tiếp.

A. Nền execution và nguồn trạng thái
- Owner/scope xuyên Goal → Path → Mission → Run → Activity → IssuedItem/Attempt.
- Một evaluator có quyền quyết định outcome; submit lưu bài, exposure và tiến vị trí atomic.
- Run/ActivityRun bền vững, draft version, issued identity, start/submit/evaluate idempotent.
- Chốt concurrency bằng MySQL/giao dịch/index phù hợp, không chỉ AnyAsync rồi Insert.
- Tách Legacy/V4, ngăn route hoặc callback cũ ghi đè V4.
- Một nguồn thực thi; PlanJson chỉ snapshot/projection/legacy read.

B. Nội dung và chính sách
- Chọn chương pilot từ curriculum/question data đang có. Nếu dữ liệu chưa đủ, dùng chương phương trình phù hợp cấp lớp trong V4 làm bộ nội dung mẫu tách biệt.
- 6–8 Skill với prerequisite hợp lệ; không đổi node lớn thành Skill bằng cách đổi tên.
- Nội dung versioned, authoring + preview + publish theo quyền Teacher/publisher.
- Renderer có schema rõ, rubric, lỗi thường gặp, duration, mode và fallback.
- Không tự nhận nội dung AI soạn đã được giáo viên thật duyệt. Seed mẫu chỉ vào môi trường demo/test có nhãn rõ; nội dung thật giữ Draft tới khi người có quyền duyệt.
- Policy theo Skill và mặc định pilot mục 12.6/14.1; snapshot lúc bắt đầu; thiếu câu không hạ tiêu chí.

C. Planner và tuyến cá nhân
- Goal có cấu trúc, lịch rảnh, deadline tùy chọn và đầu ra.
- Projection kỹ năng từ evidence hiện hữu; Unknown riêng với năng lực thấp.
- Diagnostic ngắn có giới hạn, không bắt làm lại nếu đủ bằng chứng.
- Preview/accept, một tuyến Active theo scope V4, revision có lý do.
- Bản đồ/tuyến có đầu ra, eligibility, trạng thái và căn cứ.
- Chi tiết ngắn hạn, kiểm tra thời gian khả thi; đề xuất chính và tối đa hai lựa chọn hợp lệ.
- Kết quả bài tập/kiểm tra theo câu và kỹ năng phải có đường tác động thật tới lựa chọn nhiệm vụ.

D. Trải nghiệm Mission và Agenda
- Trang Hôm nay theo thời gian 5/15/30 phút và lịch rảnh, nghĩa vụ giáo viên, Run dở.
- Không gian Mission riêng, tái sử dụng input toán/scratchpad khi có ích.
- Ít nhất WorkedExample/CompleteSolution, FindError, IndependentCheck là ba trải nghiệm hoạt động thật; có Explore template hữu ích và TransferChallenge cuối chặng.
- Mỗi hoạt động có tương tác/feedback đúng mục đích, không chỉ đổi tiêu đề trang luyện tập.
- Resume khác thiết bị, pending grading/review, draft conflict và lỗi mạng có phục hồi.
- Kết quả tách hoạt động đã làm, outcome, năng lực đã chứng minh, điều chưa rõ và next action.

E. Điều chỉnh và tích hợp
- Đi nhanh bằng chứng minh, bổ trợ theo lỗi có căn cứ, điều chỉnh thời gian, recall review và sổ tay cơ bản.
- Không regenerate Path sau mỗi Attempt; không đổi nhiệm vụ đang làm âm thầm.
- Không đếm evidence hai lần khi Assignment và Path dùng cùng kết quả.
- Teacher review/override tới muộn dùng evaluator chung, không thua AI job cũ.
- Trợ giúp AI có ngữ cảnh qua abstraction runtime hiện có, fallback deterministic; server chặn trợ giúp không hợp lệ ở IndependentCheck.
- Ghi lý do/nguồn/policy/evidence reference để giải thích quyết định, không log secrets hoặc prompt học sinh không cần thiết.

F. Tích hợp và nghiệm thu kỹ thuật
- Hoàn thành E2E và regression, đồng bộ tài liệu, seed nội dung demo có thể tái tạo.
- Đối chiếu cả 38 AC, nêu đủ bằng chứng cho từng trạng thái.
- Không tự tuyên bố stakeholder acceptance hoặc hiệu quả học tập đã chứng minh. Pilot người thật là việc cần người dùng/giáo viên thực hiện, hãy chuẩn bị hướng dẫn và dữ liệu thu thập.

Đừng chờ người dùng duyệt từng quyết định triển khai thông thường. Khi cần giả định, chọn phương án hợp lý, ghi rõ trong decision log và tiếp tục. Nếu thiếu nội dung đã được giáo viên duyệt thì hoàn thiện kỹ thuật với demo/test tách biệt, đồng thời ghi rõ dependency nội dung trước phát hành.

5. RÀNG BUỘC CHẤT LƯỢNG VÀ DỮ LIỆU

- Giữ modular monolith, business logic ở BLL, không nhét planner/evaluator vào React hoặc Controller.
- Tenant từ auth/context; dữ liệu client gửi không được cấp quyền.
- Scope phải được kiểm tra lại ở start, issue, trợ giúp, draft, submit và evaluate.
- Không sửa/xóa Attempt cũ, không gắn FK bằng suy đoán thời gian.
- Không xóa bảng hoặc dữ liệu để làm migration chạy được; không reset/drop database đang dùng.
- Được chạy migration/test trong database kiểm thử tách biệt theo cơ chế repository. Không tự áp schema mới lên database chứa dữ liệu thật; chuẩn bị script, hướng dẫn và kế hoạch backup/rollback cho bước rollout cần thiết.
- Không tạo học sinh giả, kết quả giả hoặc trạng thái Passed hardcode trong luồng sản phẩm. Fixture/demo seed phải được phân biệt rõ.
- Không hạ trust gate, bỏ teacher review, bỏ reasoning rubric hoặc cho điểm mastery cố định để luồng demo trông thành công.
- Không dùng điểm tổng để gán đều mức hiểu biết cho mọi Skill.
- Không trả đáp án đúng/rubric bí mật qua DTO học sinh trước khi được phép.
- Content mới Published bất biến; invalidation/thu hồi phải được xử lý rõ.
- Không để AI/network unavailable chặn lưu bài, resume hoặc đọc/học nội dung Published.
- Không thay tiêu chí để khớp kết quả test. Nếu test cũ không còn đúng vì thay đổi được V4 cho phép, giải thích và thêm test hành vi thay thế bảo vệ invariant.
- Không gửi email/thông báo ngoài hệ thống, deploy, stage, commit hoặc push. Để working tree cho Codex review độc lập.

6. KIỂM THỬ PHẢI THỰC HIỆN

Trước sửa chạy baseline phù hợp, lưu kết quả để phân biệt lỗi có sẵn. Sau từng work package chạy test liên quan; cuối cùng chạy build/backend/frontend/regression thích hợp theo scripts thực tế.

Bắt buộc bao phủ:
1) Ba hồ sơ An/Bình/Chi hoặc fixture tương đương: cùng Goal nhưng tuyến/nhiệm vụ khác có lý do.
2) Cùng học sinh sau bài kiểm tra mới: thay đổi nhiệm vụ đúng Skill, không chỉ đổi lời giải thích.
3) Unknown, incomplete coverage, chỉ có điểm tổng, thời gian không đủ, nghỉ học.
4) Guided cao nhưng independent thấp; thiếu checkpoint; đã lộ lời giải; pending grading/review; teacher override tới muộn.
5) Start/submit/evaluate retry và cạnh tranh từ DbContext/connection độc lập trên MySQL.
6) Cross-student/cross-center/wrong-class/path/activity ID ở tất cả endpoint nhạy cảm.
7) Draft/resume browser context khác, mất response sau submit, draft conflict.
8) AI timeout/schema sai/out-of-allowlist và trợ giúp bị chặn server-side trong IndependentCheck.
9) Câu retire, nội dung thiếu, retry cạn câu và content version cũ.
10) Legacy data/route/worker, bảo toàn evidence và không regeneration ngoài luật.
11) Mobile và thao tác bàn phím với các activity cốt lõi.
12) GET không có side effect và UI không báo Twin delta/mở khóa khi chưa có kết quả commit.

EF InMemory không chứng minh FK, unique index hay concurrency MySQL. Test chỉ so khớp chuỗi source không thay cho test hành vi. Không có môi trường relational/browser thì ghi NOT RUN với lý do, vẫn thực hiện phần kiểm thử độc lập còn làm được; không đổi thành PASS.

Không dựng luồng E2E bằng cách gọi thẳng hàm evaluate trên các Attempt seed rồi gọi đó là submit → grade → gate → Twin → next action. Cần ít nhất một kiểm thử đi qua các integration thực tế.

Không lặp vô hạn cùng command/fix. Khi vấn đề môi trường không tiến triển, ghi bằng chứng, chuyển sang phần độc lập và để lại hướng tái hiện cụ thể.

7. BÀN GIAO ĐỂ CODEX REVIEW ĐỘC LẬP

Tạo/cập nhật:
- docs/plans/EDUTWIN-V4-IMPLEMENTATION-PLAN.md
- docs/decisions/EDUTWIN-V4-IMPLEMENTATION-DECISIONS.md
- docs/verification/EDUTWIN-V4-IMPLEMENTATION-REPORT.md

Báo cáo bàn giao phải có:
- Branch/HEAD đầu và cuối, dirty baseline, phạm vi thay đổi có sẵn bạn đã tích hợp.
- Những hành vi đã chạy được và cách tái hiện bằng UI/API.
- Bảng đủ V4-01…V4-38: Implementation status, Verification status, file/symbol, test/lệnh, kết quả, giới hạn. Tách IMPLEMENTED/VERIFIED/NOT RUN/BLOCKED/PARTIAL; không gộp “có code” thành “đã nghiệm thu”.
- Danh sách migration, schema/API thay đổi, nội dung demo và content còn chờ giáo viên duyệt.
- Kiến trúc nguồn trạng thái, evaluator, quyền, idempotency và legacy boundary sau cùng.
- Lệnh kiểm thử thực tế, số pass/fail/skip, lỗi baseline và lỗi mới; không bịa log.
- Hướng dẫn khởi chạy/seed/demo bằng dữ liệu mẫu không nhạy cảm; không in password/token vào tài liệu được commit.
- Đường review kỹ thuật đặc biệt: grade → evidence → Twin → planner; một Run được resume; một teacher review tới muộn; một kết quả làm thay đổi tuyến.
- Các phần chưa làm, dependency môi trường/người thật và rủi ro trước rollout.

Trong trả lời cuối: mô tả ngắn thay đổi thực tế, kết quả kiểm thử, file báo cáo và những việc còn thiếu. Không tự APPROVED hoặc tuyên bố V4 hoàn thành nếu AC cốt lõi chưa đạt.

Nếu hết context/quota, lưu checkpoint vào báo cáo, ghi việc đã xong/đang dở/lệnh tiếp theo. Khi tiếp tục phải đọc lại trạng thái Git và checkpoint; không làm lại từ đầu, không bỏ các phần khó trong im lặng.

BẮT ĐẦU NGAY:
Đọc ngữ cảnh, ghi baseline, trình bày ngắn phạm vi/kế hoạch/test rồi tiến hành triển khai theo thứ tự. Giữ thông báo tiến độ có ý nghĩa và tiếp tục đến khi phạm vi bản đầu tiên được hoàn thành hoặc còn blocker cụ thể đã được ghi nhận và không thể xử lý bằng phần việc độc lập khác.
```
