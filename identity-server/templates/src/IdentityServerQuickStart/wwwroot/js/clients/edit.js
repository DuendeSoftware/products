// edit-client-submit-handler.js
const form = document.getElementById('editClientForm');

if (!form) {
  console.error("No Form!");
  return;
}

console.log("FORM");

form.addEventListener('submit', async e => {

  console.log("SUBMIT");

  e.preventDefault();

  // grab antiforgery token
  const token = form.querySelector('[name="__RequestVerificationToken"]')?.value || '';

  // build FormData + include clicked button
  const data = new FormData(form);

  if (e.submitter && e.submitter.name) {
    data.append(e.submitter.name, e.submitter.value);
  }

  console.log(data);
  return;

  try {
    const resp = await fetch(form.action, {
      method: 'POST',
      credentials: 'same-origin',
      redirect: 'manual',
      headers: {
        'RequestVerificationToken': token,
        'X-Requested-With': 'XMLHttpRequest'
      },
      body: data
    });

    const html = await resp.text();
    const body = document.getElementById('editClientModalBody');
    body.innerHTML = html;

  } catch (err) {
    console.error('Error posting form:', err);
    alert('An error occurred while saving. Please try again.');
  }

});
