import { LitElement, html } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import { authStyles } from '../styles';

/**
 * Internal component for OTP email request (send code button or form)
 * Not intended for direct external use
 */
@customElement('auth-otp-send')
export class AuthOtpSend extends LitElement {
  static styles = authStyles;

  @property({ type: String }) emailValue = '';
  @property({ type: Boolean }) disabled = false;
  @property({ type: Boolean }) showEmailInput = true;
  @property({ type: Boolean }) isSecondaryButton = false;

  render() {
    console.log('auth-otp-send render:', {
      showEmailInput: this.showEmailInput,
      emailValue: this.emailValue,
      disabled: this.disabled
    });
    if (this.showEmailInput) {
      return this._renderForm();
    } else {
      return this._renderButton();
    }
  }

  private _renderForm() {
    return html`
      <form part="form" @submit=${this._handleSubmit}>
        <div part="field" class="form-field">
          <label part="label" for="email">Email</label>
          <input
            part="input"
            type="email"
            id="email"
            name="email"
            required
            autocomplete="email"
            placeholder="user@domain.com"
            .value=${this.emailValue}
            @input=${this._handleInput}
            ?disabled=${this.disabled}
          />
        </div>
        
        <button part="primary-button" type="submit" class="btn-primary" ?disabled=${this.disabled}>
          ${this.disabled ? 'Sending code...' : 'Email me a code'}
        </button>
      </form>
    `;
  }

  private _handleInput(e: Event) {
    const input = e.target as HTMLInputElement;
    console.log('_handleInput called, value:', input.value);
    this.emailValue = input.value;
  }

  private _renderButton() {
    const buttonClass = this.isSecondaryButton ? 'btn-secondary' : 'btn-primary';
    const buttonPart = this.isSecondaryButton ? 'secondary-button' : 'primary-button';
    
    return html`
      <button 
        part="${buttonPart}"
        class="${buttonClass}" 
        @click=${this._handleClick}
        ?disabled=${this.disabled}
      >
        ${this.disabled ? 'Sending code...' : 'Email me a code'}
      </button>
    `;
  }

  private _handleSubmit(e: Event) {
    console.log('_handleSubmit called!', e);
    e.preventDefault();
    const form = e.target as HTMLFormElement;
    
    const emailInput = this.renderRoot.querySelector('input[name="email"]') as HTMLInputElement;
    const emailValue = emailInput.value;
    
    console.log('OTP Send Submit:', {
      emailInput,
      emailInputValue: emailInput.value,
      thisEmailValue: this.emailValue,
      finalEmailValue: emailValue
    });
    
    this.dispatchEvent(new CustomEvent('otp-send', {
      detail: {
        email: emailValue
      },
      bubbles: true,
      composed: true
    }));
  }

  private _handleClick() {
    this.dispatchEvent(new CustomEvent('otp-send', {
      detail: {
        email: this.emailValue
      },
      bubbles: true,
      composed: true
    }));
  }
}
