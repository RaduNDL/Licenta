document.addEventListener('DOMContentLoaded', function () {
    const searchInput = document.getElementById('patientSearch');
    const table = document.getElementById('patientsTable');

    if (searchInput && table) {
        searchInput.addEventListener('keyup', function () {
            const filter = searchInput.value.toLowerCase();
            const rows = table.getElementsByTagName('tr');

            for (let i = 1; i < rows.length; i++) {
                const nameCell = rows[i].getElementsByTagName('td')[0];
                const emailCell = rows[i].getElementsByTagName('td')[1];

                if (nameCell && emailCell) {
                    const nameText = nameCell.textContent || nameCell.innerText;
                    const emailText = emailCell.textContent || emailCell.innerText;

                    if (nameText.toLowerCase().indexOf(filter) > -1 || emailText.toLowerCase().indexOf(filter) > -1) {
                        rows[i].style.display = "";
                    } else {
                        rows[i].style.display = "none";
                    }
                }
            }
        });
    }
});