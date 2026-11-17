import { LitElement, html } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import { authStyles } from '../styles';
import './auth-error-display';
import './auth-otp-send';
import './auth-provider-buttons';

interface ExternalProvider {
  id: string;
  displayName: string;
  challengeUrl: string;
}

/**
 * Internal unified form component that orchestrates OTP/external auth
 * Not intended for direct external use
 */
@customElement('auth-unified-form')
export class AuthUnifiedForm extends LitElement {
  static styles = authStyles;

  @property({ type: String }) error = '';
  @property({ type: String }) emailValue = '';
  @property({ type: Boolean }) showOtpAuth = false;
  @property({ type: Boolean }) showOtpEmailInput = false;
  @property({ type: Array }) externalProviders: ExternalProvider[] = [];
  @property({ type: Boolean }) disabled = false;

  render() {
    const showDivider = this.showOtpAuth && this.externalProviders.length > 0;
    
    return html`
      <div part="card" class="auth-form">
        <h3 part="form-title">Sign In</h3>
        
        <auth-error-display .message=${this.error}></auth-error-display>
        
        ${this.showOtpAuth ? html`
          <auth-otp-send
            .emailValue=${this.emailValue}
            .showEmailInput=${this.showOtpEmailInput}
            .isSecondaryButton=${false}
            .disabled=${this.disabled}
            @otp-send=${this._handleOtpSend}
          ></auth-otp-send>
        ` : ''}
        
        ${showDivider ? html`
          <div part="divider" class="divider">
            <span>or</span>
          </div>
        ` : ''}
        
        ${this.externalProviders.length > 0 ? html`
          <auth-provider-buttons
            .providers=${this.externalProviders}
            @provider-select=${this._handleProviderSelect}
          ></auth-provider-buttons>
        ` : ''}
      </div>
    `;
  }

  private _handleOtpSend(e: CustomEvent) {
  }

  private _handleProviderSelect(e: CustomEvent) {
  }
}
