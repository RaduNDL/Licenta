(() => {
    "use strict";

    if (window.__chatInboxInitialized) return;
    window.__chatInboxInitialized = true;

    const SEL = {
        form: "form[data-chat-form='1']",
        body: "#chatBody",
        input: ".chat-input",
        err: ".chat-send-error",
        token: "input[name='__RequestVerificationToken']"
    };

    const state = {
        sending: false,
        queue: [],
        seen: new Set(),
        conn: null,
        conversationKey: null,
        userId: null,
        lastSyncUtc: null,
        syncTimer: null,
        pollTimer: null
    };

    const qs = (s, r = document) => r.querySelector(s);

    const getForm = () => qs(SEL.form);
    const getBody = () => qs(SEL.body);
    const getInput = f => qs(SEL.input, f);

    function getToken(form) {
        return qs(SEL.token, form)?.value || qs(SEL.token)?.value || "";
    }

    function showError(form, msg) {
        const el = qs(SEL.err, form);
        if (!el) return;
        el.textContent = msg || "";
        el.classList.toggle("d-none", !msg);
    }

    function nearBottom(body) {
        return body.scrollHeight - body.scrollTop - body.clientHeight < 200;
    }

    function scrollBottom(body, smooth) {
        if (!body) return;
        body.scrollTo({
            top: body.scrollHeight,
            behavior: smooth ? "smooth" : "auto"
        });
    }

    function escapeText(s) {
        return (s ?? "").replace(/[&<>"']/g, c => ({
            "&": "&amp;",
            "<": "&lt;",
            ">": "&gt;",
            "\"": "&quot;",
            "'": "&#39;"
        }[c]));
    }

    function formatHHmm(iso) {
        const d = new Date(iso);
        if (isNaN(d)) return "";
        return d.toLocaleTimeString([], {
            hour: "2-digit",
            minute: "2-digit",
            hour12: false
        });
    }

    function bootstrapSeenFromDom() {
        const wrap = getBody();
        if (!wrap) return;

        let maxUtc = null;

        wrap.querySelectorAll(".message-row[data-id]").forEach(el => {
            const id = el.getAttribute("data-id");
            if (id) state.seen.add(String(id));

            const utc = el.getAttribute("data-utc");
            if (utc) {
                const d = new Date(utc);
                if (!isNaN(d)) {
                    if (!maxUtc || d > maxUtc) maxUtc = d;
                }
            }
        });

        if (maxUtc) state.lastSyncUtc = maxUtc.toISOString();
    }

    function appendMessage({ id, body, sentAtUtc, mine, pending }) {
        const wrap = getBody();
        if (!wrap) return;

        const sid = id ? String(id) : "";

        if (sid && state.seen.has(sid)) return;
        if (sid) state.seen.add(sid);

        const stick = nearBottom(wrap);

        const row = document.createElement("div");
        row.className = `message-row ${mine ? "mine" : "other"}`;
        if (sid) row.dataset.id = sid;
        if (sentAtUtc) row.dataset.utc = String(sentAtUtc);
        if (pending) row.dataset.pending = "1";

        row.innerHTML = `
            <div class="message-bubble">
                <span class="message-text">${escapeText(body)}</span>
                <span class="message-time">${pending ? "..." : formatHHmm(sentAtUtc)}</span>
            </div>
        `;

        wrap.appendChild(row);

        if (stick || mine) scrollBottom(wrap, true);
    }

    function tryReconcilePendingByBody(realId, sentAtUtc, body) {
        const wrap = getBody();
        if (!wrap) return false;

        const pending = Array.from(wrap.querySelectorAll(".message-row.mine[data-pending='1']"));
        if (!pending.length) return false;

        const incomingBody = String(body ?? "").trim();
        const incomingTime = new Date(sentAtUtc);
        const incomingMs = isNaN(incomingTime) ? null : incomingTime.getTime();

        for (let i = pending.length - 1; i >= 0; i--) {
            const row = pending[i];
            const txt = row.querySelector(".message-text")?.textContent ?? "";
            const localBody = txt.trim();

            if (localBody !== incomingBody) continue;

            const rowUtc = row.getAttribute("data-utc");
            if (incomingMs && rowUtc) {
                const rowTime = new Date(rowUtc);
                if (!isNaN(rowTime)) {
                    const diff = Math.abs(incomingMs - rowTime.getTime());
                    if (diff > 120000) continue;
                }
            }

            row.dataset.id = String(realId);
            row.dataset.utc = String(sentAtUtc);
            row.removeAttribute("data-pending");

            const t = row.querySelector(".message-time");
            if (t) t.textContent = formatHHmm(sentAtUtc);

            state.seen.add(String(realId));
            state.lastSyncUtc = String(sentAtUtc);
            return true;
        }

        return false;
    }

    function reconcilePending(tempId, realId, sentAtUtc) {
        const wrap = getBody();
        if (!wrap) return;

        const el = wrap.querySelector(`[data-id="${tempId}"]`);
        if (!el) return;

        el.dataset.id = String(realId);
        el.dataset.utc = String(sentAtUtc);
        el.removeAttribute("data-pending");

        const t = el.querySelector(".message-time");
        if (t) t.textContent = formatHHmm(sentAtUtc);

        state.seen.add(String(realId));
        state.lastSyncUtc = String(sentAtUtc);
    }

    function markFailed(tempId) {
        getBody()
            ?.querySelector(`[data-id="${tempId}"]`)
            ?.classList.add("send-failed");
    }

    async function postMessage(form, text) {
        const fd = new FormData(form);
        fd.set("Input.NewMessageBody", text);

        const headers = {
            "X-Requested-With": "XMLHttpRequest",
            "Accept": "application/json"
        };

        const tok = getToken(form);
        if (tok) headers["RequestVerificationToken"] = tok;

        const res = await fetch(form.action, {
            method: "POST",
            body: fd,
            headers,
            credentials: "same-origin"
        });

        const data = await res.json().catch(() => null);

        if (!res.ok || !data?.ok) throw new Error("Send failed");
        return data;
    }

    async function drainQueue(form) {
        if (state.sending) return;
        state.sending = true;

        while (state.queue.length) {
            const item = state.queue.shift();

            try {
                const data = await postMessage(form, item.text);

                reconcilePending(
                    item.tempId,
                    data.messageId,
                    data.sentAtUtc
                );

                showError(form, "");
            } catch {
                markFailed(item.tempId);
                showError(form, "Message was not sent");
                state.queue.unshift(item);
                await new Promise(r => setTimeout(r, 2000));
            }
        }

        state.sending = false;
    }

    function enqueue(form) {
        const ta = getInput(form);
        if (!ta) return;

        const text = ta.value.trim();
        if (!text) return;

        ta.value = "";
        ta.style.height = "auto";

        const tempId = "tmp_" + Date.now();

        appendMessage({
            id: tempId,
            body: text,
            sentAtUtc: new Date().toISOString(),
            mine: true,
            pending: true
        });

        state.queue.push({ text, tempId });
        drainQueue(form);
    }

    function buildSyncUrl() {
        const base = window.location.pathname;
        const params = new URLSearchParams();
        params.set("handler", "Sync");
        params.set("conversationKey", state.conversationKey || "");
        if (state.lastSyncUtc) params.set("after", state.lastSyncUtc);
        return `${base}?${params.toString()}`;
    }

    async function syncMessages() {
        if (!state.conversationKey) return;

        try {
            const url = buildSyncUrl();

            const res = await fetch(url, {
                headers: { "X-Requested-With": "XMLHttpRequest" },
                credentials: "same-origin"
            });

            const data = await res.json().catch(() => null);
            const list = data?.messages;
            if (!Array.isArray(list) || !list.length) return;

            for (const m of list) {
                const mine = String(m.senderId) === String(state.userId);

                appendMessage({
                    id: m.messageId,
                    body: m.body,
                    sentAtUtc: m.sentAtUtc,
                    mine,
                    pending: false
                });

                state.lastSyncUtc = String(m.sentAtUtc);
            }
        } catch { }
    }

    function stopTimers() {
        if (state.syncTimer) clearInterval(state.syncTimer);
        if (state.pollTimer) clearInterval(state.pollTimer);
        state.syncTimer = null;
        state.pollTimer = null;
    }

    function startPeriodicSync() {
        if (state.syncTimer) clearInterval(state.syncTimer);
        state.syncTimer = setInterval(() => {
            syncMessages();
        }, 15000);
    }

    function startPollingSync() {
        if (state.pollTimer) return;
        state.pollTimer = setInterval(() => {
            syncMessages();
        }, 3000);
    }

    function stopPollingSync() {
        if (!state.pollTimer) return;
        clearInterval(state.pollTimer);
        state.pollTimer = null;
    }

    function initSignalR(form) {
        state.userId = form?.dataset?.userId || "";
        state.conversationKey = form?.dataset?.conversationKey || "";

        if (!state.userId || !state.conversationKey) return;

        if (typeof signalR === "undefined") {
            startPollingSync();
            startPeriodicSync();
            return;
        }

        state.conn = new signalR.HubConnectionBuilder()
            .withUrl("/notificationHub")
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .build();

        state.conn.on("message:new", p => {
            if (!p) return;
            if (String(p.conversationKey) !== String(state.conversationKey)) return;

            const mine = String(p.senderId) === String(state.userId);

            if (mine) {
                const reconciled = tryReconcilePendingByBody(p.messageId, p.sentAtUtc, p.body);
                if (reconciled) return;
            }

            appendMessage({
                id: p.messageId,
                body: p.body,
                sentAtUtc: p.sentAtUtc,
                mine,
                pending: false
            });

            state.lastSyncUtc = String(p.sentAtUtc);
        });

        state.conn.onreconnecting(() => {
            startPollingSync();
        });

        state.conn.onreconnected(() => {
            stopPollingSync();
            syncMessages();
        });

        state.conn.onclose(() => {
            startPollingSync();
        });

        const start = async () => {
            try {
                await state.conn.start();
                stopPollingSync();
                await syncMessages();
                startPeriodicSync();
            } catch {
                startPollingSync();
                startPeriodicSync();
                setTimeout(start, 5000);
            }
        };

        start();
    }

    document.addEventListener("DOMContentLoaded", () => {
        const form = getForm();
        if (!form) return;

        const ta = getInput(form);
        if (!ta) return;

        ta.addEventListener("keydown", e => {
            if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                enqueue(form);
            }
        });

        ta.addEventListener("input", function () {
            this.style.height = "auto";
            this.style.height = Math.min(this.scrollHeight, 150) + "px";
        });

        form.addEventListener("submit", e => {
            e.preventDefault();
            enqueue(form);
        });

        bootstrapSeenFromDom();
        scrollBottom(getBody(), false);
        initSignalR(form);
    });

    window.addEventListener("beforeunload", () => {
        stopTimers();
        state.conn?.stop();
    });
})();