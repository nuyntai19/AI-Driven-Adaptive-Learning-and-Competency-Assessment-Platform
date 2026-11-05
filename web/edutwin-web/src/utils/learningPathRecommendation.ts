import type { LearningInsightsDto, SkillCapabilityStatusDto } from "../types/learningPath";

export function recommendLearningGoal(subject: string, skills: SkillCapabilityStatusDto[], insights?: LearningInsightsDto) {
  const observations = insights?.skills ?? [];
  const weak = observations.filter(s => s.wrongCount > 0).sort((a, b) => b.wrongCount - a.wrongCount || b.questionCount - a.questionCount).slice(0, 3);
  const strong = skills.filter(s => s.state === "Confirmed" && observations.some(o => o.skillId === s.skillId && o.questionCount >= 5 && o.correctCount / o.questionCount >= .8)).slice(0, 3);
  const goalType = weak.length ? "Foundation" : strong.length ? "Advanced" : "Diagnostic";
  const targets = weak.length ? weak.map(s => s.skillId) : strong.length ? strong.map(s => s.skillId)
    : [...observations.filter(s => s.questionCount > 0).map(s => s.skillId), ...skills.filter(s => s.state !== "Confirmed").map(s => s.skillId)].filter((id, index, list) => list.indexOf(id) === index).slice(0, 3);
  const names = targets.map(id => skills.find(s => s.skillId === id)?.skillName).filter(Boolean).join(", ");
  return {
    goalType, targetSkillIds: targets,
    title: `${goalType === "Advanced" ? "Thử thách nâng cao" : goalType === "Foundation" ? "Củng cố phần còn vướng" : "Xác định điểm xuất phát"} · ${subject}`,
    observation: weak.length ? weak.map(s => `${s.skillName}: sai ${s.wrongCount}/${s.questionCount} câu khác nhau ở lần làm gần nhất.`).join(" ")
      : strong.length ? `Đã làm vững ${names} qua bài kiểm tra; đề xuất thử sức với câu khó hơn.`
        : `Có ${insights?.gradedQuestionCount ?? 0} câu khác nhau đã chấm. Chưa đủ cơ sở để kết luận toàn môn đã tốt hoặc cần học lại nền tảng.`,
    improvement: goalType === "Advanced" ? "Vận dụng kỹ năng đã nắm vào câu độ khó 4–5/5, giảm luyện lại câu cơ bản."
      : goalType === "Foundation" ? "Sửa phần từng trả lời sai, luyện theo chuỗi rồi tự kiểm tra bằng câu mới."
        : "Kiểm tra phần đã biết trước; chỉ củng cố chỗ còn vướng, tránh học lại toàn bộ.",
    focus: names || "Các kỹ năng đang có trong giáo trình",
    minutes: 15, days: 5,
  };
}
