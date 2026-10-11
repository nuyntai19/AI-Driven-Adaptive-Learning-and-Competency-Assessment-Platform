import { httpClient } from './httpClient';
import type { PlatformOperationsData, PlatformAiUsageData, PlatformStorageData } from '../types/platformOperations';
export const platformOperationsApi = {
  operations: async (centerId?: string, signal?: AbortSignal) => (await httpClient.get<{data: PlatformOperationsData}>('/platform/operations', {params: {centerId}, signal})).data.data,
  usage: async (days: number, signal?: AbortSignal) => (await httpClient.get<{data: PlatformAiUsageData}>('/platform/ai-usage', {params: {days}, signal})).data.data,
  storage: async (signal?: AbortSignal) => (await httpClient.get<{data: PlatformStorageData}>('/platform/storage', {signal})).data.data,
};
