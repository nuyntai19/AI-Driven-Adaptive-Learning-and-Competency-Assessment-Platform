# Tối ưu chấm và phân tích AI — giai đoạn 1

Triển khai local ngày 2026-10-07. Phạm vi: tăng khả năng xử lý nhiều câu hỏi, giữ nguyên chấm đáp án, phân tích lập luận và bằng chứng phục vụ Digital Twin/lộ trình cá nhân. Chưa chuyển mô hình hay gom nhiều học sinh/câu hỏi vào một prompt.

## Luồng xử lý

1. Nộp bài lưu bài làm và job như trước; mỗi câu đã trả lời vẫn có phân tích AI riêng, gồm đáp án, lập luận, ảnh nháp và tài liệu tham khảo của giáo viên. Trắc nghiệm vẫn chạy AI phân tích.
2. Worker chạy tối đa `AIAnalysisWorker__MaxConcurrentJobs` job trong các scope độc lập. API mặc định 4; quota provider mặc định chỉ cho 2 cuộc gọi cùng lúc khi project chưa xác minh.
3. Điều phối provider theo project và model bằng bảng SQL dùng chung giữa các instance. Kiểm tra số cuộc gọi đang chạy, RPM, ước lượng input TPM, RPD và cooldown. SDK gọi một lần; hàng đợi chịu trách nhiệm retry, tránh nhiều tầng retry nối tiếp.
4. Kết quả hợp lệ được lưu checkpoint trước transaction chấm. Khóa/version/owner/lease được kiểm tra nguyên tử. Fingerprint gồm model/chính sách prompt/schema, phiên bản câu hỏi, bài làm và hash từng ảnh.
5. Cập nhật analysis, evidence, Twin, history, tiến độ, trạng thái job và yêu cầu tổng hợp trong một transaction ngắn, khóa theo học sinh. Các kết quả về sớm chờ bằng chứng trước đó theo `Attempt.CreatedAt, AttemptId`, sử dụng checkpoint khi chạy lại. Không giữ khóa trong khi gọi Gemini.
6. Sau commit, xóa checkpoint trong cùng transaction; thông tin chẩn đoán chính thức vẫn nằm trong analysis/evidence/history. Tránh lưu hai bản đầy đủ lâu dài.
7. Hàng đợi tổng hợp gộp theo trung tâm/học sinh/môn/bài tập. Revision và lease bảo vệ việc enqueue mới trong khi worker đang chạy. Đợi các câu còn đang phân tích rồi cập nhật gợi ý học tập và nhận xét tổng hợp. Lỗi có retry lưu trong SQL. Duyệt/chấm lại của giáo viên cũng enqueue trong transaction.

Quota tạm thời không làm tiêu hao số retry dùng để tạo fallback chấm. Lỗi database điều phối/checkpoint không bị biến thành kết luận về bài làm. Timeout/phản hồi không hợp lệ vẫn tuân theo cơ chế fallback/giáo viên xem xét hiện có. Kết quả giáo viên, rubric, khóa toàn bài, hủy câu theo bài tập và Evidence Gate giữ quyền quyết định như trước.

## Cấu hình

```dotenv
GEMINI_LIST_KEY=["YOUR_KEY_1","YOUR_KEY_2"]
Gemini__Model=gemini-2.5-flash
AIAnalysisWorker__MaxConcurrentJobs=4
Gemini__MaxConcurrentRequests=2
GEMINI_QUOTA_POOLS=[]
```

Không thay model đang dùng trong `.env`. Bảy key từ bảy tài khoản Pro chưa chứng minh bảy quota API độc lập. Khi chưa có project/quota được xác minh, tất cả key dùng chung pool bảo thủ; không nhân giới hạn lên bảy lần.

Khi biết project và giới hạn trong AI Studio, cấu hình từng pool. `KeyIndexes` là vị trí từ 0 trong danh sách key sau khi bỏ trùng; mỗi vị trí phải thuộc đúng một pool. Các số trong ví dụ sau chỉ là cấu trúc minh họa, phải thay bằng số được xác minh:

```dotenv
GEMINI_QUOTA_POOLS=[{"projectId":"verified-project-id","keyIndexes":[0,1],"maxConcurrentRequests":2,"requestsPerMinute":0,"inputTokensPerMinute":0,"requestsPerDay":0}]
```

Giá trị 0 nghĩa là chưa biết giới hạn đó, không phải quota provider vô hạn. Cooldown 429 vẫn có hiệu lực. Bảng quota chỉ chứa ID pool băm, bộ đếm và lease; không lưu key hoặc bài làm. Ước lượng token không phải tokenizer chính xác; số token trả về được dùng để đối soát. Provider vẫn có thể từ chối theo giới hạn thực tế. Lịch đếm ngày dùng múi giờ Pacific.

## Kiểm chứng

- Test phục hồi sau lỗi commit: giữ kết quả AI, lần chạy sau không gọi provider lại, chỉ một analysis/evidence chính thức.
- Test thứ tự: kết quả câu sau về trước, chờ rồi commit đủ evidence/Twin/history theo thứ tự bài nộp.
- Test nhận diện checkpoint: đổi đáp án, ảnh, lời giải tham khảo, model/prompt hoặc phiên bản câu hỏi làm đổi fingerprint.
- Test trắc nghiệm: vẫn gọi AI và tạo evidence riêng.
- Test quota: số cuộc gọi đang chạy, RPM/TPM/RPD, hết lease, cooldown qua khởi động lại và cuộc gọi khác thành công.
- Test hàng đợi: gộp 50 sự kiện, giữ revision mới và retry sau lỗi.
- MySQL riêng: nhiều service provider cùng tranh quota, worker cũ không ghi đè, hoàn thành ngược thứ tự vẫn đủ bằng chứng.
- MySQL datetime(6): lease được chuẩn hóa tới microsecond trước khi lưu, tránh hàng đợi không xác nhận hoàn thành vì .NET timestamp có độ chính xác cao hơn.

Kết quả cuối: full backend 3.824 pass, 0 fail, 67 test tích hợp skip trong lượt đó; bảy trường hợp MySQL được chạy riêng và pass (quota nhiều instance, fencing checkpoint, completion ngược thứ tự, tranh chấp/recovery cùng học sinh, replay của giáo viên, enqueue sau giáo viên sửa và precision của lease). EF không có model drift. Docker API/SQL local đã cập nhật, readiness đạt, dữ liệu đã đối soát giữ nguyên và có backup riêng trong `%TEMP%/EduTwin-backups`. Bảy key và model `gemini-2.5-flash` được giữ nguyên. Chưa commit/push.

Lệnh kiểm thử (database tích hợp do fixture tạo tên riêng và tự dọn):

```powershell
dotnet test tests/EduTwin.BLL.Tests/EduTwin.BLL.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v quiet
node scripts/test_ai_processing.cjs 'FullyQualifiedName~AIAnalysisJobProcessorMySqlTests|FullyQualifiedName~TeacherOverrideMySqlTests' --no-build
```

Benchmark worker dùng provider giả, không gọi API thật và không tạo dữ liệu học sinh trong database đang sử dụng:

| Số câu | Worker đồng thời | Độ trễ giả/câu | Thời gian đo |
|---:|---:|---:|---:|
| 50 | 1 | 10 ms | 798 ms |
| 50 | 2 | 10 ms | 753 ms |
| 50 | 4 | 10 ms | 200 ms |
| 50 | 8 | 10 ms | 115 ms |
| 100 | 1 | 10 ms | 1.603 ms |
| 100 | 2 | 10 ms | 778 ms |
| 100 | 4 | 10 ms | 392 ms |
| 100 | 8 | 10 ms | 199 ms |
| 5.000 | 4 | 1 ms | 16.326 ms |

Đây là kiểm tra worker và scope, không gồm quota SQL/AI thật/Twin commit. Độ trễ timer, tải máy và quota làm thay đổi thời gian thực. Không suy ra rằng Gemini sẽ chấm 100 câu trong 392 ms hay production tăng đúng 4 lần. Môi trường local hiện giới hạn provider ở 2, dù worker có 4 slot.

## Vận hành và giới hạn

- Migration `20261006173556_AddAIProcessingCoordination` chỉ thêm ba bảng; không xóa hoặc sửa bài làm có sẵn. Apply SQL trước khi chạy image mới.
- `edutwin.ai.stage.duration` ghi thời gian job/provider/commit/post-processing; `edutwin.ai.queue.wait` là thời gian từ lúc job đủ điều kiện chạy. Có bộ đếm checkpoint hit, outcome, 429 và token input/output/thinking/cache. Dùng các chỉ số này để chọn concurrency từ quota thực; không chỉ dựa vào số key.
- Không gọi stress test provider thật trong lần triển khai này. Chưa xác minh RPM/TPM/RPD và quyền truy cập của từng project, nên chưa có cam kết thời gian chấm thực tế cho 100 câu.
- Các bản tổng hợp/gợi ý được cập nhật nền. Bằng chứng từng câu được commit ngay; gợi ý có thể xuất hiện sau vài giây khi các câu đã phân tích xong.
- Giai đoạn sau chỉ quyết định dựa trên số đo: prompt/token tối ưu, model phù hợp, caching hoặc batch cho công việc không tương tác. Không giảm phân tích lỗi sai dùng xây dựng lộ trình.

Nguồn quota đã tham khảo trong giai đoạn thiết kế: [Gemini rate limits](https://ai.google.dev/gemini-api/docs/rate-limits), [API troubleshooting](https://ai.google.dev/gemini-api/docs/troubleshooting), [Google AI plans](https://ai.google.dev/gemini-api/docs/google-ai-plans).

## Giai đoạn 2 — microbatch thử nghiệm, 2026-10-07

Đã bật microbatch local tối đa **3 câu**, cửa sổ 150 ms, tối đa 3 ảnh và 24.000 ký tự đầu vào cho một nhóm. Chỉ ghép cùng trung tâm, học sinh, bài tập, ngôn ngữ và phiên bản schema. Luyện tập không có bài tập, bài quá dài hoặc một câu có nhiều ảnh giữ đường gọi riêng, không cắt bỏ dữ liệu. Số nhóm thực tế còn phụ thuộc job có tới cùng cửa sổ hay không; không bảo đảm mọi đề 50 câu chỉ cần đúng 17 calls.

`IPartitionedAIService` mang ngữ cảnh thực thi riêng; không thêm định danh tenant/học sinh vào hợp đồng JSON gửi provider. Một nhóm chỉ dùng một lần gọi provider nhưng có item ID ngẫu nhiên và phân tích riêng từng câu. Parser đối chiếu ID (không dùng vị trí mảng), kiểm tra schema và knowledge node của từng câu. Kết quả thiếu/không hợp lệ chỉ làm câu đó retry; ID lạ/trùng gây loại envelope mơ hồ. Các kết quả tốt đi qua checkpoint, fencing, commit đúng thứ tự, evidence và Twin như trước. Khởi động lại vẫn phục hồi từ SQL; bộ gom RAM không thay hàng đợi bền vững. Cancel một câu không hủy nhóm còn câu khác đang chờ.

Trắc nghiệm vẫn được phân tích AI. Điểm đáp án, rubric, quyền giáo viên, scoped voiding và Evidence Gate không thay đổi. Prompt dùng lại nguyên các quy tắc chống anchoring/ngụy biện và tài liệu giáo viên; không rút bớt dữ liệu lộ trình để tiết kiệm requests.

Migration `20261007054616_AddAIAnalysisProfileProvenance` chỉ thêm nullable `reasoning_analyses.analysis_profile_version`. Provider/model/policy là metadata do server gắn, không cho model tự khai báo. `model_name` cũ nay được điền cho phân tích mới. Checkpoint fingerprint chứa profile; đổi profile làm mất hiệu lực checkpoint cũ. Hai giao diện giáo viên/học sinh hiển thị đúng nguồn Gemini hoặc Groq.

### Runtime local và provider khác

- Model chính vẫn `gemini-2.5-flash`. Đã tách hai project được xác minh thành hai quota pool riêng: mỗi pool 1 in-flight, 5 RPM, 250.000 input TPM, 20 RPD. Năm key chưa ánh xạ project ở chung một pool bảo thủ 1 in-flight, quota số chưa xác minh. Không giả định cả bảy cùng quota hoặc cùng project. Bộ đếm SQL chỉ biết calls của ứng dụng; Google vẫn là nguồn giới hạn thực khi tài khoản có hoạt động bên ngoài.
- GenerateContent 429: nhận biết quota ID ngày khác quota ID phút; quota ngày cooldown tới reset Pacific, quota tạm dùng backoff và retryDelay khi có. Không áp dụng nguyên xi tên lỗi của Interactions API. Không log nội dung lỗi provider/key/bài làm.
- Có adapter Groq dùng model cố định `qwen/qwen3.8-27b`, ảnh và JSON mode + parser/semantic validation ở server. Quota SQL tính cả output, TPD và cửa sổ ngày bảo thủ 24 giờ; không chỉ nhìn RPD. Không tự bỏ ảnh hoặc đổi model khi gặp lỗi.
- Groq **chưa bật/chưa benchmark thật**, vì chưa có key. Chọn Groq hoặc override Gemini model khác phải bật `AIGrading__AlternativeProfileApproved=true` sau khi người vận hành nghiệm thu chất lượng/điều khoản; đây không phải mức confidence AI. Chưa thêm failover tự động sang model chưa được nghiệm thu.
- Không cài/chạy local model. Không đổi billing. Không gửi bài thật/hồ sơ học sinh trong benchmark.

```dotenv
AIGrading__MicroBatchEnabled=true
AIGrading__BatchSize=3
AIGrading__BatchWindow=00:00:00.150
AIGrading__Provider=Gemini
AIGrading__GeminiModel=
AIGrading__AlternativeProfileApproved=false
# Thiết lập riêng, không commit: AIGrading__GroqApiKey; AIGrading__GroqOrganizationId
```

### Kiểm chứng

- Backend: 3.840 pass, 0 fail, 67 MySQL tests skip trong lượt full suite; 18 kiểm thử MySQL liên quan chạy riêng và pass, 0 skip, với fixture database, không dùng database đang test thủ công. EF không có model drift.
- Frontend: 543 pass, 0 fail; TypeScript/Vite build đạt (cảnh báo bundle MathLive hiện có vẫn còn).
- Fake-provider microbatch: 50 câu/size 3 → 17 calls; 100 câu/size 5 → 20 calls, đủ kết quả riêng; isolation, missing/duplicate/unknown ID, partial failure, cancellation và ảnh được kiểm tra. Đây là số calls trong test gom đủ nhóm, không phải độ trễ Gemini cho 50/100 câu thật.
- Provider thật: 5 tình huống toán giả (trắc nghiệm, miền xác định tương đương, ngụy biện gạch chữ số, phương pháp khác mẫu, thiếu nghiệm). Hai request mỗi model, không retry/load test: cả Gemini 2.5 Flash và 3.5 Flash-Lite có JSON hợp lệ và 5/5 kết luận đúng/sai/lập luận đúng kỳ vọng. Báo cáo chi tiết riêng nằm trong `storage/verification`, không chứa key/dữ liệu học sinh.

| Nhóm trong cùng tập thử | Gemini 2.5 Flash | Gemini 3.5 Flash-Lite |
|---|---:|---:|
| 3 câu | 12,74 s | 5,81 s |
| 2 câu | 10,50 s | 3,23 s |

Đây chỉ là hai mẫu latency/model, không phải P95 hay nghiệm thu chất lượng toàn bộ môn học/ảnh chữ viết tay. Flash-Lite nhận diện tên phương pháp đủ cả năm câu trong tập thử; 2.5 để trống tên ở vài câu đơn giản. Chưa dùng kết quả nhỏ này để tự đổi model chính. Bước sau là tập chuẩn có giáo viên đối chiếu, ảnh nháp, bài dài, các môn khác và benchmark pipeline hoàn chỉnh.

Công cụ benchmark cô lập (chỉ dữ liệu giả, tối đa hai requests/lượt; chạy từ root repository):

```powershell
dotnet run --project scripts/ai-grading-benchmark -- --live --model gemini-3.5-flash-lite --key-index 1 --batch-size 3
```

Docker API/web đã build và cập nhật. SQL áp dụng có backup trong `%TEMP%/EduTwin-backups`, readiness đạt; kiểm kê users/students/classes/questions/assignments/attempts/analyses/evidence/history giữ nguyên. Chưa commit/push.

## Giai đoạn 3 — nghiệm thu pipeline và trial Flash-Lite, 2026-10-07

Người dùng xác nhận năm key còn lại lấy từ năm Gmail riêng của bạn bè và yêu cầu tiếp tục, không yêu cầu đăng nhập các tài khoản đó. Không cần mật khẩu tài khoản để dùng key. Project của key 3–7 dùng nhãn nội bộ `owner-reported-independent-project-N`; đây không phải project ID Google đã được đọc từ dashboard. Mỗi key có một pool riêng theo xác nhận của người dùng. Đối với Flash-Lite, pool 2 dùng giới hạn UI đã thấy 15 RPM/250.000 input TPM/500 RPD; các pool khác dùng trần ứng dụng bảo thủ 5 RPM/250.000 input TPM/20 RPD. Không khẳng định các trần bảo thủ là quota thực của Google, và không tính bộ đếm SQL là số lượt Google còn lại khi chủ tài khoản dùng API ở nơi khác.

### Kiểm tra quyền model bằng API, không đăng nhập Gmail

Mỗi probe gửi một câu toán giả, không gửi dữ liệu học sinh. Kết quả:

| Key | Gemini 2.5 Flash | Gemini 3.5 Flash-Lite |
|---|---|---|
| 3 | 503 tạm thời trong lượt thử, không kết luận key hỏng | JSON hợp lệ |
| 4–6 | HTTP 404 trong lượt thử | JSON hợp lệ |
| 7 | JSON hợp lệ | JSON hợp lệ |

Google hiện giới hạn model 2.5 cho người đã chủ động dùng trước đó; project mới được hướng sang model mới. Kết quả probe phù hợp với khác biệt quyền truy cập model, không phải chứng cứ key 4–6 bị vô hiệu. Xem [tài liệu model 2.5](https://ai.google.dev/gemini-api/docs/models/gemini-2.5-flash). Client nay thử project khác khi gặp 401/403/404 và nhớ credential/model bị từ chối trong 15 phút; không retry credential đó liên tục, không log key và không tự đổi sang một model chưa được chấp thuận.

### Pipeline quan hệ 50/100 câu

Thêm hai integration tests từ **bài nộp đã lưu** → discovery/lease → microbatch → quota SQL → strict parsing → checkpoint/fencing → commit → evidence/Twin/history/progress → recommendation thật và cache nhận xét tổng hợp giả. Provider và nội dung nhận xét tổng hợp là giả; MySQL, worker, request factory, kiểm tra ảnh, parser, checkpoint, chính sách evidence, cập nhật Twin và tiến độ là code thật. Bài 100 câu còn mô phỏng quota 429 một lần và một phản hồi ngoài miền điểm để kiểm tra partial retry. Không test Chrome, không ghi dữ liệu vào database `edutwin` đang sử dụng.

Phát hiện và sửa vòng retry thừa: checkpoint của câu sau được discovery giữ lại tới khi các bài nộp trước của cùng học sinh hết pending; inference mới vẫn song song. Kiểm tra thứ tự trong transaction vẫn giữ nguyên. Khi có tiến triển, worker đọc tiếp ngay; checkpoint bị chặn không cần thêm một giây delay sau khi được mở khóa. Đây là tối ưu lên lịch/SQL, không thay thứ tự Mastery.

| Chỉ số lượt đo cuối, provider giả | 50 câu bình thường | 100 câu có lỗi mô phỏng |
|---|---:|---:|
| Calls provider | 25 | 52 |
| Ảnh truyền đúng hash | 10 | 20 |
| Evidence / history riêng | 50 / 50 | 100 / 100 |
| Trùng evidence/history / fallback | 0 / 0 / 0 | 0 / 0 / 0 |
| Retry scheduled (gồm quota/order) | 47 | 203 |
| Wall time xử lý, không gồm tạo fixture | 35,6 s | 83,0 s |
| Completion P95 trong test | 34,7 s | 80,8 s |

Trước bộ lọc checkpoint, test tương ứng ghi 748/1.031 retry và 112,6/150,4 s. Các lần đo có tải máy khác nhau, nên số retry và kiểm chứng integrity là bằng chứng chính; không dùng tỷ lệ wall time làm cam kết provider thật. Cửa sổ 150 ms/bốn slot không luôn gom đủ ba câu, vì vậy calls thực tế trong pipeline nhiều hơn benchmark gom sẵn 17/34. Giữ bốn slot đã kiểm chứng; thử sáu slot không làm giảm calls và có một lượt lỗi, không chọn làm mặc định. Transport fixture đã được chỉnh để lỗi giải phóng lease không biến thành thất bại chấm, đúng semantics client production.

### Độ đúng và lựa chọn model local trial

Tập mở rộng 13 tình huống có nguyên hàm từng phần, nghiệm ngoại lai, chia cho 0, đơn vị vật lý, tiếng Anh, cân bằng hóa học và hai ảnh nháp tổng hợp. Ảnh được vẽ bằng code/font, **không phải** bộ ảnh chữ viết tay thực. Đáp án cuối đúng nhưng ảnh tính sai phải nhận AnswerAssessment Correct / ReasoningVerdict Invalid; cần giáo viên xem lập luận, không tự đổi điểm đáp án.

- Gemini 2.5 Flash: lượt đầu 13/13, năm calls tổng khoảng 86,1 s.
- Flash-Lite với temperature=0: 12/13 ở hai lượt; nhận ra lỗi lập luận trong ảnh nhưng nhầm đánh giá đáp án cuối thành Incorrect.
- Làm rõ nguồn finalAnswer độc lập với reasoning/scratchpad. Theo [khuyến nghị Google cho Gemini 3.x](https://ai.google.dev/gemini-api/docs/troubleshooting), giữ temperature mặc định 1 cho model 3.x. Lượt nghiệm thu sau đó đạt 13/13, năm calls tổng khoảng 19,7 s. Đây là một lượt smoke chất lượng, không phải P95 sản xuất hoặc chứng nhận toàn bộ môn học.
- Bật **trial local** model `gemini-3.5-flash-lite`, microbatch tối đa 3 câu, bốn slot job; không đổi billing và chưa bật Groq/local model. Profile `method-agnostic-v2` ghi cả temperature để checkpoint/analysis có đúng provenance; khi khôi phục một fallback cũng giữ phiên bản profile trong snapshot lịch sử.

Backend full: 3.847 pass, 0 fail, 69 relational tests skip do chưa cấp connection trong lượt full; hai bulk MySQL chạy riêng pass, 0 skip. Bộ MySQL 18 trường hợp được chạy lại trên code cuối và pass, 0 skip (5 phút 16 giây); EF không có model drift. Frontend không đổi logic trong giai đoạn này, lượt đã kiểm chứng 543 pass/build đạt. API Docker được build/cập nhật theo code mới, giữ schema/volume và dữ liệu người dùng. Chưa coi đây là nghiệm thu production hay phép đo 50/100 câu bằng provider thật; không tự tuyên bố đã xác minh dashboard quota cả bảy project.

### Chuẩn bị SQL cho các script rollout local

Các script `scripts/ops/apply_ai_processing_local.cjs` và `apply_ai_microbatch_local.cjs` chỉ đọc SQL đã được người vận hành rà soát; không tự sinh hay tự xóa database. Trước khi dùng trên máy khác, tạo `storage/migrations` và xuất hai khoảng migration bằng `dotnet ef migrations script` với project `src/EduTwin.DAL`, startup project `src/EduTwin.API`:

- `20261006140310_AddAcademicSharingAndRubricHistory` → `20261006173556_AddAIProcessingCoordination`, output `storage/migrations/20261006173556_AddAIProcessingCoordination.sql`.
- `20261006173556_AddAIProcessingCoordination` → `20261007054616_AddAIAnalysisProfileProvenance`, output `storage/migrations/20261007054616_AddAIAnalysisProfileProvenance.sql`.

Rà soát SQL trước khi chạy từng script theo thứ tự; database phải ở phiên bản nguồn hoặc đích được chỉ định và không còn job chấm đang chạy. SQL sinh ra, backup, key và báo cáo thử nghiệm riêng không đưa lên Git.
