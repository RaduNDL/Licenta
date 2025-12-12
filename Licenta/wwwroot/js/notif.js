
(function () {
    "use strict";


    if (typeof signalR === "undefined") {
        console.warn("SignalR client library not found. Realtime notifications disabled.");
        return;
    }

    function getBellButton() {
        var icon = document.querySelector(".nav-icon-pill i.bi-bell");
        if (!icon) return null;
        return icon.closest("a");
    }

    function increaseBadge() {
        var bell = getBellButton();
        if (!bell) return;

        var badge = bell.querySelector(".notification-badge");
        var current = 0;

        if (badge && badge.textContent) {
            var text = badge.textContent.trim();
            if (text === "99+") {
                current = 99;
            } else {
                var parsed = parseInt(text, 10);
                if (!isNaN(parsed)) {
                    current = parsed;
                }
            }
        }

        current += 1;

        if (!badge) {
            badge = document.createElement("span");
            badge.className = "notification-badge";
            bell.appendChild(badge);
        }

        badge.textContent = current > 99 ? "99+" : current.toString();

        if (!badge.style.display || badge.style.display === "none") {
            badge.style.display = "flex";
        }
    }

    if (!getBellButton()) {
        return;
    }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/notificationHub")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveNotification", function (data) {
        console.log("New notification:", data);
        increaseBadge();
    });

    function startConnection() {
        connection.start()
            .then(function () {
                console.log("SignalR Connected.");
            })
            .catch(function (err) {
                console.error("SignalR failed:", err.toString());
                setTimeout(startConnection, 5000);
            });
    }

    startConnection();
})();
