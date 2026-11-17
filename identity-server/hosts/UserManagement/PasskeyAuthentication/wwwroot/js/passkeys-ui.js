// UI layer for passkey management - handles DOM manipulation and event binding

// =============================================================================
// Shared UI Helpers
// =============================================================================

function isLocalUrl(url) {
  try { return new URL(url, location.origin).origin === location.origin; }
  catch { return false; }
}

function showStatus(message, isError) {
  const statusDiv = document.getElementById('passkey-status');
  statusDiv.textContent = message;
  statusDiv.className = 'alert ' + (isError ? 'alert-danger' : 'alert-success');
  statusDiv.classList.remove('d-none');
}

function setButtonLoading(btn, message) {
  btn.disabled = true;
  btn.innerHTML = `<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>${message}`;
}

function disableButtonWithMessage(btn, message) {
  btn.disabled = true;
  btn.innerHTML = `<i class="bi bi-exclamation-triangle me-2"></i>${message}`;
}

// =============================================================================
// Login Page - Passkey Authentication
// =============================================================================

function resetLoginButton(btn) {
  btn.disabled = false;
  btn.innerHTML = '<i class="bi bi-key me-2"></i>Use passkey';
}

function resetDiscoverableLoginButton(btn) {
  btn.disabled = false;
  btn.innerHTML = '<i class="bi bi-fingerprint me-2"></i>Use passkey with discoverable credentials';
}

async function handlePasskeyLogin() {
  const btn = document.getElementById('passkey-login-btn');
  const emailInput = document.getElementById('Email');
  const email = emailInput?.value?.trim();

  if (!email) {
    showStatus('Please enter your email address first.', true);
    emailInput?.focus();
    return;
  }

  // Basic email validation
  if (!email.includes('@')) {
    showStatus('Please enter a valid email address.', true);
    emailInput?.focus();
    return;
  }

  try {
    await authenticateWithPasskey(email, {
      onStart: () => setButtonLoading(btn, 'Starting...'),
      onWaitingForAuthenticator: () => setButtonLoading(btn, 'Waiting for authenticator...'),
      onSuccess: (result) => {
        const returnUrl = document.querySelector('input[name="ReturnUrl"]')?.value;
        window.location.href = returnUrl && isLocalUrl(returnUrl) ? returnUrl : '/';
      },
      onError: (message) => {
        showStatus(message, true);
        resetLoginButton(btn);
      }
    });
  } catch (error) {
    console.error('Unexpected passkey login error:', error);
    showStatus('An unexpected error occurred.', true);
    resetLoginButton(btn);
  }
}

async function handleDiscoverablePasskeyLogin() {
  const btn = document.getElementById('passkey-discoverable-login-btn');

  try {
    await authenticateWithDiscoverablePasskey({
      onStart: () => setButtonLoading(btn, 'Starting...'),
      onWaitingForAuthenticator: () => setButtonLoading(btn, 'Waiting for authenticator...'),
      onSuccess: (result) => {
        const returnUrl = document.querySelector('input[name="ReturnUrl"]')?.value;
        window.location.href = returnUrl && isLocalUrl(returnUrl) ? returnUrl : '/';
      },
      onError: (message) => {
        showStatus(message, true);
        resetDiscoverableLoginButton(btn);
      }
    });
  } catch (error) {
    console.error('Unexpected passkey login error:', error);
    showStatus('An unexpected error occurred.', true);
    resetDiscoverableLoginButton(btn);
  }
}

function initLoginPage(loginBtn, discoverableLoginBtn) {
  loginBtn.addEventListener('click', handlePasskeyLogin);
  if (discoverableLoginBtn) {
    discoverableLoginBtn.addEventListener('click', handleDiscoverablePasskeyLogin);
  }
}

// =============================================================================
// Profile Page - Passkey Registration
// =============================================================================

function resetRegisterButton(btn) {
  btn.disabled = false;
  btn.innerHTML = '<i class="bi bi-plus-circle me-2"></i>Add passkey';
}

function getDefaultPasskeyName() {
  const date = new Date().toLocaleString([], { dateStyle: 'short', timeStyle: 'medium' });
  return `Passkey (${date})`;
}

async function handleRegisterPasskey(name) {
  const btn = document.getElementById('add-passkey-btn');

  try {
    await registerPasskey(name, {
      onStart: () => setButtonLoading(btn, 'Starting...'),
      onWaitingForAuthenticator: () => setButtonLoading(btn, 'Waiting for authenticator...'),
      onSuccess: (result) => {
        // Navigate via GET to refresh the page. Using a query parameter ensures
        // the URL always differs from the current one, forcing a real navigation
        // and avoiding a reload() which could re-POST a prior form submission.
        window.location.href = window.location.pathname + '?registered';
      },
      onError: (message) => {
        showStatus(message, true);
        resetRegisterButton(btn);
      }
    });
  } catch (error) {
    console.error('Unexpected passkey registration error:', error);
    showStatus('An unexpected error occurred.', true);
    resetRegisterButton(btn);
  }
}

function initProfilePage(btn) {
  const modal = document.getElementById('passkeyNameModal');
  const nameInput = document.getElementById('passkeyNameInput');
  const confirmBtn = document.getElementById('confirmPasskeyBtn');
  const cancelBtn = document.getElementById('cancelPasskeyBtn');

  function showModal() {
    modal.style.display = 'flex';
    nameInput.focus();
  }

  function hideModal() {
    modal.style.display = 'none';
  }

  modal.addEventListener('click', (e) => {
    if (e.target === modal) {
      hideModal();
    }
  });

  btn.addEventListener('click', () => {
    nameInput.value = getDefaultPasskeyName();
    showModal();
    nameInput.select();
  });

  cancelBtn.addEventListener('click', () => {
    hideModal();
  });

  confirmBtn.addEventListener('click', async () => {
    const name = nameInput.value.trim() || getDefaultPasskeyName();
    hideModal();
    await handleRegisterPasskey(name);
  });
}

// =============================================================================
// Initialization
// =============================================================================

document.addEventListener('DOMContentLoaded', function () {
  const loginBtn = document.getElementById('passkey-login-btn');
  const discoverableLoginBtn = document.getElementById('passkey-discoverable-login-btn');
  const registerBtn = document.getElementById('add-passkey-btn');

  // Check for WebAuthn support
  if (!window.PublicKeyCredential) {
    if (loginBtn) {
      disableButtonWithMessage(loginBtn, 'Passkeys Not Supported');
    }
    if (discoverableLoginBtn) {
      disableButtonWithMessage(discoverableLoginBtn, 'Passkeys Not Supported');
    }
    if (registerBtn) {
      disableButtonWithMessage(registerBtn, 'Passkeys Not Supported');
      showStatus('Your browser does not support passkeys (WebAuthn).', true);
    }
    return;
  }

  const params = new URLSearchParams(window.location.search);
  if (params.has('registered')) {
    history.replaceState(null, '', window.location.pathname);
    showStatus('Passkey registered successfully!', false);
  }

  // Initialize based on which page we're on
  if (loginBtn) {
    initLoginPage(loginBtn, discoverableLoginBtn);
  }

  if (registerBtn) {
    initProfilePage(registerBtn);
  }
});
