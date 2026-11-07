# Đánh giá prompt chấm bài độc lập

Chạy ở repository root. Cần .NET SDK 10; không cần khởi động API, MySQL, frontend hoặc đăng nhập.
Project tham chiếu mã hiện tại trong repository, không sao chép prompt thành một phiên bản riêng:

- `AIAnalysisRequestFactory`, các preliminary grader và chuẩn hóa đáp án;
- `GeminiPromptBuilder`, `GeminiResponseJsonSchema`, `GoogleGenAIGenerateContentClient`;
- `StrictAIAnalysisResponseParser`, `AnalyzeReasoningResponseValidator`.

Đây là thí nghiệm **từng câu với Gemini hoặc Groq**. Chọn đúng provider/model đang triển khai qua `-Provider`, `-Model` hoặc environment.
Runner dùng temperature và các phần ảnh theo adapter hiện tại. Không gọi database, thay đổi điểm/Twin, hoặc kích hoạt fallback.
Gemini dùng trực tiếp `GoogleGenAIGenerateContentClient`; Groq dùng cùng HTTP shape của `GroqGenerateContentClient`
(`temperature=0`, `max_completion_tokens=2048`, `response_format=json_object`, cùng thứ tự text/ảnh), vì transport production
Groq bắt buộc có SQL quota coordinator. Endpoint provider được cố định trong mã, không cho chuyển API key sang URL tùy ý.
Thí nghiệm không bao phủ microbatch, quota SQL, worker retry hay profile kiểm tra ảnh độc lập. Runner từ chối provider lạ
và cấu hình independent visual inspection để tránh âm thầm chạy một profile khác. Các luồng này cần nghiệm thu riêng với runtime hệ thống.

## Chạy offline trước

```powershell
.\test\scripts\run-ai-prompts.ps1 -Mode SelfTest
.\test\scripts\run-ai-prompts.ps1 -Mode Prepare
```

`SelfTest` kiểm tra allowlist, duplicate keys, enum, confidence, ảnh giả, prompt injection được giữ nguyên dưới dạng dữ liệu,
canonical request builder, HTTP envelope Groq và các bất biến export. Transport giả chỉ dùng trong self-test;
kiểm tra atomic report tạo file tạm trong thư mục tạm hệ điều hành rồi tự dọn, không sinh báo cáo model trong `results/`.
`Prepare` kiểm tra toàn bộ fixture và xuất `parameters` + `prompt`, **không có `output` và không gọi model**.

Tương đương CLI trên mọi hệ điều hành:

```text
dotnet run --project test/ai-prompts/AI.PromptEvaluation.csproj -c Release -- --self-test
dotnet run --project test/ai-prompts/AI.PromptEvaluation.csproj -c Release -- --prepare
```

## Gọi model thật bằng API key do lập trình viên cung cấp

```powershell
# Nhập API key ẩn; chỉ truyền cho tiến trình con qua environment, không ghi vào file/lịch sử lệnh.
.\test\scripts\run-ai-prompts.ps1 -Mode Live -Model '<model-dang-trien-khai>' -PromptForApiKey
# Hoặc chọn deployment Groq tường minh:
.\test\scripts\run-ai-prompts.ps1 -Mode Live -Provider Groq -Model '<model-groq-dang-trien-khai>' -PromptForApiKey
```

Trong CI, cấp secret `AI_PROMPT_API_KEY` qua secret store và đặt `AI_PROMPT_PROVIDER` (`Gemini`/`Groq`) cùng `AI_PROMPT_MODEL`, rồi chạy:

```text
dotnet run --project test/ai-prompts/AI.PromptEvaluation.csproj -c Release -- --live
```

Runner cũng nhận `AIGrading__Provider`, `Gemini__ApiKey`/`Gemini__Model` hoặc `AIGrading__GroqApiKey`/`AIGrading__GroqModel`
từ environment hiện tại. Không tự đọc `.env` hoặc dùng key trong repository.
Không có tham số command line cho API key. Không gửi credential của người dùng hệ thống cho model.
Nếu có `AIGrading__GeminiModel` trong environment, giá trị này được ưu tiên hơn `Gemini__Model` để khớp deployment override;
`-Model`/`AI_PROMPT_MODEL` luôn ưu tiên cao hơn.

Mặc định giới hạn 20 fixture và 60 giây/câu. Tăng bằng `-Limit` (tối đa 100) hoặc `-TimeoutSeconds` (tối đa 120).
Mỗi fixture gọi model một lần qua adapter; không thêm retry/fallback chấm bài. API key có thể phát sinh chi phí provider.
File mới được tạo trong `test/ai-prompts/results/`; đường dẫn này được Git bỏ qua. `-Output` chỉ chấp nhận file chưa tồn tại.

## Hợp đồng file đánh giá

File live là một JSON array. **Mỗi phần tử chỉ có ba trường**:

```json
{
  "parameters": {
    "teacher": { "questionType": "ShortAnswer", "correctAnswer": "7", "solution": "3+4=7", "maxScore": 10 },
    "student": { "timeSpentSeconds": 45, "confidence": 90, "answerChanges": 0 }
  },
  "prompt": {
    "teacher": { "questionText": "Tính 3+4", "problemImage": null },
    "student": { "finalAnswer": "7", "answerDisplayLatex": null, "reasoningText": "3+4=7", "scratchpadImage": null }
  },
  "output": "...đầu ra thực tế của model; ví dụ cấu trúc, không phải kết quả đã chạy..."
}
```

Ví dụ trên rút gọn `parameters`; file thật giữ các lựa chọn, chủ đề, ngôn ngữ, expected reasoning và rubric giáo viên đã nhập.
`prompt` trong báo cáo là **nội dung giáo viên/học sinh thực sự có thể nhập**, không phải toàn bộ wire prompt.
Wire prompt thực tế được tạo từ builder hiện hành; system instructions, schema nội bộ, ID giả lập để dựng context,
preliminary correctness, canonical answers, repair hints, model/quota configuration và telemetry không được xuất.
API key không nằm trong prompt hoặc report.

`output` giữ nguyên nội dung thực tế do provider trả về (object JSON nếu parse được; string nếu JSON hỏng).
Parser/semantic validation không sửa hoặc thay nó bằng fallback. Kết quả kiểm tra, case ID, model setting, lỗi và expected values chỉ ở console/fixture developer,
không thêm vào file đánh giá. Nếu gọi provider lỗi trước khi có nội dung, không tạo một phần tử với output giả/null.
Vì vậy số phần tử có thể ít hơn số fixture; console ghi số response và case gọi lỗi. Report được ghi sau từng response
bằng file tạm cùng thư mục rồi rename để giữ bản hoàn chỉnh gần nhất khi tiến trình bị ngắt. Output bị cắt vì giới hạn token
Groq vẫn được giữ nguyên để developer xem, nhưng case bị đánh FAIL.

Exit code `0` khi mọi case đã nhận output, đạt contract và expected observations; `1` khi bất kỳ case lỗi hoặc bất biến sai.
`PASS` là kết quả kiểm tra tự động cụ thể, không thay thế giáo viên/developer đánh giá chất lượng nội dung.

## Fixture và coverage

`cases/*.json` chỉ chấp nhận `teacher` và `student` với field allowlist. Trường không biết, enum số, key JSON lặp,
server-owned field, ảnh sai định dạng và rubric không hợp lệ bị từ chối **trước mọi model call**.
Không strip các chuỗi HTML/injection trong dữ liệu hợp lệ: cần giữ đúng dữ liệu để kiểm tra tính chống prompt injection.
Không có API cho phép người dùng tự đặt preliminary correctness, allowed node IDs hoặc server repair instructions.
Topic names tương ứng những chủ đề giáo viên có thể chọn; numeric IDs dùng cho factory chỉ được tạo trong bộ nhớ và không xuất trong parameters.

17 case mặc định gồm: đúng/sai, phân số/tọa độ tương đương, đáp án đúng nhưng quy tắc sai, không có lập luận,
`khum`, đáp án tiếng Anh với giải thích tiếng Việt, phương pháp khác hợp lệ, rubric theo thang điểm gốc,
HTML trong reasoning, injection trong reasoning học sinh, đề và lời giải giáo viên, thiếu ảnh nháp cho rubric hình học,
và đề phép cộng `3 + 4 = ?` chỉ có trong ảnh PNG tổng hợp.
Case ảnh thiếu yêu cầu điểm hình vẽ 0/2, trạng thái `Missing`, nhưng giữ đủ 3/3 điểm tính toán/lập luận đúng.
Các loại câu hỏi: ShortAnswer, MultipleChoice (case05/06) và Essay/Manual (case09).
Ảnh đề tổng hợp gốc ở `assets/arithmetic-question.png` (500 × 140, 1.9 KiB); case16 nhúng chính byte PNG đó
vào `teacher.problemImage.dataBase64`. Không dùng ảnh mô phỏng mờ/không có nội dung để tuyên bố kiểm tra thị giác thành công.
`expectations.json` là oracle do developer đặt, không được gửi cho model hoặc export.
Các expected score là đề xuất của AI, không phải điểm cuối cùng của hệ thống.

Thêm trường hợp bằng cách sao chép một fixture và sửa dữ liệu giáo viên/học sinh. Đổi `-Cases` để chạy thư mục riêng;
file `expectations.json` (nếu có) nằm ở thư mục cha của `cases`. Khi không có oracle, runner vẫn kiểm tra contract hiện hành
và console ghi `expected-observations=NOT_CONFIGURED`, không khẳng định chất lượng ngữ nghĩa đã đạt.
Mỗi tên file chỉ gồm chữ ASCII, số, `_`, `-`; mọi fixture trong thư mục đều được kiểm tra trước khi gọi model.

Ảnh tùy chọn: `teacher.problemImage` hoặc `student.scratchpadImage` có đúng field `dataBase64` (PNG thuần base64).
Harness áp dụng kiểm tra CRC/dimensions/decompression của ứng dụng với giới hạn chặt 2 MiB cho cả hai vai trò;
production scratchpad chấp nhận tới 5 MiB, nên thử tải 2–5 MiB thuộc Cypress/API runtime test.
Confidence theo UI là **0–100**, không phải 0–1. Giá trị thời gian/thay đổi đáp án là telemetry mà client học sinh gửi,
không phải quyền sửa điểm hoặc trạng thái job.

Chỉ dùng dữ liệu tổng hợp trong fixture được commit. Các file chứa bài học sinh thật và kết quả provider nên lưu ngoài Git
hoặc đặt hậu tố `.local.json`; không nhúng credential vào nội dung fixture.
