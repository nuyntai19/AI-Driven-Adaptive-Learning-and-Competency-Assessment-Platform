# Chấm bản vẽ hình học — bản sửa và kiểm chứng

> Cập nhật 10/10/2026: sau khi người dùng đồng ý tradeoff, đã triển khai bước đọc ảnh độc lập. Trạng thái “chưa triển khai” bên dưới là lịch sử ngày 09/10; xem [báo cáo Test 8/9](GEOMETRY-INDEPENDENT-VISION-TEST8-9-2026-10-10.md) cho code và kết quả mới. Kết quả Test 5–8 không bị sửa thủ công.

## Phạm vi

Sửa trường hợp Test 5 cho trọn 10/10 dù ảnh thiếu tên M, ô đánh dấu góc vuông và ký hiệu BM=MC. Giữ nguyên câu #20069, bài làm/ảnh Test 5 và thao tác duyệt của người dùng. Không stage, commit hay push; không đổi API key/model/quota/concurrency, không migration/reset database.

Thao tác kiểm thử qua Chrome dùng skill computer-use; đọc SQL/log chỉ để đối chiếu. Không duyệt điểm thay giáo viên. Câu tự luận Manual vẫn được AI chấm đề xuất và giáo viên chốt.

## Những thay đổi đã triển khai

1. Giáo viên nhập **Yêu cầu ảnh nháp** riêng cho từng tiêu chí rubric (mỗi dòng một mục). Đây là mục tiêu do giáo viên xác định, không đoán từ tên câu hay tự suy ra bằng từ khóa. Câu cũ không có trường này vẫn đọc được.
2. AI phải trả một quan sát cho từng mục: `Present`, `Missing` hoặc `Unclear`, kèm tiêu chí, chỉ số yêu cầu, ảnh nháp nguồn và giải thích tiếng Việt. Thứ tự ảnh của từng câu được giữ riêng trong batch; chỉ số nguồn không trỏ sang ảnh đề/câu khác.
3. Server kiểm tra đủ mục, không trùng, ID/phạm vi ảnh hợp lệ. Thiếu/không rõ một mục thì không thể cho trọn tiêu chí ảnh; không có mục nào nhìn thấy thì tiêu chí ảnh bằng 0. Điểm tính toán được chấm độc lập. Tổng điểm rubric do server tính, UI quy đổi thang 10.
4. Prompt phân biệt hình hỗ trợ tùy chọn với mục tiêu bắt buộc vẽ/ghi tên/đánh dấu. Không lấy lời đề hoặc lời tự khai làm bằng chứng nhìn thấy. Không coi chữ ghi tên điểm, nền lưới hoặc hai cạnh gặp nhau là ký hiệu góc vuông. Không yêu cầu bố trí A/B/C theo một hướng sách giáo khoa cố định.
5. Thiếu ký hiệu hình được phản ánh bằng quan sát thị giác và tiêu chí ảnh; không tự gộp thành lỗi suy luận khi lập luận toán học đúng. Chốt kiểm tra lập luận mâu thuẫn vẫn giữ nguyên, không nới lỏng để cho kết quả qua.
6. Lưu quan sát thị giác cùng JSON điểm rubric và hiển thị cho học sinh/giáo viên. Không sửa lại các điểm lịch sử. Profile phân tích chuyển sang `visual-evidence-v5` để không dùng lại checkpoint của hợp đồng cũ khi xử lý mới.
7. Kiểm tra ảnh tải từ storage bằng số lượng, MIME và toàn bộ bytes. Adapter chỉ hỗ trợ chữ không được âm thầm bỏ ảnh. Thêm log số ảnh, vai trò, số byte và SHA256; không log key, base64 hoặc toàn bộ bài làm. Kiểm thử SDK bắt HTTP bằng transport giả lập chứng minh bytes được đóng gói đúng, không mở mạng.
8. Thêm **Đính kèm PNG có sẵn** để thử lại cùng ảnh một cách chính xác. Giữ nguyên bytes tệp, không nén hay vẽ lại; giới hạn PNG/5 MiB/4096 px, server tiếp tục kiểm tra PNG đầy đủ. Import không thay đổi nét trên canvas và không xuất hiện ở bài đã nộp chỉ đọc.

## Sự cố phát hiện trong kiểm thử và cách xử lý

### Schema Gemini HTTP 400

Lượt đầu Test 6 gửi đúng ảnh nhưng bị provider từ chối trước khi chấm. Schema mới có `visualEvidence.maxItems=240`. Hai request chẩn đoán bằng nội dung mẫu, khác nhau duy nhất ở giới hạn này: có giới hạn trả HTTP 400 `INVALID_ARGUMENT`, bỏ giới hạn trả HTTP 200. Không gửi bài học sinh trong hai probe schema.

Đã bỏ **chỉ** giới hạn lớn ở schema provider để tránh nở grammar; strict parser vẫn chặn quá 240 quan sát, validator vẫn yêu cầu đúng số mục giáo viên nhập. Có kiểm thử riêng cho schema single/batch, serialization SDK và chốt giới hạn server. Không coi lỗi payload 400 là hết quota hay xoay hết key để chữa.

[Tài liệu structured outputs của Google](https://ai.google.dev/gemini-api/docs/structured-output) cũng lưu ý schema lớn/phức tạp có thể bị từ chối và đúng cấu trúc không đảm bảo đúng ngữ nghĩa. Nguyên nhân của lượt này được đối chứng bằng request mẫu, không chỉ suy luận từ tài liệu.

### Kết luận thị giác và điểm chưa đúng sau khi bỏ lỗi schema

Test 6 chấm lại thành công, cho 7/10, nhưng **chưa đạt**: nhận ra thiếu M/BM=MC, đồng thời bịa dấu góc vuông ở C và nói nhãn B/C sai hướng; phần tính toán 3/4 dù nhận xét hoàn toàn đúng. Không duyệt kết quả này.

Một probe đọc riêng đúng PNG, không kèm đáp án mẫu/lời học sinh, nhận ra không có ký hiệu góc vuông hay độ dài. Đây là đối chứng chẩn đoán, **không** phải một bước gọi AI mới được áp dụng cho mọi bài. Đã làm rõ quy tắc nhãn chữ/ký hiệu, hướng hình và chấm phần tính toán độc lập trong prompt production, giữ nguyên model.

Test 7 lần đầu bị chặn `ReasoningConsistency` cả lượt đầu và bounded repair. Do log cũ chỉ có tên quy tắc chung, không lưu raw response, chưa truy hồi được tiểu điều kiện đã vi phạm. Đã bổ sung token chẩn đoán do server định nghĩa (không chứa nội dung provider) và quy tắc rõ về thiếu dấu hình khác với thiếu bước suy luận. Không thay điểm/fallback bằng tay.

## Dữ liệu thử và bằng chứng ảnh

- PNG gốc: `storage/verification/test5-original-sketch.png`, 1200×800, 84.064 byte.
- SHA256: `5E1C3F74E1588B64C069D29615F4C5DDFC5AB1BB16C116A2DDE1ED367CDBF11D`.
- Hình có tam giác ABC, A trái trên, B trái dưới, C phải trên, đoạn từ A tới BC; thiếu tên M, ô góc vuông và dấu hai đoạn bằng nhau. Chữ C góc cạnh ngoài đỉnh không phải dấu góc vuông.
- Đáp án: `BC = 10 cm; AM = 5 cm.` Lập luận Pythagore và trung tuyến tới cạnh huyền đúng. Không thêm lời tự nhận “đủ ký hiệu”.
- Câu clone #20070: rubric 4 điểm hình dựng, 2 điểm tên/ký hiệu, 4 điểm tính toán; thêm 2 mục ảnh hình dựng và 4 mục ảnh ký hiệu. Không thay câu gốc #20069.
- Test 6: assignment `40d4a298-654f-4642-b998-f7463b17ec36`, attempt 213. Chỉ student01.
- Test 7: assignment `6fc8d710-dade-4c77-9997-36e7fd9468ba`, attempt 214. Cùng câu #20070, ảnh và lời giải; chỉ student01. Tạo mới vì sản phẩm không cho chấm lại AI một job đã thành công, kể cả giáo viên. Không bypass chốt này hoặc reset job SQL.
- Log manifest/adapter Test 6 và Test 7 có đúng ảnh vai trò Student và hash gốc. Đối chứng SDK offline xác nhận data không bị bỏ ở serialization. Không lưu payload mạng thật/base64.

Ảnh kiểm chứng: `storage/verification/test6-original-image-attached.jpg`, `test7-published.jpg`, `test7-visual-evidence-still-inconsistent.jpg`. Ảnh cuối thể hiện 8,5/10 chưa phải kết quả đạt, với trạng thái “Đã thấy” nhưng nội dung nói thiếu chữ M. Tab kết quả Test 7 được giữ để người dùng kiểm tra; `reviewed_at=NULL`.

## Thời gian các lượt đã đo

| Lượt | Kết quả | Thời gian |
| --- | --- | --- |
| Test 6 đầu, 15:27:31 UTC | Fallback, HTTP 400, 2 request do retry_count=1 | Nhận bài → terminal 11,68 giây; không phải thời gian chấm thành công |
| Test 6 chấm lại, bấm 15:38:54,520 UTC | AI 7/10, không fallback, chưa đạt nội dung | Provider 5,65 giây; worker 6,62 giây; quan sát UI sau 23,02 giây là mốc đọc, không phải latency provider |
| Test 7 đầu, nhận 15:52:20 UTC | Phản hồi không nhất quán, bounded repair cũng bị chặn | Provider hai lượt 5,78 và 4,62 giây |
| Test 7 chấm lại, bấm 15:58:07,620 UTC | AI 8,5/10, không fallback, chưa đạt hoàn toàn nội dung | Provider 7,79 giây; worker 8,78 giây; server nhận retry → terminal 10,70 giây; click → terminal khoảng 12,64 giây |

**Chưa kết luận chấm hình đạt.** Test 7 sau sửa cuối có hình 4/4, ký hiệu 0,5/2, tính toán đúng 4/4. Đã nhận đúng thiếu dấu góc vuông/BM=MC, nhưng quan sát yêu cầu nhãn M vẫn có `status=Present` trong khi mô tả nói thiếu M; còn nhận xét B/C không đúng vị trí “thông thường” dù hình xoay/lật được phép. Không duyệt điểm này. Đây là lỗi ngữ nghĩa model, không được coi 8,5/10 là bằng chứng bản sửa đã hoàn tất.

Không dùng thời gian từ `job.created_at` cũ đến `completed_at` sau chấm lại để tính latency; khoảng đó bao gồm thời gian sửa code/chờ thao tác.

### Đối chứng model để chọn bước tiếp theo

- Đọc checklist riêng bằng `gemini-3.5-flash-lite` vẫn có lỗi: một quan sát nói có C, quan sát khác nói C không xuất hiện. Chưa tích hợp một vision call riêng dùng lại Lite vì bằng chứng thử chưa đạt.
- Thử `MEDIA_RESOLUTION_HIGH` với Lite không cải thiện trong mẫu này: provider vẫn báo 1.080 token ảnh và bịa dấu góc vuông tại C/dấu trên AB. Không đổi media resolution production.
- Đọc danh sách model bằng GET cho thấy project key đầu có tên `gemini-3.5-flash`; danh sách này **không** xác minh tier/quota/billing của cả 7 project.
- Một probe Flash đầy đủ bị cắt khi maxOutputTokens=2000 vì có 1.916 token suy nghĩ. Không dùng phần JSON bị cắt làm kết quả.
- Probe Flash đầy đủ với maxOutputTokens=8192, JSON schema đơn giản, đúng PNG gốc, không đáp án/lời tự khai: HTTP 200, finish STOP, **6.509 ms**. Nhận A trái trên, B trái dưới, C phải trên; AB/AC/BC và đoạn A tới BC; không có dấu góc vuông, không có dấu bằng nhau, `hasExplicitM=false`. Input 1.179 token, output 106, thinking 1.151. Chỉ là một mẫu, chưa chứng minh độ chính xác mọi hình.
- Đề xuất tiếp: Flash đầy đủ đọc ảnh riêng **chỉ cho câu rubric bắt buộc chấm hình**, khóa quan sát trước khi Lite chấm rubric/tính toán. Thêm request và độ trễ cho nhóm hình; không đổi model chấm mọi câu. **Chưa triển khai/bật profile mới: cần người dùng chọn tradeoff quota/latency và cho phép cấu hình model ảnh riêng.**

## Kiểm thử

- Frontend: `npm test` — **647 pass, 0 fail**, 09/10/2026. Import giữ nguyên PNG, validation rubric, hiển thị bằng chứng và luồng chấm hiện có.
- Backend sau thay đổi chẩn đoán cuối: nhóm AI + AIAnalysis + GradingCriteria — **414 pass, 17 MySQL integration skip, 0 fail**. Các test bị skip chưa được xác minh trong lượt này.
- Build .NET và Docker API/Web pass; API bản cuối đã được khởi động localhost và dùng ở lần chấm lại Test 7. Không coi test mock là bằng chứng mọi ảnh đều được model đọc đúng.
- `git diff --check` trong phạm vi thay đổi không có lỗi whitespace; chỉ có cảnh báo LF/CRLF thông thường.

## Danh sách tệp trong phạm vi bản sửa này

Các tệp dưới đây vốn có thể đã mang thay đổi ở lượt trước; danh sách chỉ mô tả **delta chấm bằng chứng ảnh**, không nhận toàn bộ diff hiện tại là mới sửa trong lượt này.

### Backend và contracts

- `src/EduTwin.Contracts/CurriculumAndQuestions/GradingCriteria.cs` — mục tiêu ảnh trên từng tiêu chí.
- `src/EduTwin.BLL/CurriculumAndQuestions/GradingCriteriaValidator.cs` — giới hạn và kiểm tra mục tiêu.
- `src/EduTwin.BLL/AssessmentAndReasoning/AI/AnalyzeReasoningRequest.cs` — chuyển mục tiêu ảnh sang request.
- `src/EduTwin.BLL/AssessmentAndReasoning/AI/AnalyzeReasoningResponse.cs` — quan sát thị giác AI.
- `src/EduTwin.Contracts/AssessmentAndReasoning/RubricGrade.cs` — lưu quan sát trong grade JSON.
- `src/EduTwin.BLL/AssessmentAndReasoning/AI/StrictAIAnalysisResponseParser.cs` — cấu trúc/giới hạn quan sát.
- `src/EduTwin.BLL/AssessmentAndReasoning/AI/AnalyzeReasoningResponseValidator.cs` — đủ bằng chứng, phạm vi ảnh, điểm nhất quán; token chẩn đoán lỗi suy luận.
- `src/EduTwin.BLL/AssessmentAndReasoning/AI/AIAnalysisValidationException.cs` — token chẩn đoán an toàn.
- `src/EduTwin.BLL/AssessmentAndReasoning/Processing/AIAnalysisRequestFactory.cs` — copy mục tiêu và ảnh vào request.
- `src/EduTwin.BLL/AssessmentAndReasoning/Processing/AIAnalysisJobProcessor.cs` — đối chiếu bytes và persist quan sát.
- `src/EduTwin.API/AssessmentAndReasoning/AI/IGeminiGenerateContentClient.cs` — adapter không âm thầm bỏ ảnh.
- `src/EduTwin.API/AssessmentAndReasoning/AI/GoogleGenAIGenerateContentClient.cs` — log manifest/hash ở adapter.
- `src/EduTwin.API/AssessmentAndReasoning/AI/ReasoningBatchExecutor.cs` — manifest vai trò ảnh, chẩn đoán validation.
- `src/EduTwin.API/AssessmentAndReasoning/AI/GeminiResponseJsonSchema.cs` — cấu trúc quan sát; không maxItems lớn ở provider.
- `src/EduTwin.API/AssessmentAndReasoning/AI/GeminiPromptBuilder.cs` — rubric thị giác, phân biệt nhãn/dấu và logical gaps.
- `src/EduTwin.API/AssessmentAndReasoning/AI/AIGradingOptions.cs`, `GeminiAIService.cs` — phiên bản profile.

### Frontend

- `web/edutwin-web/src/types/questions.ts`, `types/reviews.ts` — contracts mục tiêu/quan sát.
- `web/edutwin-web/src/components/teacher/GradingCriteriaEditor.tsx` — nhập mục tiêu ảnh mỗi tiêu chí.
- `web/edutwin-web/src/utils/rubric.ts` — giữ mục tiêu khi hydrate và kiểm tra đầu vào.
- `web/edutwin-web/src/components/reviews/RubricGradeView.tsx` — quan sát đã thấy/còn thiếu/chưa rõ.
- `web/edutwin-web/src/components/math/ScratchpadInlinePanel.tsx` — import PNG chỉ ở chế độ được sửa.
- `web/edutwin-web/src/utils/scratchpadPngImport.ts` — đọc bytes PNG nguyên bản (tệp mới).

### Kiểm thử mới/cập nhật

- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/VisualEvidenceGradingTests.cs` (mới).
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/GeminiImageTransportTests.cs` (mới, SDK thật/HTTP giả lập).
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/AIAnalysisContractTests.cs`, `GeminiResponseJsonSchemaTests.cs`, `GeminiAIServiceTests.cs`.
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/Processing/AIAnalysisRequestFactoryTests.cs`.
- `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/GradingCriteriaValidatorTests.cs`.
- `web/edutwin-web/tests/rubric.test.ts`, `gradingWorkflow.test.ts`, `scratchpadPngImport.test.ts` (mới).

## Giới hạn và cách dùng

- Giáo viên cần cấu hình mục tiêu ảnh trong rubric cho câu đánh giá kỹ năng vẽ. Không tự backfill hay thay các câu/bài đã dùng; có thể clone để lập phiên bản mới.
- Validator chứng minh cấu trúc và logic nhất quán, **không** tự chứng minh nhận diện thị giác đúng. Vẫn cần kiểm thử nhiều hình đầy đủ/thiếu/sai/khó đọc và giáo viên duyệt điểm Manual.
- Chưa thêm vision call độc lập vào production. Các probe đọc ảnh riêng chỉ dùng chẩn đoán. Batch, hàng đợi SQL và giới hạn gọi AI giữ nguyên. Chưa đo lại 50/100 câu nên không cam kết latency không đổi cho mọi tải.
- Cần theo dõi riêng chức năng “AI chấm lại kết quả đã thành công nhưng sai”; hiện retry chỉ dành cho fallback/thất bại. Không tự mở rộng quyền retry trong bản sửa hình này.
