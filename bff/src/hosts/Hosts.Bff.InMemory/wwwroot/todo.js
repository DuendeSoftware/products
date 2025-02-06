const todoUrl = "/api/todos";
const todos = document.getElementById("todos");

document.getElementById("createNewButton").addEventListener("click", createTodo);
const name = document.getElementById("name");
const date = document.getElementById("date");


async function createTodo() {
    let request = new Request(todoUrl, {
        method: "POST",
        headers: {
            "content-type": "application/json",
            'x-csrf': '1'
        },
        body: JSON.stringify({
            name: name.value,
            date: date.value,
        })
    });

    let result = await fetch(request);
    if (result.ok) {
        var item = await result.json();
        addRow(item);
    }
}

async function showTodos() {
    let result = await fetch(new Request(todoUrl, {
        headers: {
            'x-csrf': '1'
        },
    }));

    if (result.ok) {
        let data = await result.json();
        data.forEach(item => addRow(item));
    }
}

function addRow(item) {
    let row = document.createElement("tr");
    row.dataset.id = item.id;
    todos.appendChild(row);

    function addCell(row, text) {
        let cell = document.createElement("td");
        cell.innerText = text;
        row.appendChild(cell);
    }

    function addDeleteButton(row, id) {
        let cell = document.createElement("td");
        row.appendChild(cell);
        let btn = document.createElement("button");
        cell.appendChild(btn);
        btn.textContent = "delete";
        btn.addEventListener("click", async () => await deleteTodo(id));
    }

    addDeleteButton(row, item.id);
    addCell(row, item.id);
    addCell(row, item.date);
    addCell(row, item.name);
    addCell(row, item.user);
}


async function deleteRow(id) {
    let row = todos.querySelector(`tr[data-id='${id}']`);
    if (row) {
        todos.removeChild(row);
    }
}

async function deleteTodo(id) {
    let request = new Request(todoUrl + "/" + id, {
        headers: {
            'x-csrf': '1'
        },
        method: "DELETE"
    });

    let result = await fetch(request);
    if (result.ok) {
        deleteRow(id);
    }
}


showTodos();


