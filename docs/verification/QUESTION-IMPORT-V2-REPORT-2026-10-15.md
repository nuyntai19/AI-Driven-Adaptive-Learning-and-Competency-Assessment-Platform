# Kiểm chứng import DOCX và câu hỏi có ảnh — 15/10/2026

Phạm vi người dùng duyệt: **E0/E1/E3/E4** trong [plan](../plans/QUESTION-IMPORT-DOCX-TSV-IMAGES-PLAN-2026-10-15.md). Không triển khai TSV; E5 cần spec riêng. Báo cáo hoàn tất toàn bộ các gate kiểm thử kỹ thuật và nghiệm thu.

## Chức năng và quyết định kỹ thuật

| Phần | Kết quả |
| --- | --- |
| E0 | Chốt template 2.0, `docx-1.0`/`media-1.0`, route V2 riêng, subset OMML và bộ corpus có kiểm thử. Decode dùng SkiaSharp 4.153.1 + Linux.NoDependencies 4.153.1 (MIT); ZIP/XML dùng .NET, không Office automation. |
| E1 | ZIP gồm questions.xlsx/CSV và media; mapping ImageRef tường minh, decode PNG/JPEG/WebP tĩnh, orientation/nền trắng/resize/PNG verifier, staging/ACL/TTL/quota/lease, lưu ảnh cùng transaction Draft. |
| E3 | Mẫu DOCX thật có metadata/môn nguồn, danh mục topic, Guide tiếng Việt và bảng hai cột mỗi câu. Ghép run không thêm khoảng trắng, lỗi có bảng/ô/đoạn, một ảnh inline trong ô ảnh. |
| E4 | Phân số ngang/noBar, căn, sub/super/subsup/prescripts, tích phân/tổng, giới hạn, hàm, delimiter, ma trận/hệ, accents; LaTeX inline/display giữ qua editor. Cấu trúc ngoài subset lỗi rõ. |

Các trường hợp chủ động báo chưa hỗ trợ: phân số Word `lin/skw`, phantom/box/unknown OMML; Word tự do, MathType/OLE, tracked changes/fields, bảng lồng/gộp, dữ kiện ngoài bảng/header/footer/footnote, ảnh crop/xoay/lật/nổi/kéo giãn sai tỷ lệ hoặc ảnh trong option/lời giải. Native OMML không thay đổi giới hạn chấm toán; đáp án native Equation không được dùng TextExact để tránh so sánh sai chuỗi bọc công thức.

Không thêm migration. Không thay guard XLSX/CSV V1 hoặc buộc file V1 phải có ảnh. API_CONTRACTS và UI_UX_SPEC đã bổ sung profile V2; Nginx chỉ nâng request preview V2 lên 32 MiB.

## Corpus và bằng chứng tự động

- Hai template do generator frontend thực tạo được lưu trong `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/Fixtures/Import/frontend-template-v2.docx` và `.zip`; parser đối chiếu ba câu, topic, môn, Unicode/LaTeX và một ảnh. Đây là corpus mẫu có cấu trúc, chưa phải chứng nhận đọc mọi tài liệu từ mọi phiên bản Word.
- Parser kiểm tra 500 bảng câu đầy đủ, run chia giữa lệnh LaTeX, native inline/display, ảnh dùng chung, metadata/version/môn, alias trùng, vị trí nguồn và lỗi theo câu. Corpus bất lợi gồm archive path/CRC/encryption/external content, XML budget, tracked changes/field, bảng lồng/gộp, ảnh ngoài ô/crop/anchor và media thiếu/orphan.
- Converter kiểm tra biểu thức và cấu trúc đầy đủ; unknown/duplicate/native hidden content không bị ghép text hoặc bỏ qua. Test editor hydrate → sửa → serialize → hydrate giữ `$$...$$`, escaped dollar và surrounding text; validator V1/V2 vẫn riêng.
- Media test kiểm tra format/nền trắng, APNG/WebP động, byte/pixel/cancellation, asset ACL/revision/tenant/actor, tamper, lease, TTL/discard, cleanup lỗi giữ quota tới khi retry và Unix link/mode.
- Full frontend: **770/770 unit test đạt**, TypeScript đạt, build production đạt, bundle **4/4 đạt**, lint phạm vi thay đổi đạt. Có kiểm thử event handler thật cho việc chờ DELETE phiên cũ trước POST mới và dọn token trả sai profile.
- Linux smoke: chạy normalizer trong image runtime API với network tắt, output PNG 1×1/84 bytes được verifier chấp nhận. Native library không cần cài font/GUI/Office.
- Linux container SDK: **25/25 media/use-case test đạt**, gồm Unix mode/symlink thực, tất cả EXIF 1–8, ảnh động thật, quota, cleanup fault, ảnh dùng chung và TextExact guard. Log `scratch/import-tools/import-v2-linux-tests.log`. Nginx dùng đúng file mount theo Dockerfile: `nginx -t` đạt, log `scratch/import-tools/import-v2-nginx.log`.

Log frontend cuối: `scratch/import-tools/import-v2-web-final-{tests,build}.log`; log chi tiết agent `scratch/import-v2-fixtures/frontend-{unit,tsc,build,bundle,lint}.log`. Cảnh báo chunk MathLive lớn đã có từ trước; bundle budget vẫn đạt.

## MySQL thật và benchmark

Chạy container QA riêng `codex-question-import-mysql-20261012` (MySQL 8.0.35, port 33307); mỗi fact tạo database tên GUID riêng và dọn đúng database đó. Không dùng database ứng dụng chính. `max_allowed_packet=67.108.864` bytes.

Lần chạy đầu **2/2 fact đạt** (`question-import-v2-mysql.trx`):

| Lô | Xem trước | Xem trước + commit | Ghi chú |
| --- | --- | --- | --- |
| CSV 500 câu V1 | 103 ms | 1.254 ms | 65.457.768 bytes allocated |
| V2 500 câu, 20 JPEG | 13.071 ms | 16.319 ms | Upload 22.524.290 bytes; PNG chuẩn hóa 28.957.880 bytes; 216.484.144 bytes allocated; process peak working set 405.561.344 bytes |

Fact V2 gây lỗi **sau SaveChanges đã INSERT ảnh vào MySQL**, kiểm tra không còn Question/Option/Image, giữ ảnh preview, rồi confirm lại chỉ tạo đúng một lô. Kiểm tra HasImage/BLOB/hash và consume token/dọn PNG sau commit. Các bước bổ sung editor thay/xóa/khôi phục ảnh, activation, student ACL và AI context được chạy lại trong vòng kiểm chứng cuối.

Số đo phụ thuộc máy, không là bảo đảm latency cho mọi nguồn. Ngưỡng vận hành ban đầu để theo dõi: lô corpus này preview ≤30 giây và preview+commit ≤45 giây, working set của process test ≤768 MiB. Decode một worker/process, byte/pixel/field/XML bounded; cancellation kiểm tra giữa native calls. Nguồn sát 24 triệu pixel có thể khác thời gian corpus. Chưa tuyên bố SLO peak cho mọi biểu đồ/ảnh đầu vào.

## Phạm vi vận hành

Staging local, snapshot process-local, một instance; restart làm phiên cũ mất hiệu lực và startup sweep dọn orphan. Linux directory 0700/file 0600; trên Windows cần chạy tài khoản dịch vụ riêng và đặt temp/staging trên vùng có ACL riêng của tài khoản đó. Không dùng volume staging cho nhiều OS user hoặc public webroot. Không có durable receipt/multi-instance; commit không xác định yêu cầu kiểm tra ngân hàng trước khi nhập lại.

Ảnh được normalize lại ở server, không OCR/tự suy đáp án; giáo viên phải xem độ rõ tại preview. Không gọi API AI bên ngoài trong kiểm thử: kiểm chứng context ảnh bằng service ghi nhận/request factory và pipeline test hiện có.

## Kiểm chứng cuối

- Full backend Release: **4.357 đạt, 104 bỏ qua có điều kiện, 0 lỗi**, tổng 4.461. Lượt đầu phát hiện thiếu attribute policy tại method theo quy ước coverage của repo; đã chuyển sang khai báo rõ policy mỗi endpoint và chạy lại toàn bộ đạt. TRX `question-import-v2-full-final-pass.trx`, log `scratch/import-tools/import-v2-backend-full-final.log`.
- Browser QA trên API 5501/web 3302 và database QA riêng: tải mẫu Word theo môn thật; thêm Equation OMML dạng khối và đồ thị tổng hợp 800×450 → preview **3 hợp lệ/0 lỗi** → phóng/thu ảnh → lưu **3 Draft (#20036–20038)**. Mở #20036, xác nhận công thức trong popover, thêm chữ, lưu rồi mở lại: `data-math-display=true`, LaTeX `y={x}^{2}`, ảnh vẫn 800×450 và nội dung sửa còn nguyên.
- Browser ZIP: tải gói V2 thật từ UI → preview **3 hợp lệ/0 lỗi**, có ImageRef/ảnh; chọn rõ Q001 → lưu **1 Draft (#20039), bỏ qua 2 câu** đúng lựa chọn. Không upload lên hệ thống bên ngoài hoặc gọi AI live.
- Ảnh bằng chứng local: [DOCX preview](evidence/question-import-v2-docx-preview-20261015.jpg). Ảnh test là đồ thị tổng hợp, không dữ liệu học sinh.
- MySQL vòng cuối mở rộng đạt **2/2 fact** (TRX `tests/EduTwin.BLL.Tests/TestResults/question-import-v2-mysql-final.trx`, log `scratch/import-tools/import-v2-mysql-final.log`):
  - Xác nhận chu trình ảnh hoàn chỉnh: import câu có ảnh → Draft → editor thay ảnh mới (dataUrl) → editor xóa ảnh (`HasImage=false`, blob bị dọn) → editor khôi phục ảnh gốc → kích hoạt câu hỏi (Draft → Active thành công).
  - Xác nhận phân quyền học sinh (student ACL): học sinh không đọc được ảnh khi chưa làm bài (`null`); sau khi nộp Attempt thì đọc được đúng binary PNG của câu hỏi.
  - Xác nhận tích hợp AI: `AIAnalysisRequestFactory` đóng gói chính xác ảnh câu hỏi vào `Question.ImageParts` và không lẫn vào `StudentSubmission.ImageParts`.
  - Benchmark cuối: Lô CSV 500 câu V1 xem trước 102 ms / xem trước + commit 1.232 ms (allocated 65 MB); Lô V2 500 câu + 20 ảnh JPEG (upload 22,5 MB, PNG chuẩn hóa 28,9 MB) xem trước 12.764 ms / xem trước + commit 15.584 ms (allocated 300 MB, process peak working set 508 MB). Toàn bộ nằm trong ngưỡng vận hành đã định.
