# Kế Hoạch Triển Khai Phase 2A — Unified Math Input & Authoring Experience

> **Tài liệu**: `docs/plans/POST-R09-MATH-CANONICALIZATION-PHASE2A-PLAN.md`
> **Trạng thái**: Kế hoạch Phase 2A v3.4 — Đã hoàn thành và nghiệm thu Gate 2A.1 (Commit `6b11f52`, forward commit `9d0d5e4`); Hoàn thiện kế hoạch chi tiết Gate 2A.2 theo đánh giá của Codex trước khi triển khai
> **Baseline Git**: `9d0d5e4` (`student/answer`)
> **Phạm vi**: **Chuẩn bị triển khai Gate 2A.2 (CenterManager Authoring Integration)**. Tuyệt đối không sửa đổi Phase 1, không bắt đầu Phase 2B (MathEquivalent/CAS), chưa bắt đầu Gate 2A.3–2A.5, không commit/push code tính năng trong bước lập kế hoạch này.

---

## 1. Khảo Sát Hiện Trạng Tuyến Đường & Dữ Liệu Thực Tế (Current-State Inventory)

Dưới đây là bảng kiểm kê chi tiết theo đúng tuyến đường thực tế trong [App.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/App.tsx) và codebase backend (`src/EduTwin.*`):

| Route thực tế trong `App.tsx` | Actor | Trường nhập liệu | Component hiện tại | Format gửi Backend | Evaluation Mode | Vấn đề & Rủi ro phát hiện |
|---|---|---|---|---|---|---|
| `/hoc-tap/luyen-tap`<br>`/hoc-tap/luyen-tap/:questionId`<br>([LearningPlayerPage.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/LearningPlayerPage.tsx)) | **Student** | Đáp án câu hỏi MCQ | Radio buttons (`<button>`) chọn option | `finalAnswer: optionId`<br>`answerDisplayLatex: ""` | `TextExact` | Nhãn option chưa được render đồng nhất qua KaTeX nếu nội dung chứa công thức toán. |
| `/hoc-tap/luyen-tap`<br>`/hoc-tap/luyen-tap/:questionId`<br>([LearningPlayerPage.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/LearningPlayerPage.tsx)) | **Student** | Đáp án ShortAnswer | [VisualMathField.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/math/VisualMathField.tsx) (MathLive) | `finalAnswer: plainText`<br>`answerDisplayLatex: latex` | `TextExact`<br>`NumericRational`<br>`Coordinate2D`<br>`Manual` | `finalAnswer` lấy từ `normalizeMathLivePlainText(rawPlainText, latex)` trong khi `answerDisplayLatex` là LaTeX. Cần khóa cứng payload cho từng mode (đặc biệt `Coordinate2D` và `NumericRational`). |
| `/hoc-tap/luyen-tap`<br>([LearningPlayerPage.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/LearningPlayerPage.tsx)) | **Student** | Lập luận tư duy / Bước giải | `<textarea>` thuần + regex check KaTeX preview | `reasoningText: string` | Tùy câu hỏi (`reasoningRequired`) | Dùng regex `/[\\{^_\\]]/.test(...)` để bật `<MathFormulaPreview>` KaTeX dưới ô textarea. Không có công cụ chèn công thức thuận tiện cho học sinh. |
| `/quan-ly/cau-hoi/tao-moi`<br>`/quan-ly/cau-hoi/:id`<br>([QuestionEditorPage.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/QuestionEditorPage.tsx)) | **CenterManager** | Đáp án chuẩn (`correctAnswer`) | `<input className="cm-input font-mono">` + [MathFormulaPreview.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/math/MathFormulaPreview.tsx) | `correctAnswer: string` | Cả 4 mode (`TextExact`, `NumericRational`, `Coordinate2D`, `Manual`) | **Không có MathLive**. Người soạn phải tự gõ chuỗi LaTeX hoặc tọa độ. Không có live client syntax hint; chỉ khi submit API mới biết đáp án có hợp lệ hay không. |
| `/quan-ly/cau-hoi/tao-moi`<br>`/quan-ly/cau-hoi/:id`<br>([QuestionEditorPage.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/QuestionEditorPage.tsx)) | **CenterManager** | Đề bài (`questionText`), Option, Lời giải (`solution`) | `<textarea>` / `<input>` + [MathInputToolbar.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/math/MathInputToolbar.tsx) + [MathFormulaPreview.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/math/MathFormulaPreview.tsx) | `questionText: string`<br>`options[].optionText`<br>`solution: string` | N/A (Hỗn hợp văn bản + công thức) | [MathInputToolbar.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/math/MathInputToolbar.tsx) chèn ký tự Unicode giả (`□/□`, `²`, `√`) vào textarea. |
| `/giao-vien/cau-hoi/tao-moi`<br>`/giao-vien/cau-hoi/:id`<br>([TeacherQuestionEditorView.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/teacher/TeacherQuestionEditorView.tsx)) | **Teacher** | Đáp án chuẩn (`correctAnswer`) | `<input className="th-input w-full text-xs">` (Thuần HTML input) | `correctAnswer: string` | Cả 4 mode | **Nghiêm trọng: Không có MathLive và KHÔNG CÓ KaTeX Preview!** Giáo viên gõ text mù hoàn toàn, dễ dẫn đến sai cú pháp LaTeX cho `NumericRational` hoặc `Coordinate2D`. |
| `/giao-vien/cau-hoi/tao-moi`<br>`/giao-vien/cau-hoi/:id`<br>([TeacherQuestionEditorView.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/teacher/TeacherQuestionEditorView.tsx)) | **Teacher** | Đề bài (`questionText`), Option, Lời giải | `<textarea>` / `<input>` + [MathInputToolbar.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/math/MathInputToolbar.tsx) + [TeacherMathFormulaPreview.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/teacher/TeacherMathFormulaPreview.tsx) | `questionText: string`, `solution: string` | N/A | Dùng component preview riêng ([TeacherMathFormulaPreview.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/teacher/TeacherMathFormulaPreview.tsx)) trùng lặp với bên CenterManager. |
| `/hoc-tap/bai-tap/:id`<br>([StudentAssignmentDetailPage.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/StudentAssignmentDetailPage.tsx))<br>`/giao-vien/cham-bai`<br>`/quan-ly/duyet-bai`<br>([AssignmentGradingWorkspace.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/reviews/AssignmentGradingWorkspace.tsx)) | **Student**<br>**Teacher**<br>**CenterManager** | Xem lại đáp án đã nộp & đáp án chuẩn | [RichMathText.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/math/RichMathText.tsx) (Readonly KaTeX) | N/A (Chỉ đọc) | Cả 4 mode | Chưa có component readonly chuẩn hóa cho một đáp án toán học độc lập (hiện đang dùng chuỗi nối tạm thời). |
| Modal Import CSV/Excel<br>([QuestionImportModal.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/teacher/QuestionImportModal.tsx)) | **Teacher** / **CenterManager** | Bảng xem trước câu hỏi import | Bảng HTML `<td>{q.questionText}</td>`, `<td>{q.correctAnswer}</td>` | N/A (Xem trước dữ liệu trả về từ Preview Token) | Cả 4 mode | **Không render công thức toán**: Các công thức LaTeX (ví dụ `\frac{1}{2}`, `(1; 2)`) hiển thị dưới dạng chuỗi thô. |

---

## 2. Hợp Đồng Tuần Tự Hóa Dữ Liệu Bắt Buộc (Mandatory Serialization Contract)

Để đảm bảo ba tác nhân (Student, Teacher, CenterManager) khi dùng component chung đều gửi dữ liệu và lưu trữ dữ liệu đồng nhất lên API backend, bảng hợp đồng dưới đây là **bắt buộc tuân thủ tuyệt đối**:

| Chế độ & Loại câu hỏi | Giá trị gửi lên API (`apiValue`) | Giá trị trình bày (`displayLatex` / render) | Ánh xạ trường API cụ thể | Quy tắc định dạng chuẩn |
|---|---|---|---|---|
| `MultipleChoice` + `TextExact` | Student: `finalAnswer = optionId`<br>Authoring: `correctAnswer = optionLabel` (ví dụ `"A"`, `"B"`) | `answerDisplayLatex = ""` | Student: `SubmitAttemptRequest.finalAnswer`<br>Authoring: `CreateQuestionRequest.correctAnswer`<br>`UpdateQuestionRequest.correctAnswer` | Student gửi `optionId`. Soạn đề (`QuestionEditorPage.tsx`): tự động đồng bộ `correctAnswer` từ nhãn `optionLabel` của lựa chọn có `isCorrect = true`, đáp ứng đồng thời cả 2 điều kiện backend: đúng 1 option có `isCorrect = true` và `correctAnswer` không được rỗng. |
| `ShortAnswer` + `TextExact` | Văn bản thô (`rawText`) | Văn bản thô (hoặc KaTeX nếu chứa `$..$`) | Student: `SubmitAttemptRequest.finalAnswer`<br>`answerDisplayLatex: ""` (không bắt buộc LaTeX)<br>Authoring: `CreateQuestionRequest.correctAnswer` | Chuỗi ký tự chuẩn xác từng chữ cái / từ khóa, không ép thành công thức toán học. |
| `ShortAnswer` + `NumericRational` (Học sinh) | `finalAnswer = plainText` (ví dụ `3/4`, `-1.5`, `12`) | `answerDisplayLatex = latex` (ví dụ `\frac{3}{4}`, `-1.5`, `12`) | Student: `SubmitAttemptRequest.finalAnswer`<br>`SubmitAttemptRequest.answerDisplayLatex` | `finalAnswer` dùng cho deterministic grading bước 1 ở backend; `answerDisplayLatex` dùng để AI và Teacher xem đúng ký hiệu trực quan học sinh đã thấy. |
| `ShortAnswer` + `NumericRational` (Soạn đề) | `correctAnswer = serializedApiValue` (ví dụ `3/4` hoặc `\frac{3}{4}` hoặc `-0.5`) | `displayLatex = latex` dùng cho KaTeX preview | Teacher/Manager: `CreateQuestionRequest.correctAnswer`<br>`UpdateQuestionRequest.correctAnswer` | `serializedApiValue` là giá trị chuỗi thô được backend `MathAnswerNormalizer` chấp nhận (`backendAcceptedRawValue`). `CreateQuestionUseCase` / `UpdateQuestionUseCase` lưu đúng chuỗi raw này; **canonical value chỉ được backend sinh tại ranh giới chấm điểm và AI analysis**. |
| `ShortAnswer` + `Coordinate2D` | **`(${xPlain}; ${yPlain})`** | `\left(${xLatex};\, ${yLatex}\right)` | Student: `SubmitAttemptRequest.finalAnswer`<br>Authoring: `CreateQuestionRequest.correctAnswer` | **Khóa cứng dấu chấm phẩy `;` làm dấu phân cách** (ví dụ: `(1.5; 2.5)` hoặc `(1/2; -3)`). Tuyệt đối không dùng dấu phẩy `,` để tránh mơ hồ với dấu phẩy thập phân kiểu Việt Nam. |
| `ShortAnswer` + `Manual` | `finalAnswer = rawAnswerText`<br>`reasoningText = optionalReasoning` | Văn bản thô hoặc KaTeX nếu chứa `$..$` | Student: `SubmitAttemptRequest.finalAnswer`<br>`SubmitAttemptRequest.reasoningText`<br>Authoring: `CreateQuestionRequest.correctAnswer` | `finalAnswer` là câu trả lời chính; `reasoningText` là lập luận bổ sung (nếu có). |
| `Essay` + `Manual` | Student: **`finalAnswer = mainEssayText`**<br>`reasoningText = optionalNotes`<br>`answerDisplayLatex = ""` | Văn bản phong phú chứa công thức inline `$..$` render qua `RichMathText` | Student: `SubmitAttemptRequest.finalAnswer` (Bắt buộc)<br>`SubmitAttemptRequest.reasoningText` (Tùy chọn)<br>Authoring: `CreateQuestionRequest.correctAnswer` (Bắt buộc không được rỗng) | **Student**: `finalAnswer` LUÔN LÀ BÀI TỰ LUẬN CHÍNH (trường bắt buộc của API).<br>**Authoring**: `correctAnswer` là **ĐÁP ÁN MẪU / HƯỚNG DẪN CHẤM / RUBRIC CHÍNH (bắt buộc theo `QuestionActivationPolicy` và `CreateQuestionUseCase`, tuyệt đối KHÔNG được gửi `undefined`)**. Soạn thảo qua `ModeAwareAnswerEditor` (`questionType="Essay"`, `evaluationMode="Manual"`). |

---

## 3. Ma Trận Tương Thích `questionType × evaluationMode` (Compatibility Matrix)

Component nền `ModeAwareAnswerEditor` và helper `resolveAnswerInputType` phải tuân thủ nghiêm ngặt ma trận sau (khớp 100% với [QuestionActivationPolicy.cs](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/src/EduTwin.BLL/CurriculumAndQuestions/QuestionActivationPolicy.cs) của Backend):

| `QuestionType` | `EvaluationMode` | Trạng thái | Component kích hoạt | Hành vi khi vi phạm |
|---|---|---|---|---|
| `MultipleChoice` | `TextExact` | **Hợp lệ** | OptionSelector (card radio, không dùng AnswerEditor) | N/A |
| `MultipleChoice` | `NumericRational` | **Không hợp lệ** | N/A | **Fail closed**: Trả về `SafeErrorState` |
| `MultipleChoice` | `Coordinate2D` | **Không hợp lệ** | N/A | **Fail closed**: Trả về `SafeErrorState` |
| `MultipleChoice` | `Manual` | **Không hợp lệ** | N/A | **Fail closed**: Trả về `SafeErrorState` |
| `ShortAnswer` | `TextExact` | **Hợp lệ** | `PlainTextAnswerInput` | N/A |
| `ShortAnswer` | `NumericRational` | **Hợp lệ** | `NumericRationalMathInput` | N/A |
| `ShortAnswer` | `Coordinate2D` | **Hợp lệ** | `Coordinate2DInput` | N/A |
| `ShortAnswer` | `Manual` | **Hợp lệ** | `PlainOrMultilineAnswerInput` | N/A |
| `Essay` | `Manual` | **Hợp lệ** | `MultilineProseAnswerEditor` | N/A |
| `Essay` | `TextExact` | **Không hợp lệ** | N/A | **Fail closed**: Trả về `SafeErrorState` |
| `Essay` | `NumericRational` | **Không hợp lệ** | N/A | **Fail closed**: Trả về `SafeErrorState` |
| `Essay` | `Coordinate2D` | **Không hợp lệ** | N/A | **Fail closed**: Trả về `SafeErrorState` |

> **Nguyên tắc Fail-Closed**: Mọi tổ hợp ngoài ma trận hợp lệ trên phải chuyển sang `SafeErrorState` kèm thông báo cảnh báo rõ ràng; **tuyệt đối không âm thầm fallback sang plain text** để tránh sinh dữ liệu rác vào CSDL.
>
> **Hợp đồng Readonly MultipleChoice (Khóa Hợp Đồng b — Fail-Closed)**: `ModeAwareAnswerEditor` không chịu trách nhiệm hiển thị readonly cho câu hỏi trắc nghiệm `MultipleChoice`. Với MCQ, giá trị dữ liệu `rawText` là `optionId` (UUID nội bộ). Mọi màn hình xem lại bài làm (Gate 2A.5 và các màn hình review) tiếp tục sử dụng renderer phương án riêng biệt (`OptionSelector` / `AttemptFeedbackHierarchy`) hiển thị văn bản phương án và trạng thái đúng/sai. `ModeAwareAnswerEditor` khi nhận MCQ ở chế độ readonly sẽ ủy quyền và hiển thị thông báo khu vực trắc nghiệm chuyên biệt, tuyệt đối không bao giờ hiển thị chuỗi `optionId` ra giao diện.

---

## 4. Ranh Giới Phạm Vi: Core Phase 2A vs Expansion Gate (Scope Boundary)

Nhằm tránh mở rộng phạm vi quá lớn và tuyên bố "thống nhất toàn bộ" trong khi nhiều trường hỗn hợp vẫn dùng component cũ, Phase 2A được phân định ranh giới rõ ràng:

### 4.1 Phạm vi cốt lõi của Phase 2A (Core Phase 2A)
Tập trung 100% vào việc chuẩn hóa **nhập và hiển thị đáp án toán học**:
1. **Teacher Authoring**: Trường `correctAnswer` tại `/giao-vien/cau-hoi/tao-moi` và `/giao-vien/cau-hoi/:id`.
2. **CenterManager Authoring**: Trường `correctAnswer` tại `/quan-ly/cau-hoi/tao-moi` và `/quan-ly/cau-hoi/:id`.
3. **Student Answering**: Nhập đáp án ShortAnswer, NumericRational, Coordinate2D, Manual tại `/hoc-tap/luyen-tap` và `/hoc-tap/luyen-tap/:questionId`.
4. **Readonly Answer Display**: Hiển thị KaTeX cho đáp án đã nộp và đáp án chuẩn tại:
   - [StudentAssignmentDetailPage.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/StudentAssignmentDetailPage.tsx) (`/hoc-tap/bai-tap/:id`)
   - [AssignmentGradingWorkspace.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/reviews/AssignmentGradingWorkspace.tsx) (`/giao-vien/cham-bai`, `/quan-ly/duyet-bai`)
   - [AttemptFeedbackHierarchy.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/student/AttemptFeedbackHierarchy.tsx) (chi tiết phân tích bài làm).

### 4.2 Phạm vi mở rộng (Expansion Gate — Gate 2A.Extension)
Nội dung văn bản hỗn hợp (mixed-content) chứa công thức toán được quy hoạch triển khai theo lộ trình phân tầng:
- **Ngay trong Core Gates (2A.2, 2A.3, 2A.4)**: Xây dựng shared component [InlineMathComposer.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/math/InlineMathComposer.tsx) để thay thế nguyên tử `MathInputToolbar` ở từng actor: Gate 2A.2 cho CenterManager, Gate 2A.3 cho Teacher, Gate 2A.4 cho Student (`reasoningText`).
- **Phạm vi Gate 2A.Extension**:
  - Render KaTeX công thức toán trong bảng preview câu hỏi import CSV/Excel của [QuestionImportModal.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/teacher/QuestionImportModal.tsx).
  - Xóa bỏ hoàn toàn file [MathInputToolbar.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/math/MathInputToolbar.tsx) khỏi codebase sau khi lệnh `git grep MathInputToolbar` xác nhận không còn bất kỳ import nào.

---

## 5. Thiết Kế Kiến Trúc Component Chung (Component Architecture)

Bộ component nền dùng chung được đặt tại `web/edutwin-web/src/components/math/answer-editor/`:

```
ModeAwareAnswerEditor (forwardRef Orchestrator)
│
├── PlainTextAnswerInput         ── Dùng cho TextExact (văn bản thuần / từ khóa)
├── NumericRationalMathInput     ── MathLive thu gọn: số nguyên, phân số, số thập phân, số âm
├── Coordinate2DInput            ── UI nhập tọa độ 2D có cấu trúc (X, Y) -> xuất (${x}; ${y})
├── PlainOrMultilineAnswerInput  ── Nhập câu trả lời ngắn dạng Manual
├── MultilineProseAnswerEditor   ── Ô nhập bài tự luận chính (finalAnswer) cho Essay
├── MathFallbackTextarea         ── Textarea dự phòng tự động kích hoạt khi MathLive chunk lỗi
└── MathPreviewCore (Shared)     ── Lõi KaTeX render an toàn (trust: false, throwOnError: false)
    ├── TeacherMathFormulaPreview (Wrapper bọc theme var(--th-*))
    ├── CenterManagerMathPreview (Wrapper bọc theme var(--cm-*))
    └── StudentMathPreview (Wrapper bọc Tailwind Slate/Indigo)
```

### 5.1 Interface TypeScript & `forwardRef` Contract

Áp dụng chuẩn React `forwardRef` để hỗ trợ tương thích hoàn toàn với các công cụ phụ trợ (như máy tính Casio / SideAssistant):

```typescript
export type AnswerEditorProfile = "authoring" | "answering" | "readonly";

export type QuestionAnswerEvaluationMode =
  | "TextExact"
  | "NumericRational"
  | "Coordinate2D"
  | "Manual";

export type QuestionType = "MultipleChoice" | "ShortAnswer" | "Essay";

export interface AnswerEditorValue {
  /** Giá trị plain text hoặc format API chuẩn hóa (ví dụ "3/4" hoặc "(1/2; 3)") */
  rawText: string;
  /** Biểu diễn LaTeX dùng cho MathLive và KaTeX preview render (ví dụ "\\frac{3}{4}") */
  displayLatex: string;
}

export interface AnswerEditorRef {
  insertLatex: (latex: string) => void;
  focus: () => void;
  clear: () => void;
  getValue: () => AnswerEditorValue;
}

export interface ModeAwareAnswerEditorProps {
  profile: AnswerEditorProfile;
  questionType: QuestionType;
  evaluationMode: QuestionAnswerEvaluationMode;
  value: AnswerEditorValue;
  onChange?: (val: AnswerEditorValue) => void;
  disabled?: boolean;
  readOnly?: boolean;
  placeholder?: string;
  className?: string;
  autoFocus?: boolean;
  validationError?: string | null;
  ariaLabel?: string;
}

export const ModeAwareAnswerEditor = forwardRef<AnswerEditorRef, ModeAwareAnswerEditorProps>(
  (props, ref) => {
    // Implementation tuân thủ fail-closed matrix
  }
);
ModeAwareAnswerEditor.displayName = "ModeAwareAnswerEditor";
```

### 5.2 Các Quy Tắc Vòng Đời & Đồng Bộ Trạng Thái (Lifecycle Rules)

1. **Chống Feedback Loop (No Echo Emit)**:
   - Khi component nhận prop `value` mới từ cha (ví dụ chuyển câu hỏi), nạp giá trị vào MathLive/Input nội bộ với `silenceNotifications: true` và **tuyệt đối KHÔNG phát sự kiện `onChange` ngược lên cha**.
   - Sử dụng `shouldSyncExternalValue(propValue, currentValue, lastEmittedValue)` để phân biệt thay đổi từ bên ngoài với thay đổi do chính người dùng gõ.
2. **Bảo Toàn Dữ Liệu Khi Lỗi Chunk (Fallback Preservation)**:
   - Nếu chunk MathLive tải thất bại, hiển thị `MathFallbackTextarea`.
   - Mọi nội dung người dùng gõ vào fallback textarea được lưu giữ trong `latestValueRef`. Khi người dùng bấm *"Thử tải lại"* và MathLive nạp thành công, hàm `hydrateMathFieldInstance` sẽ tự động đổ lại dữ liệu vào MathLive mà không làm mất ký tự nào.
3. **Đồng Bộ Chính Xác `disabled` và `readOnly`**:
   - `disabled`: Vô hiệu hóa input, làm mờ, không nhận focus, không submit.
   - `readOnly`: Không cho sửa nhưng vẫn cho phép chọn/copy văn bản (`select-text`), hiển thị nhãn chỉ đọc rõ ràng.
4. **Tương Thích Ref với Máy Tính Casio**:
   - Phương thức `insertLatex(latex)` trên ref cho phép thanh công cụ Casio / SideAssistant chèn ký hiệu vào vị trí con trỏ hiện tại của ô nhập.

---

## 6. Trải Nghiệm Từng Chế Độ Đánh Giá

### 6.1 ShortAnswer + TextExact
- Sử dụng `PlainTextAnswerInput` (thẻ HTML `<input>` thông thường).
- Hiển thị nhãn chú thích: *"So khớp chính xác từng ký tự (không tự động quy đổi toán học)"*.
- Không khởi tạo MathLive để tránh hao tổn tài nguyên và không làm sai lệch văn bản thô.

### 6.2 ShortAnswer + NumericRational
- Sử dụng MathLive với bàn phím số/phân số được cấu hình gọn gàng (chỉ hỗ trợ số `0-9`, dấu âm `-`, dấu thập phân `.`, phân số `\frac{a}{b}`).
- Kèm KaTeX live preview bên dưới.
- Hiển thị gợi ý cú pháp thân thiện (Syntax Hint) nếu người dùng gõ ký tự chữ cái.

### 6.3 ShortAnswer + Coordinate2D
- **Giao diện 2 ô có cấu trúc `( X ; Y )`**:
  - Ô `X` và ô `Y` là hai ô nhập số/phân số độc lập.
  - Bọc trong cặp ngoặc đơn cố định với dấu chấm phẩy `;` ở giữa.
  - **Quy tắc Serialize**: Luôn tự động ghép thành chuỗi `(${xPlain}; ${yPlain})`.
  - **Quy tắc Deserialize**: Khi nhận chuỗi từ ngoài vào, hỗ trợ phân tích cả dạng `(x; y)` và `(x, y)` để tương thích với dữ liệu cũ, nhưng khi người dùng sửa sẽ luôn xuất ra dạng dấu chấm phẩy `;`.

### 6.4 ShortAnswer + Manual & Essay + Manual
- **ShortAnswer + Manual**: Ô nhập câu trả lời ngắn `finalAnswer`. Ô lập luận `reasoningText` tách biệt.
- **Essay + Manual**:
  - `finalAnswer`: Ô nhập bài tự luận chính (`MultilineProseAnswerEditor`).
  - `reasoningText`: Ô trình bày bước giải / lập luận bổ sung (nếu có).
  - Công thức toán học là phần tử inline `$công thức$` nằm ngay trong `finalAnswer`.

---

## 7. Khóa Quyết Định UX: Loại Bỏ Bảng Ký Hiệu Cũ, MathLive Là Công Cụ Nhập Toán Duy Nhất

Tuân thủ nghiêm ngặt định hướng UX đã được phê duyệt:
1. **Loại bỏ hoàn toàn "Bảng gõ ký hiệu Toán" cũ (`MathInputToolbar`)**:
   - Không nâng cấp, không duy trì và không mở rộng `MathInputToolbar` cho bất kỳ tác nhân nào.
   - Việc gỡ bỏ `MathInputToolbar` phải diễn ra **nguyên tử** cùng lúc với việc tích hợp công cụ thay thế `InlineMathComposer` ở từng tác nhân (Gate 2A.2 cho CenterManager, Gate 2A.3 cho Teacher, Gate 2A.4 cho Student). Tuyệt đối không để xảy ra trạng thái trung gian chỉ có textarea trần khiến người dùng mất công cụ chèn công thức.
   - File component `MathInputToolbar.tsx` chỉ bị xóa khỏi codebase tại Gate 2A.Extension sau khi `git grep MathInputToolbar` xác nhận không còn bất kỳ file nào import.
2. **MathLive Virtual Keyboard là công cụ nhập toán chính**:
   - Với các trường nhập toán độc lập (`NumericRational`, `Coordinate2D`): MathLive cung cấp bàn phím ảo (virtual keyboard) và tương tác trực quan với các placeholder `[?]`.
3. **Soạn thảo văn bản trộn công thức (Mixed-Content Prose)**:
   - Với các trường dạng văn bản phong phú (`questionText`, `options`, `solution`, `reasoningText`): sử dụng shared component [InlineMathComposer.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/math/InlineMathComposer.tsx) (popover / modal MathLive kèm live KaTeX preview) để chèn công thức toán hoàn chỉnh `$..$` vào đúng vị trí con trỏ của văn bản; tuyệt đối không dùng thanh công cụ ký hiệu Unicode chắp vá.

---

## 8. Thống Nhất KaTeX Preview: Shared Core + Themed Wrappers

Áp dụng mô hình **Shared Core + Themed Wrappers**:

1. **Shared Core (`MathPreviewCore.tsx`)**:
   - Sử dụng chung hàm tiện ích gọi `katex.renderToString` với cấu hình an toàn tuyệt đối: `trust: false`, `throwOnError: false`, `output: "htmlAndMathml"`.
   - Hỗ trợ cả công thức đơn lẻ (`displayMode: true/false`) lẫn văn bản trộn công thức thông qua lõi của [RichMathText.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/math/RichMathText.tsx).
2. **Themed Wrappers**:
   - **Implementation thực tế của Teacher Preview nằm tại [src/components/teacher/MathFormulaPreview.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/teacher/MathFormulaPreview.tsx)**: Refactor file này để wrap `MathPreviewCore` với theme token `var(--th-*)`.
   - File [TeacherMathFormulaPreview.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/teacher/TeacherMathFormulaPreview.tsx) **chỉ là re-export compatibility file** (`export * from "./MathFormulaPreview"`), giữ nguyên không chỉnh sửa để tránh làm gãy các import cũ.
   - [src/components/math/MathFormulaPreview.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/math/MathFormulaPreview.tsx): Wrap `MathPreviewCore` với theme CenterManager (`var(--cm-*)`) và Student (`slate / indigo`).

---

## 9. Nguyên Tắc Xác Thực: Không Sao Chép Canonicalizer Sang Frontend

1. **Backend là Single Source of Truth**:
   - Mọi quy tắc chuẩn hóa toán học, rút gọn phân số, biến đổi tương đương, so khớp tọa độ thuộc thẩm quyền duy nhất của backend ([MathAnswerNormalizer.cs](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/src/EduTwin.BLL/AssessmentAndReasoning/PreliminaryGrading/MathAnswerNormalizer.cs), [CoordinateAnswerNormalizer.cs](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/src/EduTwin.BLL/AssessmentAndReasoning/PreliminaryGrading/CoordinateAnswerNormalizer.cs), [ShortAnswerGrader.cs](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/src/EduTwin.BLL/AssessmentAndReasoning/PreliminaryGrading/ShortAnswerGrader.cs)).
   - Frontend **tuyệt đối không viết lại bộ parser / canonicalizer thứ hai** để tránh nguy cơ phân kỳ logic giữa client và server.
2. **Vai trò của Frontend Validation trong Phase 2A**:
   - Chỉ đóng vai trò **gợi ý cú pháp không mang tính quyết định (Non-authoritative syntax hints)**:
     - Cảnh báo trường bắt buộc bị rỗng.
     - Cảnh báo nếu `NumericRational` chứa ký tự chữ cái không hợp lệ.
     - Cảnh báo nếu `Coordinate2D` chưa điền đủ cả 2 tọa độ X và Y.
   - Luôn hiển thị KaTeX preview trực quan để người dùng kiểm chứng bằng mắt.
   - Nếu tương lai cần "server canonical preview 100%", hệ thống sẽ đề xuất một API endpoint riêng biệt ở Phase 2B / 2C.

---

## 10. Chiến Lược Kiểm Thử (Test Plan Phù Hợp Hạ Tầng Hiện Tại)

Hạ tầng kiểm thử frontend hiện tại chạy qua lệnh:
```bash
npm --prefix web/edutwin-web run test  # Chạy node --test tests/*.test.ts
```
Hệ thống **không có jsdom, React Testing Library hay Vitest**. Do đó, chiến lược kiểm thử được phân định rành mạch:

### 10.1 Unit Tests (Node.js Test Runner — Không phụ thuộc DOM)
Tạo mới file `web/edutwin-web/tests/modeAwareAnswerEditorHelpers.test.ts` kiểm thử các pure functions:
1. **Kiểm thử Ma Trận Tương Thích (`questionType × evaluationMode`)**:
   - Ánh xạ đúng 6 tổ hợp hợp lệ sang component type tương ứng.
   - **Bảo đảm Fail-Closed**: Các tổ hợp không hợp lệ (`MultipleChoice + NumericRational`, `Essay + Coordinate2D`, v.v.) trả về trạng thái lỗi `SafeErrorState` hoặc throw lỗi kiểm soát, tuyệt đối không âm thầm fallback sang plain text.
2. **Kiểm thử Serialization Coordinate2D**:
   - `serializeCoordinate(x, y)`: Luôn sinh ra đúng định dạng `(${x}; ${y})` (dùng `;`), xử lý số âm, phân số, khoảng trắng.
   - `deserializeCoordinate(str)`: Parse thành công cả chuỗi dùng `;` và chuỗi cũ dùng `,`.
3. **Kiểm thử Tách Biệt Dữ Liệu Manual / Essay**:
   - Đảm bảo `finalAnswer` luôn lưu giữ nội dung tự luận / đáp án chính (không bao giờ để trống khi đã nhập).
   - Đảm bảo `reasoningText` được lưu tách biệt, không bị gộp đè vào `finalAnswer`.
4. **Kiểm thử Lựa Chọn Trình Bày Readonly**:
   - Chọn đúng `displayLatex` khi có công thức (`NumericRational`, `Coordinate2D`).
   - Chọn đúng `rawText` khi ở chế độ `TextExact` hoặc văn bản thuần.
5. **Kiểm thử Chống Feedback Loop**:
   - `shouldSyncExternalValue(propVal, currentVal, lastEmittedVal)` ngăn chặn việc re-emit ngược lên cha.

### 10.2 Browser / E2E Verification (Kiểm thử trên trình duyệt thực tế)
- MathLive web component mounting và Shadow DOM input mode toggle.
- Ngăn chặn phím mũi tên và Tab nhảy ra khỏi ô nhập toán trong quiz.
- Kích hoạt fallback textarea khi bundle MathLive lỗi và khả năng nạp lại (`hydrateMathFieldInstance`).
- Kiểm tra trực quan trên các tuyến đường thực tế: `/hoc-tap/luyen-tap`, `/quan-ly/cau-hoi/tao-moi`, `/giao-vien/cau-hoi/tao-moi`.

### 10.3 Regression Tests
- Kiểm tra toàn bộ test suite backend: `dotnet test tests/EduTwin.BLL.Tests/EduTwin.BLL.Tests.csproj --filter "FullyQualifiedName~CurriculumAndQuestions"`.
- Kiểm tra toàn diện frontend: `npm --prefix web/edutwin-web run verify` (ESLint 0 errors, unit tests pass, bundle budget pass).

---

## 11. Kế Hoạch Triển Khai Theo Từng Gate Nhỏ (Phased Checkpoints)

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  Gate 2A.1   │ ──> │  Gate 2A.2   │ ──> │  Gate 2A.3   │ ──> │  Gate 2A.4   │ ──> │  Gate 2A.5   │
│ Shared Core  │     │ Center Mgr   │     │   Teacher    │     │   Student    │     │ Readonly/E2E │
└──────────────┘     └──────────────┘     └──────────────┘     └──────────────┘     └──────────────┘
                                                                                           │
                                                                                           ▼
                                                                                    ┌──────────────┐
                                                                                    │ Gate 2A.Ext  │
                                                                                    │ MixedContent │
                                                                                    └──────────────┘
```

1. **Gate 2A.1 — Shared Component Foundation & Helper Unit Tests** (✅ HOÀN THÀNH):
   - Xây dựng thư mục `components/math/answer-editor/` với `ModeAwareAnswerEditor` (`forwardRef`), `Coordinate2DInput` (hai `VisualMathField`), `NumericRationalMathInput` (`latestValueRef`), `PlainTextAnswerInput`, `PlainOrMultilineAnswerInput`, `MultilineProseAnswerEditor`, `MathFallbackTextarea`.
   - Cài đặt Shared Preview Core riêng biệt `MathPreviewCore.tsx` cùng hai themed wrappers `components/math/MathFormulaPreview.tsx` và `components/teacher/MathFormulaPreview.tsx`, bảo toàn compatibility re-export tại `components/teacher/TeacherMathFormulaPreview.tsx`. Tách `renderSafeKatex` sang `mathPreviewUtils.ts` để giữ Fast Refresh sạch sẽ.
   - Cài đặt fail-closed compatibility matrix (6 tổ hợp hợp lệ, mọi tổ hợp còn lại trả về `SafeErrorState`, không fallback plain text; MultipleChoice readonly hiển thị delegation notice không bao giờ lộ raw optionId).
   - Hoàn thành unit test suite `tests/modeAwareAnswerEditorHelpers.test.ts` (13 subtests) trên hạ tầng `node:test`.
   - *Bằng chứng kiểm chứng Gate 2A.1*:
     - `npm test`: 284/284 tests pass (bao gồm 13 tests mới cho Gate 2A.1).
     - `npm run lint`: 0 errors, 30 warnings (giảm 1 warning so với baseline 31; không có warning mới nào trong code Gate 2A.1).
     - `npm run build`: Hoàn thành thành công (tsc -b && vite build).
     - `npm run test:bundle`: Pass (bundle budget kiểm chứng an toàn).
     - `git show --check`: Commit `6b11f52` từng tồn tại các cảnh báo trailing whitespace trong file kế hoạch (dòng 3–5, 118–121); toàn bộ đã được khắc phục triệt để bằng forward corrective commit `9d0d5e4` (không amend/rebase do commit trước đã push).
2. **Gate 2A.2 — CenterManager Authoring Integration** (⏳ CHƯA THỰC HIỆN):
   - **Mục tiêu**: Tích hợp `ModeAwareAnswerEditor` cho trường `correctAnswer` và thay thế nguyên tử `MathInputToolbar` bằng shared `InlineMathComposer` tại giao diện soạn câu hỏi CenterManager ([QuestionEditorPage.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/QuestionEditorPage.tsx)).
   - **Xử lý Essay + Manual bắt buộc có correctAnswer**:
     - Tuân thủ nghiêm ngặt `QuestionActivationPolicy` và `CreateQuestionUseCase`: câu hỏi `Essay` bắt buộc phải có `correctAnswer` không được để trống (đóng vai trò đáp án mẫu / hướng dẫn chấm / rubric chính).
     - Tuyệt đối không gửi `correctAnswer: undefined` khi tạo hoặc cập nhật câu hỏi tự luận; `QuestionEditorPage.tsx` hiển thị `ModeAwareAnswerEditor` (`questionType="Essay"`, `evaluationMode="Manual"`) để người soạn nhập đáp án mẫu trước khi kích hoạt.
   - **Đồng bộ Multiple Choice correctAnswer**:
     - Backend yêu cầu câu hỏi `MultipleChoice`: (1) phải có đúng một option `isCorrect === true`, và (2) `correctAnswer` không được rỗng.
     - `QuestionEditorPage.tsx` tự động đồng bộ `correctAnswer` từ nhãn `optionLabel` (ví dụ `"A"`, `"B"`, `"C"`, `"D"`) của phương án được đánh dấu `isCorrect: true`, đảm bảo cả hai điều kiện backend luôn thỏa mãn.
   - **Thay thế nguyên tử "Bảng gõ ký hiệu Toán" bằng Shared `InlineMathComposer`**:
     - Xây dựng component dùng chung [InlineMathComposer.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/math/InlineMathComposer.tsx) (popover / modal MathLive kèm KaTeX live preview). Khi người soạn nhấn nút *"Chèn công thức"* trên các trường văn bản hỗn hợp (`questionText`, `options[].optionText`, `solution`), công thức toán `$..$` sẽ được chèn chuẩn xác vào vị trí con trỏ (cursor position) của textarea/input.
     - Việc gỡ bỏ component `MathInputToolbar` cũ và tích hợp `InlineMathComposer` trên `QuestionEditorPage.tsx` phải diễn ra **nguyên tử trong cùng một commit**, tuyệt đối không tạo hồi quy UX (không chấp nhận trạng thái trung gian chỉ có textarea và KaTeX preview).
     - Không xóa file `MathInputToolbar.tsx` trong Gate 2A.2 vì `TeacherQuestionEditorView.tsx` vẫn đang dùng tạm thời cho đến Gate 2A.3.
   - **Bảo toàn bản nháp theo cặp `questionType:evaluationMode` (No Cross-Contamination Draft Store)**:
     - Khóa bản nháp theo cả `questionType` và `evaluationMode` (`AnswerDraftKey`):
       ```typescript
       export type AnswerDraftKey =
         | "ShortAnswer:TextExact"
         | "ShortAnswer:NumericRational"
         | "ShortAnswer:Coordinate2D"
         | "ShortAnswer:Manual"
         | "Essay:Manual";
       ```
     - Quản lý state `modeDrafts: Partial<Record<AnswerDraftKey, AnswerEditorValue>>`.
     - Phân định rạch ròi giữa `ShortAnswer:Manual` (câu trả lời ngắn) và `Essay:Manual` (đáp án mẫu / rubric tự luận dài). Khi người soạn chuyển đổi qua lại giữa ShortAnswer và Essay, hệ thống bảo toàn và khôi phục đúng bản nháp tương ứng, tuyệt đối không làm xuất hiện nhầm câu trả lời ngắn vào ô rubric tự luận hoặc ngược lại.
   - **Triết lý Thẩm Quyền và Dữ Liệu**:
     - Validation client chỉ đóng vai trò hỗ trợ UX (syntax hints non-blocking); backend `MathAnswerNormalizer` / `CoordinateAnswerNormalizer` và `ProblemDetails` là nguồn xác thực duy nhất.
     - Không tự xây dựng canonicalizer thứ hai bằng TypeScript trên frontend để tự động chuyển `3/4` thành `\frac{3}{4}`. Trường `displayLatex` chỉ dùng cho render trình bày MathLive / KaTeX.
   - **Hạ tầng kiểm thử Gate 2A.2**:
     - Xây dựng module pure helper `centerManagerQuestionEditorHelpers.ts` và unit test suite `tests/centerManagerQuestionEditorIntegration.test.ts` (trên hạ tầng `node:test`) kiểm tra:
       * Draft key resolution và bảo toàn/khôi phục draft theo từng cặp `questionType:evaluationMode`.
       * **Test case chuyên biệt chuyển `ShortAnswer:Manual ↔ Essay:Manual`** bảo đảm không trộn câu trả lời ngắn với rubric tự luận.
       * Tạo payload Create/Update (đặc biệt: Essay có `correctAnswer` hợp lệ, không undefined).
       * Tự động đồng bộ `correctAnswer` cho MultipleChoice từ option có `isCorrect = true`.
       * Chèn công thức inline vào chuỗi văn bản tại vị trí con trỏ (selection insertion).
       * Hydration và serialize `correctAnswer` sang `AnswerEditorValue`.
   - **Ranh giới tác nhân (Actor Isolation)**:
     - Chỉ thay đổi bề mặt CenterManager ([QuestionEditorPage.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/QuestionEditorPage.tsx)) và tạo mới shared component [InlineMathComposer.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/math/InlineMathComposer.tsx); không tác động đến Teacher ([TeacherQuestionEditorView.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/teacher/TeacherQuestionEditorView.tsx)) hay Student ([LearningPlayerPage.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/LearningPlayerPage.tsx)).
   - **Tiêu chí nghiệm thu Gate 2A.2**:
     - CenterManager soạn thảo mượt mà cả 4 chế độ ShortAnswer (`TextExact`, `NumericRational`, `Coordinate2D`, `Manual`), `Essay` hiển thị ô soạn đáp án mẫu/rubric và lưu `correctAnswer` không rỗng; `MultipleChoice` tự động đồng bộ `correctAnswer` từ option đúng.
     - `MathInputToolbar` được thay thế nguyên tử bằng `InlineMathComposer` cho các trường `questionText`, `options`, `solution`.
     - Chuyển đổi giữa các chế độ và giữa `ShortAnswer:Manual ↔ Essay:Manual` bảo toàn 100% bản nháp độc lập, không trộn dữ liệu.
     - 100% test suites (cũ và mới) pass, build production sạch sẽ.
3. **Gate 2A.3 — Teacher Authoring Integration** (⏳ CHƯA THỰC HIỆN):
   - Tích hợp `ModeAwareAnswerEditor` cho trường `correctAnswer` tại `/giao-vien/cau-hoi/tao-moi` và `/giao-vien/cau-hoi/:id` ([TeacherQuestionEditorView.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/teacher/TeacherQuestionEditorView.tsx)).
   - Thay thế nguyên tử `MathInputToolbar` bằng shared `InlineMathComposer` cho các trường `questionText`, `options`, `solution` của giáo viên.
   - Chấm dứt tình trạng gõ text mù, hiển thị KaTeX preview theo theme `th-*`.
   - *Tiêu chí nghiệm thu*: Giao diện soạn câu hỏi của giáo viên đồng bộ hoàn toàn với CenterManager về mặt dữ liệu và công cụ soạn thảo.
4. **Gate 2A.4 — Student Answering Integration** (⏳ CHƯA THỰC HIỆN):
   - Tích hợp `ModeAwareAnswerEditor` vào [LearningPlayerPage.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/LearningPlayerPage.tsx) (`/hoc-tap/luyen-tap`).
   - Tích hợp `InlineMathComposer` cho ô nhập lập luận / bước giải (`reasoningText`).
   - Đảm bảo ref kết nối hoàn hảo với máy tính Casio / SideAssistant.
   - *Tiêu chí nghiệm thu*: Học sinh làm bài, chuyển câu hỏi, lưu draft localStorage và nộp bài chuẩn xác với `finalAnswer` luôn có giá trị.
5. **Gate 2A.5 — Readonly Answer Display, Accessibility & E2E Verification** (⏳ CHƯA THỰC HIỆN):
   - Tích hợp hiển thị đáp án readonly tại:
     - [StudentAssignmentDetailPage.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/StudentAssignmentDetailPage.tsx)
     - [AssignmentGradingWorkspace.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/reviews/AssignmentGradingWorkspace.tsx)
     - [AttemptFeedbackHierarchy.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/student/AttemptFeedbackHierarchy.tsx)
   - Kiểm tra a11y, theme light/dark, chạy full regression test và bundle budget check.
   - *Tiêu chí nghiệm thu*: `npm run verify` pass hoàn toàn.
6. **Gate 2A.Extension — Mixed-Content Import Preview & Cleanup (Gate Mở Rộng — ⏳ CHƯA THỰC HIỆN)**:
   - Render công thức toán trong bảng preview của [QuestionImportModal.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/teacher/QuestionImportModal.tsx).
   - Chạy lệnh `git grep MathInputToolbar` xác nhận không còn bất kỳ import nào trong codebase, sau đó xóa bỏ hoàn toàn file [MathInputToolbar.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/components/math/MathInputToolbar.tsx).

---

## 12. Ma Trận Tác Động File Chi Tiết (File Impact Matrix)

| File | Phân loại Gate | Hành động | Mục đích thay đổi | Nguy cơ tiềm ẩn | Biện pháp kiểm soát |
|---|---|---|---|---|---|
| `web/edutwin-web/src/components/math/answer-editor/*` | **Core 2A.1** | **Tạo mới** | Bộ component nền `ModeAwareAnswerEditor`, `Coordinate2DInput`, `NumericRationalMathInput`, `PlainTextAnswerInput`, `MathFallbackTextarea` | Lỗi bundle hoặc lặp lại code | Tách file module rõ ràng, pure helpers độc lập |
| `web/edutwin-web/src/components/math/mathPreviewUtils.ts` | **Core 2A.1** | **Tạo mới** | Tách hàm thuần `renderSafeKatex` để giữ Fast Refresh sạch 0 warning | N/A | Pure utility module |
| `web/edutwin-web/tests/modeAwareAnswerEditorHelpers.test.ts` | **Core 2A.1** | **Tạo mới** | Unit tests cho pure helpers (mapping, compatibility matrix fail-closed, serialization, parsing, sync) bằng `node:test` | Không tương thích test runner | Chỉ dùng thư viện có sẵn `node:test` và `node:assert/strict` |
| `web/edutwin-web/src/components/math/MathFormulaPreview.tsx` | **Core 2A.1** | **Sửa đổi** | Đóng vai trò Shared Preview Core với KaTeX an toàn | Lỗi CSS theme | Chấp nhận prop `className` tùy biến theo theme |
| `web/edutwin-web/src/components/teacher/MathFormulaPreview.tsx` | **Core 2A.1** | **Sửa đổi** | **File implementation thực tế của Teacher Preview**: wrap Shared Core với theme `th-*` | Gãy UI giáo viên | Giữ trọn interface `TeacherMathFormulaPreviewProps` |
| `web/edutwin-web/src/components/teacher/TeacherMathFormulaPreview.tsx` | **Core 2A.1** | **Giữ nguyên** | **Re-export compatibility file** (`export * from "./MathFormulaPreview"`) | Gãy import cũ nếu sửa | Không sửa đổi nội dung file proxy này |
| `web/edutwin-web/src/components/math/InlineMathComposer.tsx` | **Core 2A.2** | **Tạo mới** | Shared component popover/modal MathLive để chèn công thức `$..$` vào vị trí con trỏ của textarea/input | Focus và cursor selection | Dùng selectionStart/selectionEnd chuẩn và ref |
| `web/edutwin-web/src/pages/QuestionEditorPage.tsx` | **Core 2A.2** | **Sửa đổi** | Tích hợp `ModeAwareAnswerEditor` cho `correctAnswer`, thay thế nguyên tử `MathInputToolbar` bằng `InlineMathComposer`, bảo toàn draft theo `questionType:evaluationMode` | Gãy submit CenterManager | Giữ nguyên API contract `CreateQuestionRequest` |
| `web/edutwin-web/src/pages/centerManagerQuestionEditorHelpers.ts` | **Core 2A.2** | **Tạo mới** | Pure helpers draft store (`AnswerDraftKey`), đồng bộ correctAnswer MCQ, chèn cursor formula và tạo payload Create/Update | Lỗi logic payload | Pure functions không phụ thuộc DOM |
| `web/edutwin-web/tests/centerManagerQuestionEditorIntegration.test.ts` | **Core 2A.2** | **Tạo mới** | Unit tests cho pure helpers và integration logic của Gate 2A.2 (đặc biệt test chuyển ShortAnswer:Manual ↔ Essay:Manual) trên nền `node:test` | N/A | Dùng `node:test` và `node:assert/strict` |
| `web/edutwin-web/src/pages/teacher/TeacherQuestionEditorView.tsx` | **Core 2A.3** | **Sửa đổi** | Tích hợp `ModeAwareAnswerEditor` cho `correctAnswer` của Teacher và thay thế `MathInputToolbar` bằng `InlineMathComposer` | Gãy submit Teacher | Giữ nguyên API contract và theme token `th-*` |
| `web/edutwin-web/src/pages/LearningPlayerPage.tsx` | **Core 2A.4** | **Sửa đổi** | Thay thế `VisualMathField` bằng `ModeAwareAnswerEditor`, tích hợp `InlineMathComposer` cho `reasoningText`, đảm bảo `finalAnswer` luôn có giá trị cho Essay | Mất tương thích Casio / draft | Giữ nguyên `AnswerEditorRef` và `persistCurrentAnswer` |
| `web/edutwin-web/src/pages/StudentAssignmentDetailPage.tsx` | **Core 2A.5** | **Sửa đổi** | Hiển thị đáp án readonly đã nộp bằng `ModeAwareAnswerEditor` (`profile="readonly"`) | Lệch định dạng hiển thị | Dùng đúng displayLatex theo evaluationMode |
| `web/edutwin-web/src/components/reviews/AssignmentGradingWorkspace.tsx` | **Core 2A.5** | **Sửa đổi** | Hiển thị đáp án học sinh và đáp án chuẩn readonly trong workspace chấm bài | Lệch format chấm bài | Bảo toàn logic override điểm của giáo viên |
| `web/edutwin-web/src/components/student/AttemptFeedbackHierarchy.tsx` | **Core 2A.5** | **Sửa đổi** | Hiển thị đáp án học sinh và lời giải trong cây phân tích phản hồi bài làm | Render KaTeX lỗi | Dùng shared KaTeX core với fallback |
| `web/edutwin-web/src/components/math/MathInputToolbar.tsx` | **Gate 2A.Ext** | **Xóa bỏ** | Loại bỏ hoàn toàn file bảng ký hiệu cũ sau khi `git grep` xác nhận không còn bất kỳ import nào | N/A | Chỉ xóa khi không còn file nào import |
| `web/edutwin-web/src/components/math/RichMathText.tsx` | **Gate 2A.Ext** | **Sửa đổi** | Tối ưu hóa render văn bản kết hợp công thức toán inline | Hiệu năng render | Memo hóa kết quả render KaTeX |
| `web/edutwin-web/src/components/teacher/QuestionImportModal.tsx` | **Gate 2A.Ext** | **Sửa đổi** | Render KaTeX công thức toán trong bảng preview câu hỏi import CSV/Excel | Giảm tốc độ bảng preview | Áp dụng memo hóa cho từng hàng bảng |

---

## 13. Tóm Tắt Các Quyết Định Đã Khóa (Finalized Decisions)

1. **Hợp đồng Manual / Essay**:
   - `SubmitAttemptRequest.finalAnswer` luôn là đáp án chính và là trường bắt buộc của API.
   - `reasoningText` chỉ là phần lập luận/bước giải bổ sung.
   - Với `Essay + Manual`: `finalAnswer` chứa toàn bộ bài tự luận chính (bao gồm công thức inline `$..$`), `answerDisplayLatex` để trống.
   - **Với Authoring (Soạn đề)**: `correctAnswer` của `Essay + Manual` bắt buộc không được để trống (theo `QuestionActivationPolicy` và `CreateQuestionUseCase`). Đây là đáp án mẫu / rubric / hướng dẫn chấm tự luận. Tuyệt đối không gửi `undefined`. Soạn thảo qua `ModeAwareAnswerEditor` (`questionType="Essay"`, `evaluationMode="Manual"`).
2. **Đồng bộ MultipleChoice Soạn Đề**:
   - `correctAnswer` của `MultipleChoice` tự động đồng bộ từ nhãn (`optionLabel`) của phương án có `isCorrect = true`, bảo đảm đồng thời cả hai điều kiện của backend (đúng 1 option đúng và `correctAnswer` không được rỗng).
3. **Ma trận tương thích Fail-Closed**:
   - Chỉ cho phép 6 tổ hợp hợp lệ theo `QuestionActivationPolicy`. Mọi tổ hợp sai lệch lập tức chuyển sang `SafeErrorState`, không âm thầm fallback sang plain text.
4. **Thuật ngữ Dữ liệu Soạn đề**:
   - Giá trị gửi lên API của `correctAnswer` được gọi là `backendAcceptedRawValue` hoặc `serializedApiValue`.
   - `CreateQuestionUseCase` / `UpdateQuestionUseCase` lưu chuỗi raw này; backend chỉ sinh canonical value tại ranh giới grading và AI analysis.
5. **Định dạng Coordinate2D**:
   - Khóa cứng serialization là **`(${xPlain}; ${yPlain})`** (bắt buộc dấu chấm phẩy `;`).
6. **Hạ tầng kiểm thử**:
   - Giữ nguyên `node:test`, kiểm thử pure helpers bằng unit test và kiểm thử hành vi DOM bằng trình duyệt thực tế / E2E, không cài thêm testing framework mới.
7. **Thẩm quyền chuẩn hóa**:
   - Backend là Single Source of Truth duy nhất. Client validation chỉ là gợi ý cú pháp không mang tính quyết định. Không tạo canonicalizer TypeScript thứ hai trên frontend.
8. **Ranh giới phạm vi**:
   - Khóa chặt Core Phase 2A vào `correctAnswer` (Teacher/Manager), Student answer input và Readonly answer display. Mixed-content authoring được quy hoạch thành Gate 2A.Extension rõ ràng.
9. **Component Ref**:
   - Dùng `forwardRef<AnswerEditorRef, ModeAwareAnswerEditorProps>` với quy tắc chống feedback loop nghiêm ngặt.
10. **Preview KaTeX**:
    - Sửa đổi trực tiếp tại `src/components/teacher/MathFormulaPreview.tsx` (implementation thực tế), giữ nguyên file proxy `TeacherMathFormulaPreview.tsx`.
11. **Khóa Quyết Định Toolbar / Composer**:
    - Loại bỏ hoàn toàn bảng ký hiệu cũ (`MathInputToolbar`) theo lộ trình nguyên tử: Gate 2A.2 thay bằng shared `InlineMathComposer` cho CenterManager, Gate 2A.3 cho Teacher, Gate 2A.4 cho Student. MathLive virtual keyboard là công cụ nhập toán độc lập duy nhất; văn bản trộn công thức sử dụng `InlineMathComposer`. File `MathInputToolbar.tsx` chỉ bị xóa ở Gate Extension sau khi `git grep MathInputToolbar` xác nhận sạch 100%.
12. **Bảo Toàn Bản Nháp Khóa Theo `questionType:evaluationMode` (No Auto-Wipe, No Cross-Contamination)**:
    - Bản nháp được lưu theo khóa `AnswerDraftKey` (`ShortAnswer:TextExact`, `ShortAnswer:NumericRational`, `ShortAnswer:Coordinate2D`, `ShortAnswer:Manual`, `Essay:Manual`). Khi chuyển đổi qua lại giữa các chế độ (đặc biệt `ShortAnswer:Manual ↔ Essay:Manual`), bản nháp được bảo toàn độc lập, không tự ý xóa trắng và không để câu trả lời ngắn xuất hiện nhầm trong rubric tự luận hay ngược lại.

---

*Tài liệu hiệu chỉnh v3.4 hoàn tất. Đã giải quyết triệt để vấn đề nguyên tử thay thế toolbar, phân tách draft store câu hỏi tự luận/ngắn, ghi nhận lịch sử commit và hoàn thiện kế hoạch Gate 2A.2. Dừng lại chờ Codex & Tech Lead nghiệm thu kế hoạch trước khi bắt đầu triển khai code Gate 2A.2.*
