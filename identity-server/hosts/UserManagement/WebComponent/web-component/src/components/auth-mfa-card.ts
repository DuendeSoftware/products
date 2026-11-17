import { LitElement, html } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import { authStyles } from '../styles';
import './auth-error-display';
import './auth-mfa-form';

/**
 * Internal MFA verification card component with back button
 * Not intended for direct external use
 */
@customElement('auth-mfa-card')
export class AuthMfaCard extends LitElement {
  static styles = authStyles;

  @property({ type: String }) error = '';

  render() {
    return html`
      <div part="card" class="auth-form">
        <button 
          type="button" 
          class="back-button" 
          @click=${this._handleBack}
          aria-label="Go back to sign in"
        >
          Back
        </button>
        
        <auth-error-display .message=${this.error}></auth-error-display>
        
        <auth-mfa-form
          @mfa-verify=${this._handleMfaVerify}
        ></auth-mfa-form>
      </div>
    `;
  }

  private _handleMfaVerify(e: CustomEvent) {
  }

  private _handleBack() {
    this.dispatchEvent(new CustomEvent('back-click', {
      bubbles: true,
      composed: true
    }));
  }
}
