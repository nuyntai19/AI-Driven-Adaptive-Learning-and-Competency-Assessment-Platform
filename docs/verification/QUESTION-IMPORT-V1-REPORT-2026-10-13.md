# Kiểm chứng import ngân hàng câu hỏi V1 — 13/10/2026

> Phạm vi: [plan ngày 12/10/2026](../plan/QUESTION-BANK-IMPORT-PLAN-2026-10-12.md), P0–P3. P4/P5 là roadmap có điều kiện.
> Trạng thái: **COMPLETED-v1 (P0–P3)**. Các sửa resource cuối đã có regression tests và full suite/MySQL pass.

## Hành vi đã triển khai

- Giáo viên chọn môn đích và defaults cho lô; mỗi câu resolve `TopicCode` riêng thuộc môn/trung tâm đang hoạt động. Owner là giáo viên đăng nhập, không nhận `TeacherId` tùy ý.
- Template XLSX theo môn có `Questions`, `Topics`, `Guide`, version 1.0, dropdown và ô Text. CSV UTF-8 vẫn được hỗ trợ; legacy với topic chung phải bật rõ.
- Đọc XLSX qua workbook relationships, shared/inline/rich strings và sparse cells; đọc CSV quoted multiline với vị trí dòng gốc. Header/kiểu ô/boolean/enum sai và dữ liệu ngoài profile đều báo lỗi.
- Preview đầy đủ đề, lựa chọn, đáp án, solution, expected reasoning, grading notes, JSON arrays và metadata/defaults. `invalidCount` đếm câu, `errorCount` đếm thông báo.
- Inline LaTeX `$...$` giữ source. Browser dùng KaTeX có giới hạn; backend kiểm tra cấu trúc, safety và grammar mode đáp án. Escaped dollar không bị tách thành công thức khi hiển thị hoặc mở/lưu editor.
- Confirm lấy snapshot server theo token/revision. `AllValid` chỉ khi không có câu lỗi; `SelectedValid` nhận tập key hợp lệ được chọn rõ và trả số bỏ qua. Chỉ tạo Draft, không bypass quyền kích hoạt.
- Confirm kiểm tra lại actor/owner/môn/topic; transaction lưu cả tập hoặc rollback. Khóa phiên chống confirm đồng thời; lỗi rollback cho retry, commit không xác định tiêu thụ token và yêu cầu kiểm tra ngân hàng.
- `ScoringNotes` độc lập với `Solution`; `LanguageCode` giữ qua editor. Option misconception lưu trong JSON `grading_criteria.optionMisconceptions` hiện có và projection theo option label, không thêm migration.

## Giới hạn tài nguyên

| Hạng mục | V1 |
| --- | --- |
| Upload | 10 MiB; multipart HTTP 11 MiB cho metadata/overhead |
| XLSX | 50 MiB giải nén thực, 200 entries, kiểm tra CRC/encryption |
| XML | 200.000 nodes/part; depth 64; không DTD/external resolver |
| Dữ liệu | 500 câu; 128 cột có dữ liệu/hàng; 20.000 ký tự/field |
| Math | 2.000 ký tự/formula; dừng sớm với formula quá dài; quét delimiter/braces và regex tuyến tính |
| Nội dung trước normalization | 10 MiB UTF-8, tính từng lần dùng shared string và cả dòng lỗi |
| Snapshot JSON | 30 MiB cả length và capacity; quota trước deserialize clone |
| Phiên | 30 phút; 8/actor, 256/process, tổng snapshot 30 MiB; timer cleanup |
| Admission | 2 preview đồng thời; `IMPORT_BUSY` khi đầy, cancellation giải phóng slot |

Shared strings được chuẩn hóa newline một lần và các cell dùng lại cùng reference. Fixture XLSX nhỏ lặp chuỗi CR hàng chục nghìn lần bị chặn trong parser trước khi tạo hàng loạt dữ liệu chuẩn hóa. Fixture CSV có ký tự control làm JSON mở rộng bị chặn bằng stream giới hạn. Các giới hạn trên là admission/buffer bounds; số liệu cấp phát benchmark không được diễn giải thành RSS tối đa của toàn ứng dụng.

## Kết quả kiểm thử

| Kiểm tra | Kết quả |
| --- | --- |
| Parser Release | 31 pass, 0 fail; Debug cũng 31 pass |
| Backend/controller Release | 79 pass, 0 fail |
| Full .NET Release | 4.246 pass, 103 skip, 0 fail; tổng 4.349 |
| Frontend | 723 pass, 0 fail |
| TypeScript / production Vite build | Pass |
| Bundle checks | 4 pass, 0 fail; không có static chunk cycle/vendor → entry |
| MySQL thật | 1 pass, 0 skip/fail trên bản cuối; transaction/persistence/rollback/retry/500 câu |
| Browser production frontend + Release API | XLSX thật → preview 3/3 → Draft 3/3; mở/lưu editor và bank render đúng |

Các test gồm cross-tenant/actor, subject/topic/owner bị đổi sau preview, revision/key không hợp lệ, quota/expiry/cancellation/concurrent confirm, điểm decimal(5,2), grade 10–12, mode đáp án, formula/cached-value/date/drawing, JSON arrays và round-trip misconception/ngôn ngữ.

Các test skip của full suite thuộc nhóm integration yêu cầu môi trường riêng. MySQL test riêng của import đã chạy với database tên `edutwin_import_<guid>` được tạo tạm và dọn trong `finally`, không dùng database chính. Fixture kiểm thử có dữ liệu tổng hợp.

## MySQL và benchmark

Test `QuestionImportMySqlTests.Confirm_PersistsTopicsAndFeedback_RollsBackFailedBatch_AndImports500Drafts`:

1. Áp migration hiện có lên database kiểm thử mới.
2. Import câu ở hai topic, kiểm tra Draft/grade/language/visibility/scoring notes/misconception sau đọc lại DB.
3. Cố tình lỗi INSERT options sau khi questions đã INSERT, xác nhận rollback cả tập, rồi retry cùng token thành công.
4. Import 500 short-answer Draft; xác nhận toàn bộ số câu/option và không tạo Active.

Lần đo bản cuối: CSV 500 câu, preview **189 ms**, preview + commit **1.642 ms**, tổng cấp phát .NET **65.395.392 byte**. Đây là một phép đo trên MySQL local 8.0.35, .NET 10 Release, không phải SLO hay load test nhiều người dùng. Ghi batch hai lần SaveChanges (questions rồi options) giảm việc quét tracking lặp lại; benchmark trước tối ưu là 5.959 ms và 964.982.888 byte.

TRX local trong `scratch/import-tools/test-results/`: `question-import-v1-final.trx` (full suite) và `question-import-mysql-final.trx` (MySQL/benchmark). Output cuối trong `scratch/import-tools/full-suite-v1-final.log` và `mysql-v1-final.log`. Các file runtime này được ignored; reusable tests/fixtures nằm trong source repository.

## Kiểm thử qua trình duyệt

Frontend production phục vụ local ở cổng 3302, Release API ở cổng 5501 và MySQL kiểm thử riêng ở cổng 33307.

- Dùng workbook `EduTwin_Questions_MATH_V1.xlsx` tải từ generator thực, chọn môn Toán/khối 12.
- Preview báo 3 câu đủ điều kiện, 0 lỗi; các topic `MATH-FUNCTIONS`, `MATH-EXP-LOG`, `MATH-ANTIDERIVATIVE` được resolve riêng.
- Mở chi tiết câu phân số: đề, đáp án và lời giải hiển thị KaTeX đúng; mode `NumericRational`, metadata grade/topic đúng.
- Confirm `AllValid` thành công: 3 Draft ID `20030`, `20031`, `20032`; modal hiển thị link mở từng câu.
- Mở câu `20031` trong editor, xem source `Tính $\frac{1}{2}+\frac{1}{3}$.`, lưu lại thành công. Ngân hàng vẫn Draft, đáp án `5/6`, khối 12 và phân số không double-escape.
- Khởi động API Release sau các sửa resource cuối và chạy lại preview cùng workbook: vẫn 3 câu hợp lệ, 0 lỗi. Test MySQL bản cuối kiểm chứng confirm/rollback ở bản này.

Bằng chứng local (ignored theo chính sách repository):

- [Kết quả tạo 3 Draft](evidence/question-import-v1/import-draft-result.jpg)
- [Ngân hàng sau mở/lưu editor](evidence/question-import-v1/draft-bank-after-edit.jpg)

Browser smoke này kiểm tra luồng giáo viên. Rendering học sinh dùng chung `RichMathText` và các test math/student hiện có; không tuyên bố đã thực hiện browser E2E mới cho toàn bộ assignment/submission của học sinh hoặc publish các câu thử nghiệm.

## Phạm vi còn lại và vận hành

- P4: rubric/mapping nhiều sheet, block math round-trip, media package và JSON exchange.
- P5: DOCX/GIFT/Moodle XML/QTI; OCR/AI trong nhiệm vụ riêng.
- Server không có engine KaTeX đầy đủ; draft có thể được tạo qua API với command chưa được client kiểm tra. Publish policy hiện có vẫn áp dụng, giáo viên rà soát trước kích hoạt.
- Session process-local mất khi restart và không chia sẻ nhiều instance; chưa có durable receipt hoặc idempotency qua restart/commit uncertainty.
- Không thêm migration/package backend, không commit/push. Build và browser QA dùng dịch vụ/cơ sở dữ liệu tạm; Docker image/stack chính chưa được rebuild hay rollout. Cần build lại ứng dụng để dùng source mới.
- Các thay đổi có sẵn trong `run.bat` và `run.ps1` được giữ nguyên, không thuộc phần triển khai này.

Contract/spec đã đồng bộ: `API_CONTRACTS.md` mục 48.1–48.2, `UI_UX_SPEC.md` mục 9.1, amendment `CONSTITUTION.md` và mô tả JSON criteria trong `DATABASE_SCHEMA.md`.
