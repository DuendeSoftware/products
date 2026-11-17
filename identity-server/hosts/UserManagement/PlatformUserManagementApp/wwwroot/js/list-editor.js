// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

// Client-side add/remove for list-of-complex attribute items.
// Works with [data-list-attr] containers that hold .list-item elements
// and .list-item-add / .list-item-remove buttons.

document.addEventListener('DOMContentLoaded', () => {
    document.addEventListener('click', (e) => {
        const removeBtn = e.target.closest('.list-item-remove');
        if (removeBtn) {
            const listItem = removeBtn.closest('.list-item');
            if (listItem) {
                listItem.remove();
            }
            return;
        }

        const addBtn = e.target.closest('.list-item-add');
        if (addBtn) {
            const container = addBtn.closest('[data-list-attr]');
            if (!container) return;

            const items = container.querySelectorAll('.list-item');
            const lastItem = items[items.length - 1];
            if (!lastItem) return;

            // Find the highest existing index
            let maxIndex = -1;
            items.forEach(item => {
                item.querySelectorAll('input, select').forEach(el => {
                    const match = el.name && el.name.match(/\[(\d+)]/);
                    if (match) {
                        maxIndex = Math.max(maxIndex, parseInt(match[1], 10));
                    }
                });
            });
            const newIndex = maxIndex + 1;

            // Clone the last item
            const clone = lastItem.cloneNode(true);

            // Update indices and clear values
            clone.querySelectorAll('input, select').forEach(el => {
                if (el.name) {
                    el.name = el.name.replace(/\[\d+]/, `[${newIndex}]`);
                }
                if (el.id) {
                    el.id = el.id.replace(/\[\d+]/, `[${newIndex}]`);
                }
                if (el.tagName === 'SELECT') {
                    el.selectedIndex = 0;
                } else {
                    el.value = '';
                }
            });

            // Update label for attributes
            clone.querySelectorAll('label').forEach(label => {
                if (label.htmlFor) {
                    label.htmlFor = label.htmlFor.replace(/\[\d+]/, `[${newIndex}]`);
                }
            });

            // Insert before the Add button
            container.insertBefore(clone, addBtn);
        }
    });
});
