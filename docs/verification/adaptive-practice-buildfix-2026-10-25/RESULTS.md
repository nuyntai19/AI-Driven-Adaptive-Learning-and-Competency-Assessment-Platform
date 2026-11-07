# Kết quả sửa build và tương thích model/DTO

Ngày: 25/10/2026 (Asia/Saigon). Branch: `origin/SkinnySnail/adaptive-practice`. Base HEAD: `dd47b89dc5133022f7cb298c9bdf0adde5dbab2a`; các sửa đổi còn trong working tree, chưa commit.

**API và web đã build Docker thành công. Bộ test chọn lọc có 17 PASS, 0 FAIL.**

| Xác minh | Kết quả | Bằng chứng |
|---|---|---|
| Build API Release qua Dockerfile dự án | PASS, exit 0 | [api-build.txt](api-build.txt) |
| Build frontend TypeScript + Vite qua Dockerfile dự án | PASS, exit 0 | [web-build.txt](web-build.txt) |
| Adaptive Practice engine | 9/9 PASS | [adaptive-buildfix.trx](adaptive-buildfix.trx) |
| Tương thích model/DTO và EF metadata | 2/2 PASS | [adaptive-buildfix.trx](adaptive-buildfix.trx) |
| TeacherStudentNotes fixtures/use cases | 6/6 PASS | [adaptive-buildfix.trx](adaptive-buildfix.trx) |
| Response SubjectDto qua HTTP thật | PASS với 2 môn học: name=subjectName, code=subjectCode | [subject-dto-http.json](subject-dto-http.json) |
| HTTP/MySQL acceptance bổ sung | 10 PASS, 1 FAIL, 25 BLOCKED | [adaptive_api_results.json](adaptive_api_results.json) |
| Cleanup môi trường E2E | PASS: schema/user/container test đã được dọn theo ownership manifest | [cleanup.txt](cleanup.txt) |

## Thay đổi

- Thêm `KnowledgeNode.Name` làm alias đọc/ghi của `NodeName`, và `Subject.Name` làm alias của `SubjectName`. Cả hai được đánh dấu `NotMapped`, không tạo cột database mới.
- Thêm `SubjectDto.Name/Code` là alias chỉ đọc. API camelCase trả thêm `name/code`, đồng thời giữ `subjectName/subjectCode`; TypeScript khai báo các alias tương thích là optional, readonly.
- Trang Adaptive Practice Overview dùng `subjectName/subjectCode` và bỏ biến auth không dùng.
- Engine lọc bằng thuộc tính EF đã map `IsActive`, kiểm tra navigation Assignment nullable, dùng đúng DTO lựa chọn câu hỏi và `OptionLabel/OptionText`, lấy tên học sinh từ `Student.FullName`.
- Giữ tracking nhất quán khi liên kết Subject/KnowledgeNode với phiên/câu hỏi, tránh gắn hai instance cùng khóa trong một DbContext.
- Cập nhật fixture Adaptive Practice theo model thực tế: tên trường, trạng thái, required strings và timestamp UTC. Cấu hình test InMemory riêng cho Attempt có ID được engine cấp; không thay phạm vi ID hay cách sinh ID trong production.
- Cập nhật fixture ghi chú giáo viên: bỏ `PasswordSalt` đã không còn trong model, bổ sung timestamp UTC; chuẩn bị một lần cập nhật thực sự để tạo row version cũ và thêm Student thứ hai cho ca liệt kê. Giữ nguyên các assertion nghiệp vụ.

## Phạm vi và giới hạn

Các build cuối sử dụng tag riêng `edutwin-adaptive-e2e-api:dd47b89-buildfix` và `edutwin-adaptive-e2e-web:dd47b89-buildfix`, có label working tree; không thay image/container ứng dụng chính. Test .NET build toàn bộ project test và thực thi riêng nhóm AdaptivePractice cùng TeacherStudentNotes, không phải toàn bộ regression suite.

Runtime E2E đã khởi động trên schema riêng. Login, proposal ba chế độ và một số kiểm tra phân quyền đã qua. Ca APAPI09 thất bại vì thiếu `adaptive_practice_sessions`; các bảng còn lại của phân hệ cũng cần migration hợp lệ. Bước build thành công không giải quyết việc migration Adaptive Practice chưa được EF áp dụng.

HTTP acceptance và kiểm tra SubjectDto được chạy trên image buildfix đầu tiên, trước chỉnh sửa tracking được phát hiện trong unit test. Image API cuối đã được rebuild và toàn bộ 17 test đã chạy lại thành công sau chỉnh sửa đó. Không gắn nhãn PASS cho các luồng phiên chưa chạy được do schema thiếu. Browser E2E chưa được thực thi.

Không sửa thuật toán chấm/adaptation hoặc các thiếu sót nghiệp vụ khác trong lượt sửa build này. Kết quả cũ trong thư mục `adaptive-practice-e2e-2026-10-25` được giữ nguyên như bằng chứng trước khi sửa; dùng báo cáo này cho kết quả mới.

## Tái hiện test .NET

Trong container SDK .NET 10, chạy:

```text
dotnet test tests/EduTwin.BLL.Tests/EduTwin.BLL.Tests.csproj --filter "FullyQualifiedName~AdaptivePractice|FullyQualifiedName~TeacherStudentNotesUseCaseTests"
```

Các bước tiếp theo để hoàn tất E2E là sửa metadata/snapshot của migration Adaptive Practice, áp dụng vào schema test mới và chạy lại suite HTTP/MySQL trước các kiểm tra trình duyệt.
