import { useState, useSyncExternalStore } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { useAuthStore } from "../../stores/authStore";
import { notificationsApi } from "../../api/notificationsApi";
import { notificationAccess, notificationPollInterval } from "../../utils/notificationHelpers";

function subscribe(callback: () => void) {
  document.addEventListener("visibilitychange", callback);
  window.addEventListener("online", callback);
  window.addEventListener("offline", callback);
  return () => {
    document.removeEventListener("visibilitychange", callback);
    window.removeEventListener("online", callback);
    window.removeEventListener("offline", callback);
  };
}
const isForeground = () => !document.hidden && navigator.onLine;
export function useNotifications() {
  const user = useAuthStore((s) => s.user);
  const access = notificationAccess(user);
  const key = ["notifications", user?.centerId, user?.userId] as const;
  const client = useQueryClient();
  const active = useSyncExternalStore(subscribe, isForeground, () => false);
  const [jitter] = useState(() => Math.floor(Math.random() * 3000));
  const summary = useQuery({
    queryKey: [...key, "summary"], queryFn: ({ signal }) => notificationsApi.summary(signal),
    enabled: !!user && access.read && active, staleTime: 25_000, retry: false,
    refetchOnWindowFocus: true, refetchIntervalInBackground: false,
    refetchInterval: (query) => notificationPollInterval(active,
      isAxiosError(query.state.error) ? query.state.error.response?.status : undefined,
      query.state.fetchFailureCount, jitter),
  });
  const read = useMutation({
    mutationFn: (ids: string[]) => notificationsApi.read(ids),
    onSuccess: () => client.invalidateQueries({ queryKey: key }),
  });
  return { user, key, access, summary, read, active };
}
