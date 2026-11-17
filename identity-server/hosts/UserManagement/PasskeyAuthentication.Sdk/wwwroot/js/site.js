// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

function showStatus(statusDiv, message, isError) {
  statusDiv.className = 'alert alert-dismissible fade show ' + (isError ? 'alert-danger' : 'alert-success');
  statusDiv.textContent = message;
  const closeBtn = document.createElement('button');
  closeBtn.type = 'button';
  closeBtn.className = 'btn-close';
  closeBtn.ariaLabel = 'Close';
  closeBtn.addEventListener('click', () => {
    statusDiv.textContent = '';
    statusDiv.className = 'd-none';
  });
  statusDiv.appendChild(closeBtn);
}
