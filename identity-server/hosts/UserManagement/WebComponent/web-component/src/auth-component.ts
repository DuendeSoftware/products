import { LitElement, html } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import { ConfigController } from './config-controller';
import { authStyles } from './styles';
import type { AuthState } from './types';
import './components/auth-unified-form';
import './components/auth-otp-card';
import './components/auth-mfa-card';

@customElement('auth-component')
export class AuthComponent extends LitElement {
  static styles = authStyles;

  @property({ type: String, attribute: 'config-url' })
  configUrl = '/api/auth/config';

  @property({ type: String, attribute: 'state-url' })
  stateUrl = '/api/auth/state';

  @property({ type: String, attribute: 'cancel-url' })
  cancelUrl = '/api/auth/cancel';

  @state() private currentState: AuthState = 'loading-config';
  @state() private error: string | null = null;
  @state() private sessionId: string | null = null;
  @state() private email: string = '';

  private configController: ConfigController;

  constructor() {
    super();
    // Initialize with empty string, will be set properly in connectedCallback
    this.configController = new ConfigController(this, '');
  }

  connectedCallback() {
    super.connectedCallback();
    // Update the controller's configUrl now that the attribute has been parsed
    this.configController.configUrl = this.configUrl;
    
    // Load configuration and state
    this.loadConfiguration();
  }

  updated(changedProperties: Map<string, any>) {
    super.updated(changedProperties);
    
    if (changedProperties.has('currentState')) {
      this.emitEvent('auth-state-change', { 
        state: this.currentState
      });
    }
  }

  async loadConfiguration() {
    this.currentState = 'loading-config';
    await this.configController.loadConfig();

    if (this.configController.error) {
      this.error = this.configController.error.message;
      this.currentState = 'config-error';
      this.emitEvent('auth-error', { error: this.error, method: 'config' });
    } else if (this.configController.config) {
      // After config loads, check authentication state
      await this.checkAuthenticationState();
    }
  }

  private async checkAuthenticationState() {
    try {
      const response = await fetch(this.stateUrl, {
        headers: { 'Accept': 'application/json' },
        credentials: 'same-origin'
      });

      if (!response.ok) {
        throw new Error('Failed to fetch authentication state');
      }

      const state = await response.json();
      this.handleAuthenticationState(state);
    } catch (error) {
      this.error = (error as Error).message;
      this.currentState = 'config-error';
      this.emitEvent('auth-error', { error: this.error, method: 'state' });
    }
  }

  private handleAuthenticationState(state: any) {
    // Server tells us exactly what state to be in
    this.sessionId = state.sessionId || null;
    this.email = state.email || '';

    if (state.authenticated) {
      this.currentState = 'authenticated';
      this.emitEvent('auth-success', { method: 'existing-session' });
    } else {
      // Map server's nextState to component state
      this.currentState = state.nextState as AuthState;
      
      if (state.nextState === 'mfa-required') {
        this.emitEvent('mfa-required', { sessionId: state.sessionId });
      }
    }
  }

  public reset() {
    this.error = null;
    this.cancelAuthenticationFlow();
  }

  private async cancelAuthenticationFlow() {
    try {
      // Call server to clear any temporary authentication state
      const response = await fetch(this.cancelUrl, {
        method: 'POST',
        headers: { 'Accept': 'application/json' },
        credentials: 'same-origin'
      });

      if (!response.ok) {
        console.warn('Failed to cancel authentication flow');
      }

      // Clear local state and reload
      this.sessionId = null;
      this.email = '';
      await this.checkAuthenticationState();
    } catch (error) {
      console.error('Error cancelling authentication flow:', error);
      // Fallback to just resetting local state
      this.sessionId = null;
      this.email = '';
      this.currentState = 'unified-form';
    }
  }

  private async handleOtpSendDirect(email: string) {
    if (!email) {
      this.error = 'Please enter your email address';
      return;
    }

    this.email = email;
    this.error = null;
    this.currentState = 'sending-otp';

    try {
      const otpMethod = this.configController.getPrimaryMethodByType('otp');
      if (!otpMethod?.endpoints?.sendOtp) {
        throw new Error('OTP method not configured');
      }

      const response = await fetch(otpMethod.endpoints.sendOtp, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ email }),
        credentials: 'same-origin'
      });

      const result = await response.json();

      if (!response.ok || !result.success) {
        throw new Error(result.error || 'Failed to send OTP');
      }

      // Use standardized response
      this.sessionId = result.sessionId;
      if (result.nextAction === 'otp-verify') {
        this.currentState = 'otp-verify';
      }
    } catch (error) {
      this.error = (error as Error).message;
      this.currentState = 'unified-form';
      this.emitEvent('auth-error', { error: this.error, method: 'otp' });
    }
  }

  private async handleOtpVerifyDirect(code: string) {
    if (!code) {
      this.error = 'Please enter the verification code';
      return;
    }
    
    const cleanCode = code.trim().toUpperCase().replace(/-/g, '');

    if (cleanCode.length < 8) {
      this.error = 'Please enter the complete verification code';
      return;
    }

    this.error = null;

    try {
      const endpoint = this.getPrimaryMethodEndpoint('otp', 'verifyOtp');
      const response = await fetch(endpoint, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ sessionId: this.sessionId, code }),
        credentials: 'same-origin'
      });

      const result = await response.json();

      // Use standardized response
      if (result.nextAction === 'mfa-required') {
        this.sessionId = result.sessionId;
        this.currentState = 'mfa-required';
        this.emitEvent('mfa-required', { sessionId: result.sessionId });
        return;
      }

      if (!response.ok || !result.success) {
        throw new Error(result.error || 'Invalid code');
      }

      this.handleAuthSuccess('otp');
    } catch (error) {
      this.error = (error as Error).message;
      this.emitEvent('auth-error', { error: this.error, method: 'otp' });
    }
  }

  private async handleMfaVerifyDirect(code: string) {
    if (!code) {
      this.error = 'Please enter the verification code';
      return;
    }
    
    const cleanCode = code.replace(/\s/g, '');

    if (cleanCode.length !== 6) {
      this.error = 'Please enter all 6 digits';
      return;
    }

    this.currentState = 'mfa-verify';
    this.error = null;

    try {
      const endpoint = this.getSecondaryAuthenticatorEndpoint('totp', 'verifyMfa');
      const response = await fetch(endpoint, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ sessionId: this.sessionId, code: cleanCode }),
        credentials: 'same-origin'
      });

      const result = await response.json();

      if (!response.ok || !result.success) {
        throw new Error(result.error || 'Invalid code');
      }

      this.handleAuthSuccess('totp');
    } catch (error) {
      this.error = (error as Error).message;
      this.currentState = 'mfa-required';
      this.emitEvent('auth-error', { error: this.error, method: 'totp' });
    }
  }

  private _handleOtpSendClick(e: CustomEvent) {
    const { email } = e.detail;
    console.log('e.detail:' + e.detail);
    console.log('email:' + email);
    this.email = email;
    this.handleOtpSendDirect(email);
  }

  private _handleOtpVerifyEvent(e: CustomEvent) {
    const { code } = e.detail;
    this.handleOtpVerifyDirect(code);
  }

  private _handleMfaVerifyEvent(e: CustomEvent) {
    const { code } = e.detail;
    this.handleMfaVerifyDirect(code);
  }

  private _handleProviderClick(e: CustomEvent) {
    const { provider } = e.detail;
    this.handleProviderClick(provider.challengeUrl);
  }

  private handleProviderClick(challengeUrl: string) {
    window.location.href = challengeUrl;
  }

  private handleAuthSuccess(method: string) {
    this.currentState = 'authenticated';
    this.emitEvent('auth-success', { method });
  }

  private getPrimaryMethodEndpoint(methodType: string, endpointKey: string): string {
    const method = this.configController.getPrimaryMethodByType(methodType);
    if (method?.endpoints && method.endpoints[endpointKey]) {
      return method.endpoints[endpointKey];
    }
    throw new Error(`Endpoint ${endpointKey} not found for primary method ${methodType}`);
  }

  private getSecondaryAuthenticatorEndpoint(factorType: string, endpointKey: string): string {
    const factor = this.configController.getSecondaryAuthenticator(factorType);
    if (factor?.endpoints && factor.endpoints[endpointKey]) {
      return factor.endpoints[endpointKey];
    }
    throw new Error(`Endpoint ${endpointKey} not found for secondary factor ${factorType}`);
  }

  private emitEvent(name: string, detail: any) {
    this.dispatchEvent(new CustomEvent(name, {
      detail,
      bubbles: true,
      composed: true
    }));
  }

  render() {
    return html`
      <div part="container" class="auth-container">
        <slot name="logo"></slot>
        
        <slot name="header"></slot>
        
        ${this.renderState()}
        
        <slot name="footer"></slot>
      </div>
    `;
  }

  private renderState() {
    switch (this.currentState) {
      case 'loading-config':
        return html`<div class="loading">Loading...</div>`;
      case 'config-error':
        return html`
          <div class="error-message">
            ${this.error || 'Failed to load configuration'}
          </div>
        `;
      case 'unified-form':
        return this.renderUnifiedForm();
      case 'sending-otp':
        return this.renderUnifiedForm();
      case 'otp-verify':
        return this.renderOtpVerifyForm();
      case 'mfa-required':
        return this.renderMfaForm();
      case 'mfa-verify':
        return html`<div class="loading">Verifying...</div>`;
      case 'authenticated':
        return html`<div class="success">Authentication successful</div>`;
      default:
        return html`<div class="error-message">Unknown state</div>`;
    }
  }

  private renderUnifiedForm() {
    const primaryMethods = this.configController.getEnabledPrimaryAuthenticators();
    const otpMethod = primaryMethods.find(m => m.type === 'otp');
    const externalMethod = primaryMethods.find(m => m.type === 'external');
    
    const isSendingOtp = this.currentState === 'sending-otp';
    
    // Prepare external providers
    const externalProviders = externalMethod && 'providers' in externalMethod.config 
      ? (externalMethod.config as any).providers
      : [];
    
    return html`
      <auth-unified-form
        .error=${this.error}
        .emailValue=${this.email}
        .showPasswordAuth=${false}
        .showRememberMe=${false}
        .showOtpAuth=${!!otpMethod}
        .showOtpEmailInput=${!!otpMethod}
        .showOtpAsSecondary=${false}
        .externalProviders=${externalProviders}
        .disabled=${isSendingOtp}
        .showRegistrationLink=${false}
        @otp-send=${this._handleOtpSendClick}
        @provider-select=${this._handleProviderClick}
      ></auth-unified-form>
    `;
  }

  private renderOtpVerifyForm() {
    return html`
      <auth-otp-card
        .error=${this.error || ''}
        .email=${this.email}
        .title=${'Verify Email'}
        .description=${''}
        @otp-verify=${this._handleOtpVerifyEvent}
        @back-click=${this.reset}
      ></auth-otp-card>
    `;
  }

  private renderMfaForm() {
    return html`
      <auth-mfa-card
        .error=${this.error || ''}
        @mfa-verify=${this._handleMfaVerifyEvent}
        @back-click=${this.reset}
      ></auth-mfa-card>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'auth-component': AuthComponent;
  }
}
