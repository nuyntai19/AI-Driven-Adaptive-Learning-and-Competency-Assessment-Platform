import React from "react";
import { CasioInlinePanel } from "./CasioInlinePanel";
import { ScratchpadInlinePanel } from "./ScratchpadInlinePanel";
import { FunctionGrapher } from "./FunctionGrapher";

export type AssistantToolTab = "casio" | "scratchpad" | "graph";

export interface SideAssistantWorkspaceProps {
  activeTab: AssistantToolTab;
  onChangeTab: (tab: AssistantToolTab) => void;
  onClose: () => void;
  onInsertResult?: (result: string) => void;
  centerId: string;
  userId: string;
  clientSubmissionId: string;
  isScratchpadAttached?: boolean;
  isReadOnly?: boolean;
  onExportScratchpadPng?: (blob: Blob) => void;
  onAttachSnapshot?: (blob: Blob, dataUrl: string) => void;
}

export const SideAssistantWorkspace: React.FC<SideAssistantWorkspaceProps> = ({
  activeTab,
  onChangeTab,
  onClose,
  onInsertResult,
  centerId,
  userId,
  clientSubmissionId,
  isScratchpadAttached,
  isReadOnly = false,
  onExportScratchpadPng,
  onAttachSnapshot,
}) => {
  return (
    <aside
      aria-label="Khung trợ lý học tập kế bên"
      className="flex flex-col h-[780px] max-h-[calc(100vh-2rem)] min-h-[700px] bg-slate-900/95 rounded-3xl border border-slate-700/80 shadow-2xl overflow-hidden backdrop-blur-md animate-in fade-in slide-in-from-right-4 duration-200"
    >
      {/* Top Header with Tab Switchers & Close Button */}
      <header className="flex items-center justify-between px-3.5 py-2.5 bg-slate-950/85 border-b border-slate-800 shrink-0">
        <div className="flex items-center gap-1.5" role="tablist" aria-label="Công cụ trợ lý">
          <button
            type="button"
            role="tab"
            aria-selected={activeTab === "casio"}
            onClick={() => onChangeTab("casio")}
            className={`flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl text-xs font-bold transition-all cursor-pointer ${
              activeTab === "casio"
                ? "bg-amber-500 text-slate-950 shadow-xs"
                : "text-slate-400 hover:text-slate-200 hover:bg-slate-800/60"
            }`}
          >
            <span>🖩</span>
            <span>Casio fx-580</span>
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeTab === "scratchpad"}
            onClick={() => onChangeTab("scratchpad")}
            className={`flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl text-xs font-bold transition-all cursor-pointer ${
              activeTab === "scratchpad"
                ? "bg-emerald-600 text-white shadow-xs"
                : "text-slate-400 hover:text-slate-200 hover:bg-slate-800/60"
            }`}
          >
            <span>✏️</span>
            <span>Bảng nháp</span>
            {isScratchpadAttached && (
              <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" title="Đã có ảnh nháp đính kèm" />
            )}
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeTab === "graph"}
            onClick={() => onChangeTab("graph")}
            className={`flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl text-xs font-bold transition-all cursor-pointer ${
              activeTab === "graph"
                ? "bg-indigo-600 text-white shadow-xs"
                : "text-slate-400 hover:text-slate-200 hover:bg-slate-800/60"
            }`}
          >
            <span>📈</span>
            <span>Vẽ đồ thị</span>
          </button>
        </div>

        <button
          type="button"
          onClick={onClose}
          className="p-1.5 rounded-xl text-slate-400 hover:text-white hover:bg-slate-800 transition-colors cursor-pointer flex items-center gap-1 text-xs font-bold"
          title="Thu nhỏ khung trợ lý"
          aria-label="Đóng khung trợ lý"
        >
          <span>✕</span>
          <span className="hidden sm:inline">Thu gọn</span>
        </button>
      </header>

      {/* Main Tool Content */}
      <div className="flex-1 overflow-hidden p-2.5 bg-slate-900/60 flex flex-col min-h-0">
        {activeTab === "casio" && (
          <CasioInlinePanel onInsertResult={onInsertResult} />
        )}

        {activeTab === "scratchpad" && (
          <ScratchpadInlinePanel
            centerId={centerId}
            userId={userId}
            clientSubmissionId={clientSubmissionId}
            isAttached={isScratchpadAttached}
            isReadOnly={isReadOnly}
            onExportPng={onExportScratchpadPng}
            onAttachSnapshot={!isReadOnly ? onAttachSnapshot : undefined}
          />
        )}

        {activeTab === "graph" && (
          <FunctionGrapher />
        )}
      </div>
    </aside>
  );
};
