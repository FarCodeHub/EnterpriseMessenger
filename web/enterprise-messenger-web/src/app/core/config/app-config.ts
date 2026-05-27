import { environment } from '../../../environments/environment';

export interface AppConfig {
  production: boolean;
  apiBaseUrl: string;
  signalRHubUrl: string;
  appName: string;
}

export const appConfig: AppConfig = environment;