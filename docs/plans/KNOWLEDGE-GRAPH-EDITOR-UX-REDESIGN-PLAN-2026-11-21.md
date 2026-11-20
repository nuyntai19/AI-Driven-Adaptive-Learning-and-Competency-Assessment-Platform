# Kế hoạch thiết kế lại giao diện chỉnh sửa khung đồ thị tri thức

- Ngày: **21/11/2026**, múi giờ Asia/Bangkok.
- Trạng thái: **Đã được người dùng duyệt và đã triển khai giao diện ngày 21/11/2026**. Xem [báo cáo triển khai và kiểm chứng](../verification/KNOWLEDGE-GRAPH-EDITOR-UX-IMPLEMENTATION-2026-11-21.md).
- Phạm vi: giao diện soạn khung tri thức, bộ lọc, tương tác trên đồ thị, hướng dẫn và trình bày lỗi.
- Ràng buộc bắt buộc: **giữ nguyên logic nghiệp vụ, API, database, quyền và các điều kiện lưu/duyệt hiện có**.
- Cơ sở khảo sát: bốn ảnh người dùng cung cấp trong cuộc trò chuyện và mã nguồn tại HEAD `1e22ca7`, có đối chiếu các tệp đang làm việc. Số dòng có thể thay đổi; tên tệp/hàm trong tài liệu là điểm định vị chính.
- Tài liệu ban đầu chỉ là kế hoạch; việc triển khai bắt đầu sau yêu cầu duyệt và thực hiện của người dùng. Nội dung dưới đây giữ vai trò đặc tả đã duyệt.

## 1. Quyết định thiết kế đề xuất để duyệt

1. Đưa **đồ thị lên làm vùng làm việc chính**. Người dùng chọn chủ đề hoặc đường nối để thao tác ngay trong ngữ cảnh; không phải cuộn qua hai danh sách dài rồi mới xem kết quả ở dưới.
2. Tách **chọn khung đang soạn** khỏi **lọc thư viện để tìm chủ đề**. Cả hai đều nhìn rõ Khối 10/11/12 và trạng thái khung; lọc thư viện không làm đổi khung đang soạn.
3. Thay danh sách tích chọn bằng các hành động có tên rõ: `Thêm vào khung`, `Gỡ khỏi khung`, `Tạo liên kết`, `Đặt nhóm cha`. Một đối tượng không có đồng thời checkbox và nút cùng làm một việc.
4. Chọn hai nút trên đồ thị để tạo quan hệ; chọn một đường nối để chỉnh quan hệ trong bản nháp. Không yêu cầu kéo dây hoặc dùng chuột phải mới thực hiện được.
5. Bổ sung nút **`? Giải thích quan hệ`**, có bảng ý nghĩa, ví dụ, chiều mũi tên và tác động thực tế trong project.
6. Thay lỗi chỉ xuất hiện ở góc màn hình bằng **bảng lỗi cố định trong không gian soạn + đánh dấu đúng nút/cạnh/trường nhập + hành động “Đến vị trí lỗi”**.
7. Giữ các chức năng xem, tìm kiếm, phóng/thu, định vị, danh sách thay thế bằng bàn phím và các giới hạn hiệu năng đã có. Không thay renderer hoặc cài thư viện đồ thị mới trong phạm vi này.

**Mục tiêu trải nghiệm:** người dùng mới biết đang sửa khung nào, thêm chủ đề ở đâu, mũi tên có nghĩa gì, thay đổi nào chưa lưu, lỗi nằm ở đâu và bước tiếp theo để sửa.

## 2. Vấn đề quan sát được và thay đổi tương ứng

| Hiện trạng | Vấn đề với người mới | Thay đổi đề xuất |
| --- | --- | --- |
| Một combobox trộn “Phù hợp Khối 12”, đang chọn, chủ đề nháp, khung và giáo trình | Không biết đang lọc theo khối, trạng thái hay nguồn | Tách bộ chọn khung và bộ lọc thư viện thành các tầng độc lập, có nhãn rõ |
| Danh sách chủ đề/quan hệ dài nằm trên sơ đồ | Phải ghi nhớ tên rồi cuộn xuống mới xem được tác động | Đồ thị ở giữa, thư viện bên trái, chi tiết theo lựa chọn bên phải |
| Một quan hệ có cả checkbox và “Bỏ khỏi bản nháp” | Hai cách gọi gây hiểu nhầm là hai nghiệp vụ khác nhau | Chỉ một hành động `Gỡ quan hệ khỏi khung` |
| Nhãn bên trái có thể dùng loại của thư viện, ô bên phải dùng loại trong bản nháp | Cùng dòng có thể thể hiện hai loại quan hệ khác nhau | Nhãn chính luôn dùng loại hiệu lực trong bản nháp; loại gốc chỉ trong mục thông tin thư viện |
| Bảng chi tiết nút gọi mọi cạnh là tiên quyết | Hiểu sai ý nghĩa đồ thị | Render theo đúng loại và chiều của từng cạnh; tiêu đề chung là `Các quan hệ của chủ đề` |
| Nút cha chỉ là một lựa chọn trong danh sách | Dễ tưởng cha là kiến thức bắt buộc học trước | Tách thao tác `Đặt nhóm cha`, có câu giải thích và lớp hiển thị cấu trúc riêng |
| Ảnh 4 chỉ báo “Cây chủ đề có chu trình.” | Không biết hai nút nào gây lỗi, sửa ở đâu | Hiện đường vòng cụ thể, đánh dấu từng nút và trường nhóm cha, có nút điều hướng |
| Trạng thái “Nháp” gắn vào chủ đề theo suy luận sử dụng | Dễ tưởng đó là trạng thái phê duyệt riêng của chủ đề | Trạng thái Draft/Published gắn với **khung**; chủ đề hiển thị các nơi đang được dùng |
| Hủy thay đổi cấu trúc có thể bị hiểu là hủy toàn bộ thao tác vừa làm | Tạo/sửa thư viện thực tế đã gọi API riêng | Tách rõ “đã ghi thư viện” và “cấu trúc khung chưa lưu”; không hứa rollback dữ liệu đã ghi |

Ảnh 4 cho thấy “Giải tích thực và phức” chọn “Đại số trừu tượng” làm cha, đồng thời “Đại số trừu tượng” chọn “Giải tích thực và phức” làm cha. Đây là vòng cha–con hai nút; thông báo mới phải diễn giải chính ví dụ này khi dữ liệu xác nhận được.

## 3. Phạm vi phải giữ nguyên

### 3.1. Các hành vi nghiệp vụ không thay đổi

- Một khung thuộc một môn, một khối và một phiên bản; tôn trọng trạng thái, chủ sở hữu, quyền và `isLegacy` hiện có.
- Chủ đề có thể được dùng trong nhiều khung/nhiều khối. Không tạo bản sao chủ đề chỉ vì đưa vào khung khác.
- `parentNodeId` trong node mapping và quan hệ `PartOf` trong edge mapping là hai cơ chế độc lập. Không tự chuyển đổi hoặc đồng bộ chúng.
- Đổi cha không tự tạo cạnh tiên quyết. Kéo nền, chọn nút hoặc đổi vị trí hiển thị không đổi thứ tự học hay `orderIndex`.
- Chỉ dùng bốn loại quan hệ hiện có; không đảo chiều, gộp hoặc đổi trọng số để sơ đồ đẹp hơn.
- Giữ nguyên logic `nodeMappings`, `edgeMappings`, `rowVersion`, lưu nháp, rà soát và duyệt khung hiện hành.
- Duyệt không tự lưu các chỉnh sửa còn chưa lưu. Bản đã duyệt/chuyển tiếp vẫn đi qua luồng sao chép thành bản nháp hiện có khi cần soạn mới.
- Giữ điều kiện tiên quyết ngoài khối: nguồn ở khung đã duyệt, không legacy, cùng môn, khối thấp hơn, có nút nguồn; đích thuộc khung đang soạn. Không thêm ngoại lệ.
- Gỡ khỏi khung không gọi API xóa thư viện. Xóa thư viện vẫn là xóa mềm có kiểm tra phụ thuộc hiện có.
- Không thay thuật toán lộ trình, đánh giá năng lực, điều kiện học, chấm bài hoặc suy luận nguyên nhân lỗi.
- Không buộc đồ thị phải liên thông, có cha hoặc có cạnh tiên quyết. Đó là thay đổi nghiệp vụ ngoài yêu cầu này.

### 3.2. Những việc được làm ở frontend

Được tổ chức lại component/state phục vụ hiển thị, thay control, thêm hướng dẫn, preview cấu trúc chưa lưu và chẩn đoán lỗi từ dữ liệu hiện có. Chẩn đoán mới chỉ giải thích lỗi/rủi ro, **không thêm điều kiện chặn lưu/duyệt** ngoài điều kiện hiện tại. Server vẫn là nguồn quyết định cuối.

Không thêm API hoặc thay payload để lấy đường vòng/phụ thuộc chi tiết. Khi frontend không đủ dữ liệu để xác định vị trí lỗi, phải thông báo giới hạn đó, không tự đoán hoặc yêu cầu backend mới để hoàn thành kế hoạch.

## 4. Bố cục giao diện mới

### 4.1. Hai trạng thái làm việc

**Chọn khung:** chọn môn → tab Khối 10/11/12 → tab `Đã duyệt` / `Bản nháp` / `Tất cả`; `Lưu trữ` là bộ lọc bổ sung. Danh sách thẻ khung ghi tên, khối, trạng thái, nhãn `Hiện hành` nếu có, số chủ đề và quan hệ khi dữ liệu cung cấp. Nút `Mở để xem`, `Tiếp tục soạn`, `Sao chép thành bản nháp`, `Tạo khung trống` dùng các luồng có sẵn và đúng quyền.

**Đang soạn:** chỉ một khung đích tại một thời điểm, luôn hiển thị môn + khối + tên + `Bản nháp` + tình trạng lưu. Bộ chọn khối/khung đích khóa khi có thay đổi chưa lưu hoặc mutation đang chạy, giữ cơ chế hiện tại; hiển thị lý do ngay bên cạnh thay vì chỉ tooltip.

Nếu URL đã chỉ rõ khung/khối thì mở đúng ngữ cảnh đó. Không tự chuyển về Khối 12 hoặc chọn bản đầu danh sách. Không có bản đã duyệt thì ghi rõ, không dùng toàn bộ thư viện môn để giả làm khung hiện hành.

### 4.2. Wireframe desktop

```text
┌ Môn: Toán  /  Khối 12  /  Toán siêu cấp  [Bản nháp] ─────────────────┐
│ [Đổi khung]    Chưa lưu cấu trúc • 2 thay đổi    [? Giải thích quan hệ]│
├────────────────────┬──────────────────────────────┬─────────────────┤
│ THƯ VIỆN CHỦ ĐỀ    │ KHUNG ĐANG SOẠN              │ CHI TIẾT        │
│ Khối 10 | 11 | 12  │ [+ Chủ đề] [Tạo liên kết]    │ Chủ đề / Quan hệ│
│ Trạng thái khung   │ [Đặt nhóm cha]               │ đang chọn       │
│ Nguồn: Khung...    │ Tìm trong khung   [Vừa khung]│                 │
│ Tìm tên/mã...      │                              │ Nội dung        │
│                    │       [A] ──quan hệ──> [B]   │ Thao tác rõ tên │
│ Chủ đề A [Thêm]    │                              │                 │
│ Chủ đề B [Đã có]   │ [Cấu trúc cha–con: ẩn/hiện]  │ [Gỡ khỏi khung] │
├────────────────────┴──────────────────────────────┴─────────────────┤
│ CẦN XỬ LÝ (1)  A nằm trong B, B lại nằm trong A. [Đến vị trí lỗi]   │
├─────────────────────────────────────────────────────────────────────┤
│ [Hủy thay đổi cấu trúc]     [Lưu bản nháp]     [Rà soát & duyệt]      │
└─────────────────────────────────────────────────────────────────────┘
```

- Desktop rộng: thư viện khoảng 280–320 px; chi tiết 300–340 px; đồ thị nhận phần còn lại. Các kích thước là điểm bắt đầu để QA, không ép ba cột trên màn hình thiếu chỗ.
- Khoảng 1024–1279 px: tối đa hai vùng cạnh nhau, thư viện/chi tiết mở trong drawer, không để đồ thị thành một dải hẹp.
- Tablet/mobile: đồ thị là vùng chính; `Thư viện`, `Chi tiết`, `Cần xử lý` mở dạng drawer/bottom sheet. Danh sách là phương thức thao tác đầy đủ khi canvas quá nhỏ.
- Thanh lưu nằm trong vùng soạn, không che node, trường nhập hoặc nội dung lỗi; chừa khoảng đáy và tính safe area.
- Các tùy chọn nhãn trọng số, kiểu đường nối, bộ lọc hiển thị nâng cao gom vào `Tùy chọn hiển thị`. Mặc định vẫn dùng cấu hình đang có, không thay ý nghĩa layout.
- Không còn hai khu vực quản lý chủ đề/quan hệ dài buộc người dùng cuộn qua trước khi thấy đồ thị.

## 5. Bộ lọc khối, trạng thái khung và thư viện chủ đề

### 5.1. Tách hai loại bộ lọc

| Khu vực | Bộ lọc | Có đổi khung đang soạn? |
| --- | --- | --- |
| Trang chọn khung / `Đổi khung` | Khối 10/11/12; trạng thái khung; tên khung | Có, chỉ sau khi mở khung; không cho đổi khi cấu trúc chưa lưu |
| Thư viện bên trái | Khối nguồn, trạng thái khung nguồn, khung/giáo trình nguồn, tên/mã chủ đề | Không; chỉ thay danh sách có thể tìm để thêm |
| Toolbar đồ thị | Tìm trong khung, nhóm nội dung, ẩn nút đã tắt, tập trung | Không; chỉ thay phần đang xem |

Trong thư viện dùng nhãn **`Tìm chủ đề từ khối`** để tránh nhầm với **`Khối của khung đang soạn`**. Mặc định khối nguồn bằng khối đích, nhưng người dùng có thể tìm chủ đề ở khối khác theo khả năng hiện có. Chọn nguồn không tự gán khối mới cho chủ đề hoặc thay khối đích.

### 5.2. Cấu trúc bộ lọc thư viện

1. Khối nguồn: `Khối 10` / `Khối 11` / `Khối 12` / `Tất cả` / `Chưa có thông tin khối`.
2. Trạng thái **khung nguồn**: `Tất cả` / `Đã duyệt` / `Bản nháp`; `Lưu trữ` và `Dữ liệu chuyển tiếp` ở bộ lọc bổ sung, mặc định không trộn vào kết quả thông thường.
3. Nguồn cụ thể: thẻ khung đã lọc, hoặc `Các chủ đề của khối này`. Chế độ `Theo giáo trình` là nguồn riêng có nhãn trạng thái giáo trình, không gộp chung ý nghĩa với trạng thái khung.
4. Tình trạng trong khung đích: `Tất cả` / `Chưa thêm vào khung` / `Đã có trong khung`.
5. Tìm tên/mã; hiển thị số kết quả trong phạm vi quyền hiện có. Có `Xóa bộ lọc` và chip cho biết các điều kiện đang áp dụng.

Các điều kiện kết hợp theo AND; thuộc nhiều khung trong một tập nguồn là phép hợp node ID, không nhân đôi dòng chủ đề. Đã chọn khung nguồn cụ thể thì khối/trạng thái phải tương thích; đổi khối/trạng thái làm khung nguồn không còn khớp thì bỏ chọn nguồn cụ thể và thông báo ngắn, không giữ một bộ lọc ẩn cho kết quả rỗng.

### 5.3. Nguồn dữ liệu và quy tắc hiển thị chính xác

- Phân loại theo `gradeLevel`, `status`, `nodeMappings` của các phiên bản được API cho đọc. Không suy khối từ mã `MATH12...` hoặc tên chủ đề.
- Trong API thư viện toàn môn, `gradeLevels` hiện phản ánh khung **đã duyệt, hiện hành, không legacy**; không chứa đầy đủ thông tin các bản nháp. Vì vậy không dùng riêng trường này để kết luận một chủ đề không thuộc bản nháp Khối 10/11/12.
- Bộ lọc theo khung phải lấy membership từ `listVersions`/`nodeMappings`. Có thể dùng `gradeLevels` làm thông tin bổ sung, nhưng không thay membership nguồn đã chọn.
- Khi đang xem khung đích, trạng thái `Đã có trong khung` dựa trên node mappings cục bộ, bao gồm thay đổi chưa lưu. Thẻ nguồn của khung khác vẫn dựa trên dữ liệu đã tải của khung đó.
- Chủ đề mới chưa có mapping/khối rõ ràng nằm trong `Chưa có thông tin khối`, có shortcut hiển thị cùng kết quả của khối đích với nhãn riêng `Chưa xác định khối`, không khẳng định nó thuộc cả ba khối.
- Không âm thầm chèn chủ đề không khớp vào kết quả một bộ lọc khối chính xác. Chủ đề đã có trong bản nháp luôn tìm được qua tab `Trong khung` và trên canvas, ngay cả khi không khớp bộ lọc thư viện hiện tại.
- Một chủ đề có thể hiện nhiều badge: `Khối 10 · Đã duyệt`, `Khối 12 · Bản nháp`. Đó là trạng thái **nơi sử dụng**, không phải một trạng thái phê duyệt độc lập của chủ đề.
- Không gọi mọi chủ đề chưa bị khóa là “chủ đề nháp”. Dùng `Chưa thấy sử dụng trong khung đã duyệt` khi đủ dữ liệu; tải thiếu/lỗi phải ghi `Chưa tải đủ thông tin sử dụng`, không khẳng định “chưa được dùng”.
- Giữ logic `lockedTopicIds` cho điều kiện xóa hiện có. Lọc để thấy một nút không đồng nghĩa được sửa/xóa nút đó.
- Không yêu cầu quyền đọc rộng hơn để phục vụ bộ lọc. Dữ liệu thiếu quyền không được mô tả là “không tồn tại”.

## 6. Các thao tác chính trên đồ thị

### 6.1. Thêm chủ đề đã có và tạo chủ đề mới

**Đã có trong thư viện:** tìm ở panel trái → bấm `Thêm vào khung` → node xuất hiện trong preview, được chọn và có badge `Chưa lưu`. Ánh xạ đúng thao tác thêm `{ nodeId, parentNodeId: null }` hiện tại. Chủ đề đã có thì hiện `Đã có trong khung` và hành động `Định vị`, không cho thêm trùng.

**Tạo mới:** toolbar `+ Chủ đề` → hai lựa chọn `Từ thư viện` / `Tạo chủ đề mới`. Tạo mới mở lại form hiện có; mã tự sinh, quy tắc tên/loại/mã và payload giữ nguyên. Khi API thành công, thêm vào cấu trúc nháp cục bộ theo cơ chế hiện tại và đưa vào tầm nhìn.

Thông báo: `Đã tạo chủ đề trong thư viện. Chủ đề đã được thêm vào cấu trúc đang soạn; bấm “Lưu bản nháp” để ghi cấu trúc khung.` Không hiển thị “Đã lưu tất cả”.

Không bắt buộc kéo-thả. Nếu bổ sung kéo thẻ thư viện vào canvas thì đó chỉ là shortcut cùng hành động; vị trí thả là thông tin hiển thị, không ghi `orderIndex`, nhóm cha hay tọa độ nghiệp vụ. Kéo-thả là phần tùy chọn sau luồng bấm hoạt động đầy đủ.

### 6.2. Chọn và chỉnh chủ đề

- Bấm hoặc Enter trên node → panel chi tiết bên phải, làm rõ tên/mã/loại, nhóm cha trong khung đích, nơi sử dụng và thay đổi chưa lưu.
- Nhóm hành động chính: `Tạo liên kết từ đây`, `Đặt nhóm cha`, `Gỡ chủ đề khỏi khung`.
- `Sửa nội dung chủ đề trong thư viện` mở dialog hiện có nếu được phép. Ghi rõ phạm vi thư viện; không giả rằng thay tên/mã chỉ có hiệu lực ở bản nháp này.
- Chức năng xem câu hỏi/tạo bài tập giữ đúng điều kiện và scope đang có; không bật cho node chưa có ngữ cảnh lưu hợp lệ chỉ vì nó xuất hiện trên preview.
- Các mục `Xóa chủ đề khỏi thư viện`/tắt hoạt động nằm ở nhóm quản lý thư viện nếu luồng hiện tại cho phép. Không bật lại các cờ sửa/xóa canonical đang bị khóa ở trang chỉ xem chỉ để có nút trên canvas.

### 6.3. Tạo liên kết bằng cách chọn hai nút

1. Bấm `Tạo liên kết`, hoặc chọn A → `Tạo liên kết từ đây`.
2. Trên canvas hiện hướng dẫn `Bước 1/2: Chọn chủ đề nguồn`, sau đó `Bước 2/2: Chọn chủ đề đích`. A được đánh dấu `Nguồn`; B được đánh dấu `Đích`.
3. Hai đầu khác nhau; giữ các giới hạn nguồn/đích hiện có. Phím Esc/`Hủy tạo liên kết` thoát chế độ, không ghi dữ liệu.
4. Mở panel ngắn có bốn thẻ quan hệ kèm câu ví dụ, thay cho combobox loại quan hệ nằm giữa hàng dài. Không chọn ngầm một loại rồi ghi ngay khi vừa bấm B.
5. Hiện một câu đầy đủ theo quan hệ đã chọn, ví dụ `A là kiến thức tiên quyết cho B` hoặc `A là một phần của B`, và mũi tên A → B. Các thiết lập trọng số/nguồn nằm bên dưới; giá trị mặc định giữ như luồng hiện tại.
6. Bấm `Tạo liên kết` mới gọi API tạo hiện có; thành công mới đưa mapping vào bản nháp. Nút đang gửi có trạng thái tải, không cho gửi lặp.
7. Nếu quan hệ đã có trong thư viện: trình bày chính quan hệ đó và hành động `Thêm quan hệ có sẵn vào khung`; không gọi tạo lặp hoặc âm thầm áp dụng. Nếu đã có trong bản nháp thì `Định vị quan hệ`.

Nguồn/đích phải chọn được bằng danh sách tìm kiếm và bàn phím trong panel, không buộc người dùng tìm một node rất xa trên canvas. Click-to-connect là luồng chính; kéo dây từ cổng nối chỉ là cải tiến tùy chọn, không cần cho nghiệm thu cơ bản.

### 6.4. Chỉnh/gỡ một quan hệ

- Chọn đường nối hoặc dòng trong `Các quan hệ trong khung` → panel chi tiết hiển thị **loại hiệu lực trong bản nháp**, tên nguồn, tên đích, chiều và trọng số hiện tại.
- Đổi loại/trọng số cập nhật **edge mapping cục bộ** như hiện tại, không PATCH loại của cạnh gốc.
- Khi chuyển từ tiên quyết sang loại khác, giữ hành vi xóa `sourceGraphVersionId` của mapping. Không tự kéo nguồn ngoài khung vào khung; quan hệ không còn hợp lệ phải được chỉ rõ để người dùng sửa/gỡ.
- `Đảo chiều tiên quyết trong bản nháp` chỉ có đúng các điều kiện hiện tại; dùng lại tìm/tạo cạnh ngược rồi thay mapping. Không đổi source/target của canonical edge tại chỗ hoặc mở rộng đảo chiều sang mọi loại quan hệ.
- Chỉ một nút **`Gỡ quan hệ khỏi khung`**: xóa mapping cục bộ, đánh dấu chưa lưu, không gọi DELETE. Dòng giải thích: `Quan hệ vẫn còn trong thư viện môn. Thay đổi có hiệu lực với khung này khi lưu bản nháp.`
- Không còn checkbox áp dụng và nút gỡ song song. Danh sách quan hệ là cách truy cập thay thế canvas, dùng cùng hành động và cùng state.
- `Xóa quan hệ khỏi thư viện` không phải đường tắt cho gỡ mapping. Không thêm hành động này vào luồng đang không hỗ trợ; nơi nào đã có thì giữ nguyên quyền, cảnh báo và guard hiện tại.
- Trọng số là số 0–1 với nhãn/giới hạn rõ. Không đổi mặc định, không diễn giải là xác suất lỗi hoặc ngưỡng qua môn; 0 không được coi là “không áp dụng”.

### 6.5. Đặt nhóm cha bằng đồ thị

- Chọn A → `Đặt nhóm cha` → chỉ dẫn `Chọn nhóm cha cho A`. Có nút `Để ở cấp ngoài cùng` tương đương `parentNodeId: null`.
- Chọn B đang thuộc khung → câu xác nhận `Đặt A bên trong nhóm B`; áp dụng cập nhật parent mapping cục bộ. Tự chọn chính A bị loại như control hiện tại.
- Phương án thay thế: hộp tìm theo tên/mã của các nút trong khung. Không bắt buộc dùng combobox nhỏ trên mọi hàng.
- Cạnh cha–con nếu cần hiển thị là **lớp hướng dẫn cấu trúc riêng**, bật bằng `Hiện cấu trúc cha–con`, có nhãn `Nằm trong nhóm`. Không giả làm cạnh `PartOf`, không tính vào số quan hệ đã áp dụng và không gửi qua edge mappings.
- Nội dung trợ giúp: `Nhóm cha dùng tổ chức nội dung. Muốn yêu cầu học nền tảng trước, hãy tạo quan hệ Tiên quyết.`
- Không tự tạo node kiểu Chapter, không thay loại node, không áp đặt cấp phân loại mới. Không ẩn các lựa chọn đang hợp lệ theo nghiệp vụ chỉ vì chúng có vẻ chưa hợp lý về môn học.

### 6.6. Gỡ chủ đề và các phụ thuộc đang có

- Chọn A → `Gỡ chủ đề khỏi khung`. Nếu có ảnh hưởng, hiển thị bản tóm tắt trước khi xác nhận.
- Bảo toàn hành vi hiện tại: xóa node mapping A; các con trực tiếp có A làm cha chuyển về không có cha; **không tự xóa các edge mappings đang nối A**.
- Nếu cạnh còn trỏ tới A sau khi gỡ, chúng vẫn xuất hiện trong bảng `Cần xử lý`, kèm nhãn thiếu nguồn/đích; không được biến mất khỏi chẩn đoán chỉ vì không còn đủ hai node để vẽ cạnh bình thường.
- Thông báo mẫu: `Gỡ A khỏi cấu trúc đang soạn. 2 chủ đề con sẽ về cấp ngoài cùng. 3 quan hệ còn tham chiếu A cần được xử lý trước khi lưu.` Có `Xem quan hệ bị ảnh hưởng`.
- Người dùng tự thêm lại A hoặc gỡ từng quan hệ như các thao tác hiện có. Không cascade xóa, không tự xóa thư viện để làm cho bản nháp hợp lệ.

### 6.7. Tiên quyết ngoài khối

- Đưa ngữ cảnh ngoài khối vào panel của quan hệ: `Nguồn: Khung ... · Khối 10 · Đã duyệt` → `Đích: B · Khối 12`.
- Giữ lựa chọn khung nguồn theo bộ lọc điều kiện hiện có; không mở thao tác tạo mới ngoài khối bằng cách vượt giới hạn form đang có. Nếu chỉ có thể cấu hình từ một cạnh đã có trong thư viện thì dùng đúng đường đó.
- Nếu cần vẽ nguồn để người dùng hiểu, dùng card ngữ cảnh riêng ghi `Tiên quyết ngoài khung`; không thêm nó vào node mappings, số chủ đề hoặc thao tác sửa của khung đích.
- Không lấy cha của nguồn ngoài khối để cho phép đặt cha của nút trong khung này.

## 7. Nút “? Giải thích quan hệ”

Đặt ở toolbar soạn, gần chú giải đồ thị và trong panel tạo/sửa quan hệ. Dùng một nội dung chung, mở dialog/drawer bằng click hoặc bàn phím; không chỉ tooltip cần hover. Có tiêu đề, nút đóng, Esc và trả focus về nơi mở.

### Nội dung bắt buộc

| Bạn muốn diễn đạt | Quan hệ nên chọn | Ví dụ A → B | Tác động cần hiểu |
| --- | --- | --- | --- |
| “A là một phần của B” | **Thuộc về** | Đạo hàm → Giải tích | Phân chia nội dung; không tự bắt học B trước A |
| “A và B có nội dung liên hệ” | **Liên quan** | Hàm số bậc hai → Phương trình bậc hai | Liên hệ nội dung; không tự khóa thứ tự học |
| “Sai kiến thức A có thể dẫn đến sai B” | **Gây lỗi** | Quy tắc dấu → Giải bất phương trình | Mô tả khả năng nguồn lỗi; không tự bắt học lại A |
| “Cần đáp ứng nền tảng A trước khi tiến tới B” | **Tiên quyết** | Phương trình bậc nhất → Hệ phương trình bậc nhất | Được lộ trình dùng để xét nền tảng/thứ tự theo logic hiện có |

Thêm ba ghi chú ngắn:

1. `Mũi tên đọc từ nguồn A sang đích B. Với “Thuộc về”, A là phần nhỏ, B là phần bao chứa.`
2. `Nhóm cha tổ chức cây nội dung và được cấu hình riêng; không tự tạo quan hệ Thuộc về hay Tiên quyết.`
3. `Quan hệ Gây lỗi hiện chưa đồng nghĩa hệ thống tự truy lỗi và tự giao bài theo cạnh này. Trọng số 1 không có nghĩa chắc chắn sai 100%.`

Không đưa các thuật ngữ DAG/enum/API vào phần hướng dẫn chính. Nếu cần tài liệu kỹ thuật thì để mục mở rộng, không lẫn với câu giải thích cho giáo viên mới.

### Nhãn trong panel chi tiết phải đúng loại và chiều

| Loại | Khi chọn nút nguồn A | Khi chọn nút đích B |
| --- | --- | --- |
| Tiên quyết | `Là nền tảng cho → B` | `Cần nền tảng từ ← A` |
| Thuộc về | `Thuộc về → B` | `Có thành phần ← A` |
| Liên quan | `Liên quan đến → B` | `Có liên kết liên quan từ ← A` |
| Gây lỗi | `Lỗ hổng tại đây có thể gây lỗi ở → B` | `Lỗi ở đây có thể liên quan lỗ hổng tại ← A` |

Giữ hướng lưu thực tế của `RelatedTo`; không tự sinh bản ghi ngược dù lời giải thích nói về sự liên hệ giữa hai nội dung.

## 8. Thông báo lỗi và đánh dấu vị trí

### 8.1. Ba mức hiển thị

1. **Lỗi trường nhập:** ngay dưới trường, kèm `aria-invalid`/`aria-describedby`, không xóa dữ liệu đã nhập. Ví dụ mã trùng, tên rỗng, trọng số ngoài khoảng.
2. **Lỗi cấu trúc:** bảng `Cần xử lý (n)` luôn truy cập được trong workspace, mỗi mục có mô tả, đối tượng, cách sửa và `Đến vị trí lỗi`.
3. **Lỗi tải/server/toàn khung:** banner trong đúng panel với `Thử lại` khi phù hợp và mã hỗ trợ trong mục chi tiết. Không gán nhầm cho một node khi không biết node nào.

Toast chỉ là bổ sung cho thành công/thông báo ngắn; không phải nơi duy nhất giữ lý do thao tác thất bại. Dùng khả năng `data-toast-mirror="off"` hiện có cho bảng lỗi đã hiện rõ, tránh đọc lặp và nhiều toast giống nhau; không đổi hành vi toast toàn ứng dụng.

### 8.2. Chỉ rõ lỗi “Cây chủ đề có chu trình” trong ảnh 4

Ví dụ nội dung:

> **Chưa lưu được: hai chủ đề đang chọn nhau làm nhóm cha.**
>
> Giải tích thực và phức nằm trong Đại số trừu tượng, nhưng Đại số trừu tượng lại nằm trong Giải tích thực và phức.
>
> Vòng cấu trúc: Giải tích thực và phức → Đại số trừu tượng → Giải tích thực và phức.
>
> Hãy chọn một trong hai chủ đề để đổi nhóm cha hoặc đưa về cấp ngoài cùng.
>
> `[Đến Giải tích thực và phức]` `[Đến Đại số trừu tượng]`

- Hai node có viền + icon cảnh báo, không chỉ đổi màu. Hai trường nhóm cha trong inspector/danh sách cấu trúc đều có liên kết tới cùng lỗi.
- Hiện lớp cha–con để minh họa vòng lỗi, ghi rõ đây là quan hệ nhóm cha; không tô nhầm cạnh tiên quyết/PartOf nếu chúng không phải nguyên nhân.
- Vòng dài dùng đúng chuỗi node có tên và mã; giới hạn phần tóm tắt, mở `Xem toàn bộ vòng` nếu dài. Không chỉ trả về “có chu trình”.
- Bấm `Đến vị trí lỗi` mở inspector đúng node, đưa đối tượng vào tầm nhìn và focus trường/hành động liên quan. Nếu bị ẩn bởi bộ lọc canvas, tạm hiện phạm vi cần thiết và cho `Trở lại chế độ xem trước`; không đổi bộ lọc nguồn thư viện hoặc dữ liệu.
- Đồ thị không hợp lệ vẫn phải có layout/fallback hữu hạn để xem và sửa; nếu canvas không bố trí được vòng thì danh sách cấu trúc trong cùng workspace vẫn định vị và chỉnh được.

### 8.3. Ma trận lỗi và phản hồi mong muốn

| Tình huống | Nội dung người dùng thấy | Vị trí đánh dấu / hành động |
| --- | --- | --- |
| Vòng cha–con | Nêu chuỗi “A nằm trong B … quay lại A” và cách đổi cha | Các node/parent fields; nút mở đúng chủ đề |
| Vòng cạnh Tiên quyết/Thuộc về | Nêu chuỗi có **loại từng cạnh** A —Tiên quyết→ B —Thuộc về→ A | Các cạnh và node trong chu trình; mở chi tiết từng cạnh |
| Nguồn/đích không có trong bản nháp | `Quan hệ A → B thiếu chủ đề B trong khung này` | Mục quan hệ bị ảnh hưởng; `Thêm lại B` nếu có quyền/dữ liệu hoặc `Gỡ quan hệ khỏi khung` |
| Thiếu/không hợp lệ khung nguồn tiên quyết | Nêu đang thiếu khung đã duyệt ở khối thấp hơn chứa A | Trường khung nguồn; chỉ dẫn chọn nguồn hiện có hoặc gỡ quan hệ |
| Mapping chủ đề/cạnh không còn trong thư viện | `Không tìm thấy chủ đề/quan hệ #... trong dữ liệu thư viện đã tải` | Placeholder trong bảng lỗi; gỡ mapping theo thao tác hiện có; không kết luận chắc chắn “đã xóa” chỉ từ việc không thấy |
| Chủ đề đã tắt/không còn dùng được | Nêu đối tượng và trạng thái khi dữ liệu xác nhận | Node/card thư viện; giải thích server vẫn kiểm tra lại |
| Trùng mã chủ đề | `Mã ... đã được dùng` | Trường mã trong dialog; giữ tên/mô tả; lấy lại gợi ý mã theo luồng hiện có |
| Trùng liên kết cùng chiều/cùng loại | `Đã có quan hệ A → B loại ... trong thư viện` | Hiện quan hệ có sẵn; hành động thêm vào khung hoặc định vị, có thao tác người dùng rõ ràng |
| Trọng số sai | `Nhập trọng số từ 0 đến 1` | Trường trọng số và cạnh tương ứng nếu đang sửa |
| Xung đột RowVersion | `Khung vừa thay đổi ở phiên khác; chưa thể lưu thay đổi này` | Banner toàn khung; tải bản mới bằng hành động hiện có, không tự ghi đè hoặc tự merge |
| Xóa/sửa bị chặn bởi sử dụng học thuật | Nêu lý do server trả về an toàn; liệt kê phụ thuộc chỉ khi dữ liệu có | Giữ dialog mở, đánh dấu đối tượng; không tự gỡ giáo trình/câu hỏi để vượt khóa |
| 403/404 hoặc phạm vi không còn truy cập được | Nêu không thể truy cập/thao tác trong phạm vi hiện tại | Banner ngữ cảnh; không lộ tên tài nguyên ngoài quyền |
| Mạng/5xx | `Chưa hoàn tất thao tác. Kiểm tra kết nối rồi thử lại` và mã hỗ trợ nếu có | Giữ dữ liệu form/cấu trúc cục bộ; không báo thành công hoặc tự POST lại |
| Đang tải/refetch thư viện | `Đang tải dữ liệu để kiểm tra cấu trúc` | Loading, chưa kết luận “thiếu chủ đề/cạnh” khi dữ liệu chưa xác định |

Backend hiện dùng chung kiểm tra vòng cho Tiên quyết và Thuộc về trong cùng khung, dù thông báo có thể chỉ ghi “Quan hệ tiên quyết có chu trình”. UI phải giải thích đúng các cạnh thực tế, không đổi quy tắc kiểm tra đó.

### 8.4. Chẩn đoán phía giao diện, không đổi validation nghiệp vụ

- Dùng parent mappings cục bộ để tìm chu trình cha–con; dùng edge mappings hiệu lực + endpoint thư viện để xác định cạnh có vấn đề.
- Kiểm tra vòng cạnh chẩn đoán theo đúng tập loại/điều kiện server đang dùng: Tiên quyết và Thuộc về nội khung; RelatedTo/CausesErrorIn không bị đánh dấu sai là vòng tiên quyết.
- Trạng thái chẩn đoán gồm `Đã xác định`, `Chưa đủ dữ liệu`, `Lỗi do máy chủ trả về`. Không bịa danh sách node từ một chuỗi lỗi chung.
- Nếu không tái hiện được lỗi server trên dữ liệu đã tải: hiển thị lý do server an toàn và `Chưa xác định được vị trí từ dữ liệu hiện có`; vẫn cho xem cấu trúc, mã hỗ trợ và tải lại khi phù hợp.
- Không gọi API kiểm tra mới khi người dùng rê chuột/chọn node. Chẩn đoán không tự sửa dữ liệu, không tự cắt cạnh gây vòng và không thêm guard nghiệp vụ mới.
- Giữ các điều kiện disabled hiện có của lưu/duyệt, đặc biệt `busy`, `dirty`, `stale`, dữ liệu thư viện chưa sẵn sàng, `invalidDraftEdges`, reviewed và số node. Lỗi dự báo mới chỉ hiển thị, không tự tạo điều kiện cấm mới.
- Với bản đã duyệt/chỉ xem, đánh dấu lỗi không được mở ra quyền sửa; chỉ điều hướng được trong các hành động đang được phép.

## 9. Trạng thái chưa lưu, lưu và duyệt

### 9.1. Phân biệt thay đổi cấu trúc với ghi thư viện

| Hành động UI | Khi nào ghi server? | “Hủy thay đổi cấu trúc” có hoàn tác được? |
| --- | --- | --- |
| Thêm/gỡ chủ đề có sẵn vào/khỏi khung; đổi nhóm cha | Khi `Lưu bản nháp` | Có, về node mappings của lần lưu gần nhất |
| Thêm/gỡ quan hệ có sẵn; đổi loại/trọng số/khung nguồn trong mapping | Khi `Lưu bản nháp` | Có, về edge mappings của lần lưu gần nhất |
| Tạo chủ đề hoặc cạnh mới | API tạo ghi thư viện ngay; membership khung còn chờ lưu | Chỉ hủy membership chưa lưu; không xóa đối tượng đã tạo |
| Sửa nội dung/mã/loại chủ đề trong thư viện | Dialog lưu chủ đề gọi API riêng | Không; phải nói rõ đã lưu thư viện |
| Đảo chiều tiên quyết trong bản nháp | Có thể tạo cạnh ngược ở thư viện ngay, rồi đổi mapping cục bộ | Hủy mapping được; không tự xóa cạnh ngược đã tạo |
| Xóa chủ đề trong thư viện theo luồng hiện có | DELETE riêng, có guard | Không tự phục hồi bằng nút hủy cấu trúc |
| Pan/zoom/focus/bộ lọc xem | Không ghi cấu trúc | Không tạo dirty |

Không thêm autosave hoặc Undo/Redo toàn bộ thao tác trong kế hoạch này. Không dùng “Hoàn tác” để ám chỉ rollback mọi API.

### 9.2. Thanh thao tác

- `Lưu bản nháp`: payload và điều kiện hiện tại; pending rõ ràng; thành công chỉ báo đúng phần đã lưu, cập nhật RowVersion đúng kết quả server.
- `Hủy thay đổi cấu trúc`: phục hồi node/edge mappings từ snapshot đã lưu. Nếu vừa có ghi thư viện, nhắc ngay trong xác nhận rằng nội dung thư viện đã lưu sẽ không bị hủy.
- `Rà soát & duyệt`: mở panel tóm tắt số chủ đề/quan hệ/khối và lỗi hiện có. Giữ bước xác nhận `Tôi đã rà soát...` trước hành động duyệt; đây là một checkbox xác nhận riêng, không phải checkbox áp dụng hàng loạt.
- Không cho duyệt khi dirty theo điều kiện hiện có. Chỉ dẫn `Lưu bản nháp trước khi duyệt`; không tự lưu rồi duyệt trong cùng một nhấp.
- Khi đổi môn/khối/phiên bản/thoát soạn trong ứng dụng: giữ chặn chuyển ngữ cảnh nếu có thay đổi cấu trúc chưa lưu; hướng người dùng tới lưu hoặc hủy. Không chuyển trước rồi mới hiện cảnh báo.
- Refetch thư viện hoặc phiên bản không được ghi đè state dirty. Request tạo cũ không được đưa node/cạnh vào khung mới; giữ cơ chế chống include request sai phiên bản đang có.

## 10. Thiết kế state và tích hợp kỹ thuật cho agent coding

### 10.1. Một nguồn state cấu trúc duy nhất

Hiện node/edge mappings chưa lưu nằm trong `KnowledgeVersionPanel`, còn canvas ở trang cha dùng graph đã nạp từ server. Khi chuyển sang thao tác trực tiếp, **canvas, inspector, danh sách thay thế và payload lưu phải đọc cùng một state bản nháp**.

Đề xuất tách hook/controller giao diện `useKnowledgeDraftEditor` chứa mappings, base RowVersion, dirty/reviewed/busy và các action UI; nâng state lên mức workspace soạn hoặc dùng context cục bộ cho workspace. Đây là refactor frontend, không đổi service hoặc hợp đồng dữ liệu.

```text
API phiên bản + thư viện đã được phép đọc
  ├─ snapshot cấu trúc đã lưu
  └─ state cấu trúc đang soạn (một nguồn duy nhất)
        ├─ preview đồ thị và lớp cha–con
        ├─ inspector + danh sách thay thế
        ├─ bảng chẩn đoán lỗi
        └─ saveVersion(nodeMappings, edgeMappings, rowVersion) hiện có
```

- Preview nội khung được dựng từ catalog node/edge + **mapping cục bộ hiệu lực**. Catalog parent/loại cạnh/trọng số không được ghi đè lựa chọn của bản nháp.
- Dữ liệu thiếu endpoint/mapping vẫn có mục trong lỗi, không bị lọc mất khỏi state để làm preview “đẹp”. Không dùng preview đã lọc làm payload lưu.
- ID render/occurrence/canonical phải ánh xạ tường minh. API mutation và mappings dùng canonical ID theo luồng hiện tại; selection/định vị dùng đúng render ID. Không đổi bigint/string thành Number.
- Context ngoài khối và đường hướng dẫn cha–con có ID loại riêng, không được đưa vào payload như node/cạnh thật.
- Chế độ class/curriculum/historical và read-only tiếp tục dùng projection/scope hiện tại; không đưa draft preview vào những nơi này.
- Bộ lọc, panel đang mở, lựa chọn A/B tạo cạnh và issue đang focus là view state, không phải graph mappings.

### 10.2. Các thành phần đề xuất

Tên là gợi ý để agent chia trách nhiệm, không bắt buộc đúng tên tệp; các trách nhiệm phải có:

| Thành phần | Trách nhiệm |
| --- | --- |
| FrameworkPicker | Bộ chọn môn/khối/trạng thái/khung, mở/clone/tạo theo API hiện có |
| TopicLibraryPanel | Bộ lọc nguồn, provenance, tìm kiếm, thêm/định vị chủ đề |
| KnowledgeGraphCanvas hiện có | Render, chọn node/cạnh, chọn hai đầu, highlight issue, camera; không giữ bản sao mappings |
| DraftInspector | Chi tiết node/cạnh, action của bản nháp và tách biệt quản lý thư viện |
| RelationHelpDialog | Một nguồn nội dung giải thích quan hệ dùng ở các điểm mở |
| DraftIssuesPanel | Danh sách lỗi có địa chỉ node/edge/field và điều hướng |
| DraftActionBar | Trạng thái lưu, hủy cấu trúc, rà soát/duyệt |
| useKnowledgeDraftEditor / helper preview | State giao diện dùng chung và chuyển mapping thành preview; giữ semantics các handler hiện tại |
| helper diagnostics | Chẩn đoán từ dữ liệu đã tải, trả issue có ID/field/path, không mutation hoặc thêm guard server |

### 10.3. Bản đồ tệp hiện có

Các đường dẫn dưới đây tương đối từ root repository để agent định vị; mở code hiện tại trước khi triển khai, không dựa vào tài liệu cũ thay mã đang chạy.

| Tệp | Vai trò / yêu cầu khi triển khai |
| --- | --- |
| `web/edutwin-web/src/components/academic/KnowledgeGraphWorkspace.tsx` | Entry workspace dùng chung; không tạo một trình soạn rời khỏi route đang hoạt động |
| `web/edutwin-web/src/pages/teacher/TeacherKnowledgeGraphView.tsx` | Ngữ cảnh, quyền, API tạo, canvas và inspector; gom thành workspace soạn, sửa nhãn mọi cạnh bị gọi là tiên quyết |
| `web/edutwin-web/src/components/teacher/KnowledgeVersionPanel.tsx` | Mappings, save/publish/reverse/remove và điều kiện dirty hiện tại; tách state và UI, không bỏ tính năng khi bỏ danh sách cũ |
| `web/edutwin-web/src/components/teacher/KnowledgeGraphCanvas.tsx` | Kế thừa camera, tìm kiếm, LOD, danh sách quan hệ và truy cập bàn phím; bổ sung tương tác soạn theo props/callback |
| `web/edutwin-web/src/components/teacher/KnowledgeNodeEditDialog.tsx` | Giữ logic mã tự sinh, payload và lỗi trong dialog; bổ sung thông tin phạm vi thư viện và lỗi trường |
| `web/edutwin-web/src/utils/knowledgeDraftTopics.ts` | Tách/compose bộ lọc hiển thị mới; giữ guard khóa chủ đề và phân biệt trạng thái sử dụng |
| `web/edutwin-web/src/utils/knowledgeDraftEdges.ts` | Kế thừa invalidDraftEdges, findReversePrerequisite, replaceDraftPrerequisite; không đổi semantics khi thay control |
| `web/edutwin-web/src/utils/knowledgeScope.ts` | Giữ query keys, occurrence/canonical mapping và URL scope |
| `web/edutwin-web/src/api/knowledgeGraphApi.ts`, `web/edutwin-web/src/types/knowledgeGraph.ts` | Đọc hợp đồng hiện có; không sửa endpoint/payload/schema để tiện UI |
| `web/edutwin-web/src/utils/problemDetails.ts`, `academicLifecycleError.ts` | Dùng thông tin lỗi an toàn; không hiển thị raw stack/SQL/HTML từ server |
| `web/edutwin-web/src/components/AlertToastHost.tsx` | Dùng cơ chế opt-out mirror hiện có ở workspace; không viết lại hệ thống toast toàn app |
| Các tệp `knowledgeGraphScene`, `knowledgeGraphLayout`, `knowledgeGraphViewport`, worker/layout hooks | Giữ engine/routing/hiệu năng đã triển khai; chỉ mở API view cần để định vị/highlight, không đổi nghĩa thứ hạng học thuật |

Backend chỉ đối chiếu, **không sửa**: `src/EduTwin.BLL/KnowledgeGraph/KnowledgeGraphVersionService.cs`, `KnowledgeGraphScopeReader.cs`, các use case tạo/sửa/xóa node/edge, và phần Recommendations/Planning. Không động tới migrations/seed đang có thay đổi của công việc khác.

## 11. Trạng thái rỗng, tải, quyền và khả năng truy cập

- Chưa chọn môn/khối: chỉ dẫn chọn ngữ cảnh, không đưa người dùng vào canvas trống không giải thích.
- Khối chưa có khung đã duyệt: hiển thị bản nháp được phép đọc và CTA tạo/sao chép nếu có quyền; không tự công bố bản nháp.
- Khung trống: canvas có hướng dẫn `Thêm chủ đề từ thư viện hoặc tạo chủ đề mới`; khung trống vẫn lưu theo khả năng hiện có, duyệt vẫn giữ điều kiện ít nhất một node.
- Không có kết quả lọc: hiện chip bộ lọc, `Xóa bộ lọc`/`Xem chủ đề chưa xác định khối`; không tạo chủ đề mới tự động.
- Lỗi tải khung, thư viện và nguồn khung được hiển thị riêng, có retry đúng query. Các vùng đã tải vẫn xem được; không coi lỗi tải là mảng rỗng hợp lệ.
- Published/Archived/legacy/không đủ quyền: xem và trợ giúp được, các thao tác soạn giữ nguyên guard; thông báo lý do rõ, không chỉ nút mờ thiếu giải thích.
- Mọi thao tác canvas đều có đường qua danh sách/tìm kiếm/inspector; không phụ thuộc hover, màu, kéo dây hay right-click.
- Enter/Space kích hoạt control; Esc hủy chế độ nối/đặt cha trước khi đóng panel theo thứ tự rõ. Không override phím khi đang nhập văn bản.
- Dùng nhãn tên + mã khi trùng tên chủ đề. Nút trợ giúp icon có accessible name; focus ring rõ, trả focus sau dialog.
- Chỉ một live region chính thông báo lỗi mới; lựa chọn/dirty dùng status nhẹ, không đọc lại toàn bộ bảng lỗi mỗi lần rê chuột.
- Kiểm tra ở 360, 768, 1024 và 1440 px; không có cuộn ngang toàn trang, panel dài có vùng cuộn hợp lý và nút lưu vẫn truy cập được.

## 12. Các giai đoạn triển khai sau khi duyệt

| Giai đoạn | Công việc | Điều kiện hoàn thành |
| --- | --- | --- |
| P1 — Nền state giao diện | Tách controller, preview chung, giữ toàn bộ API/guard; tạo fixture dữ liệu đại diện | Canvas/list/inspector/save cùng thấy một mappings; refetch không mất dirty |
| P2 — Chọn khung và thư viện | Tab khối, trạng thái, nguồn; provenance đúng; panel trái và trạng thái tải/rỗng | Tìm được chủ đề theo từng khối/khung Draft/Published, không suy từ mã |
| P3 — Soạn trên đồ thị | Click-to-connect, inspector, đặt cha, thêm/gỡ rõ tên, danh sách thay thế, read-only parity | Hoàn thành thêm/sửa/gỡ cấu trúc mà không cần checkbox áp dụng |
| P4 — Giải thích và lỗi | Help dialog, nhãn đúng loại/chiều, issue panel, highlight/định vị, lỗi field/server | Tái hiện ảnh 4 và sửa được đúng hai nhóm cha; không cần đọc toast để đoán |
| P5 — Lưu/duyệt và QA | Rà soát trạng thái ghi thư viện/cấu trúc, responsive, keyboard, hồi quy | Đạt ma trận mục 13, kiểm tra frontend và bằng chứng browser |

Mỗi giai đoạn là một bước thực hiện trong cùng thiết kế đã duyệt, không phải tự ý cắt phần khó sang “tương lai”. Kéo-thả thư viện/kéo dây là tùy chọn; toàn bộ luồng click, lỗi và lọc là phần bắt buộc.

## 13. Tiêu chí nghiệm thu và kiểm chứng

### 13.1. Kịch bản người dùng bắt buộc

| ID | Kịch bản | Kết quả đạt |
| --- | --- | --- |
| AC01 | Vào danh sách khung, lần lượt chọn 10/11/12 và Draft/Published | Thẻ khung đúng khối/trạng thái; Hiện hành tách biệt; nguồn thiếu quyền không bị lộ |
| AC02 | Đang soạn Khối 12 nhưng lọc thư viện nguồn Khối 10 | Context đích vẫn Khối 12; chỉ danh sách nguồn đổi, không đổi mappings |
| AC03 | Chủ đề chỉ nằm trong Draft Khối 11 và không có gradeLevels Published | Vẫn tìm được qua Draft/Khối 11; không bị gán cho mọi khối |
| AC04 | Một chủ đề xuất hiện trong nhiều khối/khung; hai chủ đề trùng tên | Một thẻ theo node ID ở thư viện, provenance đủ rõ; tên kèm mã phân biệt |
| AC05 | Thêm chủ đề thư viện vào khung rồi định vị | Preview cập nhật ngay; membership chưa lưu; không POST tạo chủ đề mới |
| AC06 | Tạo chủ đề mới rồi hủy cấu trúc chưa lưu | Chủ đề vẫn ở thư viện theo API đã ghi; UI nói rõ, không tự DELETE |
| AC07 | Chọn A rồi B tạo từng loại quan hệ | Đọc được câu A–quan hệ–B; chỉ ghi khi xác nhận; endpoints và enum đúng |
| AC08 | Quan hệ cùng chiều/cùng loại đã có | Có hành động dùng cạnh có sẵn; không tạo trùng, không tự áp dụng khi chỉ mở form |
| AC09 | Chọn cạnh để đổi loại/trọng số trong draft | Canvas, inspector, danh sách, payload cùng dùng mapping mới; cạnh gốc không bị PATCH loại |
| AC10 | Gỡ quan hệ khỏi khung | Chỉ một hành động; bỏ mapping, không DELETE thư viện; có hiệu lực khi lưu |
| AC11 | Gỡ chủ đề có con và có cạnh | Con về không cha như trước; cạnh không bị tự gỡ; lỗi chỉ rõ từng cạnh thiếu endpoint |
| AC12 | Đặt A làm con B, B làm con A như ảnh 4 | Có đường vòng tên/mã và hai nút được đánh dấu; điều hướng mở đúng trường nhóm cha; không chỉ toast |
| AC13 | Vòng Tiên quyết/Thuộc về và vòng RelatedTo/CausesErrorIn | Hai loại đầu được chẩn đoán đúng theo rule chung; hai loại sau không bị gọi nhầm là lỗi tiên quyết |
| AC14 | Chọn một nút có đủ bốn loại cạnh vào/ra | Inspector dùng đúng loại/chiều; không gọi tất cả là “Tiên quyết cho” |
| AC15 | Thêm/gỡ/đổi mapping rồi filter, pan, đổi panel và refetch | Không mất dirty; filter/pan không tạo dirty mới; hidden items vẫn trong payload |
| AC16 | Dữ liệu thư viện đang tải/refetch sau tạo cạnh ngược | Không báo giả “cạnh không tồn tại”; include request cũ không chạy sang khung mới |
| AC17 | Save trả 409, 400, 403/404, lỗi mạng/5xx | Giữ cấu trúc và form; không báo thành công, không tự ghi đè; thông báo có nơi sửa khi biết |
| AC18 | Chọn khung Published, Archived, legacy, actor chỉ đọc | Không mở quyền sửa; helper và định vị vẫn dùng được; sao chép theo quyền hiện có |
| AC19 | Cấu hình tiên quyết ngoài khối | Chọn đúng nguồn Published khối thấp hơn; node ngữ cảnh không tính vào mappings/count |
| AC20 | Review/duyệt khi còn dirty, chưa rà soát, hoặc khung không node | Điều kiện giữ như trước; không auto-save/auto-publish |
| AC21 | Dùng hoàn toàn bàn phím và màn hình 360/768/1024/1440 px | Thêm/nối/đặt cha/gỡ/đến lỗi/help/lưu đều truy cập được; không tràn ngang |
| AC22 | Khung có node rời rạc, không cha, không quan hệ | Vẫn thao tác/lưu/duyệt theo luật cũ; không thêm yêu cầu bắt buộc cạnh/cha |
| AC23 | Quan hệ trọng số 0 vẫn đang áp dụng | Vẫn vẽ/hiện đúng trạng thái; không tự gỡ hoặc coi là false |

### 13.2. Kiểm thử và bằng chứng agent phải cung cấp

- Bổ sung test hành vi cho helper lọc đa khối/trạng thái, overlay mappings → preview, chẩn đoán vòng và thiếu endpoint; kiểm tra loading/unknown, string IDs và cùng node nhiều nguồn.
- Giữ regression của `knowledgeDraftEdges.test.ts`, `knowledgeScope.test.ts`, `knowledgeNodeCode.test.ts`, các test viewport/scene/layout-worker và quyền đã có.
- Một số test hiện tại trong `knowledgeDraftTopics.test.ts` kiểm tra chuỗi JSX/checkbox/combobox cũ. Cập nhật theo hành vi mới, không giữ UI cũ chỉ để regex pass; không xóa các bảo đảm payload/guard/ràng buộc đi kèm.
- Kiểm tra qua browser bằng fixture/mocks hoặc dữ liệu thử được phép: các AC nêu trên, đặc biệt ảnh 4, cancel sau API tạo thành công, save conflict và bàn phím. Ảnh chụp không thay thế kiểm tra request payload.
- Chạy `npm test`, `npm run build` từ `web/edutwin-web`; lint các tệp đổi hoặc toàn frontend theo quy ước repo; kiểm tra bundle budget hiện có nếu thay chunk/import/canvas. Không nới budget để đưa thư viện mới vào.
- Đối chiếu network cho các luồng quan trọng: thêm/gỡ mapping không gọi DELETE; sửa edge mapping không PATCH loại thư viện; create/reverse giữ API hiện có; save gửi đầy đủ mappings và base RowVersion.
- Không cần đổi hay mở rộng test backend cho một thay đổi frontend thuần túy. Nếu phát hiện cần sửa backend mới đạt một chi tiết thiết kế, ghi rõ giới hạn và đề xuất riêng; không mở rộng phạm vi ngầm.
- Bàn giao: ảnh desktop/mobile, ảnh lỗi có highlight, ảnh hộp trợ giúp, kết quả kiểm tra, danh sách tệp đổi và giới hạn còn lại có bằng chứng. Không ghi “đã kiểm thử” cho việc chưa chạy.

## 14. Các điểm cố ý không đưa vào lần này

- Tự suy ra quan hệ từ tên chủ đề bằng AI; tự bổ sung tiên quyết còn thiếu.
- Thêm luật bắt buộc mọi node có cha/cạnh hoặc đổi điều kiện duyệt khung.
- Đồng bộ tự động parent và PartOf; thay cách lộ trình dùng các quan hệ.
- Bulk apply/bulk delete mới, cascade xóa phụ thuộc, tự merge xung đột, autosave, Undo/Redo toàn bộ API.
- Chỉnh khung đã duyệt tại chỗ; thêm quyền thao tác cho CenterManager/actor chỉ đọc.
- API mới trả đường vòng, vị trí node, trạng thái phê duyệt riêng của chủ đề hoặc endpoint tìm kiếm mới.
- Di chuyển sang React Flow/renderer khác hoặc làm lại thuật toán layout. Kế hoạch này kế thừa kết quả quan sát đồ thị đã có trong `docs/knowledge-graph-visualization-plan.md`; tài liệu cũ không phải nguồn quyết định cho nghiệp vụ phiên bản hiện tại.

## 15. Chỉ dẫn bàn giao cho agent triển khai sau khi duyệt

1. Đọc tài liệu này và AGENTS.md, khảo sát CodeGraph/mã hiện tại trước khi sửa; xác nhận lại các handler và guard đã mô tả vì worktree có thể thay đổi.
2. Triển khai P1–P5 trong frontend, bắt đầu từ một nguồn state dùng chung. Không chỉ trang trí lại danh sách cũ rồi coi là đã chuyển sang thao tác trên đồ thị.
3. Giữ khả năng thao tác bằng danh sách có nhãn rõ để hỗ trợ bàn phím/mobile; không bắt mọi người phải kéo-thả.
4. Đối chiếu từng AC và ma trận hành vi ghi/hủy; không chấp nhận UI mới có vẻ đúng nhưng gọi mutation khác phạm vi.
5. Không sửa backend, migrations, seed, học máy/AI, thuật toán lộ trình hoặc các thay đổi có sẵn của người khác. Không commit/push/deploy nếu chưa được yêu cầu riêng.
6. Nếu một chi tiết chỉ làm được bằng đổi hợp đồng API/logic, dừng riêng hạng mục đó, nêu bằng chứng và phương án UI dùng dữ liệu hiện có; không âm thầm vượt phạm vi.

**Nội dung xin duyệt:** bố cục đồ thị làm trung tâm; bộ lọc khối/trạng thái/nguồn tách biệt; thao tác click trên node/cạnh thay checkbox áp dụng; trợ giúp quan hệ; bảng lỗi có highlight/định vị; và việc giữ nguyên toàn bộ ràng buộc nghiệp vụ/API được nêu trong kế hoạch.
