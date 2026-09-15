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
    teacherId?: string | null;
    nodeIds?: string[];
  },
  isCenterManager: boolean
): CreateCurriculumRequest {
  return {
    title: formData.title.trim(),
    description: formData.description,
    subjectId: formData.subjectId,
    teacherId: isCenterManager && formData.teacherId?.trim() ? formData.teacherId.trim() : undefined,
    nodeIds: formData.nodeIds || []
  };
}

export function buildUpdateCurriculumPayload(formData: {
  title: string;
  description?: string;
  rowVersion: string;
}): UpdateCurriculumRequest {
  return {
    title: formData.title.trim(),
    description: formData.description,
    rowVersion: formData.rowVersion
  };
}

export function isConcurrencyConflictError(error: any): boolean {
  return error?.response?.status === 409 || error?.status === 409;
}
