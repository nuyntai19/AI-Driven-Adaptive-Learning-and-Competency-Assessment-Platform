import { useEffect, useRef } from "react";

interface UseModalAccessibilityOptions {
  isOpen: boolean;
  onClose: () => void;
  containerRef: React.RefObject<HTMLElement | null>;
  initialFocusRef?: React.RefObject<HTMLElement | null>;
}

export function useModalAccessibility({
  isOpen,
  onClose,
  containerRef,
  initialFocusRef,
}: UseModalAccessibilityOptions) {
  const previousActiveElement = useRef<HTMLElement | null>(null);
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;

  const wasOpenRef = useRef(false);

  // Focus & Scroll Lock Effect
  useEffect(() => {
    if (!isOpen || typeof window === "undefined" || typeof document === "undefined") {
      if (wasOpenRef.current) {
        // Modal just closed: restore focus to previous trigger element
        wasOpenRef.current = false;
        if (previousActiveElement.current && typeof previousActiveElement.current.focus === "function") {
          previousActiveElement.current.focus();
        }
      }
      return;
    }

    // Modal just opened: save previous active element
    if (!wasOpenRef.current) {
      if (document.activeElement instanceof HTMLElement) {
        previousActiveElement.current = document.activeElement;
      }
      wasOpenRef.current = true;
    }

    // Scroll lock on body
    const originalOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    // Set initial focus only on opening
    const focusTimer = setTimeout(() => {
      // If user is already focused on an element inside this modal, NEVER steal focus
      if (containerRef.current && document.activeElement && containerRef.current.contains(document.activeElement)) {
        return;
      }

      if (initialFocusRef?.current) {
        initialFocusRef.current.focus();
      } else if (containerRef.current) {
        // Prefer first editable input/select/textarea if available over close '✕' button
        const firstInput = containerRef.current.querySelector<HTMLElement>(
          'input:not([type="hidden"]):not([disabled]), textarea:not([disabled]), select:not([disabled])'
        );
        if (firstInput) {
          firstInput.focus();
        } else {
          const focusable = containerRef.current.querySelectorAll<HTMLElement>(
            'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"]):not([disabled])'
          );
          if (focusable.length > 0) {
            focusable[0].focus();
          } else {
            containerRef.current.focus();
          }
        }
      }
    }, 50);

    return () => {
      clearTimeout(focusTimer);
      document.body.style.overflow = originalOverflow;
    };
  }, [isOpen, containerRef, initialFocusRef]);

  // Keyboard trap (Escape & Tab cycling)
  useEffect(() => {
    if (!isOpen || typeof window === "undefined") return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        event.stopPropagation();
        onCloseRef.current();
        return;
      }

      if (event.key === "Tab" && containerRef.current) {
        const focusableElements = Array.from(
          containerRef.current.querySelectorAll<HTMLElement>(
            'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"]):not([disabled])'
          )
        ).filter((el) => el.offsetParent !== null || el.offsetWidth > 0 || el.offsetHeight > 0);

        if (focusableElements.length === 0) {
          event.preventDefault();
          return;
        }

        const firstElement = focusableElements[0];
        const lastElement = focusableElements[focusableElements.length - 1];

        if (event.shiftKey) {
          if (document.activeElement === firstElement || !containerRef.current.contains(document.activeElement)) {
            event.preventDefault();
            lastElement.focus();
          }
        } else {
          if (document.activeElement === lastElement || !containerRef.current.contains(document.activeElement)) {
            event.preventDefault();
            firstElement.focus();
          }
        }
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => {
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen, containerRef]);
}
