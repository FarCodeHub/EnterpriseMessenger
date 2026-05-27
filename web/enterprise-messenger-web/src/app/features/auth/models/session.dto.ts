export interface SessionDto {
  deviceId: string;
  activeRefreshTokensCount: number;
  latestExpiresAtUtc: string;
  createdAtUtc: string;
}