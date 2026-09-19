export interface SubjectThemeConfig {
  key: string;
  name: string;
  color: string;
  darkColor: string;
  softBg: string;
  darkSoftBg: string;
  borderColor: string;
  darkBorderColor: string;
  initials: string;
  bg?: string;
  border?: string;
}

const PRESET_SUBJECTS: Record<string, SubjectThemeConfig> = {
  toan: {
    key: "cobalt",
    name: "Toán",
    color: "#3157D5",
    darkColor: "#5C83F6",
    softBg: "#EEF3FF",
    darkSoftBg: "#1B2B50",
    borderColor: "#C8D8FA",
    darkBorderColor: "#2E457D",
    initials: "TO",
  },
  van: {
    key: "berry",
    name: "Ngữ văn",
    color: "#B83268",
    darkColor: "#DE568E",
    softBg: "#FDF1F5",
    darkSoftBg: "#3E1627",
    borderColor: "#F7BFD2",
    darkBorderColor: "#6D2745",
    initials: "NV",
  },
  anh: {
    key: "teal",
    name: "Tiếng Anh",
    color: "#0B7A67",
    darkColor: "#1DBDA3",
    softBg: "#EBF7F4",
    darkSoftBg: "#133832",
    borderColor: "#B7E4DC",
    darkBorderColor: "#1C544B",
    initials: "TA",
  },
  ly: {
    key: "orange",
    name: "Vật lý",
    color: "#B85C00",
    darkColor: "#E68A2E",
    softBg: "#FFF5EB",
    darkSoftBg: "#3D2611",
    borderColor: "#FCD8B3",
    darkBorderColor: "#66411E",
    initials: "VL",
  },
  hoa: {
    key: "violet",
    name: "Hóa học",
    color: "#6B38C4",
    darkColor: "#956DF0",
    softBg: "#F5F0FF",
    darkSoftBg: "#281D47",
    borderColor: "#D8C5FA",
    darkBorderColor: "#4B3782",
    initials: "HH",
  },
  sinh: {
    key: "green",
    name: "Sinh học",
    color: "#16835B",
    darkColor: "#29AC7B",
    softBg: "#EDF8F3",
    darkSoftBg: "#143B2C",
    borderColor: "#BAE5D1",
    darkBorderColor: "#205A43",
    initials: "SH",
  },
  su: {
    key: "terracotta",
    name: "Lịch sử",
    color: "#A8422B",
    darkColor: "#D46950",
    softBg: "#FDF2EF",
    darkSoftBg: "#3B1C15",
    borderColor: "#F3C4B8",
    darkBorderColor: "#633125",
    initials: "LS",
  },
  dia: {
    key: "sky",
    name: "Địa lý",
    color: "#1E73BE",
    darkColor: "#4EA4ED",
    softBg: "#EFF6FC",
    darkSoftBg: "#162E45",
    borderColor: "#C2DCF4",
    darkBorderColor: "#274C70",
    initials: "ĐL",
  },
};

const FALLBACK_PALETTES: Omit<SubjectThemeConfig, "name" | "initials">[] = [
  {
    key: "cobalt",
    color: "#3157D5",
    darkColor: "#5C83F6",
    softBg: "#EEF3FF",
    darkSoftBg: "#1B2B50",
    borderColor: "#C8D8FA",
    darkBorderColor: "#2E457D",
  },
  {
    key: "berry",
    color: "#B83268",
    darkColor: "#DE568E",
    softBg: "#FDF1F5",
    darkSoftBg: "#3E1627",
    borderColor: "#F7BFD2",
    darkBorderColor: "#6D2745",
  },
  {
    key: "teal",
    color: "#0B7A67",
    darkColor: "#1DBDA3",
    softBg: "#EBF7F4",
    darkSoftBg: "#133832",
    borderColor: "#B7E4DC",
    darkBorderColor: "#1C544B",
  },
  {
    key: "orange",
    color: "#B85C00",
    darkColor: "#E68A2E",
    softBg: "#FFF5EB",
    darkSoftBg: "#3D2611",
    borderColor: "#FCD8B3",
    darkBorderColor: "#66411E",
  },
  {
    key: "violet",
    color: "#6B38C4",
    darkColor: "#956DF0",
    softBg: "#F5F0FF",
    darkSoftBg: "#281D47",
    borderColor: "#D8C5FA",
    darkBorderColor: "#4B3782",
  },
  {
    key: "green",
    color: "#16835B",
    darkColor: "#29AC7B",
    softBg: "#EDF8F3",
    darkSoftBg: "#143B2C",
    borderColor: "#BAE5D1",
    darkBorderColor: "#205A43",
  },
];

/**
 * Deterministically resolve a subject's visual identity.
 * Same subject name always yields the identical theme across renders and components.
 */
export function getSubjectTheme(subjectName?: string | null): SubjectThemeConfig {
  if (!subjectName || !subjectName.trim()) {
    return {
      key: "default",
      name: "Chung",
      color: "#6546D7",
      darkColor: "#856BEE",
      softBg: "#F0EDFD",
      darkSoftBg: "#241E47",
      borderColor: "#D7CFFB",
      darkBorderColor: "#4A3B8C",
      initials: "ALL",
    };
  }

  const normalized = subjectName
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "");

  const buildTheme = (base: SubjectThemeConfig): SubjectThemeConfig => ({
    ...base,
    name: subjectName,
    bg: base.softBg,
    border: base.borderColor,
  });

  if (normalized.includes("toan")) return buildTheme(PRESET_SUBJECTS.toan);
  if (normalized.includes("van") || normalized.includes("ngu van")) return buildTheme(PRESET_SUBJECTS.van);
  if (normalized.includes("anh") || normalized.includes("english")) return buildTheme(PRESET_SUBJECTS.anh);
  if (normalized.includes("ly") || normalized.includes("vat ly") || normalized.includes("vat li")) return buildTheme(PRESET_SUBJECTS.ly);
  if (normalized.includes("hoa")) return buildTheme(PRESET_SUBJECTS.hoa);
  if (normalized.includes("sinh")) return buildTheme(PRESET_SUBJECTS.sinh);
  if (normalized.includes("su") || normalized.includes("lich su")) return buildTheme(PRESET_SUBJECTS.su);
  if (normalized.includes("dia") || normalized.includes("dia ly")) return buildTheme(PRESET_SUBJECTS.dia);

  // Hash-based deterministic fallback for custom / unseen subjects
  let hash = 0;
  for (let i = 0; i < subjectName.length; i++) {
    hash = (hash << 5) - hash + subjectName.charCodeAt(i);
    hash |= 0;
  }
  const index = Math.abs(hash) % FALLBACK_PALETTES.length;
  const palette = FALLBACK_PALETTES[index];

  const words = subjectName.trim().split(/\s+/);
  const initials =
    words.length >= 2
      ? (words[0][0] + words[1][0]).toUpperCase()
      : subjectName.slice(0, 2).toUpperCase();

  return {
    ...palette,
    name: subjectName,
    initials,
    bg: palette.softBg,
    border: palette.borderColor,
  };
}
