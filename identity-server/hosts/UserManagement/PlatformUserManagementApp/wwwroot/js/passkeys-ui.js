// UI layer for passkey authentication - handles DOM manipulation and event binding

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
// Login Page - Discoverable Passkey Authentication
// =============================================================================

function resetDiscoverableLoginButton(btn) {
  btn.disabled = false;
  btn.innerHTML = '<i class="bi bi-fingerprint me-2"></i>Sign in with passkey';
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

function initLoginPage(discoverableLoginBtn) {
  if (discoverableLoginBtn) {
    discoverableLoginBtn.addEventListener('click', handleDiscoverablePasskeyLogin);
  }
}

// =============================================================================
// Home Page - Passkey Registration
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

function initRegistrationPage(btn) {
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
  const discoverableLoginBtn = document.getElementById('passkey-discoverable-login-btn');
  const registerBtn = document.getElementById('add-passkey-btn');

  // Check for WebAuthn support
  if (!window.PublicKeyCredential) {
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
  if (discoverableLoginBtn) {
    initLoginPage(discoverableLoginBtn);
  }

  if (registerBtn) {
    initRegistrationPage(registerBtn);
  }
});
