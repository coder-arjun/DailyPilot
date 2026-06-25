// Polls the server for pending browser notifications (PRD §11) and shows them
// as Bootstrap toasts, also using the Web Notifications API when permitted.
(function () {
    if ("Notification" in window && Notification.permission === "default") {
        Notification.requestPermission();
    }

    const container = document.getElementById("notificationContainer");

    function showToast(message) {
        if (!container) return;
        const el = document.createElement("div");
        el.className = "toast align-items-center text-bg-primary border-0";
        el.setAttribute("role", "alert");
        el.innerHTML =
            '<div class="d-flex">' +
            '<div class="toast-body">' + message + '</div>' +
            '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>' +
            '</div>';
        container.appendChild(el);
        const toast = new bootstrap.Toast(el, { delay: 10000 });
        toast.show();
    }

    async function poll() {
        try {
            const res = await fetch("/api/notifications/pending", { headers: { "Accept": "application/json" } });
            if (!res.ok) return;
            const items = await res.json();
            for (const item of items) {
                showToast(item.message);
                if ("Notification" in window && Notification.permission === "granted") {
                    new Notification("DayPilot", { body: item.message });
                }
                fetch("/api/notifications/ack/" + item.id, { method: "POST" });
            }
        } catch (e) { /* offline / not logged in */ }
    }

    poll();
    setInterval(poll, 60000);
})();
