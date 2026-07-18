import axios, { AxiosError } from "axios";
import type { InternalAxiosRequestConfig } from "axios";
import { useAuthStore } from "../stores/authStore";
import type { LoginResponse } from "../types/auth";

export const publicTransport = axios.create({
  baseURL: "/api/v1",
  withCredentials: true,
});

export const httpClient = axios.create({
  baseURL: "/api/v1",
  withCredentials: true,
});

httpClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const accessToken = useAuthStore.getState().accessToken;
    if (accessToken && config.headers) {
      config.headers.Authorization = `Bearer ${accessToken}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

interface RetryableConfig extends InternalAxiosRequestConfig {
  _retry?: boolean;
}

let refreshPromise: Promise<string> | null = null;

httpClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as RetryableConfig;

    if (!originalRequest || !error.response) {
      return Promise.reject(error);
    }

    const { status } = error.response;
    const { url } = originalRequest;

    const isAuthEndpoint =
      url === "/auth/login" || url === "/auth/refresh" || url === "/auth/logout";

    if (status === 401 && !originalRequest._retry && !isAuthEndpoint) {
      originalRequest._retry = true;

      if (!refreshPromise) {
        refreshPromise = publicTransport
          .post<LoginResponse>("/auth/refresh")
          .then((res) => {
            const { accessToken, user } = res.data.data;
            useAuthStore.getState().setSession(accessToken, user);
            return accessToken;
          })
          .catch((err) => {
            useAuthStore.getState().clearSession();
            return Promise.reject(err);
          })
          .finally(() => {
            refreshPromise = null;
          });
      }

      try {
        const newAccessToken = await refreshPromise;
        if (originalRequest.headers) {
          originalRequest.headers.Authorization = `Bearer ${newAccessToken}`;
        }
        return httpClient(originalRequest);
      } catch (refreshError) {
        return Promise.reject(refreshError);
      }
    }

    return Promise.reject(error);
  }
);
