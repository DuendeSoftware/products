import { css } from 'lit';

export const authStyles = css`
  :host {
    display: block;
    
    /* Colors - Duende-inspired palette with CSS custom properties */
    --auth-primary-color: var(--duende-primary, #5B6FED);
    --auth-primary-hover: var(--duende-primary-hover, #4557D2);
    --auth-primary-light: var(--duende-primary-light, #EEF0FF);
    --auth-text-primary: var(--duende-text, #1F2937);
    --auth-text-secondary: var(--duende-text-secondary, #6B7280);
    --auth-text-muted: var(--duende-text-muted, #9CA3AF);
    --auth-border-color: var(--duende-border, #E5E7EB);
    --auth-border-focus: var(--duende-border-focus, #5B6FED);
    --auth-background: var(--duende-background, #FFFFFF);
    --auth-background-secondary: var(--duende-background-secondary, #F9FAFB);
    --auth-error-color: var(--duende-error, #EF4444);
    --auth-error-background: var(--duende-error-background, #FEF2F2);
    --auth-success-color: var(--duende-success, #10B981);
    --auth-success-background: var(--duende-success-background, #ECFDF5);
    
    /* Spacing */
    --auth-spacing-xs: var(--spacing-xs, 0.5rem);
    --auth-spacing-sm: var(--spacing-sm, 0.75rem);
    --auth-spacing-md: var(--spacing-md, 1rem);
    --auth-spacing-lg: var(--spacing-lg, 1.5rem);
    --auth-spacing-xl: var(--spacing-xl, 2rem);
    --auth-spacing-2xl: var(--spacing-2xl, 2.5rem);
    
    /* Typography */
    --auth-font-family: var(--font-family, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif);
    --auth-font-size-xs: var(--font-size-xs, 0.75rem);
    --auth-font-size-sm: var(--font-size-sm, 0.875rem);
    --auth-font-size-base: var(--font-size-base, 1rem);
    --auth-font-size-lg: var(--font-size-lg, 1.125rem);
    --auth-font-size-xl: var(--font-size-xl, 1.5rem);
    --auth-font-size-2xl: var(--font-size-2xl, 1.875rem);
    --auth-font-weight-normal: var(--font-weight-normal, 400);
    --auth-font-weight-medium: var(--font-weight-medium, 500);
    --auth-font-weight-semibold: var(--font-weight-semibold, 600);
    --auth-line-height-tight: var(--line-height-tight, 1.25);
    --auth-line-height-normal: var(--line-height-normal, 1.5);
    
    /* Layout */
    --auth-border-radius: var(--border-radius, 0.5rem);
    --auth-border-radius-lg: var(--border-radius-lg, 0.75rem);
    --auth-border-width: var(--border-width, 1px);
    --auth-max-width: var(--max-width, 400px);
    --auth-shadow-sm: var(--shadow-sm, 0 1px 2px 0 rgb(0 0 0 / 0.05));
    --auth-shadow: var(--shadow, 0 4px 6px -1px rgb(0 0 0 / 0.1), 0 2px 4px -2px rgb(0 0 0 / 0.1));
    --auth-shadow-lg: var(--shadow-lg, 0 10px 15px -3px rgb(0 0 0 / 0.1), 0 4px 6px -4px rgb(0 0 0 / 0.1));
    --auth-focus-ring: var(--focus-ring, 0 0 0 3px var(--auth-primary-light));
    
    /* Transitions */
    --auth-transition-fast: var(--transition-fast, 150ms ease-in-out);
    --auth-transition-base: var(--transition-base, 200ms ease-in-out);
    
    font-family: var(--auth-font-family);
    color: var(--auth-text-primary);
  }

  .auth-container {
    max-width: var(--auth-max-width);
    margin: 0 auto;
    padding: var(--auth-spacing-md);
  }

  .auth-logo {
    display: flex;
    justify-content: center;
    margin-bottom: var(--auth-spacing-xl);
  }

  .auth-logo svg,
  .auth-logo img {
    width: 64px;
    height: 64px;
  }

  .default-logo {
    width: 64px;
    height: 64px;
    background: linear-gradient(135deg, var(--auth-primary-color) 0%, var(--auth-primary-hover) 100%);
    border-radius: var(--auth-border-radius-lg);
    display: flex;
    align-items: center;
    justify-content: center;
    box-shadow: var(--auth-shadow);
  }

  .default-logo::before {
    content: '';
    width: 32px;
    height: 32px;
    background: white;
    border-radius: var(--auth-border-radius);
    opacity: 0.9;
  }

  .auth-header {
    text-align: center;
    margin-bottom: var(--auth-spacing-xl);
  }

  .auth-form {
    background: var(--auth-background);
    border-radius: var(--auth-border-radius-lg);
    padding: var(--auth-spacing-2xl);
    box-shadow: var(--auth-shadow-lg);
    border: var(--auth-border-width) solid var(--auth-border-color);
  }

  .auth-form h3 {
    margin: 0 0 var(--auth-spacing-xs) 0;
    font-size: var(--auth-font-size-2xl);
    font-weight: var(--auth-font-weight-semibold);
    text-align: center;
    line-height: var(--auth-line-height-tight);
    color: var(--auth-text-primary);
  }

  .form-description {
    color: var(--auth-text-secondary);
    margin-bottom: var(--auth-spacing-lg);
    text-align: center;
    font-size: var(--auth-font-size-sm);
    line-height: var(--auth-line-height-normal);
  }

  .form-field {
    margin-bottom: var(--auth-spacing-md);
  }

  .form-field label {
    display: block;
    margin-bottom: var(--auth-spacing-xs);
    font-weight: var(--auth-font-weight-medium);
    font-size: var(--auth-font-size-sm);
    color: var(--auth-text-primary);
  }

  .form-field input {
    width: 100%;
    padding: 0.75rem;
    border: var(--auth-border-width) solid var(--auth-border-color);
    border-radius: var(--auth-border-radius);
    font-size: var(--auth-font-size-base);
    font-family: var(--auth-font-family);
    box-sizing: border-box;
    transition: all var(--auth-transition-base);
    background: var(--auth-background);
    color: var(--auth-text-primary);
  }

  .form-field input::placeholder {
    color: var(--auth-text-muted);
  }

  .form-field input:hover {
    border-color: var(--auth-text-secondary);
  }

  .form-field input:focus {
    outline: none;
    border-color: var(--auth-border-focus);
    box-shadow: var(--auth-focus-ring);
  }

  .form-field input:disabled {
    background: var(--auth-background-secondary);
    color: var(--auth-text-muted);
    cursor: not-allowed;
  }

  .form-checkbox {
    display: flex;
    align-items: center;
    gap: var(--auth-spacing-xs);
    margin-bottom: var(--auth-spacing-lg);
  }

  .form-checkbox input[type="checkbox"] {
    width: auto;
    cursor: pointer;
    accent-color: var(--auth-primary-color);
  }

  .form-checkbox label {
    font-size: var(--auth-font-size-sm);
    cursor: pointer;
    margin: 0;
  }

  .btn-primary {
    width: 100%;
    padding: 0.875rem;
    background: var(--auth-primary-color);
    color: white;
    border: none;
    border-radius: var(--auth-border-radius);
    font-size: var(--auth-font-size-base);
    font-weight: var(--auth-font-weight-semibold);
    font-family: var(--auth-font-family);
    cursor: pointer;
    transition: all var(--auth-transition-base);
    box-shadow: var(--auth-shadow-sm);
  }

  .btn-primary:hover {
    background: var(--auth-primary-hover);
    box-shadow: var(--auth-shadow);
    transform: translateY(-1px);
  }

  .btn-primary:active {
    transform: translateY(0);
  }

  .btn-primary:disabled {
    background: var(--auth-text-muted);
    cursor: not-allowed;
    transform: none;
    box-shadow: none;
    opacity: 0.6;
  }

  .btn-secondary {
    width: 100%;
    padding: 0.875rem;
    background: var(--auth-background);
    color: var(--auth-primary-color);
    border: var(--auth-border-width) solid var(--auth-border-color);
    border-radius: var(--auth-border-radius);
    font-size: var(--auth-font-size-base);
    font-weight: var(--auth-font-weight-medium);
    font-family: var(--auth-font-family);
    cursor: pointer;
    transition: all var(--auth-transition-base);
  }

  .btn-secondary:hover {
    background: var(--auth-background-secondary);
    border-color: var(--auth-primary-color);
  }

  .btn-secondary:disabled {
    border-color: var(--auth-border-color);
    color: var(--auth-text-muted);
    cursor: not-allowed;
    opacity: 0.6;
  }

  .btn-secondary:disabled:hover {
    background: var(--auth-background);
    border-color: var(--auth-border-color);
  }

  .btn-link {
    background: none;
    border: none;
    color: var(--auth-primary-color);
    cursor: pointer;
    text-decoration: none;
    padding: var(--auth-spacing-xs);
    font-size: var(--auth-font-size-base);
    font-family: var(--auth-font-family);
    transition: color var(--auth-transition-fast);
  }

  .btn-link:hover {
    color: var(--auth-primary-hover);
    text-decoration: underline;
  }

  .error-message {
    padding: var(--auth-spacing-md);
    background: var(--auth-error-background);
    border: var(--auth-border-width) solid var(--auth-error-color);
    border-radius: var(--auth-border-radius);
    color: var(--auth-error-color);
    margin-bottom: var(--auth-spacing-md);
    font-size: var(--auth-font-size-sm);
    line-height: var(--auth-line-height-normal);
  }

  .success {
    padding: var(--auth-spacing-2xl);
    text-align: center;
    color: var(--auth-success-color);
    font-weight: var(--auth-font-weight-medium);
    font-size: var(--auth-font-size-lg);
  }

  .loading {
    padding: var(--auth-spacing-2xl);
    text-align: center;
    color: var(--auth-text-secondary);
    font-size: var(--auth-font-size-base);
  }

  .provider-buttons {
    display: flex;
    flex-direction: column;
    gap: var(--auth-spacing-sm);
  }

  .provider-buttons-inline {
    display: flex;
    flex-direction: column;
    gap: var(--auth-spacing-sm);
  }

  .provider-button {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: var(--auth-spacing-sm);
    padding: 0.875rem;
    border: var(--auth-border-width) solid var(--auth-border-color);
    border-radius: var(--auth-border-radius);
    background: var(--auth-background);
    cursor: pointer;
    transition: all var(--auth-transition-base);
    font-size: var(--auth-font-size-base);
    font-family: var(--auth-font-family);
    font-weight: var(--auth-font-weight-medium);
    width: 100%;
    color: var(--auth-text-primary);
  }

  .provider-button:hover {
    background: var(--auth-background-secondary);
    border-color: var(--auth-text-secondary);
    box-shadow: var(--auth-shadow-sm);
  }

  .provider-button span {
    display: flex;
    align-items: center;
    gap: var(--auth-spacing-xs);
  }

  .method-selection {
    display: flex;
    flex-direction: column;
    gap: var(--auth-spacing-md);
  }

  .method-button {
    padding: var(--auth-spacing-lg);
    border: var(--auth-border-width) solid var(--auth-border-color);
    border-radius: var(--auth-border-radius-lg);
    background: var(--auth-background);
    cursor: pointer;
    font-size: var(--auth-font-size-base);
    font-family: var(--auth-font-family);
    transition: all var(--auth-transition-base);
    text-align: left;
  }

  .method-button:hover {
    border-color: var(--auth-primary-color);
    background: var(--auth-background-secondary);
    box-shadow: var(--auth-shadow-sm);
  }

  .method-button-title {
    font-weight: var(--auth-font-weight-semibold);
    margin-bottom: var(--auth-spacing-xs);
    color: var(--auth-text-primary);
  }

  .method-button-description {
    color: var(--auth-text-secondary);
    font-size: var(--auth-font-size-sm);
    line-height: var(--auth-line-height-normal);
  }

  .form-footer {
    margin-top: var(--auth-spacing-lg);
    text-align: center;
    padding-top: var(--auth-spacing-md);
    border-top: var(--auth-border-width) solid var(--auth-border-color);
  }

  .link-button {
    background: none;
    border: none;
    color: var(--auth-primary-color);
    cursor: pointer;
    text-decoration: none;
    padding: var(--auth-spacing-xs);
    font-size: var(--auth-font-size-sm);
    font-family: var(--auth-font-family);
    transition: color var(--auth-transition-fast);
  }

  .link-button:hover {
    color: var(--auth-primary-hover);
    text-decoration: underline;
  }

  .form-field small {
    display: block;
    margin-top: var(--auth-spacing-xs);
    color: var(--auth-text-secondary);
    font-size: var(--auth-font-size-xs);
    line-height: var(--auth-line-height-normal);
  }

  .back-button {
    background: none;
    border: none;
    color: var(--auth-primary-color);
    cursor: pointer;
    padding: var(--auth-spacing-xs) 0;
    margin-bottom: var(--auth-spacing-md);
    font-size: var(--auth-font-size-sm);
    font-family: var(--auth-font-family);
    display: flex;
    align-items: center;
    gap: var(--auth-spacing-xs);
    transition: color var(--auth-transition-fast);
    font-weight: var(--auth-font-weight-medium);
  }

  .back-button:hover {
    color: var(--auth-primary-hover);
  }

  .divider {
    display: flex;
    align-items: center;
    text-align: center;
    margin: var(--auth-spacing-lg) 0;
    color: var(--auth-text-muted);
    font-size: var(--auth-font-size-xs);
    text-transform: uppercase;
    font-weight: var(--auth-font-weight-medium);
    letter-spacing: 0.05em;
  }

  .divider::before,
  .divider::after {
    content: '';
    flex: 1;
    border-bottom: var(--auth-border-width) solid var(--auth-border-color);
  }

  .divider span {
    padding: 0 var(--auth-spacing-md);
  }

  /* Accessibility improvements */
  @media (prefers-reduced-motion: reduce) {
    * {
      transition: none !important;
      animation: none !important;
    }
  }

  /* Focus visible for keyboard navigation */
  *:focus-visible {
    outline: 2px solid var(--auth-primary-color);
    outline-offset: 2px;
  }
`;
