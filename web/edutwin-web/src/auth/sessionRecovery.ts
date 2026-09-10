export interface SessionRecoveryInput {
  status: number;
  errorCode?: string;
  alreadyRetried: boolean;
  isAuthEndpoint: boolean;
}

export const shouldAttemptSessionRefresh = ({
  status,
  errorCode,
  alreadyRetried,
  isAuthEndpoint,
}: SessionRecoveryInput) =>
  status === 401 &&
  !alreadyRetried &&
  !isAuthEndpoint &&
  (errorCode === "AUTHORIZATION_VERSION_STALE" || errorCode === undefined);
