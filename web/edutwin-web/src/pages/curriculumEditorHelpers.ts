import type { CreateCurriculumRequest, UpdateCurriculumRequest } from "../types/curriculum.ts";

export interface CurriculumFormValidationResult {
  isValid: boolean;
  errorMessage?: string;
}

export function shouldDisplayTeacherSelector(options: {
  isEditMode: boolean;
  isCenterManager: boolean;
}): boolean {
  return !options.isEditMode && options.isCenterManager;
}

export function validateCurriculumForm(
  formData: {
    title?: string;
    subjectId?: string;
    teacherId?: string | null;
    gradeLevel?: number | null;
  },
  options: {
    isEditMode: boolean;
    isCenterManager: boolean;
  }
): CurriculumFormValidationResult {
  if (!formData.title || !formData.title.trim()) {
    return { isValid: false, errorMessage: "Vui lòng nhập tiêu đề lộ trình." };
  }

  if (!options.isEditMode) {
    if (!formData.subjectId || !formData.subjectId.trim()) {
      return { isValid: false, errorMessage: "Vui lòng chọn môn học." };
    }

    if (formData.gradeLevel === null || formData.gradeLevel === undefined || ![10, 11, 12].includes(formData.gradeLevel)) {
      return { isValid: false, errorMessage: "Vui lòng chọn khối học áp dụng (Khối 10, 11 hoặc 12) cho giáo trình mới." };
    }

    if (options.isCenterManager && (!formData.teacherId || !formData.teacherId.trim())) {
      return { isValid: false, errorMessage: "Vui lòng chọn giáo viên phụ trách lộ trình." };
    }
  }

  return { isValid: true };
}

export function buildCreateCurriculumPayload(
  formData: {
    title: string;
    description?: string;
    subjectId: string;
    gradeLevel?: number | null;
    teacherId?: string | null;
    nodeIds?: string[];
  },
  isCenterManager: boolean
): CreateCurriculumRequest {
  return {
    title: formData.title.trim(),
    description: formData.description,
    subjectId: formData.subjectId,
    gradeLevel: formData.gradeLevel !== undefined ? formData.gradeLevel : null,
    teacherId: isCenterManager && formData.teacherId?.trim() ? formData.teacherId.trim() : undefined,
    nodeIds: formData.nodeIds || []
  };
}

export function buildUpdateCurriculumPayload(formData: {
  title: string;
  description?: string;
  gradeLevel?: number | null;
  rowVersion: string;
}): UpdateCurriculumRequest {
  return {
    title: formData.title.trim(),
    description: formData.description,
    gradeLevel: formData.gradeLevel !== undefined ? formData.gradeLevel : null,
    rowVersion: formData.rowVersion
  };
}

export function isConcurrencyConflictError(error: any): boolean {
  return error?.response?.status === 409 || error?.status === 409;
}
