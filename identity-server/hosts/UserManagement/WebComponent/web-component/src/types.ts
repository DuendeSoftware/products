// Type definitions for the authentication component

export type PrimaryMethodType = 'otp' | 'external';
export type SecondaryAuthenticatorType = 'totp';

export type AuthState =
  | 'loading-config'
  | 'config-error'
  | 'unified-form'
  | 'sending-otp'
  | 'otp-verify'
  | 'mfa-required'
  | 'mfa-verify'
  | 'authenticated'
  | 'error';

export interface AuthConfig {
  version: string;
  primaryAuthenticators: PrimaryMethod[];
  secondaryAuthenticators: SecondaryAuthenticator[];
  settings: AuthSettings;
}

export interface PrimaryMethod {
  type: PrimaryMethodType;
  enabled: boolean;
  priority: number;
  title?: string;
  description?: string;
  config: OtpAuthConfig | ExternalAuthConfig;
  endpoints?: Record<string, string>;
}

export interface SecondaryAuthenticator {
  type: SecondaryAuthenticatorType;
  title: string;
  description: string;
  endpoints: Record<string, string>;
}

export interface OtpAuthConfig {
  otpLength: number;
  otpExpirySeconds: number;
  allowResend: boolean;
  resendCooldownSeconds: number;
}

export interface ExternalAuthConfig {
  providers: ExternalProvider[];
}

export interface ExternalProvider {
  id: string;
  displayName: string;
  challengeUrl: string;
}

export interface AuthSettings {
  rememberMeEnabled: boolean;
}

export interface AuthenticationStateResponse {
  authenticated: boolean;
  nextState: string;
  sessionId?: string;
  email?: string;
  metadata?: any;
}

export interface AuthenticationResponse {
  success: boolean;
  nextAction?: string;
  sessionId?: string;
  error?: string;
  data?: any;
}
