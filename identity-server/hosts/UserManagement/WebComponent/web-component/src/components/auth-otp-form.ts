import { LitElement, html, css } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import { authStyles } from '../styles';

/**
 * Internal component for OTP (One-Time Password) verification form
 * Not intended for direct external use
 */
@customElement('auth-otp-form')
export class AuthOtpForm extends LitElement {
  static styles = [
    authStyles,
    css`
      .otp-input {
        text-transform: uppercase;
        letter-spacing: 0.1em;
        font-size: 1.2rem;
        text-align: center;
      }
    `
  ];

  @property({ type: String }) email = '';

  render() {
    return html`
      <h3 part="form-title">Enter Verification Code</h3>
      <p part="description" class="form-description">
        We've sent a verification code to <strong>${this.email}</strong>
      </p>
      
      <form part="form" @submit=${this._handleSubmit}>
        <div part="field" class="form-field">
          <input
            part="input"
            class="otp-input"
            type="text"
            id="otp-code"
            name="code"
            placeholder="XXXX-XXXX"
            autocomplete="one-time-code"
            required
            maxlength="9"
            @input=${this._handleFormatInput}
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
    
    this.dispatchEvent(new CustomEvent('otp-verify', {
      detail: {
        code: formData.get('code')
      },
      bubbles: true,
      composed: true
    }));
  }

  private _handleFormatInput(e: Event) {
    const input = e.target as HTMLInputElement;
    let value = input.value.replace(/[^0-9A-Za-z]/g, '').toUpperCase();
    
    // Format as XXXX-XXXX
    if (value.length > 4) {
      value = value.slice(0, 4) + '-' + value.slice(4, 8);
    }
    
    input.value = value;
  }
}
