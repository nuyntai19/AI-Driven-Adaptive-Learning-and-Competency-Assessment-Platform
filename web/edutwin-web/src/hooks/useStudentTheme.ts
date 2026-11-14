import { useEffect } from "react";

const ROOT_CLASS = "student-theme";
let mountedSurfaces = 0;

/**
 * Applies the student palette to <html> while a student surface is mounted, so
 * content rendered through portals (modals, toasts, image viewers) matches the
 * page. Reference-counted because the layout and full-screen players can overlap
 * during route changes. Also drives the pointer spotlight on `.st-spot` cards.
 */
export function useStudentTheme(): void {
  useEffect(() => {
    const root = document.documentElement;
    mountedSurfaces += 1;
    root.classList.add(ROOT_CLASS);

    const onPointerMove = (event: PointerEvent) => {
      const target = event.target instanceof Element ? event.target.closest<HTMLElement>(".st-spot") : null;
      if (!target) return;
      const rect = target.getBoundingClientRect();
      target.style.setProperty("--mx", `${event.clientX - rect.left}px`);
      target.style.setProperty("--my", `${event.clientY - rect.top}px`);
    };
    window.addEventListener("pointermove", onPointerMove, { passive: true });

    return () => {
      window.removeEventListener("pointermove", onPointerMove);
      mountedSurfaces = Math.max(0, mountedSurfaces - 1);
      if (mountedSurfaces === 0) root.classList.remove(ROOT_CLASS);
    };
  }, []);
}
