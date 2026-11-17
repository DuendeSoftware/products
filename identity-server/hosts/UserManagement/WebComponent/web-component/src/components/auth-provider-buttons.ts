import { LitElement, html } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import { authStyles } from '../styles';

interface ExternalProvider {
  id: string;
  displayName: string;
  challengeUrl: string;
}

/**
 * Internal component for rendering external authentication provider buttons
 * Not intended for direct external use
 */
@customElement('auth-provider-buttons')
export class AuthProviderButtons extends LitElement {
  static styles = authStyles;

  @property({ type: Array }) providers: ExternalProvider[] = [];

  render() {
    if (!this.providers || this.providers.length === 0) return '';
    
    return html`
      <div part="providers" class="provider-buttons-inline">
        ${this.providers.map(provider => html`
          <button
            part="provider-button"
            class="provider-button"
            @click=${() => this._handleProviderClick(provider)}
          >
            <span>Continue with ${provider.displayName}</span>
          </button>
        `)}
      </div>
    `;
  }

  private _handleProviderClick(provider: ExternalProvider) {
    this.dispatchEvent(new CustomEvent('provider-select', {
      detail: { provider },
      bubbles: true,
      composed: true
    }));
  }
}
