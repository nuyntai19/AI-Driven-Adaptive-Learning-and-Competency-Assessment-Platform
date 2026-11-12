# Kế hoạch cải thiện khả năng quan sát đồ thị tri thức

- Ngày lập: 2026-11-11 (Asia/Bangkok).
- Project: EduTwin — `C:\SINH VIENNN\.Net\AI-Driven-Adaptive-Learning-and-Competency-Assessment-Platform`.
- Mốc khảo sát: commit `c4a0766a15751e3b93ba7f56f30056b4de459858`; worktree sạch trước khi thêm tài liệu.
- Trạng thái: **đã triển khai một phần (P1, phần lớn P2, một phần P3) ngày 2026-11-12** — xem mục 14. Không thêm dependency.

## 1. Vấn đề, mục tiêu và giới hạn

Ảnh gốc `image(20261110-175741).png` đã được xem trực tiếp từ tệp đính kèm cuộc trò chuyện “Lập plan đồ thị tri thức”. Ảnh cho thấy các card Toán 12, nhiều đường cong dồn vào hành lang giữa hai cột, nhãn trọng số `1` nằm tại vùng giao nhau và một cạnh vòng ở bên phải. Việc giảm độ đậm của cạnh không giúp phân biệt rõ đường nào đi từ card nào đến card nào. Ảnh chỉ là một phần đồ thị, không đủ để suy ra tổng số node/cạnh hay hiệu năng toàn bộ dữ liệu.

Mục tiêu là theo dõi được nguồn → loại quan hệ → đích → trọng số, kể cả khi dữ liệu lớn; giữ ngữ nghĩa DAG, phạm vi học thuật, danh tính occurrence và quyền thao tác hiện có. Ưu tiên khả năng đọc và tương tác có kiểm soát thay vì luôn vẽ mọi chi tiết.

Không cam kết loại bỏ mọi giao cắt của một graph bất kỳ. Phân biệt **giao cắt đường hợp lệ** với **đường trùng nhau, đường xuyên card, nhãn che nội dung**. Ba vấn đề sau phải được xử lý hoặc có fallback rõ ràng. Không đổi trọng số, hướng quan hệ, cấu trúc học thuật hoặc API ghi dữ liệu chỉ để bố trí hình đẹp hơn.

## 2. Kết quả khảo sát repository

Các đường dẫn và dòng dưới đây tính từ root project, tại mốc khảo sát nêu trên.

| Thành phần hiện tại | Bằng chứng trong code | Hệ quả cho kế hoạch |
| --- | --- | --- |
| Route đang dùng | `web/edutwin-web/src/App.tsx:46,168–169,250` lazy-load `KnowledgeGraphWorkspace`; route cũ redirect sang `/giao-vien/do-thi-tri-thuc` | Làm trên luồng đang hoạt động; kiểm tra cả Teacher và CenterManager đi qua redirect/quyền hiện có |
| Workspace chung | `web/edutwin-web/src/components/academic/KnowledgeGraphWorkspace.tsx:15–20` render `TeacherKnowledgeGraphView` | Không xây thêm một graph UI tách rời |
| Nạp và lọc dữ liệu | `web/edutwin-web/src/pages/teacher/TeacherKnowledgeGraphView.tsx:249–285,935–943`: query theo scope, `graphOccurrences`, lọc node inactive và cạnh tương ứng, truyền vào canvas | Layout phải dùng projection hiện tại và bảo toàn bộ lọc/phạm vi |
| Phạm vi và danh tính | `web/edutwin-web/src/utils/knowledgeScope.ts:25–33,55–73`: query key có center/actor/subject/scope; occurrence có ID riêng, giữ canonical ID | Không gộp node chỉ vì cùng mã/tên/canonical ID |
| Layout node | `web/edutwin-web/src/utils/knowledgeGraphLayout.ts:59–237`: topological longest-path rank với `PrerequisiteOf`/`PartOf`; sort theo `orderIndex`, loại, mã, ID; tọa độ lưới cố định | Chưa tối ưu giao cắt theo hàng xóm, chưa dành khoảng trống theo mật độ cạnh/nhãn; phần traversal tuyến tính không có nghĩa toàn bộ pipeline tuyến tính vì còn sort |
| Routing cạnh | Cùng file, `computeEdgePath:244–315`: Bezier theo hai tọa độ; forward/same-column/backward dùng các điểm nối cố định | Hàm không nhận toàn bộ obstacles, edge ID hay nhóm cạnh; nhiều quan hệ cùng endpoints dùng chung đường, nhãn cùng vị trí |
| Render đang dùng | `web/edutwin-web/src/components/teacher/KnowledgeGraphCanvas.tsx:26,38–60,137–163`: card 240×118, gapX=85/gapY=28; SVG map mọi node/cạnh của subgraph; hit path 18 graph units; badge 36×20 tại `midX/midY` | Chưa có obstacle routing, port/lane allocation, label collision hoặc viewport culling; bounds layout chỉ tính card, có thể thiếu phần vòng cạnh/nhãn |
| Khả năng đã có | Cùng canvas: zoom/pan, vừa khung, tìm kiếm, focus lân cận, bộ chọn chương, ẩn inactive, highlight selection; hiện chỉ hiện trọng số khi scale ≥0.6 hoặc cạnh được chọn | Kế thừa các khả năng này. Ngưỡng scale đơn lẻ chưa giải quyết mật độ và nhãn chồng nhau |
| Neighborhood | `web/edutwin-web/src/utils/knowledgeGraphViewport.ts:28–41`: nút chọn, hàng xóm trực tiếp, cha/con thật và induced edges | Hub có thể vẫn tạo subgraph rất lớn; bộ chọn chương hiện là focus neighborhood, chưa phải collapse/expand cụm nhiều tầng |
| Ngữ cảnh ngoài phạm vi | `TeacherKnowledgeGraphView.tsx:905` hiển thị `externalPrerequisites` riêng | Không tự kéo node ngoài scope vào graph hoặc tính chúng vào node count của giáo trình |
| API đọc | `web/edutwin-web/src/api/knowledgeGraphApi.ts:20–33`; `web/edutwin-web/src/types/knowledgeGraph.ts:58–64,155–161` | Hiện lấy graph theo scope, chưa có hợp đồng summary/neighborhood/cursor trong client được khảo sát; API mở rộng là hạng mục tương lai |
| Stack | `web/edutwin-web/package.json`: React ^19, TypeScript ~5.7, Vite ^6; không khai báo thư viện layout graph | Giữ SVG giai đoạn đầu; không mặc định chuyển renderer hoặc cài thư viện trong lượt lập plan |
| Code cũ còn tồn tại | `web/edutwin-web/src/pages/KnowledgeGraphPage.tsx` có renderer riêng và gọi chung layout/path helper; App hiện import workspace khác | Ghi nhận consumer để tránh phá kiểu/hành vi nếu thay helper; không dùng test file cũ làm bằng chứng duy nhất cho UI đang chạy |

### Khoảng trống kiểm thử hiện tại

- `web/edutwin-web/tests/knowledgeGraphViewport.test.ts`: kiểm tra fit 96 node ở 320/768/1240 px, zoom/pan, neighborhood, wrap tên và một số source assertions. Fit card không chứng minh cạnh và nhãn không bị cắt hoặc dễ đọc.
- `web/edutwin-web/tests/centerManagerUIRefinement.test.ts`: kiểm tra rank, deterministic ordering, các dạng path và source assertions cho trang cũ; chưa đo va chạm hình học nhiều cạnh/nhãn.
- `web/edutwin-web/tests/knowledgeScope.test.ts`: cần giữ regression của scope/occurrence.
- `test/cypress/e2e/04-academic-authoring.cy.js` có kiểm tra API academic authoring; cần bổ sung browser scenario riêng cho graph visualization.
- `web/edutwin-web/tests/bundle/bundleBudget.test.ts` kiểm tra code splitting và các budget hiện có, trong đó main entry ≤150 KB. Không nới budget chung để đưa layout engine vào entry.

## 3. Kiến trúc đề xuất và quyết định công nghệ

Pipeline dự kiến:

```text
DTO + resolved scope
  → occurrence projection + adjacency/index
  → bộ lọc ngữ nghĩa + lựa chọn cụm/neighborhood
  → graph hiển thị có giới hạn
  → layout node + ports + route toàn bộ cạnh
  → placement nhãn + kiểm tra geometry + bounds
  → culling/LOD theo camera
  → SVG + inspector + danh sách quan hệ truy cập được
```

Tách dữ liệu nghiệp vụ khỏi view model: node bounds, port, route points/sections, edge bounds, label anchor/bounds, visibility reason, cluster membership và số liệu chẩn đoán. DTO không nhận tọa độ giả. Giữ ánh xạ render ID → occurrence ID → canonical ID để selection và inspector không chọn nhầm đối tượng.

**Hướng ưu tiên để thử nghiệm:** adapter ELK Layered cho node layout và routing của graph có hướng; giữ SVG và inspector hiện có. ELK Layered có ports, multi-edges, edge labels, compound graph và orthogonal routing. Đây là cơ sở chọn ứng viên, không phải bảo đảm tự động giải quyết mọi va chạm. [ELK Layered](https://eclipse.dev/elk/reference/algorithms/org-eclipse-elk-layered.html)

`elkjs` cung cấp layout và hỗ trợ Web Worker; phần render, tương tác và chính sách nhãn vẫn cần thực hiện trong ứng dụng. [Tài liệu elkjs](https://github.com/kieler/elkjs)

Trước khi chọn chính thức, chạy spike trên fixture và dữ liệu đã ẩn danh: kiểm tra port constraints, label sizing, backward/cross-cluster routing, tính ổn định, worker asset với Vite production build, giấy phép, kích thước tải và budget mục 8. Khóa phiên bản sau spike; chưa chọn số phiên bản trong plan này.

So sánh với baseline tự viết có bổ sung crossing minimization/ports. Chọn một engine chính khi đạt acceptance; giữ layout hiện tại làm fallback qua adapter trong thời gian rollout. Không duy trì hai triển khai đầy đủ lâu dài. Chưa cần chuyển sang React Flow/Canvas/WebGL: đổi renderer không tự giải quyết collision. Chỉ nghiên cứu renderer khác khi đã culling/LOD mà benchmark vẫn không đạt.

## 4. Layout node và edge routing

### 4.1. Rank, thứ tự và sự ổn định

1. Dùng `PrerequisiteOf`/`PartOf` làm backbone tăng rank như hiện tại. `RelatedTo`/`CausesErrorIn` là overlay, không được làm thay đổi ý nghĩa thứ hạng tiên quyết. Không đảo source/target DTO; mọi biến đổi nội bộ của engine phải được ánh xạ lại đúng chiều khi vẽ.
2. Crossing minimization bằng các lượt barycenter/median có giới hạn hoặc cấu hình tương đương của engine. Làm rõ `orderIndex` là ràng buộc cứng ở đâu, ưu tiên mềm ở đâu; mặc định giữ thứ tự học thuật có ý nghĩa, chỉ hoán vị phần được phép. Tie-break bằng ID ổn định; seed/options và phiên bản engine nằm trong cache key.
3. Khoảng cách layer/hàng theo số lane, fan-in/out, kích thước label thực; không chỉ tăng gap cố định toàn cục. Khởi đầu thử gapX 120–180, gapY 40–64 graph units ở scale 1, sau đó hiệu chỉnh theo benchmark.
4. Pack connected components, tính group bounds; tránh một cột dài làm toàn cảnh co bé đến mức không còn đọc được. Trạng thái cycle không hợp lệ có cảnh báo và fallback hữu hạn; không âm thầm xóa cạnh hay sửa dữ liệu để ép thành DAG.
5. Giữ vị trí đã có khi expand/local update trong giới hạn engine; không tự fit lại khi chỉ select/hover, đổi nhãn hoặc pan. Fit khi người dùng yêu cầu, lần nạp đầu hoặc đổi scope; restore camera khi quay lại cụm cùng snapshot.

### 4.2. Port, lane và đường đi

- Allocate port riêng hoặc slot ổn định cho từng cạnh theo hướng, endpoint đối diện, relation type và edge occurrence ID. Tránh tập trung tất cả fan-in/out vào đúng tâm cạnh card. Hub vượt số slot chuyển sang port group + drill-down, không nén vô hạn các slot.
- Forward backbone dùng orthogonal/polyline qua hành lang giữa layer, có thể bo góc sau kiểm tra clearance. Cạnh same-column, backward, long-span và overlay có lane riêng bên ngoài/giữa vùng node; không chỉ tăng độ cong của Bezier cũ.
- Nhóm cạnh theo cặp occurrence endpoints, xét cả chiều ngược. Parallel edges và reciprocal edges có lane/port/arrowhead phân biệt; không gộp các loại quan hệ thành một đường ở chế độ chi tiết. Giữ edge ID cho từng hit target và inspector.
- Router nhận tất cả node rectangles đã inflate clearance, reserved corridors và bounds cụm. Overlay phải route quanh node đã đặt; nếu engine không route được overlay mà giữ rank, thử router obstacle-aware có ngân sách tìm kiếm trên visibility/grid graph. Không giả định elkjs có API standalone routing phù hợp trước khi spike xác nhận.
- Clearance khởi đầu: 12 graph units giữa centerline và card không phải endpoint, 8 graph units giữa lane; xét thêm nửa stroke và arrowhead. Smoothing không được cắt qua card hoặc góc obstacle; validate geometry sau smoothing.
- Tính bounds từ node + route extrema/sections + arrowhead + label/callout; thêm padding trước `fitGraphCamera`. Cạnh vòng ở biên phải cũng phải nằm trong khung khi “Vừa khung”.
- Nếu không còn hành lang trong ngân sách: mở rộng corridor/relayout cục bộ có giới hạn; sau đó chuyển sang cụm hoặc focus. Hiển thị số cạnh tạm ẩn và cho xem trong danh sách; không vẽ xuyên card rồi coi là thành công.
- Edge bundling chỉ dành cho overview theo cặp cụm, hướng và relation type. Cạnh aggregate có số lượng và mở được danh sách cạnh gốc; không hiển thị một trọng số `1` như thể đó là trọng số của cả bó.

## 5. Tránh va chạm nhãn trọng số

1. Đo text sau khi font sẵn sàng; dùng formatter thống nhất và giữ giá trị đầy đủ trong inspector. Bounding box gồm text, padding, nền và viền; không cố định mọi badge 36×20. Cache phép đo theo text/font/theme; xử lý số thập phân và tên tiếng Việt dài.
2. Ưu tiên nhãn: cạnh selected/keyboard focus → hovered → cạnh thuộc neighborhood đang xem → phần còn lại. Tie-break bằng ID, không theo thứ tự render ngẫu nhiên. Reserve chỗ cho nhóm ưu tiên trước.
3. Tạo tập ứng viên có giới hạn dọc theo arc length/segment đủ dài, ví dụ 35/50/65%, cùng normal offsets hai phía. Tránh góc gấp, arrowhead và port; không chỉ lấy trung bình tọa độ endpoints. Offset xa cần leader line chỉ rõ cạnh sở hữu.
4. Dùng spatial index để kiểm tra label–label, label–node, label–arrowhead và label–cạnh khác. Chấp nhận badge che một đoạn của chính cạnh nó với nền đặc; không che đường của cạnh khác. Phép kiểm tra dùng screen space/CSS pixels để phản ánh thứ người dùng nhìn thấy.
5. Chọn candidate ít lệch nhất còn hợp lệ; khoảng hở nhãn khởi đầu ≥4 CSS px. Giới hạn số candidate/lần sửa cục bộ để tránh thuật toán O(L²) trên toàn graph.
6. Không có chỗ: ẩn nhãn ưu tiên thấp và tăng counter “Nhãn hiển thị x/y”; hover/focus/select vẫn đọc được trọng số qua inspector hoặc popover ngoài vùng graph. Nhãn selected không được biến mất về mặt thông tin; nếu hết chỗ trong canvas, dùng callout/panel cố định và nêu rõ nguồn/đích.
7. Trong lúc pan giữ placement theo world space khi scale không đổi; cập nhật culling bằng animation frame. Khi zoom đổi bucket LOD, debounce/recompute placement và dùng hysteresis, tránh nhãn rung liên tục. Sau resize/font/theme change phải đo lại phần cần thiết.

Chế độ nhãn dự kiến: “Tự động” mặc định, “Chỉ cạnh đang xem”, “Yêu cầu hiện trọng số”. Chế độ cuối vẫn phải qua collision solver, kèm thông báo nhãn bị ẩn và danh sách đầy đủ; không hứa hiện mọi nhãn cùng lúc trong graph dày.

## 6. LOD, clustering và điều hướng dữ liệu lớn

LOD (mức chi tiết) dựa trên scale, mật độ và ngân sách phần tử đang thấy. Các ngưỡng dưới đây là điểm khởi đầu để hiệu chỉnh, không phải kết quả đo thực tế.

| Chế độ | Gợi ý điều kiện | Hiển thị và thao tác |
| --- | --- | --- |
| Overview | Scale <0.4 hoặc graph vượt ngân sách detail | Cụm theo chương/nhóm thật; số node/cạnh; cạnh aggregate theo loại/hướng; ẩn trọng số cá nhân |
| Explore | Scale 0.4–0.8 và subgraph trong ngân sách | Node rút gọn; ưu tiên backbone; overlay bật theo filter; nhãn selected/focus/hover |
| Detail | Scale >0.8, mật độ nhãn còn phù hợp | Card đầy đủ; route riêng; nhãn qua collision solver; inspector và danh sách quan hệ |

- Dùng hysteresis khoảng 0.05 quanh ngưỡng; ưu tiên thao tác chọn/focus. Không chuyển detail chỉ vì zoom lớn nếu vẫn có quá nhiều phần tử. Ngân sách khởi đầu: ≤300 node occurrence, ≤1.500 cạnh thật, ≤80 badge đồng thời; vượt ngưỡng thì gom cụm/thêm trang, không cắt dữ liệu âm thầm.
- Cụm dựa trên `Chapter` và `parentNodeId` của projection, lồng nhau nếu cần; khóa theo scope/version/application/occurrence. Không suy chương hoặc khối từ tiền tố `nodeCode`. Node không có cha vào nhóm “Chưa phân nhóm”; parent lỗi/cycle phải có xử lý hữu hạn.
- Cùng canonical node trong các curriculum/application khác nhau vẫn là các occurrence khác nhau. Nếu overview có nhóm tổng hợp, lưu membership đầy đủ và phân biệt nguồn khi expand; không tạo quan hệ học thuật mới từ cluster edge.
- Collapse/expand có breadcrumb và số lượng thành viên, số liên kết vào/ra/nội bộ. Nêu rõ tổng số theo scope, sau filter, trong subgraph và đang render; phân biệt “ẩn do LOD” với “không tồn tại”.
- Search vẫn tìm trong toàn scope được phép, không chỉ DOM/cụm đang mở. Chọn kết quả mở đúng chuỗi cụm và focus đúng occurrence; nếu nhiều nguồn giống tên thì cho chọn nguồn/version.
- Neighborhood hỗ trợ 1-hop mặc định, mở thêm có kiểm soát. Hub có hàng nghìn hàng xóm phải lọc theo hướng/loại quan hệ và phân trang danh sách; luôn thấy tổng số và “Xem thêm”. Không gọi neighborhood là toàn bộ chương.
- Viewport culling dùng node bounds và **route bounds** với overscan, không chỉ kiểm tra hai endpoints: cạnh nối hai node ngoài màn hình vẫn có thể đi qua viewport. Phân biệt culling (chỉ mount) với filtering (đổi subgraph).
- Pin selection/focus qua culling hoặc chuyển focus vào inspector trước khi unmount; không tạo hàng nghìn điểm Tab. Bảo toàn chế độ bảng hiện có; bổ sung bảng/danh sách cạnh với nguồn, đích, loại, trọng số và provenance nếu bảng hiện tại chưa đủ.
- Không tự đưa `externalPrerequisites` vào cụm cùng scope. Giữ khối ngữ cảnh ngoài phạm vi; nếu sau này có stub visual thì phải ghi rõ chỉ tham chiếu, không tính vào số node của scope.

## 7. Hiệu năng, bộ nhớ và nạp dữ liệu

- Dựng `nodeById`, adjacency in/out, childrenByParent và indexes occurrence/provenance một lần theo revision. Canvas hiện có `.find` node trong vòng map cạnh và filter con lặp theo chapter; `graphOccurrences` có tra cứu provenance lặp. Đo và thay các đoạn nóng bằng index trước khi kết luận layout là nút thắt duy nhất.
- Layout/routing/collision của graph lớn chạy trong worker. Pan/hover/selection không làm chạy lại layout. Một frame cập nhật camera tối đa một lần qua `requestAnimationFrame`; memoize geometry, giới hạn kết quả search đang mount và virtualize danh sách dài.
- Job mang request ID + scope key + data revision + layout options + kích thước node + engine version. Bỏ kết quả cũ nếu đổi scope/filter nhanh; terminate/recreate worker khi cần hủy tác vụ không hủy được, gom các yêu cầu liên tiếp. Timeout hữu hạn và đường về bảng/focus/cụm.
- Tách cache topology/layout khỏi cache nhãn theo zoom/font. LRU có trần dung lượng; key gồm center, actor, subject, grade, version, curriculum/class/year/history, snapshot/revision và filter liên quan. Xóa cache khi logout/đổi tenant; không tái dùng chỉ theo subject ID.
- Giữ camera/selection khi geometry không đổi; nếu node bị filter hoặc dữ liệu mới xóa occurrence thì thông báo và chọn trạng thái hợp lệ. Pending/error/cancel có trạng thái đọc được; giữ snapshot cũ chỉ khi cùng scope, không dùng dữ liệu scope trước làm placeholder cho scope mới.
- Lazy-load layout engine/worker trên route graph; đo cả worker asset và dữ liệu clone/serialize, không chỉ thời gian thuật toán. SVG vẫn là renderer đầu tiên; hybrid Canvas/WebGL chỉ là nhánh nghiên cứu có benchmark và phương án accessibility tương đương.
- Với dữ liệu đủ vừa trong scope: build cluster/index ở client, không mount toàn bộ. Với graph rất lớn hoặc payload vượt budget: cần **giai đoạn backend riêng** để có summary theo cụm, search và neighborhood/cursor có snapshot/version nhất quán. Client hiện chưa có các endpoint này; không giả định tham số phân trang đã tồn tại.
- API tương lai phải trả total/hasMore, IDs ổn định, edges boundary và provenance; giữ authorization/tenant/scope ở server, không mở rộng scope ngầm. Đo payload nén, parse, projection, worker round-trip, mount và tải mạng riêng. Ngừng tải toàn bộ khi vượt ngưỡng đã đo; cung cấp search/focus/summary thay vì treo UI.

## 8. Ngân sách và cách đo

Tất cả số sau là **mục tiêu đề xuất**, cần xác nhận ở P0 trên máy tham chiếu được ghi rõ CPU/RAM, browser/version, viewport, DPR và chế độ nguồn. Dùng production build, fixture seed cố định, cùng font, 5 lượt warm-up + ít nhất 30 lượt đo; báo p50/p95 và lần tải lạnh riêng. Không suy hiệu năng từ ảnh hoặc test unit.

| Tập dữ liệu | Cách xem được chấp nhận | Ngân sách p95 từ DTO sẵn sàng tới view tương tác được |
| --- | --- | --- |
| Nhỏ: 100 node / 300 cạnh | Detail đầy đủ khi density cho phép | ≤300 ms |
| Vừa: 1.000 node / 5.000 cạnh | Overview theo cụm; detail trong budget 300/1.500 | ≤1,5 s; focus/expand đã cache ≤300 ms |
| Lớn: 10.000 node / 50.000 cạnh | Summary/cụm + neighborhood có giới hạn; không vẽ detail toàn graph | ≤3 s cho overview; focus có dữ liệu sẵn ≤500 ms |

Đối với tập lớn, phải đo cả chi phí tạo summary nếu tạo ở client; chỉ dùng summary có sẵn trong benchmark khi đã công khai điều kiện đó. Mốc API summary là gate để hỗ trợ quy mô lớn hơn khả năng client, không phải tính năng hiện có.

Các budget chung:

- Khi pan/zoom trên view trong ngân sách: p95 frame ≤33 ms, mục tiêu 60 FPS; từ input đến phản hồi chọn/focus ≤100 ms khi không cần fetch/layout mới. Đo qua trace/frame timings, không chỉ FPS trung bình.
- Main-thread task của graph trong thao tác ổn định không vượt 50 ms; layout worker có timeout khởi đầu 3 s cho view bounded. Cold load engine, fetch và parse báo riêng, đồng thời báo thời gian end-to-end để tránh che nút thắt.
- Heap tăng thêm so với cùng trang trống: mục tiêu ≤150 MB với tập vừa, ≤300 MB với tập lớn. Sau 20 vòng đổi scope/expand/collapse, không tăng tuyến tính; ghi cả worker memory nếu công cụ đo hỗ trợ và giới hạn phép đo.
- Main entry vẫn đạt budget hiện có ≤150 KB theo phép đo của test hiện tại; layout engine không nằm trong initial preload của các route khác. P0 chốt budget riêng cho graph chunk/worker dựa trên spike, có số tải nén và chưa nén.
- Không đạt budget thì hạ detail/giảm subgraph/chuyển summary, kèm trạng thái rõ; không tăng ngưỡng vô hạn. Chỉ tuyên bố hỗ trợ 10k/50k sau khi benchmark và API/data path tương ứng đạt.

## 9. Accessibility và tương tác

Giữ keyboard pan, +/−, Escape, Enter/Space hiện có; thêm điều hướng node lân cận và roving tabindex/danh sách có thể tìm kiếm. Chỉ một điểm vào graph bằng Tab, tiếp theo là inspector/controls; focus không bị mất khi collapse, culling hoặc đổi scope. Người dùng phải chọn được cạnh và đọc trọng số mà không dùng chuột.

Mục tiêu giao diện: text thường tương phản ≥4.5:1; thông tin đồ họa cần thiết/focus đạt ≥3:1; control có target tối thiểu 24×24 CSS px hoặc phương án tương đương hợp lệ. Đường cạnh mảnh phải có hit area theo screen space và danh sách thay thế; strokeWidth 18 graph units hiện có sẽ nhỏ theo zoom. [Text contrast](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html), [non-text contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html), [target size](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html)

- Dùng màu cùng dash/icon/tên loại quan hệ; không chỉ dựa vào màu. Không làm mờ nội dung cần đọc đến mức mất tương phản khi chọn node khác.
- Accessible name cạnh gồm nguồn, đích, loại, trọng số và nguồn occurrence cần phân biệt. `aria-selected`/trạng thái chọn phải phù hợp semantics control được dùng; không gán role không có hành vi tương ứng.
- Tooltip hoạt động bằng hover và keyboard focus, có thể đóng bằng Escape; touch có tap-select/inspector. Không cần double-click hoặc drag để thực hiện tác vụ thiết yếu.
- Có nút pan/zoom/fit hoặc phương án bàn phím tương đương; hỗ trợ reduced motion, dark/light theme, browser zoom 200% và viewport 320 px. Bộ lọc, bảng và inspector không tràn ngang gây mất thao tác.
- Thông báo số lượng bị ẩn/trạng thái layout qua live region có debounce; không đọc lại toàn graph mỗi frame. Kiểm tra thủ công với NVDA trên Windows; automated accessibility check chỉ là một lớp kiểm tra.

## 10. Kế hoạch triển khai theo giai đoạn

| Giai đoạn | Công việc / đầu ra | Điều kiện kết thúc |
| --- | --- | --- |
| P0 — Đo baseline và chốt thiết kế | Tái tạo vùng như ảnh bằng fixture; inventory route/consumer; thu trace/geometry/cold load; spike ELK với worker production và so baseline; chốt semantics thứ tự, budget và thiết kế label fallback | Có bảng so sánh đo được, quyết định engine và fixture seed; không lựa chọn chỉ bằng screenshot |
| P1 — Layout/routing đúng | Tách view model/adapter; port/lane allocation, obstacle routing, multi/reciprocal edges; giữ backbone rank; bounds đầy đủ; deterministic output và fallback | Không xuyên card hoặc trùng path hoàn toàn trong fixtures, fit không cắt route; consumer helper cũ vẫn tương thích hoặc được chuyển có chủ đích |
| P2 — Nhãn và tương tác | Đo chữ, candidate placement/spatial index, priority/fallback; LOD/hysteresis; selection/camera stability; keyboard và bảng cạnh | Nhãn thực sự hiển thị không va chạm; trọng số selected luôn đọc được; interaction suite và kiểm tra accessibility đạt |
| P3 — Quy mô lớn | Clustering theo hierarchy/occurrence, expand/neighborhood budgets, culling, index/cache/worker jobs; đo payload và quyết định cần backend summary | Đạt budget tập vừa/lớn trong chế độ công bố; nếu cần API thì hoàn tất hợp đồng, auth/snapshot tests và integration trước khi nhận hỗ trợ quy mô đó |
| P4 — Nghiệm thu và rollout | Browser regression, screenshot ở nhiều viewport/theme, perf trace, kiểm tra Teacher/Manager và scope/history; feature flag, tài liệu vận hành và rollback | Đủ bằng chứng mục 11–12; chạy các check repo phù hợp, không còn lỗi nghiêm trọng |

P1 và P2 là bản cải thiện khả năng đọc đầu tiên; không gọi đó là hoàn tất scalability nếu P3 chưa đạt. Owner dự kiến: frontend cho P0–P3 UI, backend cho data contract khi cần, QA/product xác nhận khả năng đọc và acceptance ở mỗi gate. Chưa đặt lịch hoàn thành khi chưa có baseline.

### Vị trí code dự kiến cho lần triển khai sau

- Sửa: `web/edutwin-web/src/utils/knowledgeGraphLayout.ts`, `knowledgeGraphViewport.ts`; `src/components/teacher/KnowledgeGraphCanvas.tsx`; `src/pages/teacher/TeacherKnowledgeGraphView.tsx` (các đường dẫn `src/...` ở đây thuộc `web/edutwin-web/`).
- Xem xét tối ưu có đo: `web/edutwin-web/src/utils/knowledgeScope.ts`; giữ regression scope/occurrence.
- Module mới dự kiến dưới `web/edutwin-web/src/`: adapter/layout worker, router/geometry, label placement, LOD/clustering/spatial index. Tên và cách chia file chốt sau P0; đây không phải file đã tạo.
- `web/edutwin-web/src/api/knowledgeGraphApi.ts`, types và backend chỉ thay khi P3 thực sự cần API summary/neighborhood. Không đưa endpoint giả vào UI.
- Tests mới/sửa dưới `web/edutwin-web/tests/`, browser spec trong `test/cypress/e2e/`; evidence sau này dưới `docs/verification/knowledge-graph-visualization/`.

## 11. Ma trận kiểm thử dự kiến

| Nhóm | Fixture / tình huống | Kiểm tra |
| --- | --- | --- |
| Tái hiện ảnh | Hai cột card, các cạnh hội tụ/giao nhau ở giữa, cạnh same-column bên phải, weight=1 lặp lại | Nguồn/đích rõ, badge không chồng, vòng cạnh không bị crop |
| Graph topology | Chain dài, layer rộng, disconnected, isolated, diamond/crossing, fan-in/out và hub degree 200+ | Rank đúng, routing tránh card, hub fallback có count và truy cập đủ cạnh |
| Edge cases | Cùng endpoints khác relation, reciprocal, backward/long-span/same-column; input self-loop/cycle/dangling không hợp lệ | Không trùng path cho cạnh riêng, arrow đúng chiều, lỗi hữu hạn không crash/hang, không sửa DTO |
| Nhãn | Weight 0/1/số thập phân, nhiều badge cùng vùng, text Việt dài, font chậm, resize/theme/zoom liên tiếp | Bounds đúng, priority deterministic, không che node/nhãn khác; fallback rõ khi hết chỗ |
| Scope/identity | Cùng canonical node nhiều application/version, class union, grade/history, inactive, empty scope, external prerequisites | Không gộp sai occurrence, không xuất hiện fake edge, không lẫn cache tenant/actor/scope; count đúng |
| Async | Đổi A→B→A khi worker/fetch chưa xong, timeout/error/unmount/logout | Không commit stale result, không hiển thị scope khác, không rò worker/listener/cache |
| Camera/culling | Cạnh băng viewport dù cả hai endpoints ngoài; select rồi pan/collapse; fit đường vòng | Culling dùng route bounds; focus/selection ổn định; không fit bất ngờ |
| Quy mô | 100/300, 1k/5k, 10k/50k; graph dày, hub, hierarchy nhiều tầng; vượt ngưỡng hỗ trợ | Số phần tử mount có trần, không im lặng mất quan hệ, perf/memory đạt đúng chế độ |
| Trình duyệt/a11y | Chromium trên Windows; viewport 320/768/1240/1920, DPR 1/2, dark/light, 200%, reduced motion, keyboard/NVDA/touch | Không mất thao tác/đọc trọng số; focus và contrast đạt; browser hỗ trợ khác theo chính sách project |

Unit/property tests kiểm tra bất biến hình học và danh tính, không chỉ regex source. Sinh graph seed cố định, hoán vị thứ tự input và so layout/route/label output trong tolerance; kiểm tra bounds hữu hạn và DTO không bị mutate. Đo giao cắt bằng segments/curve flattening có tolerance; bỏ tiếp xúc endpoint hợp lệ và giao với chính path của badge.

Browser tests phải đo bounding boxes thực sau font load, click/keyboard vào từng cạnh song song, expand/search đúng occurrence và screenshot trước/sau bằng cùng fixture/camera. Dùng Cypress harness sẵn có, không thêm test runner khác nếu chưa có lý do. Dữ liệu test cô lập; không tạo/sửa đồ thị sản xuất để chụp ảnh.

Các check cho lần triển khai sau: từ `web/edutwin-web/` chạy `npm run lint`, `npm test`, `npm run test:bundle` (script này đã build); chọn graph browser spec qua harness ở `test/` theo README. Nếu đổi backend contract thì chạy bộ .NET scope/controller/validator tương ứng và integration tests. Lượt lập plan này không chạy các check đó vì không thay code.

## 12. Tiêu chí nghiệm thu và phát hành

- [ ] **Geometry:** zero node overlap; zero edge–node intersection ngoài endpoint hợp lệ, tính cả stroke/arrowhead; zero full-path overlap của các cạnh thật khác ID trong chế độ detail. Không yêu cầu zero edge crossing của mọi topology.
- [ ] **Labels:** zero label–label/node/arrowhead overlap đối với nhãn đang hiển thị, với margin ≥4 CSS px. Báo số nhãn hiện/tổng đủ điều kiện; selected/focused edge luôn đọc được weight qua canvas hoặc panel thay thế. Không đạt mục tiêu bằng cách ẩn tất cả: fixture tái hiện ảnh và graph nhỏ thưa phải hiện đủ nhãn ở Detail/100%.
- [ ] **Crossings:** với fixture được phép reorder ở P0, giảm ít nhất 30% giao cắt so baseline; graph vốn không giao cắt không được tăng. Trường hợp bị ràng buộc thứ tự phải ghi số đo và lý do, không bóp méo ngữ nghĩa để đạt phần trăm.
- [ ] **Identity và nghiệp vụ:** hướng, loại, weight, canonical/occurrence IDs, provenance và quyền/history giữ đúng; view filter/collapse không gọi API ghi. Mọi aggregate truy ngược được về cạnh thật và có count nhất quán.
- [ ] **Bounds và camera:** “Vừa khung” chứa card, đường vòng, arrow và nhãn; chọn/hover không reset camera; search/expand quay lại được và không mất focus.
- [ ] **Scale:** đạt budget mục 8 và hiển thị đúng chế độ khi vượt ngân sách. Danh sách quan hệ cho phép tiếp cận mọi cạnh được phép trong scope, kể cả cạnh/nhãn đang ẩn.
- [ ] **Accessibility:** hoàn thành checklist keyboard, screen reader, contrast, target, touch và reduced motion; không còn lỗi nghiêm trọng trong cả kiểm tra tự động và thủ công.
- [ ] **Evidence:** lưu fixture seed/snapshot đã ẩn danh, commit/build, cấu hình máy/browser, p50/p95/cold load, DOM counts, memory, geometry counters, trace và ảnh cùng camera. Kết quả chưa đo phải đánh dấu “chưa xác minh”.
- [ ] **Rollout:** bật flag visualization mới cho fixture/nội bộ trước; so feedback đọc đường đi, tỷ lệ fallback/timeouts và performance. Rollback flag về renderer/layout cũ hoặc bảng khi lỗi; không cần migration dữ liệu. Không ghi tên node hay dữ liệu người học vào telemetry.

## 13. Rủi ro và quyết định cần chốt khi triển khai

| Rủi ro | Biện pháp / gate |
| --- | --- |
| ELK làm đổi thứ tự học thuật, hoặc overlay ảnh hưởng rank | Adapter tách backbone/overlay; fixture rank/order; chỉ chọn engine khi giữ được semantics |
| Engine/worker tải lớn hoặc lỗi đường dẫn production | Lazy-load, build asset test, cold-load budget; fallback rõ, không kéo engine vào entry |
| Full graph vẫn quá dày dù routing đẹp | Clustering, density budget, neighborhood và bảng; không chỉ tăng spacing |
| Label solver tốn CPU hoặc nhãn rung | Candidate/pass giới hạn, spatial index, priority ổn định, hysteresis và profiler |
| Collapse gộp sai occurrence | Key có provenance/scope; membership map và test class union/version |
| API full payload trở thành nút thắt | Gate P3 summary/neighborhood; đo mạng/parse/projection riêng và end-to-end |
| Test cũ khóa cú pháp helper của trang không hoạt động | Giữ compatibility có chủ đích; thay assertion implementation bằng hành vi của route đang dùng khi cần |

P0 cần chốt trên bằng chứng: engine chính; semantics cứng/mềm của `orderIndex`; budget tải graph chunk/worker và payload; quy mô dữ liệu thực của người dùng; cần backend summary ngay hay sau bản đầu. Đây là các quyết định kỹ thuật cho lần triển khai, không phải hạng mục đã thực hiện trong tài liệu này.

## 14. Trạng thái triển khai (2026-11-12)

Quyết định P0 đã chốt trong lượt này: **chọn baseline tự viết** (rank hiện tại + giảm giao cắt barycenter + port/lane + routing vuông góc), chưa spike ELK nên chưa thêm `elkjs`. Ngữ nghĩa `orderIndex`: **cứng** trong cùng `parentNodeId` (anh em giữ thứ tự và liền khối), **mềm** giữa các khối anh em/nút không có cha. Rank không đổi.

### Đã làm

| Hạng mục | Vị trí (`web/edutwin-web/`) |
| --- | --- |
| Giảm giao cắt có ràng buộc, giữ ordering tốt nhất qua 6 lượt; tùy chọn `crossingMinimization` (trang cũ không đổi hành vi) | `src/utils/knowledgeGraphLayout.ts` |
| View model: port riêng từng cạnh (lưới trái/phải lệch nhau nên stub không bao giờ trùng), lane trong hành lang, track ngang ở khe hàng cho cạnh dài/ngược, hành lang rộng theo số lane + chỗ cho nhãn; parallel/reciprocal/same-column/self-loop tách đường; hub vượt slot → `hiddenEdges` có lý do; cạnh treo → `invalidEdgeIds`; cảnh báo vòng tiên quyết; bounds gồm route + mũi tên + chỗ nhãn; scene cũ (Bezier) làm fallback | `src/utils/knowledgeGraphScene.ts`, `knowledgeGraphGeometry.ts` |
| Nhãn trọng số: đo chữ thật sau `document.fonts.ready`, formatter chung `vi-VN`, ứng viên trên đoạn dài (15–85 %, trên/hai bên đường), spatial grid, tránh thẻ/mũi tên/nhãn khác/đường khác; nhóm ưu tiên (đang chọn, đang focus, liên kết của nút đang chọn) đặt trước, phần còn lại theo độ chật; đếm “Nhãn hiển thị x/y” và số nhãn ẩn | `src/utils/knowledgeGraphLabels.ts` |
| Canvas: LOD toàn cảnh/khám phá/chi tiết có hysteresis 0,05; 3 chế độ nhãn; trần 80 badge; ngân sách 300 nút/1.500 cạnh (vượt → toàn cảnh rút gọn + thông báo); culling theo bounds thẻ và **bounds đường**, ghim mục đang chọn/focus; pan qua `requestAnimationFrame`; “Vừa khung” theo bounds đầy đủ, chọn/hover không reset camera; dash riêng cho từng loại quan hệ; hit area ≥14 px màn hình; một tab stop vào SVG, Enter/mũi tên/E/Esc điều hướng nút–cạnh; tên truy cập cạnh có nguồn, đích, loại, trọng số (kèm giáo trình khi trùng tên); callout khi nhãn của cạnh đang chọn/xem không hiện; danh sách liên kết có lọc loại và phân trang 50; live region có debounce; chỉ mục `Map` thay `.find` trong vòng render; bộ chọn “Kiểu đường nối” lưu ở `localStorage` khóa `edutwin.graph.routing=legacy` để rollback | `src/components/teacher/KnowledgeGraphCanvas.tsx`, `src/utils/knowledgeGraphViewport.ts` |
| Test hình học/danh tính (14 test) + fixture tái hiện ảnh, sinh graph có seed | `tests/knowledgeGraphScene.test.ts`, `tests/fixtures/knowledgeGraphFixtures.ts` |

### Bằng chứng đã đo (chưa phải benchmark P0 chuẩn)

- Unit: fixture ảnh 0 đường xuyên thẻ, 0 đoạn trùng giữa hai cạnh, 15/15 nhãn hiện, 0 va chạm nhãn (margin 5 đơn vị); topologies chain/wide/disconnected/diamond/long-span/backward/self-loop/cycle/random sạch; hub 200 cạnh báo ẩn thay vì nén port; output ổn định khi hoán vị input; DTO đóng băng không bị đổi.
- Giao cắt (đoạn vuông góc, cùng router): fixture ảnh 54 → 12 (−78 %); graph ngẫu nhiên seed 7 (60 nút/111 cạnh) 1.320 → 689 (−48 %); seed 11 (40/98) 1.256 → 703 (−44 %).
- Trình duyệt (harness tạm với fixture, Vite dev, Chromium 156 trong Claude desktop, Windows, 12 luồng, DPR 1,25, pane bị ẩn): ở 100 % đo `getBoundingClientRect` thật sau khi font tải: 0 chồng nhãn–nhãn/nhãn–thẻ với hở 4 px, 0 nhãn đè đường của cạnh khác; bàn phím chọn được cạnh và đọc trọng số; chọn từ danh sách đưa camera tới cạnh; graph 180/508 ở 100 % chỉ mount 12 nút/179 đường; theme tối nhận đúng token; 320 px không tràn ngang.
- Thời gian layout+route+nhãn trong trình duyệt (dev build, 30 lượt): 100 nút/282 cạnh p50 27 ms, p95 35 ms; 1.000/4.917 (80 nhãn) p50 395 ms, p95 561 ms — **vượt** mục tiêu 50 ms/main-thread task nếu chạy trên main thread; đã chuyển sang worker (mục dưới).
- `tsc -b` sạch; `npm test` 881/881; build + `bundleBudget` 4/4, main entry 140,9 KB. `eslint .` còn 1 lỗi có sẵn ở `tests/questionImportLatexGuide.test.ts` (không thuộc thay đổi này).

### Layout worker (bổ sung 2026-11-12)

| Hạng mục | Vị trí (`web/edutwin-web/`) |
| --- | --- |
| Giao thức dùng chung worker/test: chỉ clone trường cần cho layout (không gửi tên, mô tả, provenance); khóa cache = phiên bản engine + engine + scope (center:user:scopeKey) + options + hash 64-bit của mọi trường ảnh hưởng hình học/trọng số; serialize scene | `src/utils/knowledgeGraphLayoutProtocol.ts`, `knowledgeGraphScene.ts` |
| Client: request ID, kết quả trễ bị bỏ, hàng đợi theo lane gộp yêu cầu liên tiếp (yêu cầu giữa bị `superseded`), timeout 3 s → terminate và tạo worker mới, LRU 8 scene / 60.000 cạnh, worker mất scene thì client gửi lại (`adopt`), `clear()` khi đổi user/center | `src/utils/knowledgeGraphLayoutClient.ts`, `src/workers/knowledgeGraphLayout.worker.ts` |
| Hook: ≤150 nút và ≤400 cạnh tính đồng bộ; lớn hơn thì layout và nhãn đều qua worker; trong lúc chờ giữ scene cũ cùng scope kèm “Đang tính lại bố cục…”; timeout/lỗi → fallback đường cong cũ có thông báo; nhãn lỗi → vẫn đặt nhóm ưu tiên | `src/components/teacher/useKnowledgeGraphLayout.ts` |
| Canvas: toàn cảnh vẽ cạnh thành một path gộp mỗi loại (memo theo scene), cạnh nổi bật vẽ riêng có thể focus; thẻ nút `memo`, làm mờ bằng CSS (`data-dim`/`data-near`); index vật cản của bộ đặt nhãn cache theo scene | `KnowledgeGraphCanvas.tsx`, `knowledgeGraphLabels.ts` |

Đo trên **production build** của harness tạm (Chromium 156, Windows, 12 luồng, DPR 1,25), thời gian main thread bị chặn: tập 1.000/4.917 (vượt ngân sách → toàn cảnh) — chọn nút 7–14 ms (trước tối ưu 639 ms), bỏ chọn 9 ms, chọn cạnh từ danh sách 55 ms lần đầu rồi 10 ms, phóng 100 % 16–26 ms, “Vừa khung” mount lại 1.000 thẻ 70–143 ms, heap JS 38 MB. Tập 180/508 ở Chi tiết: nhãn từ worker về sau 80 ms, main thread chặn 15 ms, 0 va chạm nhãn (đo bằng pixel), đổi kiểu đường rồi quay lại lấy từ cache (28 ms). Worker là asset riêng 17,7 KB (≈7 KB nén), chỉ được tham chiếu từ chunk lazy của route; main entry vẫn 140,9 KB. Test: 10 test cho protocol/client (`tests/knowledgeGraphLayoutWorker.test.ts`).

### Chưa làm / chưa xác minh

- Spike ELK và bảng so sánh. Lần mount đầu toàn cảnh 1.000 thẻ vẫn là một tác vụ dài (≈0,5 s gồm tải module) — cần gom cụm theo chương để giảm số thẻ.
- Timeout worker tính từ lúc gửi job, nên job xếp sau một job dài cũng có thể bị tính là timeout.
- Cụm collapse/expand theo chương với cạnh aggregate, breadcrumb; pack connected components (cột dài vẫn làm toàn cảnh rất nhỏ, ví dụ 10 %).
- Hover chưa đổi placement (tránh tính lại khi rê chuột); hover dùng callout thay vì nhãn ưu tiên. Leader line cho nhãn đặt xa chưa có (ứng viên chỉ nằm sát đường).
- Frame time pan/zoom bằng trace và cold-load chưa đo (pane trình duyệt bị ẩn nên rAF dừng); kiểm tra NVDA, touch, zoom 200 %, reduced motion thủ công chưa làm.
- Cypress spec cho graph chưa thêm (harness E2E cần Docker stack); API summary/neighborhood backend chưa cần ở quy mô hiện tại.
