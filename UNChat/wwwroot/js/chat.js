import { addMessage } from "./utils.js";
import { connection } from "./connection.js";
import { searchGifs } from "./gif.js";

export let selectedReceiverId = null;

export async function selectUser(userId, userName) {
    selectedReceiverId = userId;
    document.getElementById("chatHeader").textContent = `Czat z ${userName}`;

    document.querySelectorAll("#friends .user-btn").forEach(btn => btn.classList.remove("active"));

    const selectedButton = document.querySelector(`#friends .user-btn[data-id='${userId}']`);
    if (selectedButton) {
        selectedButton.classList.add("active");
    }

    document.getElementById("messages").innerHTML = "";

    try {
        const senderId = document.getElementById("userId").value;
        if (!senderId) throw new Error("Brak ID użytkownika");

        const response = await fetch(`/api/chat/messages/${senderId}/${userId}`);
        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);

        const messages = await response.json();

        messages.forEach(msg => {
            const type = msg.senderId === senderId ? "sent" : "received";

            if (msg.message) {
                addMessage(type, msg.message, msg.timestamp, msg.id);
            }

            if (msg.attachments && msg.attachments.length > 0) {
                msg.attachments.forEach(att => {
                    if (att.filePath) {
                        addMessage(type, att.filePath, msg.timestamp, msg.id);
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

    // Obsługa wyszukiwania wiadomości
    document.getElementById("searchButton")?.addEventListener("click", handleSearch);
    document.getElementById("closeSearchResults")?.addEventListener("click", () => {
        document.getElementById("searchResults").innerHTML = "";
        document.getElementById("closeSearchResults").classList.add("d-none");
    });

    // Obsługa GIF-ów
    document.getElementById("gifSearchButton")?.addEventListener("click", () => {
        const query = document.getElementById("gifSearchInput").value.trim();
        if (query) searchGifs(query);
    });

    // Nasłuchiwanie zdarzeń
    sendButton.addEventListener("click", sendMessage);
    messageInput.addEventListener("keydown", async (event) => {
        if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            await sendMessage();
        }
    });

    // Połączenie SignalR
    connection.on("ReceiveMessage", (senderId, message, attachmentUrl, timestamp, id) => {
        if (senderId !== selectedReceiverId) return;

        if (message) {
            addMessage("received", message, timestamp, id);
        }

        if (attachmentUrl) {
            addMessage("received", attachmentUrl, timestamp, id);
        }
    });

    connection.on("MessageRemoved", (messageId) => {
        const messageElement = document.querySelector(`[data-message-id='${messageId}']`);
        if (messageElement) {
            messageElement.remove();
        }
    });

    // Heartbeat
    const heartbeatInterval = setInterval(() => {
        connection.invoke("Heartbeat").catch(err => console.error("Heartbeat error:", err));
    }, 30000);

    // Funkcja czyszczenia przy zamykaniu
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

    if (!senderId || !selectedReceiverId) {
        alert("Nie wybrano odbiorcy lub brak ID użytkownika!");
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
    formData.append("receiverId", selectedReceiverId);
    formData.append("message", message);
    if (file) formData.append("file", file);

    try {
        const res = await fetch("/api/chat/send", {
            method: "POST",
            body: formData
        });

        if (!res.ok) throw new Error(`HTTP error! status: ${res.status}`);

        const data = await res.json();

        if (data.message) addMessage("sent", data.message, data.timestamp, data.id);
        if (data.attachmentUrl) addMessage("sent", data.attachmentUrl, data.timestamp, data.id);

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
        if (content && content.includes(keyword)) {
            matches.push(msgEl);
        }
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
