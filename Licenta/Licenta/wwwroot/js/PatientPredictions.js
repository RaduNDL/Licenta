(function () {
    const root = document.getElementById("predictions-root");
    if (!root) return;

    const listUrl = root.dataset.listUrl;
    if (!listUrl) return;

    function escapeHtml(value) {
        if (value === null || value === undefined) return "";
        return String(value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function isMalignant(resultLabel) {
        const label = (resultLabel || "").toString().trim().toUpperCase();
        return (
            label.includes("MALIGNANT") ||
            label.includes("CANCER") ||
            label.includes("POSITIVE")
        );
    }

    function getResultClass(resultLabel) {
        return isMalignant(resultLabel) ? "result-cancer" : "result-safe";
    }

    function getFillClass(resultLabel) {
        return isMalignant(resultLabel) ? "confidence-cancer" : "confidence-safe";
    }

    function normalizePercent(value) {
        if (value === null || value === undefined || isNaN(value)) return 0;

        let numeric = Number(value);

        if (numeric <= 1) {
            numeric = numeric * 100;
        }

        if (numeric < 0) numeric = 0;
        if (numeric > 100) numeric = 100;

        return numeric;
    }

    function formatPercent(value) {
        if (value === null || value === undefined) return "-";
        const normalized = normalizePercent(value);
        return normalized.toFixed(2) + "%";
    }

    function formatDate(utcValue) {
        if (!utcValue) return "-";
        const d = new Date(utcValue);
        return d.toLocaleString();
    }

    function animateMeters(scope) {
        const container = scope || document;
        const meters = container.querySelectorAll(".meter-fill[data-percent]");

        meters.forEach((meter) => {
            const percent = normalizePercent(meter.getAttribute("data-percent"));

            meter.style.width = "0%";
            meter.offsetWidth;

            requestAnimationFrame(() => {
                meter.style.width = percent + "%";
            });
        });
    }

    function buildRow(item) {
        const hasProbability = item.probability !== null && item.probability !== undefined;
        const resultClass = getResultClass(item.resultLabel);
        const fillClass = getFillClass(item.resultLabel);
        const percent = normalizePercent(item.probability);

        const meterHtml = hasProbability
            ? `
                <div class="meter-track">
                    <div class="meter-fill ${fillClass}" data-percent="${percent.toFixed(2)}" style="width:0%;"></div>
                </div>`
            : "";

        return `
            <tr data-id="${escapeHtml(item.id)}">
                <td data-label="Model">
                    <span class="model-badge">${escapeHtml(item.modelName || "-")}</span>
                </td>

                <td data-label="Result" data-type="result" class="${resultClass}">
                    ${escapeHtml(item.resultLabel || "-")}
                </td>

                <td data-label="Confidence">
                    <div class="confidence-meter">
                        <span class="confidence-text">
                            ${hasProbability ? escapeHtml(formatPercent(item.probability)) : "-"}
                        </span>
                        ${meterHtml}
                    </div>
                </td>

                <td data-label="Date" class="date-cell">
                    ${escapeHtml(formatDate(item.createdAtUtc))}
                </td>

                <td data-label="Action" class="action-cell">
                    <a href="/Patient/Predictions/Details?id=${encodeURIComponent(item.id)}" class="btn-details">
                        View Analysis
                        <svg width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"></path>
                        </svg>
                    </a>
                </td>
            </tr>
        `;
    }

    function render(items) {
        let empty = document.getElementById("empty-state");
        let tableWrapper = document.getElementById("table-wrapper");
        let tbody = document.getElementById("predictions-body");

        if (!items || items.length === 0) {
            if (tableWrapper) {
                tableWrapper.remove();
            }

            if (!empty) {
                empty = document.createElement("div");
                empty.className = "empty-state";
                empty.id = "empty-state";
                empty.innerHTML = "<p>No predictions available yet.</p>";
                root.appendChild(empty);
            }

            return;
        }

        if (empty) {
            empty.remove();
        }

        if (!tableWrapper) {
            tableWrapper = document.createElement("div");
            tableWrapper.className = "table-responsive";
            tableWrapper.id = "table-wrapper";
            tableWrapper.innerHTML = `
                <table class="custom-table">
                    <thead>
                        <tr>
                            <th>Model</th>
                            <th>Result</th>
                            <th>Confidence</th>
                            <th>Date</th>
                            <th></th>
                        </tr>
                    </thead>
                    <tbody id="predictions-body"></tbody>
                </table>
            `;
            root.appendChild(tableWrapper);
        }

        tbody = document.getElementById("predictions-body");
        if (!tbody) return;

        tbody.innerHTML = items.map(buildRow).join("");
        animateMeters(tbody);
    }

    let lastSignature = "";

    async function refreshPredictions() {
        try {
            const response = await fetch(listUrl, {
                method: "GET",
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                cache: "no-store"
            });

            if (!response.ok) return;

            const items = await response.json();

            const signature = JSON.stringify(
                items.map(x => ({
                    id: x.id,
                    resultLabel: x.resultLabel,
                    probability: x.probability,
                    createdAtUtc: x.createdAtUtc
                }))
            );

            if (signature !== lastSignature) {
                lastSignature = signature;
                render(items);
            }
        } catch (error) {
            console.warn("Prediction polling failed.", error);
        }
    }

    animateMeters(document);
    refreshPredictions();
    setInterval(refreshPredictions, 3000);
})();