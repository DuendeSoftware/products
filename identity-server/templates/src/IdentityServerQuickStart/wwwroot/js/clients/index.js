document.addEventListener('DOMContentLoaded', () => {

  document.querySelectorAll('.client-edit-link').forEach(link => {
    link.addEventListener('click', event => {
      event.preventDefault();
      const id = link.getAttribute('data-client-id');
      const body = document.getElementById('editClientModalBody');
      body.innerHTML = '<div class="text-center py-5"><div class="spinner-border" role="status"></div></div>';

      fetch(`/Admin/Clients/Edit?id=${id}`)
        .then(r => r.text())
        .then(html => {
          body.innerHTML = html;
        });

      // find any <script> tags in that HTML and re-insert them to execute
      body.querySelectorAll('script').forEach(old => {
        const s = document.createElement('script');
        if (old.src) {
          s.src = old.src;
          s.async = false;
        } else {
          s.textContent = old.textContent;
        }
        old.replaceWith(s);
      });

      new bootstrap.Modal(document.getElementById('editClientModal')).show();
    });
  });

});
