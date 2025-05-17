import { addMessage } from "./utils.js";
import { connection } from "./connection.js";
import { searchGifs } from "./gif.js";

export let selectedChatId = null;

export async function selectUser(chatId, userName) {
    selectedChatId = chatId;
    document.getElementById("chatHeader").textContent = `Czat z ${userName}`;

    document.querySelectorAll("#friends .user-btn").forEach(btn => btn.classList.remove("active"));
    const selectedButton = document.querySelector(`#friends .user-btn[data-chat-id='${chatId}']`);
    if (selectedButton) selectedButton.classList.add("active");

    document.getElementById("messages").innerHTML = "";

    try {
        const response = await fetch(`/api/chat/messages/${chatId}`);
        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);

        const messages = await response.json();
        const senderId = document.getElementById("userId").value;

        messages.forEach(msg => {
            const type = msg.senderId === senderId ? "sent" : "received";

            if (msg.message) {
                addMessage(type, msg.message, msg.timestamp, msg.id, msg.senderName);

            }

            if (msg.attachments?.length > 0) {
                msg.attachments.forEach(att => {
                    if (att.filePath) {
                        addMessage(type, att.filePath, msg.timestamp, msg.id, msg.senderName);

                    }
                });
            }
        });
    } catch (error) {
        console.error("Błąd ładowania wiadomości:", error);
    }
}

export function setupChat() {
    const messageInput = document.getElementById("messageInput");
    const sendButton = document.getElementById("sendButton");
    const attachmentInput = document.getElementById("attachmentInput");

    if (!messageInput || !sendButton || !attachmentInput) {
        console.error("Brak wymaganych elementów DOM");
        return;
    }

    document.getElementById("searchButton")?.addEventListener("click", handleSearch);
    document.getElementById("closeSearchResults")?.addEventListener("click", () => {
        document.getElementById("searchResults").innerHTML = "";
        document.getElementById("closeSearchResults").classList.add("d-none");
    });

    document.getElementById("gifButton")?.addEventListener("click", () => {
        const gifContainer = document.getElementById("gifSearchContainer");
        gifContainer.classList.toggle("d-none");
    });

    let gifSearchTimeout;
    document.getElementById("gifSearchInput")?.addEventListener("input", (e) => {
        clearTimeout(gifSearchTimeout);
        const query = e.target.value.trim();
        if (!query) {
            document.getElementById("gifResults").style.display = "none";
            return;
        }
        gifSearchTimeout = setTimeout(() => searchGifs(query), 500);
    });

    document.getElementById("gifSearchButton")?.addEventListener("click", () => {
        const query = document.getElementById("gifSearchInput").value.trim();
        if (query) searchGifs(query);
    });

    sendButton.addEventListener("click", sendMessage);
    messageInput.addEventListener("keydown", async (event) => {
        if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            await sendMessage();
        }
    });

    connection.on("ReceiveMessage", (senderId, senderName, message, attachmentUrl, timestamp, id, chatId) => {
        const currentUserId = document.getElementById("userId").value;
        if (!currentUserId || senderId === currentUserId) return;
        if (chatId != selectedChatId) return;
        if (message) addMessage("received", message, timestamp, id, senderName);
        if (attachmentUrl) addMessage("received", attachmentUrl, timestamp, id, senderName);
    });

    connection.on("MessageRemoved", (messageId) => {
        const messageElement = document.querySelector(`[data-message-id='${messageId}']`);
        console.log("HALO USUN!");
        if (messageElement) messageElement.remove();
    });

    const heartbeatInterval = setInterval(() => {
        connection.invoke("Heartbeat").catch(err => console.error("Heartbeat error:", err));
    }, 30000);

    return () => {
        clearInterval(heartbeatInterval);
        connection.off("ReceiveMessage");
        connection.off("MessageRemoved");
    };
}

async function sendMessage() {
    const senderId = document.getElementById("userId").value;
    const messageInput = document.getElementById("messageInput");
    const attachmentInput = document.getElementById("attachmentInput");

    if (!senderId || !selectedChatId) {
        console.log(senderId);
        console.log(selectedChatId);
        alert("Nie wybrano czatu lub brak ID użytkownika!");
        return;
    }

    const file = attachmentInput.files[0];
    const message = messageInput.value.trim();

    if (!message && !file) {
        alert("Wpisz wiadomość lub wybierz plik!");
        return;
    }

    const formData = new FormData();
    formData.append("senderId", senderId);
    formData.append("chatId", selectedChatId);
    formData.append("message", message);
    if (file) formData.append("file", file);

    try {
        const res = await fetch("/api/chat/send", {
            method: "POST",
            body: formData
        });

        if (!res.ok) throw new Error(`HTTP error! status: ${res.status}`);

        const data = await res.json();

        if (data.message) addMessage("sent", data.message, data.timestamp, data.id, data.senderName);
        if (data.attachmentUrl) addMessage("sent", data.attachmentUrl, data.timestamp, data.id, data.senderName);

        messageInput.value = "";
        attachmentInput.value = null;
    } catch (error) {
        console.error("Błąd wysyłania wiadomości:", error);
        alert("Wystąpił błąd podczas wysyłania wiadomości");
    }
}

function handleSearch() {
    const keyword = document.getElementById("searchInput").value.trim().toLowerCase();
    const searchResultsList = document.getElementById("searchResults");

    if (!searchResultsList) return;
    searchResultsList.innerHTML = "";
    if (!keyword) return;

    const allMessages = document.querySelectorAll("#messages [data-message-id]");
    const matches = [];

    allMessages.forEach(msgEl => {
        const content = msgEl.querySelector(".message")?.textContent?.toLowerCase();
        if (content && content.includes(keyword)) matches.push(msgEl);
    });

    if (matches.length === 0) {
        const li = document.createElement("li");
        li.className = "list-group-item";
        li.textContent = "Brak pasujących wiadomości.";
        searchResultsList.appendChild(li);
        return;
    }

    matches.forEach((msgEl) => {
        const snippet = msgEl.querySelector(".message").textContent.slice(0, 50);
        const bubble = msgEl.querySelector(".message");
        const timestampEl = msgEl.querySelector(".timestamp");
        const timestamp = timestampEl ? timestampEl.textContent : "brak daty";

        const li = document.createElement("li");
        li.className = "list-group-item list-group-item-action";
        li.textContent = `${snippet}... (${timestamp})`;
        li.style.cursor = "pointer";

        li.addEventListener("click", () => {
            msgEl.scrollIntoView({ behavior: "smooth", block: "center" });
            bubble.classList.add("bg-warning", "rounded");
            setTimeout(() => bubble.classList.remove("bg-warning", "rounded"), 2000);
        });

        searchResultsList.appendChild(li);
    });

    document.getElementById("closeSearchResults").classList.remove("d-none");
}
document.getElementById("pinnedButton")?.addEventListener("click", async () => {
    const chatId = selectedChatId;
    const userId = document.getElementById("userId").value;
    const pinnedList = document.getElementById("pinnedMessages");

    // Jeśli lista już widoczna – ukryj i wyjdź
    if (!pinnedList.classList.contains("d-none")) {
        pinnedList.classList.add("d-none");
        pinnedList.innerHTML = "";
        return;
    }

    pinnedList.innerHTML = "";

    if (!chatId || !userId) {
        alert("Nie wybrano czatu!");
        return;
    }

    try {
        const response = await fetch(`/api/chat/pinned/${chatId}`);
        if (!response.ok) throw new Error("Nie udało się pobrać przypiętych wiadomości");

        const pinnedMessages = await response.json();

        if (pinnedMessages.length === 0) {
            const li = document.createElement("li");
            li.className = "list-group-item";
            li.textContent = "Brak przypiętych wiadomości.";
            pinnedList.appendChild(li);
        } else {
            pinnedMessages.forEach(msg => {
                const li = document.createElement("li");
                li.className = "list-group-item list-group-item-action";
                const snippet = msg.message?.slice(0, 50) || "Załącznik";
                li.textContent = `${snippet}... (${new Date(msg.timestamp).toLocaleString()})`;

                li.addEventListener("click", () => {
                    const targetMessage = document.querySelector(`[data-message-id='${msg.id}']`);
                    if (targetMessage) {
                        targetMessage.scrollIntoView({ behavior: "smooth", block: "center" });
                        const bubble = targetMessage.querySelector(".message");
                        if (bubble) {
                            bubble.classList.add("bg-warning", "rounded");
                            setTimeout(() => bubble.classList.remove("bg-warning", "rounded"), 2000);
                        }
                    }
                });

                pinnedList.appendChild(li);
            });
        }

        pinnedList.classList.remove("d-none");
    } catch (error) {
        console.error("Błąd pobierania przypiętych wiadomości:", error);
        alert("Nie udało się pobrać przypiętych wiadomości.");
    }
});
