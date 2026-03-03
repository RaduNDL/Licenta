(function () {
    const form = document.getElementById("requestForm");
    const btn = document.getElementById("submitBtn");
    const overlay = document.getElementById("successOverlay");
    const bar = document.getElementById("successBar");
    const charCount = document.getElementById("charCount");
    const bodyText = document.getElementById("bodyText");

    if (bodyText && charCount) {
        const update = () => (charCount.textContent = String(bodyText.value.length));
        bodyText.addEventListener("input", update);
        update();
    }

    if (!form || !btn) return;

    form.addEventListener("submit", function () {
        btn.disabled = true;

        const text = btn.querySelector(".btn-text");
        const sending = btn.querySelector(".btn-sending");
        const icon = btn.querySelector(".btn-icon");

        if (text) text.classList.add("d-none");
        if (sending) sending.classList.remove("d-none");
        if (icon) icon.classList.add("d-none");
    });

    const params = new URLSearchParams(window.location.search);
    if (params.get("sent") === "1" && overlay && bar) {
        overlay.classList.remove("d-none");
        let p = 0;
        const t = setInterval(() => {
            p += 4;
            bar.style.width = p + "%";
            if (p >= 100) clearInterval(t);
        }, 30);
    }
})();