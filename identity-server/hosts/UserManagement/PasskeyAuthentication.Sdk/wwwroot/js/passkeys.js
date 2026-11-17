// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

function base64UrlToArrayBuffer(base64url) {
  let base64 = base64url.replace(/-/g, '+').replace(/_/g, '/');
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

// Registration

async function beginRegistration() {
  const response = await fetch('/passkeys/register/begin', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' }
  });

  if (!response.ok) {
    throw new Error(await tryParseJsonError(response,'Failed to start passkey registration.'));
  }

  return await response.json();
}

async function createCredential(options) {
  const credential = await navigator.credentials.create({
    publicKey: {
      challenge: base64UrlToArrayBuffer(options.challenge),
      rp: {
        ...(options.relyingParty.id && { id: options.relyingParty.id }),
        name: options.relyingParty.name
      },
      user: {
        id: base64UrlToArrayBuffer(options.user.id),
        name: options.user.name,
        displayName: options.user.displayName
      },
      pubKeyCredParams: options.pubKeyCredParams.map(p => ({ type: p.type, alg: p.alg })),
      attestation: options.attestation,
      authenticatorSelection: options.authenticatorSelection
    }
  });

  if (!credential) {
    throw new Error('No credential was created.');
  }

  return credential;
}

async function completeRegistration(challengeId, credential, name) {
  const response = await fetch('/passkeys/register/complete', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      challengeId,
      id: credential.id,
      rawId: arrayBufferToBase64Url(credential.rawId),
      type: credential.type,
      response: {
        clientDataJSON: arrayBufferToBase64Url(credential.response.clientDataJSON),
        attestationObject: arrayBufferToBase64Url(credential.response.attestationObject),
        transports: credential.response.getTransports?.() ?? []
      },
      name
    })
  });

  if (!response.ok) {
    throw new Error(await tryParseJsonError(response, 'Failed to complete registration.'));
  }
}

// Authentication

async function beginAuthentication() {
  const response = await fetch('/passkeys/authenticate/begin', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' }
  });

  if (!response.ok) {
    throw new Error(await tryParseJsonError(response, 'Failed to begin passkey authentication.'));
  }

  return await response.json();
}

async function getCredential(options) {
  const assertion = await navigator.credentials.get({
    publicKey: {
      challenge: base64UrlToArrayBuffer(options.challenge),
      ...(options.rpId && { rpId: options.rpId }),
      allowCredentials: (options.allowCredentials ?? []).map(c => ({
        type: 'public-key',
        id: base64UrlToArrayBuffer(c.id)
      })),
      userVerification: options.userVerification || 'preferred'
    }
  });

  if (!assertion) {
    throw new Error('No credential was returned.');
  }

  return assertion;
}

async function completeAuthentication(challengeId, assertion) {
  const body = {
    challengeId,
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

  const response = await fetch('/passkeys/authenticate/complete', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  });

  if (!response.ok) {
    throw new Error(await tryParseJsonError(response, 'Failed to complete authentication.'));
  }
}

// Removal

async function removePasskey(credentialId) {
  const response = await fetch('/passkeys?credentialId=' + encodeURIComponent(credentialId), {
    method: 'DELETE'
  });

  if (!response.ok) {
    throw new Error(await tryParseJsonError(response, 'Failed to remove passkey.'));
  }
}
