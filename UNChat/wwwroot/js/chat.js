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
        const response = await fetch(`/api/chat/messages/${senderId}/${userId}`);
        const messages = await response.json();

        messages.forEach(msg => {
            const type = msg.senderId === senderId ? "sent" : "received";

            if (msg.message) {
                addMessage(type, msg.message, msg.timestamp,msg.id);
            }

            if (msg.attachments && msg.attachments.length > 0) {
                msg.attachments.forEach(att => {
                    addMessage(type, att.filePath, msg.timestamp,msg.id);
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

    async function sendMessage() {
        const senderId = document.getElementById("userId").value;
        const file = attachmentInput.files[0];
        const message = messageInput.value.trim();

        if (!selectedReceiverId || (!message && !file)) {
            alert("Wpisz wiadomość lub wybierz plik!");
            return;
        }

        const formData = new FormData();
        formData.append("senderId", senderId);
        formData.append("receiverId", selectedReceiverId);
        formData.append("message", message);
        if (file) formData.append("file", file);

        console.log(senderId, selectedReceiverId, message, file);

        const res = await fetch("/api/chat/send", {
            method: "POST",
            body: formData
        });

        const data = await res.json();

        if (data.message) addMessage("sent", data.message, data.timestamp,data.id);
        if (data.attachmentUrl) addMessage("sent", data.attachmentUrl, data.timestamp,data.id);

        messageInput.value = "";
        attachmentInput.value = null;
    }

    sendButton.addEventListener("click", sendMessage);

    messageInput.addEventListener("keydown", async (event) => {
        if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault(); // zapobiega np. dodaniu nowej linii
            await sendMessage();
        }
    });

    connection.on("ReceiveMessage", (senderId, message, attachmentUrl, timestamp,id) => {
        if (senderId !== selectedReceiverId) return;

        if (message) {
            addMessage("received", message, timestamp, id);
        }

        if (attachmentUrl) {
            addMessage("received", attachmentUrl, timestamp, id); // renderuje jako obrazek/link
        }
    });
    connection.on("MessageRemoved", (messageId) => {
        const messageElement = document.querySelector(`[data-message-id='${messageId}']`);
        if (messageElement) {
            messageElement.remove();
        }
    });

   
}





document.getElementById("gifSearchButton").addEventListener("click", () => {
    const query = document.getElementById("gifSearchInput").value.trim();
    if (query) searchGifs(query);
});
