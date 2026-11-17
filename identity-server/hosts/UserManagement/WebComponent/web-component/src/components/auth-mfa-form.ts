import { LitElement, html, css } from 'lit';
import { customElement } from 'lit/decorators.js';
import { authStyles } from '../styles';

/**
 * Internal component for MFA (Multi-Factor Authentication) verification form
 * Not intended for direct external use
 */
@customElement('auth-mfa-form')
export class AuthMfaForm extends LitElement {
  static styles = [
    authStyles,
    css`
      .mfa-input {
        letter-spacing: 0.5em;
        font-size: 1.5rem;
        text-align: center;
      }
    `
  ];

  render() {
    return html`
      <h3 part="form-title">Two-Factor Authentication</h3>
      <p part="description" class="form-description">
        Enter the 6-digit code from your authenticator app
      </p>
      
      <form part="form" @submit=${this._handleSubmit}>
        <div part="field" class="form-field">
          <input
            part="input"
            class="mfa-input"
            type="text"
            id="mfa-code"
            name="code"
            placeholder="000000"
            autocomplete="one-time-code"
            required
            maxlength="6"
            inputmode="numeric"
            pattern="[0-9]{6}"
          />
        </div>
        
        <button part="primary-button" type="submit" class="btn-primary">
          Verify Code
        </button>
      </form>
    `;
  }

  private _handleSubmit(e: Event) {
    e.preventDefault();
    const form = e.target as HTMLFormElement;
    const formData = new FormData(form);
    
    this.dispatchEvent(new CustomEvent('mfa-verify', {
      detail: {
        code: formData.get('code')
      },
      bubbles: true,
      composed: true
    }));
  }
}
