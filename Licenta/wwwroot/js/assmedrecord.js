function filterRecords() {
    const input = document.getElementById('globalSearch');
    const filter = input.value.toLowerCase();

    filterTable('activeTable', filter);

    filterTable('completedTable', filter);
}

function filterTable(tableId, filter) {
    const table = document.getElementById(tableId);
    if (!table) return;

    const rows = table.getElementsByTagName('tr');

    for (let i = 1; i < rows.length; i++) {
        const row = rows[i];
        const cells = row.getElementsByTagName('td');
        let textContent = '';

        for (let j = 0; j < cells.length - 1; j++) { 
            textContent += cells[j].textContent || cells[j].innerText;
        }

        if (textContent.toLowerCase().indexOf(filter) > -1) {
            row.style.display = "";
        } else {
            row.style.display = "none";
        }
    }
}