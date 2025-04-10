import { addMessage } from "./utils.js";
import { connection } from "./connection.js";


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
            addMessage(type, msg.message);
        });
    } catch (error) {
        console.error("Błąd ładowania wiadomości:", error);
    }
}

export function setupChat() {
    document.getElementById("sendButton").addEventListener("click", async () => {
        const messageInput = document.getElementById("messageInput");
        const senderId = document.getElementById("userId").value;
        const message = messageInput.value.trim();

        if (!selectedReceiverId || !message) {
            alert("Wybierz odbiorcę i wpisz wiadomość!");
            return;
        }

        try {
            await fetch("/api/chat/send", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ senderId, receiverId: selectedReceiverId, message })
            });

            addMessage("sent", message);
            messageInput.value = "";
        } catch (error) {
            console.error("Błąd wysyłania wiadomości:", error);
        }
    });

    connection.on("ReceiveMessage", (senderId, message) => {
        if (senderId === selectedReceiverId) {
            addMessage("received", message);
        }
    });
}

const TENOR_API_KEY = "AIzaSyAAYkqlyEc8l1sV4Qao2EguSjLWVSRzEMI";

export async function searchGifs(query) {
    const url = `https://tenor.googleapis.com/v2/search?q=${encodeURIComponent(query)}&key=${TENOR_API_KEY}&limit=10`;

    try {
        const response = await fetch(url);
        const data = await response.json();

        const resultsDiv = document.getElementById("gifResults");
        resultsDiv.innerHTML = "";

        data.results.forEach(gif => {
            const gifUrl = gif.media_formats?.tinygif?.url || gif.media[0].gif.url;
            const img = document.createElement("img");
            img.src = gifUrl;
            img.style.width = "100px";
            img.style.margin = "5px";
            img.style.cursor = "pointer";

            img.addEventListener("click", async () => {
                const senderId = document.getElementById("userId").value;
                if (!selectedReceiverId) return;

                try {
                    await fetch("/api/chat/send", {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ senderId, receiverId: selectedReceiverId, message: gifUrl })
                    });

                    addMessage("sent", `<img src="${gifUrl}" style="max-width: 150px;" />`);
                } catch (err) {
                    console.error("Błąd wysyłania GIF-a:", err);
                }

                resultsDiv.style.display = "none";
            });

            resultsDiv.appendChild(img);
        });

        resultsDiv.style.display = "block";
    } catch (error) {
        console.error("Błąd wyszukiwania GIF-ów:", error);
    }
}
document.getElementById("gifSearchButton").addEventListener("click", () => {
    const query = document.getElementById("gifSearchInput").value.trim();
    if (query) searchGifs(query);
});
