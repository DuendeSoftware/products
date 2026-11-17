import { LitElement, html } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import { authStyles } from '../styles';

/**
 * Internal component for displaying error messages
 * Not intended for direct external use
 */
@customElement('auth-error-display')
export class AuthErrorDisplay extends LitElement {
  static styles = authStyles;

  @property({ type: String }) message = '';

  render() {
    if (!this.message) return '';
    
    return html`
      <div part="error" class="error-message" role="alert">
        ${this.message}
      </div>
    `;
  }
}
