import { environment } from '../../../environments/environment';

export interface ApplicationConfig {
  production: boolean;
  apiBaseUrl: string;
  signalRHubUrl: string;
  appName: string;
}

export const applicationConfig: ApplicationConfig = environment;