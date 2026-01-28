(() => {
    "use strict";

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
        userId: null
    };

    const qs = (s, r = document) => r.querySelector(s);

    function getForm() { return qs(SEL.form); }
    function getBody() { return qs(SEL.body); }
    function getInput(form) { return qs(SEL.input, form); }

    function getToken(form) {
        const el = qs(SEL.token, form) || qs(SEL.token);
        return el ? el.value : null;
    }

    function getUserId(form) {
        return (form.getAttribute("data-user-id") || "").trim();
    }

    function showError(form, msg) {
        const el = qs(SEL.err, form);
        if (!el) return;
        el.textContent = msg || "";
        el.classList.toggle("d-none", !msg);
    }

    function nearBottom(body) {
        return (body.scrollHeight - body.scrollTop - body.clientHeight) < 140;
    }

    function scrollBottom(body, smooth) {
        if (!body) return;
        try {
            body.scrollTo({
                top: body.scrollHeight,
                behavior: smooth ? "smooth" : "auto"
            });
        } catch {
            body.scrollTop = body.scrollHeight;
        }
    }

    function escapeText(s) {
        return (s ?? "").replace(/[&<>"']/g, c => ({
            "&": "&amp;",
            "<": "&lt;",
            ">": "&gt;",
            '"': "&quot;",
            "'": "&#39;"
        }[c]));
    }

    function formatHHmm(isoUtc) {
        try {
            const d = new Date(isoUtc);
            if (isNaN(d.getTime())) return "";
            return d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
        } catch {
            return "";
        }
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
        row.dataset.id = sid;
        if (pending) row.dataset.pending = "1";

        const bubble = document.createElement("div");
        bubble.className = "message-bubble";

        const text = document.createElement("span");
        text.className = "message-text";
        text.innerHTML = escapeText(body);

        const time = document.createElement("span");
        time.className = "message-time";
        time.textContent = pending ? "…" : formatHHmm(sentAtUtc);

        bubble.appendChild(text);
        bubble.appendChild(time);
        row.appendChild(bubble);
        wrap.appendChild(row);

        if (stick) scrollBottom(wrap, true);
    }

    function reconcilePending(tempId, realId, sentAtUtc) {
        const wrap = getBody();
        if (!wrap) return;

        const rid = String(realId);
        const tid = String(tempId);

        const already = wrap.querySelector(`[data-id="${CSS.escape(rid)}"]`);
        const pending = wrap.querySelector(`[data-id="${CSS.escape(tid)}"]`);

        if (already && pending) {
            pending.remove();
            state.seen.add(rid);
            return;
        }

        if (!pending) {
            state.seen.add(rid);
            return;
        }

        pending.dataset.id = rid;
        pending.removeAttribute("data-pending");

        const time = pending.querySelector(".message-time");
        if (time) time.textContent = formatHHmm(sentAtUtc);

        state.seen.add(rid);
    }

    function markFailed(tempId) {
        const wrap = getBody();
        if (!wrap) return;

        const el = wrap.querySelector(`[data-id="${CSS.escape(String(tempId))}"]`);
        if (!el) return;

        el.classList.add("send-failed");
        const time = el.querySelector(".message-time");
        if (time) time.textContent = "failed";
    }

    function getConversationKey(form) {
        const attr = form.getAttribute("data-conversation-key");
        if (attr && attr.trim()) return attr.trim();

        const req = qs("input[name='Input.RequestId']", form);
        const kind = qs("input[name='Input.Kind']", form);

        if (req && kind)
            return `patient:${req.value}:${String(kind.value).toLowerCase()}`;

        if (req)
            return `patient:${req.value}:assistant`;

        return null;
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

        const res = await fetch(form.action || location.href, {
            method: "POST",
            body: fd,
            headers,
            credentials: "same-origin"
        });

        const data = await res.json().catch(() => null);

        if (!res.ok || !data || data.ok !== true) {
            const err =
                (data?.errors && Object.values(data.errors)[0]?.[0]) ||
                data?.error ||
                `HTTP ${res.status}`;

            throw new Error(err);
        }

        return data;
    }

    async function drainQueue(form) {
        if (state.sending) return;
        state.sending = true;

        try {
            while (state.queue.length) {
                const item = state.queue.shift();
                showError(form, "");

                try {
                    const data = await postMessage(form, item.text);
                    if (data.messageId && data.sentAtUtc) {
                        reconcilePending(item.tempId, data.messageId, data.sentAtUtc);
                    } else {
                        reconcilePending(item.tempId, item.tempId, new Date().toISOString());
                    }
                } catch (e) {
                    markFailed(item.tempId);
                    showError(form, e.message || "Send failed");
                }
            }
        } finally {
            state.sending = false;
        }
    }

    function enqueue(form) {
        const ta = getInput(form);
        if (!ta) return;

        const text = ta.value.trim();
        if (!text) return;

        ta.value = "";

        const tempId = `tmp_${Date.now()}_${Math.random().toString(16).slice(2)}`;

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

    function initSignalR(form) {
        if (typeof signalR === "undefined") return;

        state.userId = getUserId(form);
        state.conversationKey = getConversationKey(form);

        if (!state.conversationKey) return;

        state.conn = new signalR.HubConnectionBuilder()
            .withUrl("/notificationHub", {
                transport: signalR.HttpTransportType.LongPolling
            })
            .withAutomaticReconnect()
            .build();

        state.conn.on("message:new", p => {
            if (!p) return;
            if (p.conversationKey !== state.conversationKey) return;

            const mid = p.messageId ? String(p.messageId) : "";
            if (mid && state.seen.has(mid)) return;

            const mine = state.userId && String(p.senderId) === String(state.userId);

            appendMessage({
                id: p.messageId,
                body: p.body,
                sentAtUtc: p.sentAtUtc,
                mine,
                pending: false
            });
        });

        state.conn.start().catch(() => { });
    }

    document.addEventListener("DOMContentLoaded", () => {
        const form = getForm();
        if (!form) return;

        const ta = getInput(form);

        if (ta) {
            ta.addEventListener("keydown", e => {
                if (e.key === "Enter" && !e.shiftKey) {
                    e.preventDefault();
                    enqueue(form);
                }
            });
        }

        form.addEventListener("submit", e => {
            e.preventDefault();
            enqueue(form);
        });

        const body = getBody();
        if (body) scrollBottom(body, false);

        initSignalR(form);
    });
})();
