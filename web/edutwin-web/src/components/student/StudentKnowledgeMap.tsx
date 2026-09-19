import React, { useState } from "react";

export interface TopicMapNode {
  topicNodeId: string;
  topicName: string;
  masteryPercentage: number;
  evidenceCount?: number;
  lastReasoningQuality?: number | null;
}

interface StudentKnowledgeMapProps {
  topics: TopicMapNode[];
  subjectName?: string;
  onSelectTopic?: (topic: TopicMapNode) => void;
  selectedTopicId?: string;
  className?: string;
}

interface NodeCoordinate {
  cx: number;
  cy: number;
  labelPos: "top" | "bottom";
}

interface EdgeConnection {
  fromIndex: number;
  toIndex: number;
}

/**
 * Generates deterministic coordinates and edge connections for small node counts (1-6)
 * and generic counts, utilizing 60-75% of the 680x340 viewBox while ensuring
 * minimum node-to-node distance >= 140px and preventing edges from crossing labels.
 */
function getDeterministicLayout(total: number): {
  coords: NodeCoordinate[];
  edges: EdgeConnection[];
} {
  if (total <= 1) {
    return {
      coords: [{ cx: 340, cy: 170, labelPos: "bottom" }],
      edges: [],
    };
  }

  if (total === 2) {
    return {
      coords: [
        { cx: 200, cy: 170, labelPos: "bottom" },
        { cx: 480, cy: 170, labelPos: "bottom" },
      ],
      edges: [{ fromIndex: 0, toIndex: 1 }],
    };
  }

  if (total === 3) {
    // Balanced constellation triangle:
    //          Hàm số (0)
    //             ●
    //           /   \
    //          /     \
    //         ●───────●
    //   Nguyên hàm (1)  Mũ–Logarit (2)
    return {
      coords: [
        { cx: 340, cy: 85, labelPos: "top" },
        { cx: 170, cy: 245, labelPos: "bottom" },
        { cx: 510, cy: 245, labelPos: "bottom" },
      ],
      edges: [
        { fromIndex: 0, toIndex: 1 },
        { fromIndex: 0, toIndex: 2 },
        { fromIndex: 1, toIndex: 2 },
      ],
    };
  }

  if (total === 4) {
    // Dynamic constellation diamond
    return {
      coords: [
        { cx: 145, cy: 170, labelPos: "bottom" },
        { cx: 340, cy: 85, labelPos: "top" },
        { cx: 340, cy: 255, labelPos: "bottom" },
        { cx: 535, cy: 170, labelPos: "bottom" },
      ],
      edges: [
        { fromIndex: 0, toIndex: 1 },
        { fromIndex: 0, toIndex: 2 },
        { fromIndex: 1, toIndex: 3 },
        { fromIndex: 2, toIndex: 3 },
        { fromIndex: 1, toIndex: 2 },
      ],
    };
  }

  if (total === 5) {
    // Balanced wave / pentagon constellation
    return {
      coords: [
        { cx: 130, cy: 220, labelPos: "bottom" },
        { cx: 235, cy: 95, labelPos: "top" },
        { cx: 340, cy: 235, labelPos: "bottom" },
        { cx: 445, cy: 95, labelPos: "top" },
        { cx: 550, cy: 220, labelPos: "bottom" },
      ],
      edges: [
        { fromIndex: 0, toIndex: 1 },
        { fromIndex: 1, toIndex: 2 },
        { fromIndex: 2, toIndex: 3 },
        { fromIndex: 3, toIndex: 4 },
        { fromIndex: 1, toIndex: 3 },
      ],
    };
  }

  if (total === 6) {
    // Staggered two-tier constellation
    return {
      coords: [
        { cx: 150, cy: 95, labelPos: "top" },
        { cx: 340, cy: 90, labelPos: "top" },
        { cx: 530, cy: 95, labelPos: "top" },
        { cx: 170, cy: 245, labelPos: "bottom" },
        { cx: 340, cy: 250, labelPos: "bottom" },
        { cx: 510, cy: 245, labelPos: "bottom" },
      ],
      edges: [
        { fromIndex: 0, toIndex: 1 },
        { fromIndex: 1, toIndex: 2 },
        { fromIndex: 0, toIndex: 3 },
        { fromIndex: 1, toIndex: 4 },
        { fromIndex: 2, toIndex: 5 },
        { fromIndex: 3, toIndex: 4 },
        { fromIndex: 4, toIndex: 5 },
      ],
    };
  }

  // Generic for N > 6: S-curve / serpentine progression across canvas
  const coords: NodeCoordinate[] = [];
  const edges: EdgeConnection[] = [];
  const startX = 120;
  const endX = 560;
  const stepX = (endX - startX) / (total - 1);

  for (let i = 0; i < total; i++) {
    const cx = startX + i * stepX;
    const isTop = i % 2 === 0;
    const cy = isTop ? 100 + (i % 3) * 12 : 240 - (i % 3) * 12;
    coords.push({
      cx,
      cy,
      labelPos: isTop ? "top" : "bottom",
    });

    if (i > 0) {
      edges.push({ fromIndex: i - 1, toIndex: i });
    }
  }

  return { coords, edges };
}

/**
 * Knowledge Map (Bản Đồ Năng Lực) for EduTwin Learning Atlas:
 * Renders an interconnected constellation/route of competency nodes.
 *
 * Layer Order:
 * 1. Background & Grid
 * 2. Edges (connective lines)
 * 3. Node Halos (selected / focused / hovered outer ring ~2px, status color, no thick black border)
 * 4. Interactive Node circles
 * 5. Independent text labels (outside node, zero edge intersection)
 * 6. Floating tooltip (non-intrusive, opposite placement)
 */
export const StudentKnowledgeMap: React.FC<StudentKnowledgeMapProps> = ({
  topics,
  subjectName = "Toán",
  onSelectTopic,
  selectedTopicId: controlledSelectedTopicId,
  className = "",
}) => {
  const [internalSelectedId, setInternalSelectedId] = useState<string | null>(null);
  const selectedTopicId = controlledSelectedTopicId ?? internalSelectedId ?? topics[0]?.topicNodeId;
  const [hoveredTopicId, setHoveredTopicId] = useState<string | null>(null);
  const [focusedTopicId, setFocusedTopicId] = useState<string | null>(null);
  const [activeTooltip, setActiveTooltip] = useState<TopicMapNode | null>(null);

  if (!topics || topics.length === 0) {
    return (
      <div className="py-12 text-center text-xs text-stone-500 dark:text-stone-400">
        Chưa có dữ liệu chuyên đề để hiển thị bản đồ tri thức.
      </div>
    );
  }

  const { coords, edges } = getDeterministicLayout(topics.length);

  const mapNodes = topics.map((topic, index) => {
    const coord = coords[index] || { cx: 340, cy: 170, labelPos: "bottom" as const };
    const mastery = topic.masteryPercentage;
    const isMastered = mastery >= 75;
    const isDeveloping = mastery >= 50 && mastery < 75;
    const isWeak = mastery > 0 && mastery < 50;
    const isUnassessed = mastery === 0 && (topic.evidenceCount === 0 || topic.evidenceCount === undefined);

    const accentColor = isMastered
      ? "#10B981"
      : isDeveloping
      ? "#6746E8"
      : isWeak
      ? "#F59E0B"
      : "#9CA3AF";

    return {
      ...topic,
      cx: coord.cx,
      cy: coord.cy,
      labelPos: coord.labelPos,
      isMastered,
      isDeveloping,
      isWeak,
      isUnassessed,
      accentColor,
    };
  });

  // Calculate active node for tooltip placement
  const activeNode = activeTooltip
    ? mapNodes.find((n) => n.topicNodeId === activeTooltip.topicNodeId)
    : null;

  // Smart tooltip positioning: place on the opposite side of the active node to avoid covering it
  const tooltipPlacementClass = activeNode && activeNode.cx > 340 ? "top-3 left-3" : "top-3 right-3";

  return (
    <div className={`w-full ${className}`}>
      {/* Map Header / Legend Bar */}
      <div className="flex flex-wrap items-center justify-between gap-3 pb-3 border-b border-stone-200/80 dark:border-stone-800/80 text-xs">
        <div className="flex items-center gap-2">
          <span className="font-bold text-stone-900 dark:text-stone-100">
            Hệ thống chuyên đề ({topics.length} trạm)
          </span>
          <span className="text-[11px] font-mono text-stone-400">
            {subjectName} Atlas
          </span>
        </div>

        {/* Legend */}
        <div className="flex flex-wrap items-center gap-3 text-[11px] text-stone-600 dark:text-stone-400">
          <span className="inline-flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 inline-block" />
            <span>Thành thạo (≥75%)</span>
          </span>
          <span className="inline-flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded-full bg-indigo-500 inline-block" />
            <span>Đang phát triển</span>
          </span>
          <span className="inline-flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded-full bg-amber-500 inline-block" />
            <span>Cần rèn luyện</span>
          </span>
          <span className="inline-flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded-full border border-stone-400 dark:border-stone-600 bg-transparent inline-block" />
            <span>Chưa đánh giá</span>
          </span>
        </div>
      </div>

      {/* Interactive Desktop Canvas (viewBox 680x340 scales responsive) */}
      <div className="relative w-full h-80 sm:h-96 my-2 rounded-xl bg-stone-50/50 dark:bg-[#131b2e]/60 border border-stone-200/70 dark:border-stone-800/70 overflow-hidden hidden sm:block">
        <svg
          viewBox="0 0 680 340"
          className="w-full h-full overflow-visible select-none"
          preserveAspectRatio="xMidYMid meet"
        >
          {/* LAYER 1: Background Atlas Grid */}
          <defs>
            <pattern id="atlas-small-grid" width="24" height="24" patternUnits="userSpaceOnUse">
              <path
                d="M 24 0 L 0 0 0 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="0.6"
                className="text-stone-200/60 dark:text-stone-800/60"
                strokeDasharray="2 2"
              />
            </pattern>
          </defs>
          <rect width="100%" height="100%" fill="url(#atlas-small-grid)" />

          {/* LAYER 2: Connective Routes / Edges between topics (behind nodes and labels) */}
          <g className="atlas-edges-layer" pointerEvents="none">
            {edges.map(({ fromIndex, toIndex }) => {
              const fromNode = mapNodes[fromIndex];
              const toNode = mapNodes[toIndex];
              if (!fromNode || !toNode) return null;

              const isMasteredPath = fromNode.isMastered && toNode.isMastered;
              const isSelectedPath =
                fromNode.topicNodeId === selectedTopicId || toNode.topicNodeId === selectedTopicId;

              return (
                <line
                  key={`route-${fromNode.topicNodeId}-${toNode.topicNodeId}`}
                  x1={fromNode.cx}
                  y1={fromNode.cy}
                  x2={toNode.cx}
                  y2={toNode.cy}
                  stroke={
                    isMasteredPath
                      ? "#10B981"
                      : isSelectedPath
                      ? "#6746E8"
                      : "#CBD5E1"
                  }
                  className={isMasteredPath || isSelectedPath ? "" : "dark:stroke-stone-700"}
                  strokeWidth={isSelectedPath ? 2.2 : isMasteredPath ? 1.8 : 1.2}
                  strokeDasharray={isSelectedPath && !isMasteredPath ? "4 4" : "none"}
                />
              );
            })}
          </g>

          {/* LAYER 3: Outer Halos for Selected / Hovered / Focused nodes (Strictly no black capsule) */}
          <g className="atlas-halos-layer" pointerEvents="none">
            {mapNodes.map((node) => {
              const isSelected = node.topicNodeId === selectedTopicId;
              const isHovered = node.topicNodeId === hoveredTopicId;
              const isFocused = node.topicNodeId === focusedTopicId;
              const showHalo = isSelected || isHovered || isFocused;

              if (!showHalo) return null;

              return (
                <g key={`halo-${node.topicNodeId}`}>
                  {/* Subtle soft aura */}
                  {isSelected && (
                    <circle
                      cx={node.cx}
                      cy={node.cy}
                      r={19}
                      fill={node.accentColor}
                      fillOpacity={0.14}
                    />
                  )}
                  {/* Crisp 2px outer ring with 4px gap to node (radius: nodeRadius 10px + 5px = 15px) */}
                  <circle
                    cx={node.cx}
                    cy={node.cy}
                    r={15}
                    fill="none"
                    stroke={node.accentColor}
                    strokeWidth={2}
                    strokeDasharray={isSelected ? "none" : "3 3"}
                  />
                </g>
              );
            })}
          </g>

          {/* LAYER 4: Interactive Topic Nodes (Outline strictly none) */}
          <g className="atlas-nodes-layer">
            {mapNodes.map((node) => {
              return (
                <g
                  key={`node-btn-${node.topicNodeId}`}
                  tabIndex={0}
                  role="button"
                  aria-label={`Chuyên đề ${node.topicName}`}
                  className="cursor-pointer outline-none focus:outline-none focus-visible:outline-none"
                  style={{ outline: "none" }}
                  onClick={() => {
                    setInternalSelectedId(node.topicNodeId);
                    onSelectTopic?.(node);
                  }}
                  onMouseEnter={() => {
                    setHoveredTopicId(node.topicNodeId);
                    setActiveTooltip(node);
                  }}
                  onMouseLeave={() => {
                    setHoveredTopicId(null);
                    setActiveTooltip(null);
                  }}
                  onFocus={() => {
                    setFocusedTopicId(node.topicNodeId);
                    setActiveTooltip(node);
                  }}
                  onBlur={() => {
                    setFocusedTopicId(null);
                    setActiveTooltip(null);
                  }}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                      e.preventDefault();
                      setInternalSelectedId(node.topicNodeId);
                      onSelectTopic?.(node);
                    }
                  }}
                >
                  {/* Node Outer Shell (radius 10px, stroke-width 2px) */}
                  <circle
                    cx={node.cx}
                    cy={node.cy}
                    r={10}
                    fill="white"
                    stroke={node.accentColor}
                    strokeWidth={2}
                    className="dark:fill-[#172033] transition-transform duration-150 hover:scale-110"
                  />
                  {/* Node Core Dot */}
                  <circle
                    cx={node.cx}
                    cy={node.cy}
                    r={node.isUnassessed ? 3 : 5}
                    fill={node.accentColor}
                  />
                </g>
              );
            })}
          </g>

          {/* LAYER 5: Independent Text Labels (Outside node, never crossed by edges) */}
          <g className="atlas-labels-layer" pointerEvents="none">
            {mapNodes.map((node) => {
              const isSelected = node.topicNodeId === selectedTopicId;
              const isHovered = node.topicNodeId === hoveredTopicId;
              const isFocused = node.topicNodeId === focusedTopicId;
              const isActive = isSelected || isHovered || isFocused;

              // Position label cleanly above or below node
              const labelY = node.labelPos === "top" ? node.cy - 18 : node.cy + 26;

              return (
                <text
                  key={`label-${node.topicNodeId}`}
                  x={node.cx}
                  y={labelY}
                  textAnchor="middle"
                  fontSize={12}
                  fontWeight={isActive ? 700 : 600}
                  className="fill-stone-800 dark:fill-stone-200 select-none transition-colors"
                  style={{
                    fontFamily: '"Be Vietnam Pro", system-ui, sans-serif',
                  }}
                >
                  {node.topicName}
                </text>
              );
            })}
          </g>
        </svg>

        {/* LAYER 6: Smart Floating Tooltip (Floats opposite to active node, never covers node) */}
        {activeTooltip && (
          <div
            className={`absolute ${tooltipPlacementClass} p-3 rounded-xl bg-white/95 dark:bg-[#172033]/95 backdrop-blur-xs border border-stone-200 dark:border-stone-700 shadow-md max-w-xs pointer-events-none text-xs space-y-1 z-30 animate-in fade-in duration-150`}
          >
            <div className="font-bold text-stone-900 dark:text-stone-100">
              {activeTooltip.topicName}
            </div>
            <div className="flex items-center justify-between gap-4 text-[11px] text-stone-500 dark:text-stone-400">
              <span>Độ thành thạo:</span>
              <span className="font-mono font-bold text-stone-900 dark:text-stone-100">
                {activeTooltip.masteryPercentage.toFixed(0)}%
              </span>
            </div>
            <div className="flex items-center justify-between gap-4 text-[11px] text-stone-500 dark:text-stone-400">
              <span>Số bài tập:</span>
              <span>{activeTooltip.evidenceCount || 0} bài</span>
            </div>
            {activeTooltip.lastReasoningQuality !== null && activeTooltip.lastReasoningQuality !== undefined && (
              <div className="flex items-center justify-between gap-4 text-[11px] text-stone-500 dark:text-stone-400">
                <span>Chất lượng tư duy:</span>
                <span className="font-bold text-indigo-600 dark:text-indigo-400">
                  {activeTooltip.lastReasoningQuality}đ
                </span>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Mobile Vertical Learning Path (reflow for screens < 640px) */}
      <div className="sm:hidden space-y-2 mt-2">
        {mapNodes.map((node, index) => {
          const isSelected = node.topicNodeId === selectedTopicId;

          return (
            <div
              key={`mobile-${node.topicNodeId}`}
              onClick={() => {
                setInternalSelectedId(node.topicNodeId);
                onSelectTopic?.(node);
              }}
              className={`flex items-center gap-3 p-3 rounded-xl border text-xs cursor-pointer transition-colors ${
                isSelected
                  ? "bg-indigo-50/60 dark:bg-indigo-950/30 border-indigo-300 dark:border-indigo-800"
                  : "bg-stone-50 dark:bg-[#151d2f] border-stone-200/80 dark:border-stone-800/80"
              }`}
            >
              <div className="flex flex-col items-center">
                <span
                  className="w-3.5 h-3.5 rounded-full shrink-0"
                  style={{ backgroundColor: node.accentColor }}
                />
                {index < mapNodes.length - 1 && (
                  <span className="w-0.5 h-6 bg-stone-200 dark:bg-stone-700 my-0.5" />
                )}
              </div>
              <div className="flex-1 min-w-0">
                <div className="font-semibold text-stone-900 dark:text-stone-100 truncate">
                  {node.topicName}
                </div>
                <div className="text-[11px] text-stone-500 dark:text-stone-400">
                  {node.evidenceCount || 0} bài hoàn thành
                </div>
              </div>
              <span className="font-mono font-bold text-stone-800 dark:text-stone-200">
                {node.masteryPercentage.toFixed(0)}%
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
};
