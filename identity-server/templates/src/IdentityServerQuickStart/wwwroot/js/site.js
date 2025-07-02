import Tags from "../lib/bootstrap5-tags/tags.js";

$(function() {
  $('[data-confirm]').on('click', function() {
    return confirm($(this).data('confirm'));
  });

  $('form :input').on('change', function () {
    $(this).closest('form').attr('data-changed', 'true');
  });

  $('[data-unsaved]').on('click', function () {
    let changed = $(this).closest('form').attr('data-changed');
    let message = $(this).data('unsaved');
    if (changed) {
      return confirm(message);
    }
    return true;
  });

  Tags.init();
});

