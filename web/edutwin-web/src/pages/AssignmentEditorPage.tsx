import { useEffect, useMemo, useState } from "react";
import { isAxiosError } from "axios";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { useAssignment } from "../features/assignments/useAssignment";
import { useCloseAssignment } from "../features/assignments/useCloseAssignment";
import { useCreateAssignment } from "../features/assignments/useCreateAssignment";
import { usePublishAssignment } from "../features/assignments/usePublishAssignment";
import { useUpdateAssignment } from "../features/assignments/useUpdateAssignment";
import {
  useAssignableQuestions,
  useAssignmentClasses,
  useAssignmentClassStudents,
} from "../features/assignments/useAssignmentWizardOptions";
import type { CreateAssignmentRequest, TargetMode, UpdateAssignmentRequest } from "../types/assignments";

const wizardSteps = ["Lớp học", "Câu hỏi", "Đối tượng", "Xem lại"] as const;

const getErrorMessage = (error: unknown, fallback: string) => {
  if (isAxiosError(error)) {
    return error.response?.data?.detail || error.message || fallback;
  }
  return error instanceof Error ? error.message : fallback;
};

const toLocalDateTime = (value: string | null) => {
  if (!value) return "";
  const date = new Date(value);
  const timezoneOffset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - timezoneOffset).toISOString().slice(0, 16);
};

export const AssignmentEditorPage = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const isEditing = !!id;

  const assignmentQuery = useAssignment(id);
  const createMutation = useCreateAssignment();
  const updateMutation = useUpdateAssignment();
  const publishMutation = usePublishAssignment();
  const closeMutation = useCloseAssignment();

  const [step, setStep] = useState(0);
  const [classId, setClassId] = useState(() => isEditing ? "" : searchParams.get("classId") || "");
  const [title, setTitle] = useState("");
  const [instructions, setInstructions] = useState("");
  const [dueAt, setDueAt] = useState("");
  const [minimumDueAt] = useState(() => toLocalDateTime(new Date(Date.now() + 60_000).toISOString()));
  const initialStudentIds = isEditing
    ? []
    : (searchParams.get("studentIds") || "").split(",").map((value) => value.trim()).filter(Boolean);
  const [targetMode, setTargetMode] = useState<TargetMode>(initialStudentIds.length > 0 ? "SelectedStudents" : "WholeClass");
  const [questionIds, setQuestionIds] = useState<string[]>([]);
  const [studentIds, setStudentIds] = useState<string[]>(initialStudentIds);
  const [errorMessage, setErrorMessage] = useState("");

  const assignment = assignmentQuery.data?.data;
  const isDraft = assignment?.status === "Draft";
  const isPublished = assignment?.status === "Published";
  const canEdit = !isEditing || isDraft;

  const classesQuery = useAssignmentClasses();
  const selectedClass = useMemo(
    () => classesQuery.data?.data.find((item) => item.classId === classId),
    [classId, classesQuery.data?.data],
  );
  const questionsQuery = useAssignableQuestions(selectedClass?.subject.subjectId);
  const studentsQuery = useAssignmentClassStudents(classId || undefined);

  useEffect(() => {
    if (!assignment) return;

    setClassId(assignment.classId);
    setTitle(assignment.title);
    setInstructions(assignment.instructions || "");
    setDueAt(toLocalDateTime(assignment.dueAt));
    setQuestionIds(assignment.questions.map((question) => question.questionId));

    const source = assignment.targets[0]?.targetSource;
    setTargetMode(source === "WholeClass" || !source ? "WholeClass" : "SelectedStudents");
    setStudentIds(source && source !== "WholeClass" ? assignment.targets.map((target) => target.studentId) : []);
  }, [assignment]);

  const toggleQuestion = (questionId: string) => {
    setQuestionIds((current) =>
      current.includes(questionId)
        ? current.filter((item) => item !== questionId)
        : [...current, questionId],
    );
  };

  const toggleStudent = (studentId: string) => {
    setStudentIds((current) =>
      current.includes(studentId)
        ? current.filter((item) => item !== studentId)
        : [...current, studentId],
    );
  };

  const handleClassChange = (nextClassId: string) => {
    setClassId(nextClassId);
    setQuestionIds([]);
    setStudentIds([]);
    setTargetMode("WholeClass");
    setErrorMessage("");
  };

  const validateStep = (targetStep: number) => {
    if (targetStep === 0 && !classId) return "Vui lòng chọn lớp học.";
    if (targetStep === 1 && questionIds.length === 0) return "Vui lòng chọn ít nhất một câu hỏi.";
    if (targetStep === 2 && targetMode === "SelectedStudents" && studentIds.length === 0) {
      return "Vui lòng chọn ít nhất một học sinh.";
    }
    if (targetStep === 3 && !title.trim()) return "Vui lòng nhập tên bài tập.";
    if (targetStep === 3 && dueAt && new Date(dueAt).getTime() <= Date.now()) {
      return "Hạn chót phải lớn hơn thời điểm hiện tại.";
    }
    if (title.trim().length > 250) return "Tên bài tập không được vượt quá 250 ký tự.";
    return "";
  };

  const goNext = () => {
    const validationError = validateStep(step);
    if (validationError) {
      setErrorMessage(validationError);
      return;
    }
    setErrorMessage("");
    setStep((current) => Math.min(wizardSteps.length - 1, current + 1));
  };

  const buildRequest = () => ({
    title: title.trim(),
    instructions: instructions.trim(),
    dueAt: dueAt ? new Date(dueAt).toISOString() : null,
    questionIds,
    targetMode,
    studentIds: targetMode === "SelectedStudents" ? studentIds : undefined,
  });

  const saveAssignment = async (publishAfterSave: boolean) => {
    for (let index = 0; index < wizardSteps.length; index += 1) {
      const validationError = validateStep(index);
      if (validationError) {
        setStep(index);
        setErrorMessage(validationError);
        return;
      }
    }

    setErrorMessage("");
    try {
      const commonRequest = buildRequest();
      const response = isEditing && assignment
        ? await updateMutation.mutateAsync({
            id: assignment.assignmentId,
            request: { ...commonRequest, rowVersion: assignment.rowVersion } satisfies UpdateAssignmentRequest,
          })
        : await createMutation.mutateAsync({
            ...commonRequest,
            classId,
          } satisfies CreateAssignmentRequest);

      if (publishAfterSave) {
        await publishMutation.mutateAsync({
          id: response.data.assignmentId,
          request: { rowVersion: response.data.rowVersion },
        });
      }

      navigate("/quan-ly/bai-tap");
    } catch (error: unknown) {
      setErrorMessage(getErrorMessage(error, publishAfterSave ? "Không thể xuất bản bài tập." : "Không thể lưu bài tập."));
    }
  };

  const closeAssignment = async () => {
    if (!assignment) return;
    setErrorMessage("");
    try {
      await closeMutation.mutateAsync({
        id: assignment.assignmentId,
        request: { rowVersion: assignment.rowVersion },
      });
      navigate("/quan-ly/bai-tap");
    } catch (error: unknown) {
      setErrorMessage(getErrorMessage(error, "Không thể đóng bài tập."));
    }
  };

  if (isEditing && assignmentQuery.isLoading) {
    return <div className="p-10 text-center">Đang tải dữ liệu...</div>;
  }

  if (isEditing && (assignmentQuery.isError || !assignment)) {
    return (
      <div className="p-6 max-w-4xl mx-auto">
        <div className="rounded-lg border border-red-100 bg-red-50 p-4 text-red-700">
          {getErrorMessage(assignmentQuery.error, "Không tìm thấy bài tập.")}
        </div>
      </div>
    );
  }

  const isSaving = createMutation.isPending || updateMutation.isPending || publishMutation.isPending;
  const selectedQuestions = questionsQuery.data?.data.filter((question) => questionIds.includes(question.questionId)) || [];
  const selectedStudents = studentsQuery.data?.data.filter((student) => studentIds.includes(student.studentId)) || [];

  return (
    <div className="p-6 max-w-5xl mx-auto">
      <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
        <div>
          <button
            type="button"
            onClick={() => navigate("/quan-ly/bai-tap")}
            className="mb-2 text-sm font-medium text-blue-600 hover:underline"
          >
            &larr; Quay lại danh sách bài tập
          </button>
          <h1 className="text-3xl font-bold text-slate-800">
            {isEditing ? assignment?.title : "Tạo bài tập mới"}
          </h1>
          {assignment && <p className="mt-1 text-sm text-slate-500">Trạng thái: {assignment.status}</p>}
        </div>
        {isPublished && (
          <button
            type="button"
            onClick={closeAssignment}
            disabled={closeMutation.isPending}
            className="rounded-lg bg-amber-600 px-4 py-2 font-medium text-white hover:bg-amber-700 disabled:opacity-50"
          >
            {closeMutation.isPending ? "Đang đóng..." : "Đóng bài tập"}
          </button>
        )}
      </div>

      {errorMessage && (
        <div className="mb-5 rounded-lg border border-red-100 bg-red-50 p-4 text-red-700">
          {errorMessage}
        </div>
      )}

      {!canEdit && (
        <div className="mb-5 rounded-lg border border-blue-100 bg-blue-50 p-4 text-blue-700">
          Bài tập đã {assignment?.status === "Closed" ? "đóng" : "xuất bản"}; nội dung, câu hỏi và đối tượng đã được chốt.
        </div>
      )}

      <ol className="mb-6 grid grid-cols-2 gap-2 md:grid-cols-4" aria-label="Các bước tạo bài tập">
        {wizardSteps.map((label, index) => (
          <li key={label}>
            <button
              type="button"
              onClick={() => setStep(index)}
              className={`w-full rounded-lg border px-3 py-3 text-left text-sm font-medium ${
                step === index
                  ? "border-blue-600 bg-blue-50 text-blue-700"
                  : index < step
                    ? "border-green-200 bg-green-50 text-green-700"
                    : "border-slate-200 bg-white text-slate-600"
              }`}
            >
              <span className="mr-2">{index + 1}.</span>{label}
            </button>
          </li>
        ))}
      </ol>

      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
        {step === 0 && (
          <section>
            <h2 className="mb-2 text-xl font-bold text-slate-800">Chọn lớp học</h2>
            <p className="mb-5 text-sm text-slate-500">Danh sách chỉ gồm các lớp đang hoạt động mà tài khoản hiện tại được phép truy cập.</p>
            {classesQuery.isLoading ? (
              <p className="text-slate-500">Đang tải danh sách lớp...</p>
            ) : classesQuery.isError ? (
              <p className="text-red-600">{getErrorMessage(classesQuery.error, "Không thể tải danh sách lớp.")}</p>
            ) : (
              <div className="grid gap-3 md:grid-cols-2">
                {classesQuery.data?.data.map((classItem) => (
                  <label
                    key={classItem.classId}
                    className={`cursor-pointer rounded-lg border p-4 ${classId === classItem.classId ? "border-blue-500 bg-blue-50" : "border-slate-200"}`}
                  >
                    <input
                      type="radio"
                      name="assignment-class"
                      className="mr-3"
                      checked={classId === classItem.classId}
                      onChange={() => handleClassChange(classItem.classId)}
                      disabled={!canEdit || isEditing}
                    />
                    <span className="font-semibold text-slate-800">{classItem.className}</span>
                    <span className="mt-1 block pl-6 text-sm text-slate-500">
                      {classItem.subject.subjectName} · {classItem.academicYear} · {classItem.studentCount} học sinh
                    </span>
                  </label>
                ))}
                {classesQuery.data?.data.length === 0 && <p className="text-slate-500">Không có lớp học phù hợp.</p>}
              </div>
            )}
            {isEditing && !selectedClass && assignment && (
              <p className="rounded-lg bg-slate-50 p-4 text-sm text-slate-600">Lớp hiện tại: {assignment.classId}</p>
            )}
          </section>
        )}

        {step === 1 && (
          <section>
            <div className="mb-5 flex flex-wrap items-end justify-between gap-2">
              <div>
                <h2 className="text-xl font-bold text-slate-800">Chọn câu hỏi</h2>
                <p className="mt-1 text-sm text-slate-500">Chỉ hiển thị câu hỏi Active cùng môn với lớp đã chọn.</p>
              </div>
              <span className="text-sm font-medium text-blue-700">Đã chọn: {questionIds.length}</span>
            </div>
            {!selectedClass && !assignment ? (
              <p className="text-amber-700">Hãy chọn lớp học trước.</p>
            ) : questionsQuery.isLoading ? (
              <p className="text-slate-500">Đang tải câu hỏi...</p>
            ) : questionsQuery.isError ? (
              <p className="text-red-600">{getErrorMessage(questionsQuery.error, "Không thể tải câu hỏi.")}</p>
            ) : (
              <div className="max-h-[30rem] space-y-3 overflow-y-auto pr-1">
                {questionsQuery.data?.data.map((question) => (
                  <label key={question.questionId} className="flex cursor-pointer gap-3 rounded-lg border border-slate-200 p-4 hover:bg-slate-50">
                    <input
                      type="checkbox"
                      checked={questionIds.includes(question.questionId)}
                      onChange={() => toggleQuestion(question.questionId)}
                      disabled={!canEdit}
                      className="mt-1"
                    />
                    <span>
                      <span className="block font-medium text-slate-800">{question.questionText}</span>
                      <span className="mt-1 block text-xs text-slate-500">
                        ID {question.questionId} · {question.questionType} · độ khó {question.difficulty} · {question.maxScore} điểm
                      </span>
                    </span>
                  </label>
                ))}
                {questionsQuery.data?.data.length === 0 && <p className="text-slate-500">Môn học này chưa có câu hỏi Active.</p>}
              </div>
            )}
          </section>
        )}

        {step === 2 && (
          <section>
            <h2 className="mb-2 text-xl font-bold text-slate-800">Chọn đối tượng nhận bài</h2>
            <p className="mb-5 text-sm text-slate-500">
              Nhóm chênh lệch (GapGroup) phải được chụp thành danh sách học sinh tại thời điểm giao bài, vì vậy request luôn gửi SelectedStudents.
            </p>
            <div className="mb-5 grid gap-3 md:grid-cols-2">
              <label className={`rounded-lg border p-4 ${targetMode === "WholeClass" ? "border-blue-500 bg-blue-50" : "border-slate-200"}`}>
                <input
                  type="radio"
                  name="target-mode"
                  className="mr-3"
                  checked={targetMode === "WholeClass"}
                  onChange={() => setTargetMode("WholeClass")}
                  disabled={!canEdit}
                />
                <span className="font-semibold text-slate-800">Cả lớp</span>
              </label>
              <label className={`rounded-lg border p-4 ${targetMode === "SelectedStudents" ? "border-blue-500 bg-blue-50" : "border-slate-200"}`}>
                <input
                  type="radio"
                  name="target-mode"
                  className="mr-3"
                  checked={targetMode === "SelectedStudents"}
                  onChange={() => setTargetMode("SelectedStudents")}
                  disabled={!canEdit}
                />
                <span className="font-semibold text-slate-800">Học sinh được chọn / GapGroup snapshot</span>
              </label>
            </div>

            {targetMode === "SelectedStudents" && (
              <div>
                <div className="mb-3 flex items-center justify-between">
                  <span className="text-sm font-medium text-slate-700">Danh sách học sinh đang hoạt động</span>
                  <span className="text-sm font-medium text-blue-700">Đã chọn: {studentIds.length}</span>
                </div>
                {studentsQuery.isLoading ? (
                  <p className="text-slate-500">Đang tải học sinh...</p>
                ) : studentsQuery.isError ? (
                  <p className="text-red-600">{getErrorMessage(studentsQuery.error, "Không thể tải học sinh của lớp.")}</p>
                ) : (
                  <div className="grid max-h-80 gap-3 overflow-y-auto md:grid-cols-2">
                    {studentsQuery.data?.data.map((student) => (
                      <label key={student.studentId} className="flex cursor-pointer items-start gap-3 rounded-lg border border-slate-200 p-3 hover:bg-slate-50">
                        <input
                          type="checkbox"
                          checked={studentIds.includes(student.studentId)}
                          onChange={() => toggleStudent(student.studentId)}
                          disabled={!canEdit}
                          className="mt-1"
                        />
                        <span>
                          <span className="block font-medium text-slate-800">{student.fullName}</span>
                          <span className="block text-xs text-slate-500">{student.username} · Khối {student.gradeLevel}</span>
                        </span>
                      </label>
                    ))}
                    {studentsQuery.data?.data.length === 0 && <p className="text-slate-500">Lớp chưa có học sinh đang hoạt động.</p>}
                  </div>
                )}
              </div>
            )}
          </section>
        )}

        {step === 3 && (
          <section>
            <h2 className="mb-5 text-xl font-bold text-slate-800">Xem lại và xuất bản</h2>
            <div className="grid gap-5 md:grid-cols-2">
              <div className="md:col-span-2">
                <label htmlFor="assignment-title" className="mb-1 block text-sm font-medium text-slate-700">Tên bài tập</label>
                <input
                  id="assignment-title"
                  value={title}
                  onChange={(event) => setTitle(event.target.value)}
                  disabled={!canEdit}
                  maxLength={250}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2 disabled:bg-slate-50"
                />
              </div>
              <div>
                <label htmlFor="assignment-due-at" className="mb-1 block text-sm font-medium text-slate-700">Hạn chót</label>
                <input
                  id="assignment-due-at"
                  type="datetime-local"
                  value={dueAt}
                  onChange={(event) => setDueAt(event.target.value)}
                  min={minimumDueAt}
                  disabled={!canEdit}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2 disabled:bg-slate-50"
                />
                <p className="mt-1 text-xs text-slate-500">Nếu đặt hạn chót, thời điểm phải nằm trong tương lai.</p>
              </div>
              <div className="rounded-lg bg-slate-50 p-4 text-sm text-slate-700">
                <p><strong>Lớp:</strong> {selectedClass?.className || assignment?.classId}</p>
                <p><strong>Câu hỏi:</strong> {questionIds.length}</p>
                <p><strong>Đối tượng:</strong> {targetMode === "WholeClass" ? "Cả lớp" : `${studentIds.length} học sinh`}</p>
              </div>
              <div className="md:col-span-2">
                <label htmlFor="assignment-instructions" className="mb-1 block text-sm font-medium text-slate-700">Hướng dẫn</label>
                <textarea
                  id="assignment-instructions"
                  value={instructions}
                  onChange={(event) => setInstructions(event.target.value)}
                  disabled={!canEdit}
                  className="h-28 w-full rounded-lg border border-slate-300 px-3 py-2 disabled:bg-slate-50"
                />
              </div>
            </div>

            {selectedQuestions.length > 0 && (
              <details className="mt-5 rounded-lg border border-slate-200 p-4">
                <summary className="cursor-pointer font-medium text-slate-800">Xem {selectedQuestions.length} câu hỏi đã chọn</summary>
                <ol className="mt-3 list-decimal space-y-2 pl-5 text-sm text-slate-600">
                  {selectedQuestions.map((question) => <li key={question.questionId}>{question.questionText}</li>)}
                </ol>
              </details>
            )}
            {targetMode === "SelectedStudents" && selectedStudents.length > 0 && (
              <p className="mt-4 text-sm text-slate-600">
                <strong>Học sinh:</strong> {selectedStudents.map((student) => student.fullName).join(", ")}
              </p>
            )}
          </section>
        )}

        <div className="mt-8 flex flex-wrap justify-between gap-3 border-t border-slate-100 pt-5">
          <button
            type="button"
            onClick={() => step === 0 ? navigate("/quan-ly/bai-tap") : setStep((current) => current - 1)}
            className="rounded-lg border border-slate-300 px-4 py-2 font-medium text-slate-700 hover:bg-slate-50"
          >
            {step === 0 ? "Hủy" : "Bước trước"}
          </button>
          {step < wizardSteps.length - 1 ? (
            <button
              type="button"
              onClick={goNext}
              className="rounded-lg bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700"
            >
              Bước tiếp theo
            </button>
          ) : canEdit ? (
            <div className="flex flex-wrap gap-3">
              <button
                type="button"
                onClick={() => saveAssignment(false)}
                disabled={isSaving}
                className="rounded-lg border border-blue-600 px-4 py-2 font-medium text-blue-700 hover:bg-blue-50 disabled:opacity-50"
              >
                {isSaving ? "Đang lưu..." : "Lưu bản nháp"}
              </button>
              <button
                type="button"
                onClick={() => saveAssignment(true)}
                disabled={isSaving}
                className="rounded-lg bg-green-600 px-4 py-2 font-medium text-white hover:bg-green-700 disabled:opacity-50"
              >
                {publishMutation.isPending ? "Đang xuất bản..." : "Lưu và xuất bản"}
              </button>
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
};
