import { LitElement, html } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import { authStyles } from '../styles';
import './auth-error-display';
import './auth-otp-form';

/**
 * Internal OTP verification card component with back button
 * Not intended for direct external use
 */
@customElement('auth-otp-card')
export class AuthOtpCard extends LitElement {
  static styles = authStyles;

  @property({ type: String }) error = '';
  @property({ type: String }) email = '';
  @property({ type: String }) title = 'Verify Email';
  @property({ type: String }) description = '';

  render() {
    const displayDescription = this.description || 
      `We sent a verification code to ${this.email}. Please enter it below.`;
      
    return html`
      <div part="card" class="auth-form">
        <button 
          type="button" 
          class="back-button" 
          @click=${this._handleBack}
          aria-label="Go back to sign in"
        >
          ← Back
        </button>
        
        <h3 part="form-title">${this.title}</h3>
        
        ${displayDescription ? html`
          <p part="description" class="description">${displayDescription}</p>
        ` : ''}
        
        <auth-error-display .message=${this.error}></auth-error-display>
        
        <auth-otp-form
          .email=${this.email}
          @otp-verify=${this._handleOtpVerify}
        ></auth-otp-form>
      </div>
    `;
  }

  private _handleOtpVerify(e: CustomEvent) {
  }

  private _handleBack() {
    this.dispatchEvent(new CustomEvent('back-click', {
      bubbles: true,
      composed: true
    }));
  }
}
