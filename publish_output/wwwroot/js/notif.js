(function () {
    "use strict";

    if (typeof signalR === "undefined") {
        console.warn("SignalR client library not found. Realtime notifications disabled.");
        return;
    }

    function getBellButton() {
        return document.querySelector(".notification-bell-btn");
    }

    function getBadge(bell) {
        if (!bell) return null;
        return bell.querySelector(".notification-badge");
    }

    function setBadgeCount(count) {
        var bell = getBellButton();
        if (!bell) return;

        var badge = getBadge(bell);
        if (!badge) return;

        if (!count || count <= 0) {
            badge.textContent = "";
            badge.style.display = "none";
            return;
        }

        badge.textContent = count > 99 ? "99+" : String(count);
        badge.style.display = "inline-flex";
    }

    function increaseBadge() {
        var bell = getBellButton();
        if (!bell) return;

        var badge = getBadge(bell);
        if (!badge) return;

        var current = 0;
        var text = (badge.textContent || "").trim();

        if (text === "99+") current = 99;
        else {
            var parsed = parseInt(text, 10);
            if (!isNaN(parsed)) current = parsed;
        }

        current += 1;
        setBadgeCount(current);
    }

    if (!getBellButton()) return;

    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/notificationHub")
        .withAutomaticReconnect()
        .build();

    connection.on("notification:new", function () {
        increaseBadge();
    });

    connection.on("notification:cleared", function () {
        setBadgeCount(0);
    });

    function start() {
        connection.start().catch(function () {
            setTimeout(start, 5000);
        });
    }

    start();
})();
