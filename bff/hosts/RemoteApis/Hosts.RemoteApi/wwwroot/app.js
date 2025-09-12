document.addEventListener('DOMContentLoaded', function() {
    let counter = 0;
    const counterLabel = document.getElementById('counter-label');
    const addBtn = document.getElementById('add-btn');

    addBtn.addEventListener('click', function() {
        counter++;
        counterLabel.textContent = `Counter: ${counter}`;
    });
});
