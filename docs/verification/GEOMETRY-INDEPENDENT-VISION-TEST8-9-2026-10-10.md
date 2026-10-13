# Đọc ảnh độc lập và khóa bằng chứng — Test 8/9

## Phạm vi và trạng thái

Người dùng đã đồng ý triển khai đọc ảnh riêng bằng Flash đầy đủ, thay vì tiếp tục để Flash-Lite tự suy đoán ký hiệu khi chấm. Không stage/commit/push; không reset/migration database, đổi key, duyệt điểm hay sửa Test 5–8 đã nộp.

Đã triển khai backend, cấu hình opt-in và kiểm thử offline. Test 8/9 đã chạy end-to-end. Test 9 cho **8/10**, đọc đúng các ký hiệu thiếu, phần tính toán **4/4**, **22,9702 giây**, không fallback. Đây là kết quả đạt trên ảnh đối chứng này, không khẳng định mọi hình đều được chấm đúng.

## Luồng hiện tại

1. Bài làm tiếp tục vào hàng đợi SQL và worker/microbatch hiện có.
2. Chỉ câu có `criterion.visualRequirements` mới cần kiểm chứng ảnh độc lập. Có ảnh đề nhưng không có mục tiêu chấm hình, hoặc chỉ dùng ảnh nháp tùy chọn, không tự phát sinh bước này.
3. `gemini-3.5-flash` nhận ảnh nháp học sinh và checklist giáo viên. Không nhận nội dung đề, đáp án đúng, lời giải mẫu, lời tự khai của học sinh hay trọng số điểm. Ảnh đề của giáo viên không được gửi ở bước đọc nét vẽ này.
4. Server kiểm tra ID câu/tiêu chí, đủ mục, chỉ số ảnh trong từng câu, trạng thái và lời quan sát tiếng Việt rồi khóa kết quả. Nhãn/dấu không được suy ra từ lời giải mẫu.
5. `gemini-3.5-flash-lite` tiếp tục chấm bằng đề/đáp án/rubric/lời giải học sinh. Ảnh vẫn được cung cấp để đọc tính toán viết tay, nhưng quan sát cho các mục tiêu ảnh đã khóa không được thay thế.
6. Từ profile `locked-visual-v7`, mỗi tiêu chí bị trừ điểm phải có mục chưa đạt và bằng chứng riêng. Lỗi Visual không được khai thành căn cứ trừ tiêu chí Nonvisual. Lý do này được ghép vào nhận xét tiêu chí để giáo viên xem, không tự nâng điểm bằng quy tắc server.
7. Tổng rubric vẫn do server cộng; bài Essay/Manual vẫn AI chấm đề xuất và giáo viên chốt. Không có thao tác duyệt điểm tự động trong bản sửa.

## Giới hạn thời gian, request và quota

- Model chấm chính không đổi: `gemini-3.5-flash-lite`.
- Model ảnh riêng: `Gemini__VisualEvidenceModel=gemini-3.5-flash`, bật bằng `Gemini__VisualEvidenceProfileApproved=true`. `.env.example` để trống/false; `.env` thực tế vẫn bị Git ignore. Không ghi key trong báo cáo.
- Microbatch vẫn 3 câu, cửa sổ gom 500 ms. Nhiều câu chấm hình trong cùng batch có thể dùng chung một request đọc ảnh; ID và chỉ số nguồn ảnh vẫn tách theo câu.
- Hai bước dùng cùng adapter xoay key, bộ điều phối quota SQL và giới hạn chung 2 Gemini calls. Không tạo luồng gọi riêng vượt giới hạn.
- Một deadline 30 giây bao trùm đọc ảnh và chấm trong một lượt batch, không phải 30 + 30 giây. Chờ/retry quota vẫn tuân thủ cơ chế hiện có.
- Câu không có ảnh nháp: server tạo quan sát Missing, không gọi model ảnh chỉ để xác nhận ảnh vắng mặt; vẫn chấm phần tính toán.
- Lỗi đọc ảnh của một câu không loại bỏ các câu thường cùng batch. Kết quả không hợp lệ không được gán điểm tùy tiện.
- Chưa checkpoint riêng giữa hai bước: bounded retry của câu chấm hình có thể gọi đọc ảnh lại. Đây là chi phí còn cần theo dõi, không phải cam kết mỗi câu luôn đúng hai request trong mọi tình huống.
- Profile chứa hash tên model ảnh, giúp phân biệt cache/checkpoint khi đổi model/profile. Không tự chấm lại hay thay kết quả đã thành công ở bài cũ.

## Dữ liệu kiểm chứng

- Câu #20070 (clone có mục tiêu ảnh), không sửa câu #20069 gốc.
- Ảnh `storage/verification/test5-original-sketch.png`: 1200×800, 84.064 byte; SHA256 `5E1C3F74E1588B64C069D29615F4C5DDFC5AB1BB16C116A2DDE1ED367CDBF11D`.
- Có A/B/C, tam giác và đoạn từ A tới BC; thiếu tên M, ô góc vuông và dấu BM=MC. Chữ C ngoài đỉnh không phải ô góc vuông.
- Đáp án `BC = 10 cm; AM = 5 cm.`; lập luận Pythagore và trung tuyến tới cạnh huyền đúng.
- Test 8: `ffb47dcf-5f14-4c23-82e9-f014ec7ff64d`, attempt 215, chỉ student01.
- Test 9: `ecd14323-3c7b-42fd-a217-1849dded6bec`, attempt 216, chỉ student01. Tạo mới do sản phẩm chưa cho chấm lại job đã thành công; không bypass bằng SQL. Người dùng đã xác nhận xuất bản và thử Test 9.

## Test 8 — trước audit trừ điểm

- Nhận bài: 03:42:24.471962 UTC; worker bắt đầu 03:42:27.181747; hoàn tất 03:42:46.982845.
- Nhận bài → terminal: **22,5109 giây**; retry_count=0; is_fallback=0.
- Bước Flash đọc ảnh: **12.189,7502 ms**, Succeeded.
- Tổng executor gồm cả hai bước: **17.167,3855 ms**, Succeeded. Không cộng hai số trên vì thời gian executor đã bao gồm bước ảnh.
- Manifest adapter ở cả hai request khớp byte/hash PNG gốc: ảnh thực sự đã được gửi, không bị bỏ hay thay.
- Đọc ảnh đúng cả sáu mục: hình/đoạn nối/A-B-C Present; M/ô góc vuông/dấu bằng nhau Missing. Không còn gán dấu tại C hay phạt B/C theo hướng sách giáo khoa.
- Điểm: hình 4/4, ký hiệu 0/2, tính toán 3/4, tổng 7/10. Nhận xét tính toán hoàn toàn chính xác nhưng bị trừ 1 điểm là bất nhất; **không coi lượt này là toàn bộ bản sửa đạt**. Đây là lý do thêm audit điểm theo tiêu chí, không sửa thủ công kết quả Test 8.

## Test 9 — profile cuối

- Click xác nhận nộp: 04:00:24.490 UTC; server nhận 04:00:25.323665; worker bắt đầu 04:00:28.793037; hoàn tất 04:00:48.293828.
- Server nhận → terminal: **22,9702 giây**; click → terminal khoảng **23,804 giây**. Thời gian chọn/tải PNG trước khi nộp không được tính vào AI latency.
- Bước Flash đọc ảnh: **8.512,5249 ms**. Tổng executor hai bước: **17.772,063 ms**, không cộng chồng; 2 request, retry_count=0, is_fallback=0.
- Hình dựng **4/4**; tên/ký hiệu **0/2**; tính toán/lập luận **4/4**; server cộng **8/10**. Rubric đang yêu cầu đủ A/B/C/M cho nhóm điểm tên và đủ dấu hình học cho nhóm điểm ký hiệu; chỉ A/B/C không hoàn thành cả nhóm, nên 0/2 ở tiêu chí này có căn cứ. Không dùng “vẽ 80%” làm tỷ lệ điểm tự động.
- Ba mục hình/đoạn nối/A-B-C Present; M/ô góc vuông/dấu bằng nhau Missing. Quan sát và nhận xét không còn tự mâu thuẫn; thiếu dấu không biến thành lỗi suy luận.
- Nhận xét tiếng Việt, reasoningVerdict Valid, errorType Presentation. Chỉ số reasoningQuality được model trả **90/100**, không cộng vào 8/10; đây vẫn là đánh giá đề xuất riêng cần tiếp tục hiệu chuẩn, không coi một lời giải đúng là bằng chứng chỉ số này đã hoàn toàn được tách khỏi ảnh.
- `reviewed_at=NULL`: chưa duyệt/chốt thay giáo viên. Giữ nguyên Test 5–8.
- Giao diện đã hiển thị AI chấm 1/1 và bảng rubric 8/10; tab kết quả được giữ lại.
- Bằng chứng: `storage/verification/test9-ai-result-header.jpg` và `test9-locked-evidence-8points.jpg`. Bảng điểm có lý do trừ ký hiệu hiển thị đầy đủ. Hash bytes ảnh ở hai bước đều khớp PNG gốc.
- Docker API cuối: image `sha256:38e14a59063f181340eef55d12face4e4131e1a6ba4627a37e543f6176a3f32b`, đã chạy trước lúc nộp Test 9. Không suy đoán deploy thành công chỉ từ local build.

## Kiểm thử

- .NET build: 0 warning, 0 error.
- Nhóm AI + AIAnalysis + GradingCriteria: **430 pass, 17 MySQL integration skip, 0 fail** sau sửa tương thích checkpoint cũ. Kiểm thử offline không gọi provider thật.
- Frontend: **647 pass, 0 fail**, không có chỉnh frontend trong lượt này.
- Các test mới kiểm tra: prompt ảnh không lẫn đáp án/reference, truyền đúng bytes, khóa quan sát chống ghi đè, mapping nhiều câu, lỗi một câu không bỏ câu thường, không ảnh không thêm call, malformed observations, ID/chỉ số ảnh, schema opt-in, trừ điểm không lý do, dùng lỗi Visual để trừ Nonvisual và hiển thị bằng chứng trừ điểm.
- Đã phát hiện và sửa vấn đề tương thích khi trường audit null: không thêm trường null vào JSON câu thường/checkpoint cũ; DTO có constructor mặc định. Không nới kiểm tra dữ liệu profile mới để làm test qua.
- `git diff --check` phạm vi sửa không có lỗi whitespace; chỉ cảnh báo chuyển LF/CRLF.

## Tệp thay đổi trong lượt này

Danh sách dưới đây là delta đọc ảnh độc lập/audit điểm, không nhận toàn bộ hơn 300 file đang dirty là thay đổi của lượt này.

- `src/EduTwin.API/AssessmentAndReasoning/AI/GeminiVisualEvidenceInspector.cs` — mới: đọc ảnh, schema, mapping, kiểm tra, log hash, profile hash.
- `GeminiOptions.cs` — cấu hình model ảnh và gate.
- `AIGradingOptions.cs` — profile/model validation.
- `ReasoningBatchExecutor.cs` — hai bước, deadline chung, lỗi theo từng câu.
- `GeminiAIService.cs` — luồng không dùng batch cũng không bỏ qua kiểm chứng.
- `GeminiPromptBuilder.cs` — bằng chứng server khóa và audit trừ điểm.
- `GeminiResponseJsonSchema.cs` — trường audit opt-in, schema câu thường không đổi.
- `src/EduTwin.BLL/AssessmentAndReasoning/AI/AnalyzeReasoningRequest.cs` — quan sát server-owned, không đọc/ghi vào JSON người dùng.
- `AnalyzeReasoningResponse.cs` — hợp đồng audit trừ điểm, giữ tương thích cũ.
- `StrictAIAnalysisResponseParser.cs` — gắn quan sát đã khóa, parse audit và hiển thị lý do.
- `AnalyzeReasoningResponseValidator.cs` — tách kiểm tra quan sát và kiểm tra audit theo tiêu chí.
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/GeminiVisualEvidenceInspectionTests.cs` — mới: 16 trường hợp offline.
- `AIAnalysisContractTests.cs` — shape request/response mới và tương thích JSON.
- `docker-compose.yml`, `.env.example`, `.env` riêng tư — cấu hình opt-in (không chứa key trong báo cáo).
- Báo cáo này và thông báo cập nhật trong báo cáo 09/10.

Tên tệp không có tiền tố trong mỗi nhóm nằm cùng thư mục với tệp có đường dẫn đầy đủ ngay trước đó.

## Giới hạn cần nói rõ

Validator chứng minh cấu trúc, phạm vi ảnh và căn cứ trừ điểm được cung cấp; nó không tự chứng minh mọi quan sát/nội dung lý do của model là đúng. Một ảnh đối chứng chưa đủ chứng minh chấm hình nói chung đạt. Cần thêm mẫu đủ/sai/thiếu/khó đọc, xoay/lật và hình nhiều ảnh. Chưa đo lại bài 50/100 câu có mục tiêu ảnh bắt buộc nên không cam kết thời gian bằng bài tiếng Anh trước đây.

Trong lúc chuyển Teacher Math để kiểm chứng, dashboard lớp Toán 12 vẫn hiện 81 nhóm bổ trợ. Đây là quan sát ngoài phạm vi bản sửa chấm hình, cần kiểm tra riêng phạm vi giáo trình ở báo cáo giáo viên; chưa chỉnh thêm trong lượt này.
