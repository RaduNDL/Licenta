(() => {
    const $ = (q) => document.querySelector(q);

    const localTime = $("#localTime");
    const todayLabel = $("#todayLabel");
    const systemLine = $("#systemLine");

    const btnQuick = $("#btnQuick");
    const btnClose = $("#btnClose");
    const quickPanel = $("#quickPanel");
    const scrim = $("#scrim");
    const quickSearch = $("#quickSearch");
    const quickList = $("#quickList");

    const setClock = () => {
        const now = new Date();
        localTime.textContent = now.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
        todayLabel.textContent = now.toLocaleDateString([], { weekday: "long", year: "numeric", month: "long", day: "numeric" });
    };

    const statusRotate = () => {
        const items = [
            "System ready",
            "Secure session active",
            "Notifications online",
            "Audit logging enabled"
        ];
        let i = 0;
        setInterval(() => {
            i = (i + 1) % items.length;
            systemLine.textContent = items[i];
        }, 2400);
    };

    const openPanel = () => {
        quickPanel.classList.add("open");
        scrim.classList.add("open");
        quickPanel.setAttribute("aria-hidden", "false");
        scrim.setAttribute("aria-hidden", "false");
        setTimeout(() => quickSearch && quickSearch.focus(), 50);
    };

    const closePanel = () => {
        quickPanel.classList.remove("open");
        scrim.classList.remove("open");
        quickPanel.setAttribute("aria-hidden", "true");
        scrim.setAttribute("aria-hidden", "true");
        if (quickSearch) quickSearch.value = "";
        filterLinks("");
    };

    const filterLinks = (q) => {
        const query = (q || "").trim().toLowerCase();
        const links = quickList ? Array.from(quickList.querySelectorAll("a.li")) : [];
        let firstVisible = null;

        for (const a of links) {
            const t = a.getAttribute("data-title") || "";
            const ok = query.length === 0 || t.includes(query);
            a.style.display = ok ? "" : "none";
            if (ok && !firstVisible) firstVisible = a;
        }

        if (firstVisible) firstVisible.classList.add("first");
        for (const a of links) {
            if (a !== firstVisible) a.classList.remove("first");
        }
    };

    const openFirst = () => {
        const links = quickList ? Array.from(quickList.querySelectorAll("a.li")) : [];
        const first = links.find(x => x.style.display !== "none");
        if (first) window.location.href = first.href;
    };

    setClock();
    setInterval(setClock, 1000 * 15);
    statusRotate();

    if (btnQuick) btnQuick.addEventListener("click", openPanel);
    if (btnClose) btnClose.addEventListener("click", closePanel);
    if (scrim) scrim.addEventListener("click", closePanel);

    document.addEventListener("keydown", (e) => {
        const k = e.key.toLowerCase();
        const isOpen = quickPanel && quickPanel.classList.contains("open");

        if (k === "escape" && isOpen) {
            e.preventDefault();
            closePanel();
            return;
        }

        if ((e.ctrlKey || e.metaKey) && k === "k") {
            e.preventDefault();
            if (isOpen) closePanel();
            else openPanel();
            return;
        }

        if (k === "enter" && isOpen) {
            e.preventDefault();
            openFirst();
            return;
        }
    });

    if (quickSearch) {
        quickSearch.addEventListener("input", (e) => {
            filterLinks(e.target.value);
        });
    }
})();
