(function () {
    const textarea = document.querySelector(".schedule-textarea");
    const lineCountEl = document.getElementById("scheduleLineCount");
    const btnInsertExample = document.getElementById("btnInsertExample");
    const btnClear = document.getElementById("btnClear");

    if (!textarea) return;

    function normalizeLines(text) {
        return (text || "")
            .replace(/\r\n/g, "\n")
            .replace(/\r/g, "\n");
    }

    function countLines(text) {
        const t = normalizeLines(text).trim();
        if (!t) return 0;
        return t.split("\n").filter(x => x.trim().length > 0).length;
    }

    function autosize() {
        textarea.style.height = "auto";
        textarea.style.height = Math.max(textarea.scrollHeight, 220) + "px";
    }

    function refresh() {
        const lines = countLines(textarea.value);
        if (lineCountEl) lineCountEl.textContent = String(lines);
        autosize();
    }

    function insertExample() {
        const example =
            `Monday 09:00 - 13:00
Tuesday 14:00 - 18:00
Thursday from 10 AM to 2 PM
Friday 09:30 - 13:00`;

        const current = normalizeLines(textarea.value).trim();
        textarea.value = current ? (current + "\n" + example) : example;
        textarea.focus();
        refresh();
    }

    function clearAll() {
        textarea.value = "";
        textarea.focus();
        refresh();
    }

    textarea.addEventListener("input", refresh);
    window.addEventListener("load", refresh);

    if (btnInsertExample) btnInsertExample.addEventListener("click", insertExample);
    if (btnClear) btnClear.addEventListener("click", clearAll);
})();
