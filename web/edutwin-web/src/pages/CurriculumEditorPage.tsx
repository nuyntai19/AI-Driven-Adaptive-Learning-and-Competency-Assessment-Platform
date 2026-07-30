import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { 
  useCurriculum, 
  useCreateCurriculum, 
  useUpdateCurriculum, 
  useUpdateCurriculumClasses,
  useUpdateCurriculumNodes,
  usePublishCurriculum 
} from "../features/curriculum/useCurriculums";
import type { CreateCurriculumRequest, UpdateCurriculumRequest } from "../types/curriculum";

export const CurriculumEditorPage = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const isEditMode = !!id;

  const [formData, setFormData] = useState<Partial<CreateCurriculumRequest & UpdateCurriculumRequest & { classIds: string[], rowVersion: string, reviewStatus: string }>>({
    title: "",
    description: "",
    subjectId: "",
    nodeIds: [],
    classIds: [],
    rowVersion: "0",
    reviewStatus: "Draft"
  });

  const [activeTab, setActiveTab] = useState<"info" | "nodes" | "classes">("info");
  
  const { data: curriculumData, isLoading: isLoadingCurriculum } = useCurriculum(id || "");
  
  const createMutation = useCreateCurriculum();
  const updateMutation = useUpdateCurriculum();
  const updateClassesMutation = useUpdateCurriculumClasses();
  const updateNodesMutation = useUpdateCurriculumNodes();
  const publishMutation = usePublishCurriculum();

  useEffect(() => {
    if (isEditMode && curriculumData?.data) {
      const c = curriculumData.data;
      setFormData({
        title: c.title,
        description: c.description || "",
        subjectId: c.subjectId,
        nodeIds: c.nodeIds || [],
        classIds: c.classIds || [],
        rowVersion: c.rowVersion,
        reviewStatus: c.reviewStatus
      });
    }
  }, [isEditMode, curriculumData]);

  const handleInputChange = (field: keyof typeof formData, value: any) => {
    setFormData(prev => ({ ...prev, [field]: value }));
  };

  const handleSaveInfo = () => {
    if (!isEditMode) {
      createMutation.mutate(
        {
          title: formData.title!,
          description: formData.description,
          subjectId: formData.subjectId!,
          nodeIds: formData.nodeIds || []
        },
        {
          onSuccess: (res) => {
            alert("Tạo lộ trình thành công!");
            navigate(`/quan-ly/giao-trinh/${res.data.curriculumId}`);
          },
          onError: (err: any) => {
            alert("Lỗi: " + (err.response?.data?.detail || err.message));
          }
        }
      );
    } else {
      updateMutation.mutate(
        {
          id: id!,
          data: {
            title: formData.title!,
            description: formData.description,
            rowVersion: formData.rowVersion!
          }
        },
        {
          onSuccess: (res) => {
            alert("Cập nhật thông tin thành công!");
            setFormData(prev => ({ ...prev, rowVersion: res.data.rowVersion }));
          },
          onError: (err: any) => {
            alert("Lỗi: " + (err.response?.data?.detail || err.message));
          }
        }
      );
    }
  };

  const handleSaveNodes = () => {
    if (!isEditMode) return;
    updateNodesMutation.mutate(
      {
        id: id!,
        data: {
          nodeIds: formData.nodeIds || [],
          rowVersion: formData.rowVersion!
        }
      },
      {
        onSuccess: (res) => {
          alert("Cập nhật kiến thức thành công!");
          setFormData(prev => ({ ...prev, rowVersion: res.data.rowVersion }));
        },
        onError: (err: any) => {
          alert("Lỗi: " + (err.response?.data?.detail || err.message));
        }
      }
    );
  };

  const handleSaveClasses = () => {
    if (!isEditMode) return;
    updateClassesMutation.mutate(
      {
        id: id!,
        data: {
          classIds: formData.classIds || [],
          rowVersion: formData.rowVersion!
        }
      },
      {
        onSuccess: (res) => {
          alert("Gán lớp học thành công!");
          setFormData(prev => ({ ...prev, rowVersion: res.data.rowVersion }));
        },
        onError: (err: any) => {
          alert("Lỗi: " + (err.response?.data?.detail || err.message));
        }
      }
    );
  };

  const handlePublish = () => {
    if (!isEditMode) return;
    if (confirm("Bạn có chắc chắn muốn xuất bản lộ trình này? Sau khi xuất bản sẽ không thể sửa đổi.")) {
      publishMutation.mutate(
        {
          id: id!,
          data: { rowVersion: formData.rowVersion! }
        },
        {
          onSuccess: (res) => {
            alert("Xuất bản thành công!");
            setFormData(prev => ({ ...prev, rowVersion: res.data.rowVersion, reviewStatus: res.data.reviewStatus }));
          },
          onError: (err: any) => {
            alert("Lỗi: " + (err.response?.data?.detail || err.message));
          }
        }
      );
    }
  };

  if (isEditMode && isLoadingCurriculum) {
    return <div className="p-6 text-center">Đang tải dữ liệu...</div>;
  }

  const isDraft = formData.reviewStatus === "Draft";

  return (
    <div className="p-6 max-w-5xl mx-auto">
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-3xl font-bold text-slate-800">
          {isEditMode ? "Chỉnh sửa Lộ trình học" : "Tạo Lộ trình học mới"}
        </h1>
        <div className="flex gap-3">
          <button 
            onClick={() => navigate("/quan-ly/giao-trinh")}
            className="px-4 py-2 border border-slate-300 text-slate-700 rounded-lg hover:bg-slate-50 transition"
          >
            Quay lại
          </button>
          {isEditMode && isDraft && (
            <button 
              onClick={handlePublish}
              disabled={publishMutation.isPending}
              className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 transition shadow-sm font-medium disabled:opacity-50"
            >
              Xuất bản (Publish)
            </button>
          )}
        </div>
      </div>

      <div className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden mb-6">
        <div className="flex border-b border-slate-200">
          <button 
            className={`px-6 py-3 font-medium text-sm ${activeTab === 'info' ? 'text-blue-600 border-b-2 border-blue-600' : 'text-slate-600 hover:text-slate-800'}`}
            onClick={() => setActiveTab('info')}
          >
            Thông tin chung
          </button>
          <button 
            className={`px-6 py-3 font-medium text-sm ${activeTab === 'nodes' ? 'text-blue-600 border-b-2 border-blue-600' : 'text-slate-600 hover:text-slate-800'} ${!isEditMode && 'opacity-50 cursor-not-allowed'}`}
            onClick={() => isEditMode && setActiveTab('nodes')}
            disabled={!isEditMode}
          >
            Nội dung kiến thức
          </button>
          <button 
            className={`px-6 py-3 font-medium text-sm ${activeTab === 'classes' ? 'text-blue-600 border-b-2 border-blue-600' : 'text-slate-600 hover:text-slate-800'} ${!isEditMode && 'opacity-50 cursor-not-allowed'}`}
            onClick={() => isEditMode && setActiveTab('classes')}
            disabled={!isEditMode}
          >
            Phân bổ Lớp học
          </button>
        </div>

        <div className="p-6">
          {activeTab === 'info' && (
            <div className="space-y-4 max-w-2xl">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Tiêu đề lộ trình <span className="text-red-500">*</span></label>
                <input 
                  type="text" 
                  value={formData.title}
                  onChange={e => handleInputChange("title", e.target.value)}
                  disabled={!isDraft}
                  className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none disabled:bg-slate-50"
                  placeholder="Nhập tiêu đề..."
                />
              </div>
              
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">ID Môn học <span className="text-red-500">*</span></label>
                <input 
                  type="text" 
                  value={formData.subjectId}
                  onChange={e => handleInputChange("subjectId", e.target.value)}
                  disabled={isEditMode} // Không cho sửa môn học khi đã tạo
                  className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none disabled:bg-slate-50"
                  placeholder="VD: 2ed34b81-..."
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Mô tả chi tiết</label>
                <textarea 
                  rows={4}
                  value={formData.description}
                  onChange={e => handleInputChange("description", e.target.value)}
                  disabled={!isDraft}
                  className="w-full border border-slate-300 rounded-lg px-4 py-3 focus:ring-2 focus:ring-blue-500 outline-none resize-y disabled:bg-slate-50"
                  placeholder="Mô tả lộ trình học..."
                ></textarea>
              </div>

              {isDraft && (
                <div className="pt-4">
                  <button 
                    onClick={handleSaveInfo}
                    disabled={createMutation.isPending || updateMutation.isPending}
                    className="px-6 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition shadow-sm font-medium disabled:opacity-50"
                  >
                    {isEditMode ? "Cập nhật thông tin" : "Tạo mới lộ trình"}
                  </button>
                </div>
              )}
            </div>
          )}

          {activeTab === 'nodes' && (
            <div className="space-y-4 max-w-2xl">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Danh sách Knowledge Node IDs (cách nhau bởi dấu phẩy)</label>
                <textarea 
                  rows={4}
                  value={formData.nodeIds?.join(", ")}
                  onChange={e => handleInputChange("nodeIds", e.target.value.split(",").map(s => s.trim()).filter(s => s))}
                  disabled={!isDraft}
                  className="w-full border border-slate-300 rounded-lg px-4 py-3 focus:ring-2 focus:ring-blue-500 outline-none resize-y disabled:bg-slate-50"
                  placeholder="VD: 101, 102, 103"
                ></textarea>
                <p className="text-xs text-slate-500 mt-1">Các node kiến thức sẽ được học theo thứ tự đã nhập.</p>
              </div>

              {isDraft && (
                <div className="pt-4">
                  <button 
                    onClick={handleSaveNodes}
                    disabled={updateNodesMutation.isPending}
                    className="px-6 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition shadow-sm font-medium disabled:opacity-50"
                  >
                    Lưu danh sách kiến thức
                  </button>
                </div>
              )}
            </div>
          )}

          {activeTab === 'classes' && (
            <div className="space-y-4 max-w-2xl">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Danh sách Class IDs (cách nhau bởi dấu phẩy)</label>
                <textarea 
                  rows={4}
                  value={formData.classIds?.join(", ")}
                  onChange={e => handleInputChange("classIds", e.target.value.split(",").map(s => s.trim()).filter(s => s))}
                  disabled={!isDraft}
                  className="w-full border border-slate-300 rounded-lg px-4 py-3 focus:ring-2 focus:ring-blue-500 outline-none resize-y disabled:bg-slate-50"
                  placeholder="VD: eed34b81-..., fed34b81-..."
                ></textarea>
                <p className="text-xs text-slate-500 mt-1">Các lớp học sẽ được áp dụng lộ trình này.</p>
              </div>

              {isDraft && (
                <div className="pt-4">
                  <button 
                    onClick={handleSaveClasses}
                    disabled={updateClassesMutation.isPending}
                    className="px-6 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition shadow-sm font-medium disabled:opacity-50"
                  >
                    Lưu danh sách lớp học
                  </button>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
