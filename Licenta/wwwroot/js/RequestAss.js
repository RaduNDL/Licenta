(function () {
    const input = document.getElementById("reqassSearch");
    const table = document.getElementById("reqassTable");
    if (!input || !table) return;

    const rows = Array.from(table.querySelectorAll("tbody tr"));

    function normalize(s) {
        return (s || "").toLowerCase().replace(/\s+/g, " ").trim();
    }

    function apply() {
        const q = normalize(input.value);
        if (!q) {
            rows.forEach(r => r.style.display = "");
            return;
        }

        rows.forEach(r => {
            const text = normalize(r.innerText);
            r.style.display = text.includes(q) ? "" : "none";
        });
    }

    input.addEventListener("input", apply);
    window.addEventListener("load", apply);
})();
