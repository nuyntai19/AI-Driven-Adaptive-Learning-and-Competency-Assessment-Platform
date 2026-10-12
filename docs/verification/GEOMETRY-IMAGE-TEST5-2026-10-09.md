# Test 5 — đề nằm trên ảnh và bản vẽ cố ý thiếu ký hiệu

## Phạm vi và cách thử

- Thực hiện trên Chrome/student01 bằng skill computer-use, qua giao diện làm bài và Vẽ nháp. Người dùng đã kích hoạt câu hỏi và tạo Test 5.
- Assignment: `65bc2798-d931-4281-bc04-d3b55fd3a701`; Lớp Toán 10: `51000000-0000-0000-0000-000000000010`.
- Đã nộp hai câu lúc 20:47:04 ngày 09/10/2026 (UTC+7). Không bấm AI chấm lại, không duyệt điểm thay giáo viên, không thay bài làm cũ.
- “Vẽ đúng khoảng 80%” được hiểu là cố ý để hình chưa hoàn chỉnh; không ép AI trả điểm 8/10.
- Lượt này không sửa source, cấu hình, schema hoặc dữ liệu bằng SQL; chỉ tạo bài làm qua UI, đọc dữ liệu/log để kiểm chứng, lưu ảnh và báo cáo. Không stage/commit/push.

## Đầu vào và kết quả

| Câu | Đầu vào thử nghiệm | Kết quả AI | Đánh giá |
| --- | --- | --- | --- |
| 1 — #20069, attempt 212 | Vẽ tam giác ABC vuông tại A, nối trung tuyến từ A đến giữa BC, ghi A/B/C. Cố ý thiếu tên M, ô góc vuông tại A và dấu BM=MC. Tính BC=10 cm, AM=5 cm và lập luận đúng. | 10/10: hình 4/4; tên/ký hiệu 2/2; tính toán 4/4. Chất lượng lập luận 100/100, Valid. | **Không đạt kiểm thử chấm bằng chứng hình ảnh.** AI nói đầy đủ tên điểm/ký hiệu góc vuông/trung điểm trong khi ảnh không có. Không nên cho trọn tiêu chí ký hiệu. |
| 2 — #20068, attempt 211 | Toàn bộ đề/dữ kiện nằm trong ảnh; a) S=84 cm², AH=12 cm; b) AM=2√37 cm đúng; c) cố ý dùng R=abc/(2S), ra 65/4=16,25 cm. | 7/10: a 4/4; b 3/3; c 0/3. Chất lượng lập luận 70/100, Invalid, lỗi Knowledge. AI chỉ ra mẫu số phải là 4S, kết quả đúng 65/8=8,125 cm. | Đạt trường hợp thử này: phát hiện đúng lỗi chủ động ở c, không trừ a/b. Chưa đủ để kết luận mọi đề ảnh đều được chấm chính xác. |

Nhận xét AI bằng tiếng Việt. Cả hai câu là Essay/Manual: điểm trên là **đề xuất**, `reviewed_at` còn NULL, trạng thái bài làm `NeedsTeacherReview`. Chưa có điểm cuối cùng; không lấy trung bình hai điểm đề xuất làm điểm chính thức.

## Thời gian và trạng thái server

Đối chiếu bằng SELECT chỉ đọc từ `attempts`, `ai_analysis_jobs`, `reasoning_analyses` và log API trong khoảng 13:47:00–13:47:30 UTC:

- Hai attempt được tạo cùng lúc `2026-10-09 13:47:04.100486` UTC.
- Attempt 211 hoàn tất job lúc `13:47:12.873468`: **8,772982 giây** từ lúc nhận bài.
- Attempt 212 hoàn tất job lúc `13:47:16.066842`: **11,966356 giây** từ lúc nhận bài.
- Tổng thời gian server để cả hai câu hoàn tất: **11,97 giây**.
- Log có một batch thành công chứa hai câu, Gemini/model `gemini-3.5-flash-lite`, latency **7484,7294 ms** (7,48 giây).
- Hai job `Completed`, retry_count=0, last_error_code=NULL; hai analysis `is_fallback=0`.
- Giao diện được quan sát đã hoàn tất 2/2 sau khoảng **16,147 giây** tính từ thao tác xác nhận nộp. Đây là mốc quan sát, gồm độ trễ UI/polling và thao tác kiểm tra, không phải thời gian gọi model.
- Thời gian làm bài `00:10:29` trên UI không phải thời gian AI chấm.

Không ngoại suy tốc độ hai câu này thành tốc độ 50/100 câu hoặc bằng chứng kiểm thử tải.

## Vấn đề còn lại và giới hạn chẩn đoán

### 1. AI nhận xét ký hiệu hình không tồn tại

Ảnh nháp gốc đã lưu và hiển thị sau nộp. Đối chiếu rubric trong DB xác nhận:

- Tiêu chí tên/ký hiệu yêu cầu A/B/C/M, dấu góc vuông và dấu BM=MC hoặc ghi 5 cm cho hai đoạn; chỉ chấm theo ảnh thực tế.
- ScoringNotes xác định vẽ là mục tiêu đánh giá trực tiếp, không được suy ra vẽ đúng từ đáp số/lời tự khai.
- Rubric AI vẫn trả 2/2 và nhận xét “Tên các điểm và ký hiệu góc vuông, đoạn thẳng đầy đủ, chính xác.”

Đọc code xác nhận request factory chuyển rubric/scoring notes và ảnh học sinh; processor tải attachment, batch executor gửi ảnh kèm prompt với chỉ số phân biệt ảnh đề và ảnh bài làm. Chưa lưu payload mạng thực tế gửi provider, nên chưa khẳng định tuyệt đối nguyên nhân là model đọc sai ảnh hay bỏ qua yêu cầu. **Lỗi quan sát chắc chắn là kết luận thị giác không khớp ảnh đã nộp.** Không có bằng chứng đây là lỗi quota/key hoặc fallback.

Hướng cần xử lý ở lượt sửa riêng: yêu cầu bằng chứng thị giác cụ thể cho từng mục bắt buộc; tách đánh giá hình khỏi đáp số/lập luận chữ; không tự bù ký hiệu từ đề/đáp án mẫu; thiếu hoặc không đọc rõ bằng chứng phải nói rõ và chấm phần có căn cứ. Cần regression test cả hình đầy đủ, thiếu nhãn, thiếu dấu, hình sai quan hệ và không đính kèm ảnh. Không chỉ ép tổng điểm về 8 hay đổi mọi ảnh sang giáo viên chấm thay AI.

### 2. Một trường vẫn chưa dựng công thức

Ở câu 2, “Phương pháp nhận diện” còn hiện nguyên chuỗi `$R = \frac{abc}{4S}$`. Đáp án, lời nhận xét, lời giải và rubric trong trường hợp này đã dựng công thức; lỗi trình bày còn lại thuộc trường method_detected. Chưa sửa trong lượt thử nghiệm.

## Bằng chứng

- `storage/verification/test5-drawing-incomplete.jpg` — bản vẽ trước đính kèm.
- `storage/verification/test5-submitted-drawing.jpg` — ảnh bản vẽ thực tế sau nộp, thiếu M/dấu góc vuông/dấu hai đoạn bằng nhau.
- `storage/verification/test5-drawing-ai10.jpg` — AI cho 10/10, tiêu chí tên/ký hiệu 2/2.
- `storage/verification/test5-image-question-ai7.jpg` — AI cho 7/10, phát hiện đúng lỗi c; đồng thời thấy lỗi công thức ở “Phương pháp nhận diện”.

## Kết luận

Luồng đề ảnh → làm bài → đính kèm bản vẽ → nộp → AI trả rubric hoạt động, không fallback và không chờ lâu trong lượt này. Câu đề ảnh phát hiện đúng lỗi tính toán đã cài. **Chấm độ đầy đủ của hình vẽ chưa đáng tin trong trường hợp thử này, cần sửa/kiểm tra lại trước khi coi tính năng chấm vẽ là đạt.**

## Kiểm tra nguyên nhân bổ sung theo yêu cầu người dùng

Lượt này chỉ chẩn đoán; không sửa source/cấu hình/điểm, không chạy AI chấm lại, không push.

### Những gì đã xác nhận

1. DB có đúng một attachment của attempt 212: `image/png`, 84.064 byte, tạo cùng transaction bài nộp. Attempt 211 không có ảnh nháp riêng.
2. Đọc hash tệp trong volume của API: `5e1c3f74e1588b64c069d29615f4c5ddfc5ab1bb16c116a2dde1ed367cdbf11d`. Ảnh bản vẽ sau nộp hiển thị được và đúng bản cố ý thiếu ký hiệu.
3. `AIAnalysisJobProcessor.LoadAttachmentImagePartsAsync` đọc bytes từ storage của attempt, kiểm tra kích thước so với metadata và giới hạn; có lỗi đọc sẽ chuyển sang storage failure, không âm thầm coi ảnh là thành công.
4. `AIAnalysisRequestFactory` chuyển ảnh sang `StudentSubmission.ImageParts`, đồng thời giữ nguyên mô tả tiêu chí và ScoringNotes. `AllImages()` ghép ảnh đề trước, ảnh học sinh sau.
5. `ReasoningBatchExecutor` giữ toàn bộ ảnh; `GeminiPromptBuilder.BuildBatch` dùng cùng thứ tự để tạo chỉ số ảnh của từng câu, phân biệt ảnh đề và ảnh bài làm. Không tìm thấy lỗi tăng chỉ số hoặc cắt ảnh trong đoạn này.
6. DI production chọn `GoogleGenAIGenerateContentClient`. Lớp này có override thật cho `GenerateContentWithImagesAsync`, tạo `Part.InlineData` với bytes/MIME rồi gọi SDK bằng `Content.Parts`. Không chỉ gửi đường dẫn localhost hay một dòng “học sinh có ảnh”. `JsonIgnore` trên ImageParts chỉ tránh nhét base64 vào prompt chữ, không loại ảnh khỏi parts đa phương thức.
7. Interface có default implementation của `GenerateContentWithImagesAsync` quay về text-only. Đây là nguy cơ nếu một adapter tương lai không override, **không phải nguyên nhân đã xác nhận của Test 5**, vì adapter production hiện tại có override và được đăng ký đúng.
8. 43 kiểm thử offline có sẵn về prompt, request factory, phân tách/giữ ảnh của microbatch, fingerprint và tải ảnh đề đều pass (chạy test assembly Debug hiện có qua VSTest). Chúng dùng giả lập, không gọi provider; không thay thế kiểm chứng payload HTTP hay độ chính xác của model đọc ảnh.

### Điểm yếu đã tìm thấy

- Prompt chung ở `GeminiPromptBuilder.cs:68` cho phép phương pháp đúng được trọn điểm dù không có hình nếu phương pháp không cần hình. Prompt cũng khuyến khích chỉ trừ lỗi toán/lập luận thiết yếu. Những quy tắc này phù hợp khi hình là hỗ trợ tùy chọn, nhưng chưa nêu rõ ngoại lệ ưu tiên khi **vẽ/ghi nhãn/đánh dấu là mục tiêu bắt buộc của rubric**. Câu #20069 lại có mục tiêu bắt buộc ấy. Đây là xung đột cần khắc phục, nhưng chưa đủ bằng chứng để nói nó là nguyên nhân duy nhất.
- Schema trả về chỉ có điểm và comment rubric, chưa bắt model kê khai từng bằng chứng nhìn thấy/thiếu/không rõ trong ảnh.
- Validator kiểm tra cấu trúc, khoảng điểm, ID tiêu chí, ngôn ngữ và tính nhất quán lập luận; chưa kiểm tra một nhận xét “đủ nhãn/ký hiệu” dựa trên các quan sát thị giác cụ thể. JSON hợp lệ và tổng điểm server tính đúng không chứng minh điểm chuyên môn đúng.
- Chấm cả hai câu trong một batch với ảnh/đáp án mẫu/lập luận cùng ngữ cảnh vẫn có nguy cơ model suy diễn hoặc lẫn ảnh dù chỉ số trong code đúng. Chưa có thử nghiệm đối chứng độc lập để xác định mức ảnh hưởng.

### Mức độ chắc chắn

- **Không phải ảnh chưa được lưu:** đã kiểm chứng DB, tệp và ảnh sau nộp.
- **Chưa thấy bug bỏ ảnh trên đường gửi production:** code tải/forward/đóng gói ảnh đầy đủ; adapter không dùng nhánh default text-only.
- **Chưa chứng minh tuyệt đối payload lịch sử:** log Test 5 không lưu manifest image count/role/hash tại ranh giới HTTP, không lưu body gửi provider. Không thể truy hồi chắc chắn Gemini đã nhận đúng bytes nào chỉ từ log đang có.
- **Quan sát chấm sai đã chắc chắn:** AI cho trọn điểm ký hiệu và nói có các dấu không nằm trên bản vẽ. Khả năng đáng ưu tiên là suy diễn/bỏ qua bằng chứng thị giác và prompt thiếu ưu tiên rubric vẽ; chưa tách được “đọc nhầm” với “không chú ý ảnh”. Không có bằng chứng lỗi key/quota.

### Hướng khắc phục đề xuất — chưa triển khai

1. Thêm log an toàn/kiểm thử ngay chỗ đóng gói request: số ảnh, vai trò ảnh, số byte/hash đối chiếu, chỉ số trong batch. Không log base64, API key hoặc toàn bộ bài làm. Fail closed khi câu có attachment nhưng ảnh không đi vào request; loại bỏ khả năng adapter âm thầm rơi về text-only.
2. Tách quy tắc “hình tùy chọn” khỏi “vẽ là tiêu chí bắt buộc”. Không lấy tính đúng của BC/AM thay thế yêu cầu M/góc vuông/BM=MC; tiếp tục công nhận phương pháp tương đương và hình xoay/lật, không chấm độ đẹp/tỉ lệ.
3. Model phải ghi nhận từng mục thị giác với trạng thái nhìn thấy/thiếu/không rõ và mô tả vị trí/ảnh nguồn trước khi cho điểm hình. Server kiểm tra sự đầy đủ và nhất quán của các trường ấy với điểm rubric; lưu ý kiểm tra cấu trúc/logic không tự chứng minh được phát hiện thị giác đúng.
4. Chạy đối chứng chẩn đoán chỉ đọc ảnh nháp, chưa cung cấp đáp án mẫu/lời tự khai, rồi mới đánh giá điểm. So sánh ảnh này, ảnh đầy đủ và ảnh sai/không có ảnh. Đối chứng cần lượt gọi provider mới; **chưa chạy** trong lượt chẩn đoán này. Nếu batch gây lẫn, chỉ tách nhóm câu bắt buộc chấm bản vẽ, không bỏ tối ưu batch cho toàn bộ câu chữ.
5. Nếu sau sửa prompt/kiểm tra bằng chứng model vẫn không đủ chính xác, thử profile vision mạnh hơn hoặc bước đọc ảnh độc lập **chỉ cho câu cần chấm hình**, đo lại latency/quota; không tự đổi model toàn hệ thống. AI vẫn đưa điểm đề xuất, giáo viên vẫn duyệt theo cấu hình Manual.

Tài liệu chính thức đã đối chiếu: [Gemini image understanding](https://ai.google.dev/gemini-api/docs/image-understanding) xác nhận PNG là định dạng hỗ trợ và có thể gửi nhiều ảnh; tăng media resolution có thể tăng độ chi tiết nhưng cũng tăng token/latency. [Structured outputs](https://ai.google.dev/gemini-api/docs/structured-output) lưu ý cần kiểm tra giá trị trong ứng dụng dù JSON đúng schema. Không coi tăng độ phân giải hoặc thêm schema là bảo đảm chấm chính xác.

## Bản sửa tiếp theo

Sau khi người dùng cho phép triển khai, đã thêm rubric mục tiêu ảnh, quan sát thị giác, kiểm tra điểm/bằng chứng, log/hash và kiểm thử transport. Diễn biến Test 6/Test 7, sự cố trong quá trình thử và giới hạn xác minh được ghi riêng tại `docs/verification/GEOMETRY-VISUAL-EVIDENCE-FIX-2026-10-09.md`. Các phần “chưa triển khai” ở trên là trạng thái của lượt chẩn đoán ban đầu, không phải kết luận cuối của bản sửa.
