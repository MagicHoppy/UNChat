import { addMessage } from "./utils.js";
import { selectedReceiverId } from './chat.js';

export async function searchGifs(query) {
    const url = `/api/gif/search?query=${encodeURIComponent(query)}`;

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
                const formData = new FormData();
                formData.append("senderId", senderId);
                formData.append("receiverId", selectedReceiverId);
                formData.append("message", gifUrl);
                const res = await fetch("/api/chat/send", {
                    method: "POST",
                    body: formData
                });
                const data = await res.json();
                addMessage("sent", data.message);
                resultsDiv.style.display = "none";
            });

            resultsDiv.appendChild(img);
        });

        resultsDiv.style.display = "block";
    } catch (error) {
        console.error("Błąd wyszukiwania GIF-ów:", error);
    }
}