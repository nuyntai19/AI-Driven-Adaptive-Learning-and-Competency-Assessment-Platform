# Bảng phân loại tri thức hiện có để duyệt

Ngày: 22/10/2026. Trạng thái: **ĐÃ ĐƯỢC NGƯỜI DÙNG DUYỆT làm phân loại ban đầu** trong chat triển khai ngày 22/10/2026.

Xác nhận của người dùng: “Duyệt bảng này làm phân loại ban đầu”. Việc duyệt chỉ áp dụng đúng bảng dưới đây; không tự mở rộng theo những liên kết trong bản nháp V2 hoặc suy đoán từ mã node chưa phân loại.

Nguồn: audit SELECT MySQL local ngày 22/10. Khối trong bảng là phạm vi đang được giáo trình Published sử dụng; không phải suy luận từ mã/tên hoặc xác nhận chuyên môn của hệ thống. Bản nháp V2 không được dùng để tự mở rộng phạm vi khối của thư viện.

| Trung tâm | NodeId | Mã tri thức | Khối đề xuất theo Published | Bằng chứng khác cần lưu ý |
| --- | --- | --- | --- | --- |
| EDUTWIN_A | 10000 | MATH-FUNCTIONS | 10 | Cũng nằm trong Draft khối 12 V2 |
| EDUTWIN_A | 10001 | MATH-EXP-LOG | 11 | Cũng nằm trong Draft khối 12 V2 |
| EDUTWIN_A | 10002 | MATH-ANTIDERIVATIVE | 12 | Cũng nằm trong Draft khối 12 V2 |
| EDUTWIN_A | 10003 | ENG-TENSES | 10, 11, 12 | Cùng node hiện dùng trong 3 giáo trình Published |
| EDUTWIN_A | 10004 | ENG-RELATIVE-CLAUSES | 10, 11, 12 | Cùng node hiện dùng trong 3 giáo trình Published |
| EDUTWIN_A | 10005 | ENG-CONTEXT-VOCAB | 10, 11, 12 | Cùng node hiện dùng trong 3 giáo trình Published |
| EDUTWIN_A | 20006 | MATH10_PTBH | Chưa phân loại | Chưa thuộc giáo trình; không suy khối từ chữ 10 trong mã |
| EDUTWIN_B | 20000 | MATH-FUNCTIONS | 10 | Theo giáo trình Published đang dùng |
| EDUTWIN_B | 20001 | MATH-EXP-LOG | 11 | Theo giáo trình Published đang dùng |
| EDUTWIN_B | 20002 | MATH-ANTIDERIVATIVE | 12 | Theo giáo trình Published đang dùng |
| EDUTWIN_B | 20003 | ENG-TENSES | 10, 11, 12 | Cùng node hiện dùng trong 3 giáo trình Published |
| EDUTWIN_B | 20004 | ENG-RELATIVE-CLAUSES | 10, 11, 12 | Cùng node hiện dùng trong 3 giáo trình Published |
| EDUTWIN_B | 20005 | ENG-CONTEXT-VOCAB | 10, 11, 12 | Cùng node hiện dùng trong 3 giáo trình Published |

Nếu duyệt bảng này làm phân loại ban đầu: thư viện mới nhận đúng các khối trong cột đề xuất; `MATH10_PTBH` vẫn chưa phân loại. Cấu trúc Published cũ được chốt thành phiên bản giữ lịch sử; không thay NodeId, câu hỏi, điểm, bài làm hoặc mastery. Những cạnh prerequisite đi ra ngoài khối được giữ thành ngữ cảnh riêng, không tự đưa đầu nguồn vào chương trình đích.

Nếu thay đổi phân loại chuyên môn so với phạm vi Published đang dùng: ghi rõ node và khối mong muốn, áp dụng vào phiên bản thư viện mới; không viết lại cấu trúc lịch sử của giáo trình cũ.
