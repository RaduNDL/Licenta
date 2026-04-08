(() => {
    const form = document.querySelector('form[data-chat-form="1"]');
    const chatBody = document.getElementById('chatBody');

    if (!form || !chatBody) return;

    const textarea = form.querySelector('.chat-input');
    const sendButton = form.querySelector('.btn-send');
    const errorBox = document.querySelector('.chat-send-error');

    const currentUserId = form.dataset.userId || "";
    const currentConversationKey = form.dataset.conversationKey || "";

    let isSending = false;
    let isSyncing = false;

    const knownIds = new Set(
        Array.from(chatBody.querySelectorAll('.message-row[data-id]'))
            .map(x => x.dataset.id)
            .filter(Boolean)
    );

    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#39;');
    }

    function formatTime(value) {
        if (!value) return "";

        const d = new Date(value);
        if (Number.isNaN(d.getTime())) return "";

        return d.toLocaleTimeString([], {
            hour: '2-digit',
            minute: '2-digit',
            hour12: false
        });
    }

    function getLastUtc() {
        const rows = chatBody.querySelectorAll('.message-row[data-utc]');
        if (!rows.length) return "";
        return rows[rows.length - 1].dataset.utc || "";
    }

    function isNearBottom() {
        const threshold = 120;
        return chatBody.scrollHeight - chatBody.scrollTop - chatBody.clientHeight < threshold;
    }

    function scrollToBottom(force = false) {
        if (force || isNearBottom()) {
            chatBody.scrollTop = chatBody.scrollHeight;
        }
    }

    function setError(message) {
        if (!errorBox) return;

        if (!message) {
            errorBox.classList.add('d-none');
            errorBox.textContent = "";
            return;
        }

        errorBox.textContent = message;
        errorBox.classList.remove('d-none');
    }

    function autosizeTextarea() {
        if (!textarea) return;
        textarea.style.height = 'auto';
        textarea.style.height = `${Math.min(textarea.scrollHeight, 160)}px`;
    }

    function isMineMessage(msg) {
        const senderId = String(msg?.senderId || "");
        return senderId && senderId === String(currentUserId);
    }

    function appendMessage(msg) {
        if (!msg || !msg.id) return;

        const id = String(msg.id);
        if (knownIds.has(id)) return;

        knownIds.add(id);

        const mine = isMineMessage(msg);

        const row = document.createElement('div');
        row.className = `message-row ${mine ? 'mine' : 'other'}`;
        row.dataset.id = id;
        row.dataset.utc = msg.sentAtUtc || "";

        row.innerHTML = `
            <div class="message-bubble">
                <span class="message-text">${escapeHtml(msg.body || "")}</span>
                <span class="message-time">${escapeHtml(formatTime(msg.sentAtLocal || msg.sentAtUtc))}</span>
            </div>
        `;

        const shouldStick = mine || isNearBottom();
        chatBody.appendChild(row);

        if (shouldStick) {
            scrollToBottom(true);
        }
    }

    async function readJsonSafely(response) {
        const contentType = response.headers.get('content-type') || "";

        if (!contentType.includes('application/json')) {
            return null;
        }

        try {
            return await response.json();
        } catch {
            return null;
        }
    }

    async function submitMessage() {
        if (isSending) return;
        if (!textarea) return;

        const text = textarea.value.trim();
        if (!text) return;

        isSending = true;
        setError("");

        const formData = new FormData(form);

        if (!formData.get("Input.NewMessageBody")) {
            formData.set("Input.NewMessageBody", text);
        }

        if (sendButton) sendButton.disabled = true;
        textarea.readOnly = true;

        try {
            const response = await fetch(window.location.href, {
                method: 'POST',
                body: formData,
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin'
            });

            if (response.status === 401 || response.status === 403) {
                throw new Error('Session expired or wrong account is active in this browser window.');
            }

            if (response.redirected) {
                throw new Error('Request was redirected. Most likely this window is no longer authenticated as the current user.');
            }

            const data = await readJsonSafely(response);

            if (!response.ok) {
                throw new Error(data?.error || `Message could not be sent. (${response.status})`);
            }

            if (!data || !data.ok) {
                throw new Error(data?.error || 'Message could not be sent.');
            }

            appendMessage({
                id: data.id,
                body: data.body || text,
                senderId: data.senderId || currentUserId,
                sentAtUtc: data.sentAtUtc,
                sentAtLocal: data.sentAtLocal
            });

            textarea.value = "";
            autosizeTextarea();
            scrollToBottom(true);
        } catch (err) {
            setError(err?.message || 'Message could not be sent.');
        } finally {
            isSending = false;
            if (sendButton) sendButton.disabled = false;
            textarea.readOnly = false;
            textarea.focus();
        }
    }

    async function syncMessages() {
        if (isSyncing || !currentConversationKey) return;

        isSyncing = true;

        try {
            const url = new URL(window.location.href, window.location.origin);
            url.searchParams.set('handler', 'Sync');
            url.searchParams.set('conversationKey', currentConversationKey);

            const after = getLastUtc();
            if (after) {
                url.searchParams.set('after', after);
            } else {
                url.searchParams.delete('after');
            }

            const response = await fetch(url.toString(), {
                method: 'GET',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin'
            });

            if (response.status === 401 || response.status === 403) {
                setError('Session expired or wrong account is active in this browser window.');
                return;
            }

            if (!response.ok) return;

            const data = await response.json();

            if (data?.reloadRequired === true) {
                window.location.reload();
                return;
            }

            const messages = Array.isArray(data?.messages) ? data.messages : [];

            for (const msg of messages) {
                appendMessage(msg);
            }
        } catch {
        } finally {
            isSyncing = false;
        }
    }

    async function startRealtime() {
        if (!window.signalR || !currentConversationKey) return;

        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/notifications')
            .withAutomaticReconnect()
            .build();

        connection.on('message:new', (msg) => {
            if (!msg) return;
            if ((msg.conversationKey || '') !== currentConversationKey) return;
            appendMessage(msg);
        });

        connection.onreconnected(() => {
            syncMessages();
        });

        connection.onclose(() => {
            syncMessages();
        });

        try {
            await connection.start();
        } catch {
        }
    }

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        await submitMessage();
    });

    if (textarea) {
        textarea.addEventListener('keydown', async (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                await submitMessage();
            }
        });

        textarea.addEventListener('input', autosizeTextarea);
        autosizeTextarea();
    }

    scrollToBottom(true);
    startRealtime();
    syncMessages();
    setInterval(syncMessages, 2000);
})();