document.addEventListener("DOMContentLoaded", () => {
    enhanceStatus();
    enhanceFileCopy();
    wrapContainer();
});

function wrapContainer() {
    const detailsContainer = document.querySelector('.attachment-details-container');
    if (!detailsContainer) return;

    const h2 = detailsContainer.querySelector('h2');
    const headerDiv = document.createElement('div');
    headerDiv.className = 'attachment-header';
    if (h2) {
        h2.parentNode.insertBefore(headerDiv, h2);
        headerDiv.appendChild(h2);
    }

    const backBtn = detailsContainer.querySelector('.btn-secondary');
    if (backBtn) {
        const actionDiv = document.createElement('div');
        actionDiv.className = 'action-bar';
        backBtn.parentNode.insertBefore(actionDiv, backBtn);
        actionDiv.appendChild(backBtn);
    }
}

function enhanceStatus() {
    const dts = document.querySelectorAll("dt");
    dts.forEach(dt => {
        if (dt.textContent.trim() === "Status") {
            const dd = dt.nextElementSibling;
            const statusText = dd.textContent.trim();
            const wrapper = document.createElement("span");
            const dot = document.createElement("span");

            wrapper.className = "status-indicator";
            dot.className = "status-dot";

            if (/pending/i.test(statusText)) wrapper.classList.add("status-pending");
            else if (/completed|processed/i.test(statusText)) wrapper.classList.add("status-completed");
            else wrapper.classList.add("status-error");

            dd.innerHTML = "";
            wrapper.appendChild(dot);
            wrapper.appendChild(document.createTextNode(statusText));
            dd.appendChild(wrapper);
        }
    });
}

function enhanceFileCopy() {
    const fileLabel = document.querySelector(".text-muted");
    if (!fileLabel) return;

    fileLabel.addEventListener("click", () => {
        navigator.clipboard.writeText(fileLabel.textContent.trim());
        showToast("Filename copied!");
    });
}

function showToast(message) {
    let toast = document.querySelector(".toast-notification");
    if (!toast) {
        toast = document.createElement("div");
        toast.className = "toast-notification";
        document.body.appendChild(toast);
    }

    toast.textContent = message;
    toast.classList.add("toast-visible");

    setTimeout(() => {
        toast.classList.remove("toast-visible");
    }, 3000);
}