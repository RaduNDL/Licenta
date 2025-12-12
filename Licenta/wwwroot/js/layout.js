(function () {
    var body = document.body;
    if (!body) return;

    var userId = body.getAttribute("data-current-user-id");
    if (!userId) return;

    if (typeof signalR === "undefined") {
        console.error("SignalR client library not found. Make sure it is restored to ~/lib/microsoft/signalr.");
        return;
    }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/notificationHub")
        .withAutomaticReconnect()
        .build();

    function getBellButton() {
        var btn = document.querySelector(".notification-bell-btn");
        return btn || null;
    }

    function updateBadge() {
        var btn = getBellButton();
        if (!btn) return;

        var badge = btn.querySelector(".notification-badge");
        var current = 0;

        if (badge && badge.textContent) {
            var txt = badge.textContent.trim();
            current = (txt === "99+") ? 99 : parseInt(txt) || 0;
        }

        current += 1;

        if (!badge) {
            badge = document.createElement("span");
            badge.className = "notification-badge";
            btn.appendChild(badge);
        }

        badge.textContent = current > 99 ? "99+" : current.toString();
    }

    function showNotificationToast(title, messageHtml) {
        var container = document.getElementById("notification-toast-container");
        if (!container) return;

        var toast = document.createElement("div");
        toast.className = "notification-toast shadow";

        toast.innerHTML =
            '<div class="notification-toast-header">' +
            '  <span class="notification-toast-title">' + title + '</span>' +
            '  <button type="button" class="btn-close btn-close-white btn-close-sm" aria-label="Close"></button>' +
            '</div>' +
            '<div class="notification-toast-body"></div>';

        var bodyElem = toast.querySelector(".notification-toast-body");
        bodyElem.innerHTML = messageHtml;

        var closeBtn = toast.querySelector(".btn-close");
        closeBtn.addEventListener("click", function () {
            toast.classList.add("hide");
            setTimeout(function () {
                toast.remove();
            }, 200);
        });

        container.appendChild(toast);


        setTimeout(function () {
            if (!toast.classList.contains("hide")) {
                toast.classList.add("hide");
                setTimeout(function () {
                    toast.remove();
                }, 200);
            }
        }, 7000);
    }

    connection.on("ReceiveNotification", function (payload) {
        updateBadge();
        showNotificationToast(payload.title, payload.message);
    });

    connection.start().catch(function (err) {
        console.error("SignalR connection error:", err.toString());
    });
})();
