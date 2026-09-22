import React from "react";

interface StudentSubjectPatternProps {
  subjectName?: string;
  className?: string;
  opacity?: number;
}

/**
 * Subject Shape Language for EduTwin Learning Atlas:
 * Renders distinct graphic primitives based on the subject:
 * - TOÁN: Coordinate grid, axes, curves, geometric dots.
 * - VẬT LÝ: Sine waves, vectors, orbital paths.
 * - HÓA HỌC: Benzene hexagons, molecular bonds, atomic nodes.
 * - SINH HỌC: Cell membranes, organic branching routes.
 * - NGỮ VĂN: Typographic strokes, quote curves, editorial lines.
 * - TIẾNG ANH: Speech bubble geometry, phonetic blocks.
 */
export const StudentSubjectPattern: React.FC<StudentSubjectPatternProps> = ({
  subjectName = "",
  className = "",
  opacity = 0.08,
}) => {
  const norm = subjectName
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "");

  // TOÁN
  if (norm.includes("toan")) {
    return (
      <svg
        aria-hidden="true"
        className={`pointer-events-none absolute inset-0 h-full w-full ${className}`}
        style={{ opacity }}
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 400 240"
        preserveAspectRatio="none"
      >
        <defs>
          <pattern id="math-grid" width="40" height="40" patternUnits="userSpaceOnUse">
            <path d="M 40 0 L 0 0 0 40" fill="none" stroke="currentColor" strokeWidth="0.8" strokeDasharray="2 3" />
            <circle cx="40" cy="40" r="1.5" fill="currentColor" />
          </pattern>
        </defs>
        <rect width="100%" height="100%" fill="url(#math-grid)" />
        {/* Curved function plot & coordinate axis */}
        <path d="M 0 160 Q 100 80 200 130 T 400 60" fill="none" stroke="currentColor" strokeWidth="1.8" />
        <path d="M 0 120 L 400 120" fill="none" stroke="currentColor" strokeWidth="0.8" strokeDasharray="6 4" />
        <path d="M 200 0 L 200 240" fill="none" stroke="currentColor" strokeWidth="0.8" strokeDasharray="6 4" />
        <circle cx="100" cy="105" r="4" fill="currentColor" />
        <circle cx="200" cy="130" r="4" fill="currentColor" />
        <circle cx="300" cy="95" r="4" fill="currentColor" />
      </svg>
    );
  }

  // VẬT LÝ
  if (norm.includes("ly") || norm.includes("vat ly") || norm.includes("vat li")) {
    return (
      <svg
        aria-hidden="true"
        className={`pointer-events-none absolute inset-0 h-full w-full ${className}`}
        style={{ opacity }}
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 400 240"
        preserveAspectRatio="none"
      >
        {/* Orbital Ellipses */}
        <ellipse cx="200" cy="120" rx="150" ry="60" fill="none" stroke="currentColor" strokeWidth="1.2" transform="rotate(-15 200 120)" />
        <ellipse cx="200" cy="120" rx="100" ry="40" fill="none" stroke="currentColor" strokeWidth="0.8" strokeDasharray="3 3" transform="rotate(25 200 120)" />
        {/* Sine Wave */}
        <path d="M 0 120 C 50 60, 100 180, 150 120 C 200 60, 250 180, 300 120 C 350 60, 400 180, 450 120" fill="none" stroke="currentColor" strokeWidth="1.5" />
        {/* Vector Arrows */}
        <line x1="200" y1="120" x2="280" y2="70" stroke="currentColor" strokeWidth="1.5" />
        <polygon points="280,70 270,72 276,80" fill="currentColor" />
        <circle cx="200" cy="120" r="5" fill="currentColor" />
      </svg>
    );
  }

  // HÓA HỌC
  if (norm.includes("hoa")) {
    return (
      <svg
        aria-hidden="true"
        className={`pointer-events-none absolute inset-0 h-full w-full ${className}`}
        style={{ opacity }}
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 400 240"
        preserveAspectRatio="none"
      >
        {/* Hexagonal molecular lattice */}
        <g stroke="currentColor" strokeWidth="1.2" fill="none">
          <polygon points="60,60 90,42 120,60 120,95 90,112 60,95" />
          <polygon points="120,60 150,42 180,60 180,95 150,112 120,95" strokeDasharray="3 2" />
          <polygon points="90,112 120,95 120,130 90,147 60,130 60,95" />
          <polygon points="250,110 280,92 310,110 310,145 280,162 250,145" />
        </g>
        {/* Atomic Bond Nodes */}
        <circle cx="90" cy="42" r="3.5" fill="currentColor" />
        <circle cx="150" cy="42" r="3.5" fill="currentColor" />
        <circle cx="120" cy="95" r="4.5" fill="currentColor" />
        <circle cx="280" cy="92" r="3.5" fill="currentColor" />
        <circle cx="310" cy="145" r="3.5" fill="currentColor" />
      </svg>
    );
  }

  // SINH HỌC
  if (norm.includes("sinh")) {
    return (
      <svg
        aria-hidden="true"
        className={`pointer-events-none absolute inset-0 h-full w-full ${className}`}
        style={{ opacity }}
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 400 240"
        preserveAspectRatio="none"
      >
        {/* Organic branching paths & cellular nodes */}
        <path d="M 40 200 C 100 180, 140 120, 200 120 C 260 120, 300 60, 360 40" fill="none" stroke="currentColor" strokeWidth="1.6" />
        <path d="M 200 120 C 230 150, 270 170, 320 180" fill="none" stroke="currentColor" strokeWidth="1.2" strokeDasharray="3 3" />
        <circle cx="140" cy="145" r="14" fill="none" stroke="currentColor" strokeWidth="1" />
        <circle cx="140" cy="145" r="4" fill="currentColor" />
        <circle cx="280" cy="90" r="18" fill="none" stroke="currentColor" strokeWidth="1" />
        <circle cx="280" cy="90" r="5" fill="currentColor" />
      </svg>
    );
  }

  // NGỮ VĂN
  if (norm.includes("van") || norm.includes("ngu van")) {
    return (
      <svg
        aria-hidden="true"
        className={`pointer-events-none absolute inset-0 h-full w-full ${className}`}
        style={{ opacity }}
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 400 240"
        preserveAspectRatio="none"
      >
        {/* Editorial underline strokes and quote shapes */}
        <path d="M 40 50 Q 80 40, 120 50" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
        <line x1="40" y1="90" x2="340" y2="90" stroke="currentColor" strokeWidth="0.8" strokeDasharray="8 6" />
        <line x1="40" y1="125" x2="280" y2="125" stroke="currentColor" strokeWidth="0.8" strokeDasharray="8 6" />
        <line x1="40" y1="160" x2="320" y2="160" stroke="currentColor" strokeWidth="0.8" strokeDasharray="8 6" />
        <path d="M 320 40 C 335 45, 340 60, 335 75" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
        <path d="M 335 40 C 350 45, 355 60, 350 75" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
      </svg>
    );
  }

  // TIẾNG ANH
  if (norm.includes("anh") || norm.includes("english")) {
    return (
      <svg
        aria-hidden="true"
        className={`pointer-events-none absolute inset-0 h-full w-full ${className}`}
        style={{ opacity }}
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 400 240"
        preserveAspectRatio="none"
      >
        {/* Speech fragment bubble and phonetic bracket markers */}
        <path d="M 60 50 L 180 50 A 10 10 0 0 1 190 60 L 190 100 A 10 10 0 0 1 180 110 L 90 110 L 70 125 L 75 110 L 60 110 A 10 10 0 0 1 50 100 L 50 60 A 10 10 0 0 1 60 50 Z" fill="none" stroke="currentColor" strokeWidth="1.2" />
        <path d="M 230 70 L 220 70 L 220 130 L 230 130" fill="none" stroke="currentColor" strokeWidth="1.5" />
        <path d="M 330 70 L 340 70 L 340 130 L 330 130" fill="none" stroke="currentColor" strokeWidth="1.5" />
        <line x1="240" y1="100" x2="320" y2="100" stroke="currentColor" strokeWidth="1" strokeDasharray="4 3" />
      </svg>
    );
  }

  // DEFAULT / GENERAL KNOWLEDGE ATLAS
  return (
    <svg
      aria-hidden="true"
      className={`pointer-events-none absolute inset-0 h-full w-full ${className}`}
      style={{ opacity }}
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 400 240"
      preserveAspectRatio="none"
    >
      <circle cx="80" cy="60" r="3" fill="currentColor" />
      <circle cx="200" cy="100" r="4" fill="currentColor" />
      <circle cx="320" cy="60" r="3" fill="currentColor" />
      <line x1="80" y1="60" x2="200" y2="100" stroke="currentColor" strokeWidth="1" strokeDasharray="4 3" />
      <line x1="200" y1="100" x2="320" y2="60" stroke="currentColor" strokeWidth="1" strokeDasharray="4 3" />
    </svg>
  );
};
