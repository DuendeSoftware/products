// Pure passkey/WebAuthn functionality - no DOM dependencies

// Base64URL encoding/decoding utilities for WebAuthn
function base64UrlToArrayBuffer(base64url) {
  // Replace URL-safe characters with standard Base64 characters
  let base64 = base64url.replace(/-/g, '+').replace(/_/g, '/');
  // Add padding if necessary
  while (base64.length % 4 !== 0) {
    base64 += '=';
  }
  const binaryString = atob(base64);
  return Uint8Array.from(binaryString, char => char.charCodeAt(0)).buffer;
}

function arrayBufferToBase64Url(buffer) {
  const bytes = new Uint8Array(buffer);
  let binaryString = '';
  for (let i = 0; i < bytes.length; i++) {
    binaryString += String.fromCharCode(bytes[i]);
  }
  const base64 = btoa(binaryString);
  // Convert to Base64URL: replace +/= with -_
  return base64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
}

async function tryParseJsonError(response, fallbackMessage) {
  try {
    const body = await response.json();
    return body.detail || body.title || fallbackMessage;
  } catch {
    return fallbackMessage;
  }
}

/**
 * Register a new passkey for the current user.
 * @param {string} name - Optional user-friendly name for the passkey (max 255 characters)
 * @param {Object} callbacks - UI callbacks for status updates
 * @param {Function} callbacks.onStart - Called when registration begins
 * @param {Function} callbacks.onWaitingForAuthenticator - Called when waiting for user interaction
 * @param {Function} callbacks.onSuccess - Called with success result ({credentialId})
 * @param {Function} callbacks.onError - Called with error message
 */
async function registerPasskey(name, callbacks = {}) {
  callbacks.onStart?.();

  try {
    // Step 1: Fetch registration options from the server
    const beginResponse = await fetch('{registerBeginUrl}', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      }
    });

    if (!beginResponse.ok) {
      if (beginResponse.status === 401) {
        throw new Error('You must be signed in to register a passkey.');
      }
      throw new Error('Failed to start passkey registration.');
    }

    const {challengeId, options} = await beginResponse.json();

    // Step 2: Convert Base64URL strings to ArrayBuffers for WebAuthn API
    const publicKeyOptions = {
      challenge: base64UrlToArrayBuffer(options.challenge),
      rp: {
        ...(options.relyingParty.id && {id: options.relyingParty.id}),
        name: options.relyingParty.name
      },
      user: {
        id: base64UrlToArrayBuffer(options.user.id),
        name: options.user.name,
        displayName: options.user.displayName
      },
      pubKeyCredParams: options.pubKeyCredParams.map(p => ({
        type: p.type,
        alg: p.alg
      })),
      attestation: options.attestation,
      authenticatorSelection: options.authenticatorSelection
    };

    callbacks.onWaitingForAuthenticator?.();

    // Step 3: Call navigator.credentials.create to trigger the WebAuthn dialog
    const credential = await navigator.credentials.create({
      publicKey: publicKeyOptions
    });

    if (!credential) {
      throw new Error('No credential was created.');
    }

    // Step 4: Complete registration by sending attestation to server
    const completeResponse = await fetch('{registerCompleteUrl}', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({
        challengeId: challengeId,
        id: credential.id,
        rawId: arrayBufferToBase64Url(credential.rawId),
        type: credential.type,
        response: {
          clientDataJSON: arrayBufferToBase64Url(credential.response.clientDataJSON),
          attestationObject: arrayBufferToBase64Url(credential.response.attestationObject),
          transports: credential.response.getTransports?.() ?? []
        },
        name: name
      })
    });

    if (!completeResponse.ok) {
      throw new Error(await tryParseJsonError(completeResponse, 'Failed to complete registration.'));
    }

    const result = await completeResponse.json();
    callbacks.onSuccess?.(result);

  } catch (error) {
    console.error('Passkey registration error:', error);

    let message;
    if (error.name === 'NotAllowedError') {
      message = 'Passkey registration was cancelled or not allowed.';
    } else if (error.name === 'NotSupportedError') {
      message = 'Passkeys are not supported on this device or browser.';
    } else {
      message = error.message || 'An error occurred during passkey registration.';
    }

    callbacks.onError?.(message);
  }
}

/**
 * Authenticate with a second-factor passkey (for MFA flows).
 * The caller is responsible for submitting that payload to an application-owned
 * completion endpoint and handling session promotion.
 * @param {Object} callbacks - UI callbacks for status updates
 * @param {Function} callbacks.onStart - Called when authentication begins
 * @param {Function} callbacks.onWaitingForAuthenticator - Called when waiting for user interaction
 * @param {Function} callbacks.onSuccess - Called with complete request payload
 * @param {Function} callbacks.onError - Called with error message
 */
async function authenticateWithPasskey(callbacks = {}) {
  callbacks.onStart?.();

  try {
    // Step 1: Fetch authentication options from the server
    const beginResponse = await fetch('{authenticateBeginUrl}', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      }
    });

    if (!beginResponse.ok) {
      throw new Error(await tryParseJsonError(beginResponse, 'Failed to start passkey authentication.'));
    }

    const {challengeId, options} = await beginResponse.json();

    // Step 2: Convert Base64URL strings to ArrayBuffers for WebAuthn API
    const publicKeyOptions = {
      challenge: base64UrlToArrayBuffer(options.challenge),
      ...(options.rpId && {rpId: options.rpId}),
      allowCredentials: options.allowCredentials.map(cred => ({
        type: 'public-key',
        id: base64UrlToArrayBuffer(cred.id)
      })),
      userVerification: options.userVerification || 'preferred'
    };

    callbacks.onWaitingForAuthenticator?.();

    // Step 3: Call navigator.credentials.get to trigger the WebAuthn dialog
    const assertion = await navigator.credentials.get({
      publicKey: publicKeyOptions
    });

    if (!assertion) {
      throw new Error('No credential was returned.');
    }

    const completeRequest = {
      challengeId: challengeId,
      id: assertion.id,
      rawId: arrayBufferToBase64Url(assertion.rawId),
      type: assertion.type,
      response: {
        clientDataJSON: arrayBufferToBase64Url(assertion.response.clientDataJSON),
        authenticatorData: arrayBufferToBase64Url(assertion.response.authenticatorData),
        signature: arrayBufferToBase64Url(assertion.response.signature),
        userHandle: assertion.response.userHandle
          ? arrayBufferToBase64Url(assertion.response.userHandle)
          : null
      }
    };

    callbacks.onSuccess?.(completeRequest);

  } catch (error) {
    console.error('Passkey authentication error:', error);

    let message;
    if (error.name === 'NotAllowedError') {
      message = 'Passkey authentication was cancelled or not allowed.';
    } else if (error.name === 'NotSupportedError') {
      message = 'Passkeys are not supported on this device or browser.';
    } else {
      message = error.message || 'An error occurred during passkey authentication.';
    }

    callbacks.onError?.(message);
  }
}

/**
 * Authenticate with a discoverable (usernameless) passkey.
 * @param {Object} callbacks - UI callbacks for status updates
 * @param {Function} callbacks.onStart - Called when authentication begins
 * @param {Function} callbacks.onWaitingForAuthenticator - Called when waiting for user interaction
 * @param {Function} callbacks.onSuccess - Called with success result ({userVerified, backedUp})
 * @param {Function} callbacks.onError - Called with error message
 */
async function authenticateWithDiscoverablePasskey(callbacks = {}) {
  callbacks.onStart?.();

  try {
    // Step 1: Fetch authentication options from the server (no username)
    const beginResponse = await fetch('{authenticateDiscoverableBeginUrl}', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      }
    });

    if (!beginResponse.ok) {
      throw new Error(await tryParseJsonError(beginResponse, 'Failed to start passkey authentication.'));
    }

    const {challengeId, options} = await beginResponse.json();

    // Step 2: Convert Base64URL strings to ArrayBuffers for WebAuthn API
    const publicKeyOptions = {
      challenge: base64UrlToArrayBuffer(options.challenge),
      ...(options.rpId && {rpId: options.rpId}),
      allowCredentials: [], // Empty for discoverable credentials
      userVerification: options.userVerification || 'preferred'
    };

    callbacks.onWaitingForAuthenticator?.();

    // Step 3: Call navigator.credentials.get to trigger the WebAuthn dialog
    const assertion = await navigator.credentials.get({
      publicKey: publicKeyOptions
    });

    if (!assertion) {
      throw new Error('No credential was returned.');
    }

    // Step 4: Complete authentication by sending assertion to server
    const completeResponse = await fetch('{authenticateCompleteUrl}', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({
        challengeId: challengeId,
        id: assertion.id,
        rawId: arrayBufferToBase64Url(assertion.rawId),
        type: assertion.type,
        response: {
          clientDataJSON: arrayBufferToBase64Url(assertion.response.clientDataJSON),
          authenticatorData: arrayBufferToBase64Url(assertion.response.authenticatorData),
          signature: arrayBufferToBase64Url(assertion.response.signature),
          userHandle: assertion.response.userHandle
            ? arrayBufferToBase64Url(assertion.response.userHandle)
            : null
        }
      })
    });

    if (!completeResponse.ok) {
      throw new Error(await tryParseJsonError(completeResponse, 'Failed to complete authentication.'));
    }

    const result = await completeResponse.json();
    callbacks.onSuccess?.(result);

  } catch (error) {
    console.error('Passkey authentication error:', error);

    let message;
    if (error.name === 'NotAllowedError') {
      message = 'Passkey authentication was cancelled or not allowed.';
    } else if (error.name === 'NotSupportedError') {
      message = 'Passkeys are not supported on this device or browser.';
    } else {
      message = error.message || 'An error occurred during passkey authentication.';
    }

    callbacks.onError?.(message);
  }
}
