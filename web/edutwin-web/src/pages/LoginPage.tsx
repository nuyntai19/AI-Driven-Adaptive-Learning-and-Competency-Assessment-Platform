import React, { useState, useEffect } from "react";
import { useNavigate, Navigate } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { login, getCurrentUser } from "../auth/authApi";
import { useAuthStore } from "../stores/authStore";
import { mapSafeLoginError } from "../utils/problemDetails";

interface HeroSlide {
  badge: string;
  badgeColor: string;
  title: string;
  subtitle: string;
  description: string;
  bgGradient: string;
  glow1: string;
  glow2: string;
  pillars: Array<{
    title: string;
    desc: string;
    icon: string;
    iconBg: string;
  }>;
}

const HERO_SLIDES: HeroSlide[] = [
  {
    badge: "Mô hình Digital Twin & Tri thức số",
    badgeColor: "bg-amber-300",
    title: "Chuyển đổi số giáo dục với mô hình Digital Twin",
    subtitle: "Mô phỏng bản sao số tri thức người học theo thời gian thực",
    description:
      "Hệ thống liên tục cập nhật trạng thái nhận thức, ghi nhận từng phản hồi bài tập để tái hiện sống động năng lực người học qua đồ thị tri thức đa chiều.",
    bgGradient: "from-[#A81700] via-[#CB1C01] to-[#E8481A]",
    glow1: "bg-amber-300/30",
    glow2: "bg-orange-200/25",
    pillars: [
      {
        title: "Multi-Tenant Isolation",
        desc: "Cách ly hoàn toàn dữ liệu giữa các trung tâm đối tác và không gian nền tảng.",
        icon: "M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z",
        iconBg: "bg-white/15 text-white",
      },
      {
        title: "Knowledge Graph Engine",
        desc: "Mô hình hóa cấu trúc tri thức đa tầng theo cây kỹ năng và bản đồ quan hệ chuẩn đầu ra.",
        icon: "M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z",
        iconBg: "bg-white/15 text-white",
      },
      {
        title: "Real-Time Competency Sync",
        desc: "Đồng bộ tức thì tiến trình làm chủ bài học sau mỗi lượt tương tác và kiểm tra nhanh.",
        icon: "M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15",
        iconBg: "bg-white/15 text-white",
      },
    ],
  },
  {
    badge: "AI-Driven Adaptive Learning & IRT",
    badgeColor: "bg-amber-300",
    title: "Cá nhân hóa lộ trình học tập theo năng lực thực tế",
    subtitle: "Thuật toán thích ứng tối ưu hóa theo mô hình Rasch & IRT",
    description:
      "Tự động phát hiện điểm nghẽn kiến thức, điều chỉnh độ khó bài tập phù hợp với vùng phát triển gần nhất (ZPD) của từng học sinh.",
    bgGradient: "from-[#8A1300] via-[#B81A02] to-[#F2581D]",
    glow1: "bg-amber-300/30",
    glow2: "bg-orange-200/25",
    pillars: [
      {
        title: "Adaptive Question Routing",
        desc: "Lựa chọn câu hỏi thông minh giúp xác định trình độ chính xác với số lượng câu tối thiểu.",
        icon: "M13 10V3L4 14h7v7l9-11h-7z",
        iconBg: "bg-white/15 text-white",
      },
      {
        title: "Knowledge State Tracing",
        desc: "Lập bản đồ chủ đề kiến thức liên tục theo ma trận kỹ năng và thang đo Bloom.",
        icon: "M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z",
        iconBg: "bg-white/15 text-white",
      },
      {
        title: "Remedial Recommendations",
        desc: "Đề xuất bài giảng và tài liệu củng cố ngay khi phát hiện lỗ hổng kỹ năng.",
        icon: "M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253",
        iconBg: "bg-white/15 text-white",
      },
    ],
  },
  {
    badge: "Durable Interactive Examination",
    badgeColor: "bg-amber-300",
    title: "Khảo thí thông minh & Bảng vẽ nháp Vector",
    subtitle: "Chống mất bài làm ngay cả khi gặp sự cố ngắt kết nối mạng",
    description:
      "Tích hợp bảng nháp vector số hóa có khả năng tự lưu IndexedDB cục bộ, bảo vệ toàn vẹn bài làm của thí sinh trong suốt quá trình thi.",
    bgGradient: "from-[#B41C06] via-[#D9360E] to-[#F27A1D]",
    glow1: "bg-amber-300/30",
    glow2: "bg-orange-200/25",
    pillars: [
      {
        title: "Offline-Resilient Scratchpad",
        desc: "Lưu bản vẽ nháp vector vào IndexedDB an toàn, tự động khôi phục khi tải lại trang.",
        icon: "M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z",
        iconBg: "bg-white/15 text-white",
      },
      {
        title: "Anti-Tamper Audit Trail",
        desc: "Ghi nhận tiến trình làm bài bất biến, phát hiện bất thường và bảo toàn kết quả.",
        icon: "M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z",
        iconBg: "bg-white/15 text-white",
      },
      {
        title: "Instant Diagnostic Analytics",
        desc: "Phân tích phổ điểm, độ phân biệt câu hỏi và mức độ làm chủ chuẩn đầu ra.",
        icon: "M11 3.055A9.001 9.001 0 1020.945 13H11V3.055z",
        iconBg: "bg-white/15 text-white",
      },
    ],
  },
  {
    badge: "Multi-Tenant Platform Control Plane",
    badgeColor: "bg-amber-300",
    title: "Quản trị vận hành đối tác & Tenant Control Plane",
    subtitle: "Ranh giới phân định rõ ràng giữa hạ tầng nền tảng và nghiệp vụ trung tâm",
    description:
      "Bảng điều khiển tập trung cho phép thiết lập trung tâm mới, quản lý vòng đời tài khoản đối tác, thu hồi phiên bảo mật và theo dõi kiểm toán.",
    bgGradient: "from-[#9E1A05] via-[#C42A08] to-[#E9661C]",
    glow1: "bg-amber-300/30",
    glow2: "bg-orange-200/25",
    pillars: [
      {
        title: "Partner Tenant Lifecycle",
        desc: "Khởi tạo, cấu hình, tạm ngưng và kích hoạt trung tâm đối tác linh hoạt.",
        icon: "M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4",
        iconBg: "bg-white/15 text-white",
      },
      {
        title: "Personnel Access Delegation",
        desc: "Ủy quyền phân cấp quản trị viên đối tác và điều phối nhân sự minh bạch.",
        icon: "M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z",
        iconBg: "bg-white/15 text-white",
      },
      {
        title: "Quota & Subdomain Controls",
        desc: "Quản lý hạn ngạch học viên, bộ nhớ lưu trữ và định danh tenant độc lập.",
        icon: "M3 6l3 1m0 0l-3 9a5.002 5.002 0 006.001 0M6 7l3 9M6 7l6-2m6 2l3-1m-3 1l-3 9a5.002 5.002 0 006.001 0M18 7l3 9m-3-9l-6-2m0-2v2m0 16V5m0 16H9m3 0h3",
        iconBg: "bg-white/15 text-white",
      },
    ],
  },
  {
    badge: "Zero-Trust Security & Data Sanitization",
    badgeColor: "bg-amber-300",
    title: "Bảo mật Zero-Trust & Khử khuẩn dữ liệu kiểm toán",
    subtitle: "Bảo vệ toàn vẹn dữ liệu nhạy cảm theo chuẩn doanh nghiệp",
    description:
      "Cơ chế kiểm toán thông minh tự động khử khuẩn mật khẩu và khóa bí mật, kết hợp cùng năng lực thu hồi phiên làm việc tức thì trên toàn hệ sinh thái.",
    bgGradient: "from-[#A01404] via-[#C81F05] to-[#F0521B]",
    glow1: "bg-amber-300/30",
    glow2: "bg-orange-200/25",
    pillars: [
      {
        title: "Capability-Based Security",
        desc: "Bảo vệ từng endpoint bằng mã quyền năng lực nguyên tử, chặn đứng rủi ro leo thang đặc quyền.",
        icon: "M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z",
        iconBg: "bg-white/15 text-white",
      },
      {
        title: "Sanitized Audit Trail",
        desc: "Tự động khử khuẩn mật khẩu và token trước khi ghi vào nhật ký hệ thống bất biến.",
        icon: "M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2",
        iconBg: "bg-white/15 text-white",
      },
      {
        title: "Instant Session Revocation",
        desc: "Thu hồi phiên làm việc và khóa tài khoản tức thì trên toàn hệ thống khi phát hiện rủi ro.",
        icon: "M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1",
        iconBg: "bg-white/15 text-white",
      },
    ],
  },
  {
    badge: "OCC Concurrency & High Performance",
    badgeColor: "bg-amber-300",
    title: "Kiểm soát tranh chấp cao & Tối ưu hóa hiệu năng",
    subtitle: "Đảm bảo tính nhất quán dữ liệu tuyệt đối dưới tải truy cập lớn",
    description:
      "Kiến trúc OCC Concurrency sử dụng RowVersion ngăn chặn triệt để tình trạng ghi đè dữ liệu, đảm bảo thời gian phản hồi API dưới 50ms.",
    bgGradient: "from-[#8F1503] via-[#C01D03] to-[#EF6A24]",
    glow1: "bg-amber-300/30",
    glow2: "bg-orange-200/25",
    pillars: [
      {
        title: "Optimistic Concurrency Control",
        desc: "Bảo vệ thông tin trung tâm và tài khoản chống xung đột đa luồng đồng thời.",
        icon: "M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15",
        iconBg: "bg-white/15 text-white",
      },
      {
        title: "High-Throughput Submissions",
        desc: "Xử lý hàng nghìn lượt nộp bài thi cùng lúc mà không nghẽn tài nguyên cơ sở dữ liệu.",
        icon: "M13 10V3L4 14h7v7l9-11h-7z",
        iconBg: "bg-white/15 text-white",
      },
      {
        title: "In-Memory Caching Acceleration",
        desc: "Tăng tốc độ hiển thị và điều hướng trang với cơ chế tiền nạp TanStack Query tối ưu.",
        icon: "M5 3v4M3 5h4M6 17v4m-2-2h4m5-16l2.286 6.857L21 12l-5.714 2.143L13 21l-2.286-6.857L5 12l5.714-2.143L13 3z",
        iconBg: "bg-white/15 text-white",
      },
    ],
  },
];

export const LoginPage = () => {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const sessionStatus = useAuthStore((state) => state.sessionStatus);
  const user = useAuthStore((state) => state.user);
  const clearSession = useAuthStore((state) => state.clearSession);

  const [centerCode, setCenterCode] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [errorMsg, setErrorMsg] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  // Carousel Accessibility & Motion States
  const [activeSlide, setActiveSlide] = useState(0);
  const [isHovered, setIsHovered] = useState(false);
  const [isFocused, setIsFocused] = useState(false);
  const [isManuallyPaused, setIsManuallyPaused] = useState(false);
  const [prefersReducedMotion, setPrefersReducedMotion] = useState(false);

  useEffect(() => {
    const mediaQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
    setPrefersReducedMotion(mediaQuery.matches);

    const handler = (e: MediaQueryListEvent) => setPrefersReducedMotion(e.matches);
    mediaQuery.addEventListener("change", handler);
    return () => mediaQuery.removeEventListener("change", handler);
  }, []);

  const isPaused = isHovered || isFocused || isManuallyPaused || prefersReducedMotion;

  useEffect(() => {
    if (isPaused) return;
    const timer = setInterval(() => {
      setActiveSlide((prev) => (prev + 1) % HERO_SLIDES.length);
    }, 3000);

    return () => clearInterval(timer);
  }, [isPaused]);

  if (sessionStatus === "authenticated" && !isLoading) {
    if (user?.accountType === "CenterManager") {
      return <Navigate to="/quan-ly/tong-quan-trung-tam" replace />;
    }
    if (user?.accountType === "Student") {
      return <Navigate to="/hoc-tap/tong-quan" replace />;
    }
    if (user?.accountType === "Teacher") {
      return <Navigate to="/giao-vien/lop-hoc" replace />;
    }
    return <Navigate to="/" replace />;
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg("");

    const trimmedCenterCode = centerCode.trim();
    const trimmedUsername = username.trim();

    if (!trimmedCenterCode || !trimmedUsername || !password) {
      setErrorMsg("Vui lòng điền đầy đủ mã trung tâm, tên đăng nhập và mật khẩu.");
      return;
    }

    setIsLoading(true);
    try {
      queryClient.clear();
      await login({
        centerCode: trimmedCenterCode,
        username: trimmedUsername,
        password,
      });
      await getCurrentUser();
      const currentUser = useAuthStore.getState().user;
      if (currentUser?.accountType === "CenterManager") {
        navigate("/quan-ly/tong-quan-trung-tam", { replace: true });
      } else if (currentUser?.accountType === "Student") {
        navigate("/hoc-tap/tong-quan", { replace: true });
      } else if (currentUser?.accountType === "Teacher") {
        navigate("/giao-vien/lop-hoc", { replace: true });
      } else {
        navigate("/", { replace: true });
      }
    } catch (error) {
      clearSession();
      queryClient.clear();
      setErrorMsg(mapSafeLoginError(error));
    } finally {
      setIsLoading(false);
    }
  };

  const currentSlide = HERO_SLIDES[activeSlide];

  return (
    <div className="min-h-screen flex flex-col lg:flex-row bg-[#FEFCF9] text-[#15110E] [font-family:'Be_Vietnam_Pro',system-ui,sans-serif]">
      {/* Top Mobile Bar (ThemeToggle removed as requested) */}
      <div className="lg:hidden flex items-center justify-between px-6 py-4 border-b border-[#EEE7DC] bg-white/90 backdrop-blur">
        <div className="flex items-center gap-2.5">
          <div className="w-8 h-8 rounded-[10px] bg-[linear-gradient(135deg,#CB1C01,#F2581D)] flex items-center justify-center text-white font-black text-base">
            E
          </div>
          <span className="font-extrabold tracking-tight [font-family:Montserrat,'Be_Vietnam_Pro',system-ui,sans-serif]">Edu<span className="text-[#CB1C01]">Twin</span></span>
        </div>
      </div>

      {/* Left Hero Panel (Desktop Split-Screen with Auto-Sliding Carousel & Ambient Morphing Background) */}
      <div
        role="region"
        aria-label="Giới thiệu kiến trúc nền tảng EduTwin"
        aria-roledescription="carousel"
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
        onFocus={() => setIsFocused(true)}
        onBlur={(e) => {
          if (!e.currentTarget.contains(e.relatedTarget)) {
            setIsFocused(false);
          }
        }}
        className={`relative hidden lg:flex lg:w-1/2 flex-col justify-between p-10 xl:p-14 overflow-hidden text-white select-none transition-all duration-1000 ease-in-out bg-gradient-to-br ${currentSlide.bgGradient}`}
      >
        <style>{`
          @keyframes heroProgress {
            0% { width: 0%; }
            100% { width: 100%; }
          }
        `}</style>
        {/* Dynamic ambient glowing orbs with smooth transition */}
        <div
          className={`absolute -top-32 -left-32 w-[34rem] h-[34rem] rounded-full blur-3xl pointer-events-none transition-all duration-1000 ease-in-out ${currentSlide.glow1}`}
        />
        <div
          className={`absolute -bottom-32 -right-32 w-[34rem] h-[34rem] rounded-full blur-3xl pointer-events-none transition-all duration-1000 ease-in-out ${currentSlide.glow2}`}
        />
        <div className="absolute inset-0 pointer-events-none bg-[radial-gradient(60%_50%_at_100%_100%,rgba(255,255,255,0.12),transparent_70%)]" />
        <div aria-hidden="true" className="absolute -right-24 -bottom-24 h-96 w-96 rounded-full border-[48px] border-white/10 pointer-events-none" />

        {/* Centered Content Wrapper (Horizontally & Vertically centered within the left 50% pane) */}
        <div className="relative z-10 w-full max-w-xl mx-auto h-full flex flex-col justify-between">
          {/* Top Branding */}
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-white flex items-center justify-center text-[#CB1C01] font-black text-xl shadow-lg shadow-black/10">
                E
              </div>
              <div>
                <span className="text-xl font-extrabold tracking-tight text-white [font-family:Montserrat,'Be_Vietnam_Pro',system-ui,sans-serif]">EduTwin</span>
                <span className="ml-2 text-xs font-semibold px-2 py-0.5 rounded-full bg-white/15 text-white border border-white/25">
                  Multi-Tenant Cloud
                </span>
              </div>
            </div>
          </div>

          {/* Center Hero Content (Full Page Animated Slider) */}
          <div className="my-auto py-6 w-full">
            {/* Overflow-hidden track container */}
            <div className="overflow-hidden w-full">
              <div
                className={`flex ${
                  prefersReducedMotion
                    ? "transition-none"
                    : "transition-transform duration-700 ease-[cubic-bezier(0.2,0.8,0.2,1)]"
                }`}
                style={{ transform: `translateX(-${activeSlide * 100}%)` }}
              >
                {HERO_SLIDES.map((slide, idx) => {
                  const isActive = idx === activeSlide;
                  return (
                    <div
                      key={idx}
                      className={`w-full shrink-0 transition-all duration-700 px-0.5 ${
                        isActive
                          ? "opacity-100 scale-100"
                          : "opacity-20 scale-[0.97] pointer-events-none"
                      }`}
                    >
                      <div className="space-y-4">
                        <div className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-white/15 border border-white/25 text-xs text-white backdrop-blur-sm">
                          <span className={`w-2 h-2 rounded-full ${slide.badgeColor} animate-pulse`} />
                          <span>{slide.badge}</span>
                        </div>

                        <div className="space-y-1.5">
                          <h1 className="text-3xl sm:text-4xl font-extrabold text-white leading-tight tracking-tight [font-family:Montserrat,'Be_Vietnam_Pro',system-ui,sans-serif]">
                            {slide.title}
                          </h1>
                          <p className="text-sm font-semibold text-amber-200">
                            {slide.subtitle}
                          </p>
                        </div>

                        <p className="text-white/85 text-xs sm:text-sm leading-relaxed min-h-[44px]">
                          {slide.description}
                        </p>

                        {/* 3 Architecture Value Pillars for this slide */}
                        <div className="space-y-2.5 pt-1">
                          {slide.pillars.map((pillar, pIdx) => (
                            <div
                              key={pIdx}
                              className="flex items-start gap-3 p-3.5 rounded-2xl bg-white/10 border border-white/15 hover:bg-white/15 hover:-translate-y-0.5 transition-all backdrop-blur-sm"
                            >
                              <div className={`w-8 h-8 rounded-lg ${pillar.iconBg} flex items-center justify-center shrink-0 mt-0.5`}>
                                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d={pillar.icon} />
                                </svg>
                              </div>
                              <div>
                                <h2 className="text-xs font-semibold text-white tracking-wide">{pillar.title}</h2>
                                <p className="text-xs text-white/75 mt-0.5 leading-relaxed">{pillar.desc}</p>
                              </div>
                            </div>
                          ))}
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Stepper Indicator Track with Pause/Play accessibility toggle */}
            <div className="pt-6 w-full flex items-center gap-3">
              <button
                type="button"
                onClick={() => setIsManuallyPaused((prev) => !prev)}
                aria-label={isManuallyPaused ? "Tiếp tục tự động chuyển slide" : "Tạm dừng tự động chuyển slide"}
                aria-pressed={isManuallyPaused}
                title={isManuallyPaused ? "Tiếp tục phát slide (Play)" : "Tạm dừng tự động chuyển (Pause)"}
                className="p-1 rounded-md text-white/70 hover:text-white hover:bg-white/15 transition-colors focus:outline-none focus:ring-2 focus:ring-white shrink-0"
              >
                {isManuallyPaused ? (
                  <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z" />
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                ) : (
                  <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 9v6m4-6v6m7-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                )}
              </button>

              <div className="flex items-center flex-1" role="tablist" aria-label="Các trang giới thiệu năng lực">
                {HERO_SLIDES.map((slide, idx) => {
                  const isCurrent = idx === activeSlide;
                  return (
                    <div key={idx} className="flex-1 flex items-center">
                      {/* Step Dot */}
                      <button
                        type="button"
                        role="tab"
                        aria-selected={isCurrent}
                        aria-label={`Trang ${idx + 1} / ${HERO_SLIDES.length}: ${slide.title}`}
                        onClick={() => {
                          setActiveSlide(idx);
                        }}
                        className={`relative z-10 w-2.5 h-2.5 rounded-full transition-all duration-300 cursor-pointer shrink-0 focus:outline-none focus:ring-2 focus:ring-white ${
                          isCurrent
                            ? "bg-white ring-4 ring-white/30 scale-125"
                            : "bg-white/40 hover:bg-white/70"
                        }`}
                      />

                      {/* Connecting Line / Progress Bar to next step */}
                      <div className="flex-1 h-[2px] bg-white/25 mx-1.5 relative overflow-hidden rounded-full">
                        <div
                          key={`${idx}-${activeSlide}-${isPaused}`}
                          className={`h-full ${
                            isCurrent
                              ? "bg-white"
                              : "bg-transparent"
                          }`}
                          style={{
                            width: isCurrent && (isPaused || prefersReducedMotion) ? "50%" : isCurrent ? "100%" : "0%",
                            animation: isCurrent && !isPaused && !prefersReducedMotion ? "heroProgress 3000ms linear forwards" : "none",
                          }}
                        />
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          </div>

          {/* Footer info */}
          <div className="text-xs text-white/70 pt-2">
            EduTwin Platform Control Plane · Phiên bản v1.0.0
          </div>
        </div>
      </div>

      {/* Right Form Card */}
      <div className="flex-1 flex items-center justify-center p-6 sm:p-10 lg:p-16 bg-[radial-gradient(70%_60%_at_100%_0%,#FBDCCF,transparent_70%),radial-gradient(50%_50%_at_0%_100%,#FEF4DA,transparent_70%)]">
        <div className="w-full max-w-md space-y-8 bg-white border border-[#EEE7DC] p-8 sm:p-10 rounded-[24px] shadow-[0_30px_60px_-28px_rgba(21,17,14,0.35)]">
          <div>
            <h2 className="text-2xl font-extrabold tracking-tight text-[#15110E] [font-family:Montserrat,'Be_Vietnam_Pro',system-ui,sans-serif]">
              Đăng nhập hệ thống
            </h2>
            <p className="mt-2 text-xs sm:text-sm text-[#7A6D60]">
              Nhập mã định danh trung tâm và thông tin tài khoản được cấp.
            </p>
          </div>

          {errorMsg && (
            <div
              role="alert"
              className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-800 text-xs sm:text-sm flex items-start gap-2.5"
            >
              <span aria-hidden="true" className="text-base leading-none">!</span>
              <span className="flex-1">{errorMsg}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label
                htmlFor="centerCode"
                className="block text-xs font-bold uppercase tracking-wider text-[#4B4138] mb-1.5"
              >
                Mã trung tâm <span className="text-[#CB1C01]">*</span>
              </label>
              <input
                id="centerCode"
                name="centerCode"
                type="text"
                required
                autoComplete="organization"
                placeholder="Ví dụ: PLATFORM hoặc mã trung tâm"
                value={centerCode}
                onChange={(e) => setCenterCode(e.target.value)}
                className="w-full bg-white border border-[#E2D8C9] rounded-xl px-3.5 py-2.5 text-sm text-[#15110E] placeholder-[#A99C8C] focus:outline-none focus:border-[#CB1C01] focus:ring-4 focus:ring-[#FDEDE7] transition-all"
              />
              <p className="mt-1 text-[11px] text-[#7A6D60]">
                Nhập <span className="font-mono font-semibold text-[#15110E]">PLATFORM</span> nếu bạn là Quản trị viên nền tảng.
              </p>
            </div>

            <div>
              <label
                htmlFor="username"
                className="block text-xs font-bold uppercase tracking-wider text-[#4B4138] mb-1.5"
              >
                Tên đăng nhập <span className="text-[#CB1C01]">*</span>
              </label>
              <input
                id="username"
                name="username"
                type="text"
                required
                autoComplete="username"
                placeholder="Tên tài khoản..."
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                className="w-full bg-white border border-[#E2D8C9] rounded-xl px-3.5 py-2.5 text-sm text-[#15110E] placeholder-[#A99C8C] focus:outline-none focus:border-[#CB1C01] focus:ring-4 focus:ring-[#FDEDE7] transition-all"
              />
            </div>

            <div>
              <label
                htmlFor="password"
                className="block text-xs font-bold uppercase tracking-wider text-[#4B4138] mb-1.5"
              >
                Mật khẩu <span className="text-[#CB1C01]">*</span>
              </label>
              <div className="relative">
                <input
                  id="password"
                  name="password"
                  type={showPassword ? "text" : "password"}
                  required
                  autoComplete="current-password"
                  placeholder="••••••••••••"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="w-full bg-white border border-[#E2D8C9] rounded-xl px-3.5 py-2.5 pr-11 text-sm text-[#15110E] placeholder-[#A99C8C] focus:outline-none focus:border-[#CB1C01] focus:ring-4 focus:ring-[#FDEDE7] transition-all"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                  aria-pressed={showPassword}
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 p-1.5 text-[#7A6D60] hover:text-[#15110E] rounded-lg hover:bg-[#F7F2EA] transition-colors"
                >
                  {showPassword ? (
                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={1.75}
                        d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l18 18"
                      />
                    </svg>
                  ) : (
                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={1.75}
                        d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"
                      />
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={1.75}
                        d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"
                      />
                    </svg>
                  )}
                </button>
              </div>
            </div>

            <button
              type="submit"
              disabled={isLoading}
              className="mt-6 w-full flex items-center justify-center gap-2 rounded-full bg-[linear-gradient(100deg,#CB1C01,#F2581D)] shadow-[0_10px_24px_-10px_rgba(203,28,1,0.55)] hover:brightness-105 hover:-translate-y-px px-4 py-3 text-sm font-bold text-white [font-family:Montserrat,'Be_Vietnam_Pro',system-ui,sans-serif] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#CB1C01] disabled:opacity-50 transition-all cursor-pointer"
            >
              {isLoading ? (
                <>
                  <svg className="animate-spin h-4 w-4 text-white" viewBox="0 0 24 24" fill="none">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                  </svg>
                  <span>Đang xác thực...</span>
                </>
              ) : (
                <span>Đăng nhập</span>
              )}
            </button>
          </form>

          <div className="pt-4 border-t border-[#EEE7DC] text-center text-xs text-[#7A6D60]">
            Truy cập được bảo mật bằng JWT và mã hóa dữ liệu đầu cuối.
          </div>
        </div>
      </div>
    </div>
  );
};
