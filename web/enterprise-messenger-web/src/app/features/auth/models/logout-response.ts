export interface LogoutResponse {
  userId: string;
  deviceId: string | null;
  revokedTokensCount: number;
  revokedAtUtc: string;
}