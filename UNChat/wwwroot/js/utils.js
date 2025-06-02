import { connection } from "./connection.js";
function updateMessageContent(wrapper, newMessage) {
    const messageElement = wrapper.querySelector(".message");
    messageElement.innerHTML = ""; // wyczyść stare treści

    messageElement.textContent = newMessage;

}
export async function addMessage(type, message, time = null, messageId = null, senderName = "", status = "sent") {
    const messagesDiv = document.getElementById("messages");


    // Create wrapper
    const wrapper = document.createElement("div");
    wrapper.className = `d-flex flex-column ${type === "sent" ? "align-items-end" : "align-items-start"} mb-2`;
    wrapper.dataset.messageId = messageId;

    if (senderName || time) {
        const meta = document.createElement("div");
        meta.className = "message-meta small text-muted mb-1";

        // nazwa
        if (senderName) {
            const nameSpan = document.createElement("span");
            nameSpan.className = "fw-semibold";
            nameSpan.textContent = senderName;
            meta.appendChild(nameSpan);
        }

        // separator •
        if (senderName && time) meta.append(" • ");

        // czas
        if (time) {
            const timeSpan = document.createElement("span");
            timeSpan.className = "timestamp";
            timeSpan.textContent = formatMessageTime(new Date(time));
            meta.appendChild(timeSpan);
        }

        if (type === "sent" && status) {
            const statusSpan = document.createElement("span");
            statusSpan.className = "status-icon ms-1";
            statusSpan.innerHTML = getStatusIcon(status);
            meta.appendChild(statusSpan);
        }

        wrapper.appendChild(meta);
    }

    // Create inner row
    const row = document.createElement("div");
    row.className = "d-flex align-items-center"; // Flexbox: [button][message]

    // Create delete button
    const deleteButton = document.createElement("button");
    deleteButton.innerHTML = "&times;";
    deleteButton.className = "delete-button btn btn-sm btn-danger me-2"; // Margin right
    deleteButton.style.padding = "0.2rem 0.5rem";
    deleteButton.style.visibility = "hidden"; 
    wrapper.addEventListener("mouseenter", () => {
        deleteButton.style.visibility = "visible"; 
    });
    wrapper.addEventListener("mouseleave", () => {
        deleteButton.style.visibility = "hidden"; 
    });

    // Handle delete button click
    deleteButton.addEventListener("click", async () => {
        if (!messageId) return;

        const res = await fetch(`/api/chat/remove/${messageId}`, {
            method: "DELETE"
        });

        if (res.ok) {
            wrapper.remove();

        }
        else {
            console.log(res);
        }


    });

    // Create edit button
    const editButton = document.createElement("button");
    editButton.innerHTML = "✎";
    editButton.className = "edit-button btn btn-sm btn-danger me-2"; // Margin right
    editButton.style.padding = "0.2rem 0.5rem";
    editButton.style.visibility = "hidden";
    wrapper.addEventListener("mouseenter", () => {
        editButton.style.visibility = "visible"; 
    });
    wrapper.addEventListener("mouseleave", () => {
        editButton.style.visibility = "hidden";
    });

    // Handle edit button click
    editButton.addEventListener("click", async () => {
        if (!messageId) return;

        
        const newMessage = prompt("wpisz nowa wiadomosc", message)
        const res = await fetch(`/api/chat/edit/${messageId}`, {
            method: "PUT",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                messageId: messageId,
                newMessage: newMessage
            })
        });

        if (res.ok) {
            message = newMessage; // lokalna aktualizacja referencji do starej treści
            updateMessageContent(wrapper, newMessage);

        }
        else {
            console.log(res);
        }
    });

    // Create pin button
    const pinButton = document.createElement("button");
    pinButton.innerHTML = "📌";
    pinButton.className = "pin-button btn btn-sm btn-warning mx-2";
    pinButton.style.padding = "0.2rem 0.5rem";
    pinButton.style.visibility = "hidden";
    wrapper.addEventListener("mouseenter", () => {
        pinButton.style.visibility = "visible";
    });
    wrapper.addEventListener("mouseleave", () => {
        pinButton.style.visibility = "hidden";
    });
    // Handle pin button click
    pinButton.addEventListener("click", async () => {
        // Możesz oznaczyć wiadomość jako przypiętą, np. dodając klasę lub wykonując zapytanie
        const res = await fetch(`/api/chat/pin/${messageId}`, {
            method: "POST"
        });

        if (res.ok) {
            pinButton.classList.toggle("active");
            wrapper.classList.toggle("pinned");
        } else {
            console.log("Nie udało się przypiąć wiadomości", res);
        }
    });


    // Create emoji button
    const reactionButton = document.createElement("button");
    reactionButton.innerHTML = "😀";
    reactionButton.className = "pin-button btn btn-sm btn-warning";
    reactionButton.style.padding = "0.2rem 0.5rem";
    reactionButton.style.visibility = "hidden";
    wrapper.addEventListener("mouseenter", () => {
        reactionButton.style.visibility = "visible";
    });
    wrapper.addEventListener("mouseleave", () => {
        reactionButton.style.visibility = "hidden";
    });
    // Handle emoji button click
    // Handle emoji button click
    // Handle emoji button click
    reactionButton.addEventListener("click", async (e) => {
        e.stopPropagation(); // zapobiega zamykaniu od razu

        if (!messageId) return;

        // Zamknij inne otwarte menu
        document.querySelectorAll(".emoji-menu").forEach(menu => menu.remove());
        document.removeEventListener("click", handleGlobalEmojiMenuClose); // uniknij wielokrotnego dodania

        // Pobierz emoji z API
        let emojis = [];
        try {
            const res = await fetch("/api/reactions/emojis");
            emojis = await res.json(); // [{id: 1, symbol: "😀"}, ...]
        } catch (err) {
            console.error("Nie udało się pobrać emoji", err);
            return;
        }

        // Stwórz menu
        const menu = document.createElement("div");
        menu.className = "emoji-menu d-flex flex-row p-2 border rounded bg-white shadow position-absolute";
        menu.style.zIndex = 1000;
        // Osadzenie menu w wiadomości
        messageElement.style.position = "relative";
        messageElement.appendChild(menu);

        // Oblicz pozycję menu, by nie wyszło poza okno
        requestAnimationFrame(() => {
            const menuRect = menu.getBoundingClientRect();
            const containerRect = document.getElementById("messages").getBoundingClientRect();

            // Domyślnie po lewej
            menu.style.left = "0";
            menu.style.right = "auto";

            // Jeśli wychodzi poza prawą krawędź — przesuń w lewo
            if (menuRect.right > containerRect.right) {
                menu.style.left = "auto";
                menu.style.right = "0";
            }

            // Jeśli wychodzi poza dolną krawędź — przesuń menu nad przycisk
            if (menuRect.bottom > containerRect.bottom) {
                menu.style.top = "auto";
                menu.style.bottom = "100%";
            } else {
                menu.style.top = "100%";
                menu.style.bottom = "auto";
            }
        });


        // Emoji jako przyciski w linii
        emojis.forEach(emoji => {
            const btn = document.createElement("button");
            btn.className = "btn btn-sm btn-light";
            btn.textContent = emoji.symbol;
            btn.style.fontSize = "1.3rem";
            btn.style.lineHeight = "1.5";
            btn.style.padding = "0.3rem 0.5rem";

            btn.addEventListener("click", async () => {
                try {
                    const res = await fetch("/api/reactions/add", {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json"
                        },
                        body: JSON.stringify({
                            chatMessageId: messageId,
                            emojiId: emoji.id
                        })
                    });

                    if (res.ok) {
                        menu.remove();
                        document.removeEventListener("click", handleGlobalEmojiMenuClose);
                    } else {
                        console.log("Błąd dodawania reakcji", await res.text());
                    }
                } catch (err) {
                    console.error("Błąd:", err);
                }
            });

            menu.appendChild(btn);
        });

        // Osadzenie menu w wiadomości
        messageElement.style.position = "relative";
        messageElement.appendChild(menu);

        // Kliknięcie poza menu = zamknięcie
        function handleGlobalEmojiMenuClose(event) {
            if (!menu.contains(event.target)) {
                menu.remove();
                document.removeEventListener("click", handleGlobalEmojiMenuClose);
            }
        }
        setTimeout(() => {
            document.addEventListener("click", handleGlobalEmojiMenuClose);
        }, 0);
    });




    // Create message bubble
    const messageElement = document.createElement("div");
    messageElement.className = `message ${type} p-2 rounded position-relative`;

    const extension = message.split(".").pop().toLowerCase();

    if ((message.startsWith("/uploads/") && ["gif", "jpg", "jpeg", "png"].includes(extension))) {
        console.log("gif")
        const img = document.createElement("img");
        img.src = message;
        img.className = "img-fluid rounded";
        img.style.maxWidth = "200px";
        messageElement.appendChild(img);
    }
    else if (message.startsWith("http") && message.endsWith(".gif")) {
        const img = document.createElement("img");
        img.src = message;
        img.className = "img-fluid rounded";
        img.style.maxWidth = "200px";
        messageElement.appendChild(img);
    }

    else if (message.startsWith("/uploads/") && extension === "mp4") {
        const vid = document.createElement("video");
        vid.controls = true;
        vid.className = "w-100 rounded";
        const source = document.createElement("source");
        source.src = message;
        source.type = "video/mp4";
        vid.appendChild(source);
        messageElement.appendChild(vid);
    }
    else if (message.startsWith("/uploads/") && extension === "mp3") {
        const audio = document.createElement("audio");
        audio.controls = true;
        const source = document.createElement("source");
        source.src = message;
        source.type = "audio/mp3";
        audio.appendChild(source);
        messageElement.appendChild(audio);
    }
    else if (message.startsWith("/download/")) {
        const link = document.createElement("a");
        const fullFileName = message.split("/").pop();
        const cleanFileName = fullFileName.split("_").slice(1).join("_");
        link.href = message;
        link.textContent = `Załącznik: ${cleanFileName}`;
        link.download = cleanFileName;
        link.target = "_blank";
        link.className = "btn btn-sm btn-outline-primary";
        messageElement.appendChild(link);
    }
    else if (message.includes("https://www.openstreetmap.org/?mlat=")) {
        // Extract coordinates from URL
        const urlObj = new URL(message);
        const lat = parseFloat(urlObj.searchParams.get("mlat"));
        const lng = parseFloat(urlObj.searchParams.get("mlon"));
        const zoom = urlObj.hash.split("/")[1] || "16"; // Default zoom 16

        // Calculate a small bounding box around the point (~100m)
        const bboxPadding = 0.002; // Adjust this to control zoom level (smaller = more zoomed in)
        const bbox = [
            lng - bboxPadding, // min longitude
            lat - bboxPadding, // min latitude
            lng + bboxPadding, // max longitude
            lat + bboxPadding  // max latitude
        ].join(",");

        // Correct embed URL with marker and tight bounding box
        const embedUrl = `https://www.openstreetmap.org/export/embed.html?bbox=${bbox}&layer=mapnik&marker=${lat},${lng}`;

        const mapContainer = document.createElement("div");
        mapContainer.className = "map-container";
        mapContainer.style.width = "250px";
        mapContainer.style.height = "150px";
        mapContainer.style.background = "#eee";
        mapContainer.style.display = "flex";
        mapContainer.style.alignItems = "center";
        mapContainer.style.justifyContent = "center";

        const mapButton = document.createElement("button");
        mapButton.className = "btn btn-primary btn-sm";
        mapButton.textContent = "Show Map";
        mapButton.addEventListener("click", () => {
            const iframe = document.createElement("iframe");
            iframe.src = embedUrl;
            iframe.width = "250";
            iframe.height = "150";
            iframe.style.border = "0";
            mapContainer.innerHTML = "";
            mapContainer.appendChild(iframe);
        });

        mapContainer.appendChild(mapButton);
        messageElement.appendChild(mapContainer);

        // Also add a link to open full map
        const link = document.createElement("a");
        link.href = message;
        link.textContent = "Open in OpenStreetMap";
        link.target = "_blank";
        link.className = "d-block mt-1";
        messageElement.appendChild(link);
    }
    else {
        messageElement.textContent = message;
    }

    //if (time) {
    //    const timeSpan = document.createElement("small");
    //    timeSpan.className = "timestamp text-muted mb-1 d-block";
    //    const date = new Date(time);
    //    timeSpan.textContent = formatMessageTime(date);
    //    wrapper.appendChild(timeSpan);
    //}


    if (type === "sent") {
        row.appendChild(reactionButton);
        row.appendChild(pinButton);
        row.appendChild(editButton);
        row.appendChild(deleteButton);
        row.appendChild(messageElement);
    } else {
        row.appendChild(messageElement);
        row.appendChild(pinButton);
        row.appendChild(reactionButton);
    }

    
    // Add row to wrapper
    wrapper.appendChild(row);
    
    // Append wrapper
    messagesDiv.appendChild(wrapper);
    // Pobierz i pokaż reakcje pod wiadomością
    if (messageId) {
        try {
            const resReactions = await fetch(`/api/reactions/${messageId}`);
            const reactionData = await resReactions.json();

            const reactionContainer = document.createElement("div");
            reactionContainer.className = "reaction-container mt-1 d-flex flex-wrap gap-1";

            reactionData.forEach(r => {
                const btn = document.createElement("button");
                btn.className = "btn btn-sm btn-light border";
                btn.textContent = `${r.emoji} ${r.count}`;
                btn.title = r.users.map(u => u.userName).join(", ");
                btn.disabled = true;
                reactionContainer.appendChild(btn);
            });

            wrapper.appendChild(reactionContainer);
        } catch (err) {
            console.error("Nie udało się pobrać reakcji:", err);
        }
    }

    // Scroll to bottom
    messagesDiv.scrollTop = messagesDiv.scrollHeight;
}


function formatMessageTime(date) {
    const today = new Date();
    const messageDate = new Date(date);
    const diffTime = today - messageDate;
    const dayOfWeek = messageDate.toLocaleDateString('pl-PL', { weekday: 'short' });
    const dayOfMonth = messageDate.toLocaleDateString('pl-PL', { day: 'numeric', month: 'short' });

    // Jeśli wiadomość jest dzisiejsza
    if (messageDate.toDateString() === today.toDateString()) {
        return messageDate.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }

    // Jeśli wiadomość jest starsza niż 7 dni
    if (diffTime > 7 * 24 * 60 * 60 * 1000) {
        return `${dayOfMonth} ${messageDate.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`; 
    }

    // Jeśli ten sam tydzień (np. ostatni czwartek, jeśli dziś jest czwartek)
    return `${dayOfWeek} ${messageDate.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
}

connection.on("MessageEdited", (messageId, newMessage) => {
    const wrapper = document.querySelector(`[data-message-id="${messageId}"]`);
    if (wrapper) {
        updateMessageContent(wrapper, newMessage);
    }
});

connection.on("MessagePinToggled", (messageId, isPinned) => {
    const messageEl = document.querySelector(`[data-message-id="${messageId}"]`);
    if (messageEl) {
        messageEl.classList.toggle("pinned", isPinned);
        const pinBtn = messageEl.querySelector(".pin-button");
        if (pinBtn) {
            pinBtn.classList.toggle("active", isPinned);
        }
    }
});

connection.on("ReactionUpdated", (messageId) => {
    const wrapper = document.querySelector(`[data-message-id="${messageId}"]`);
    if (wrapper) {
        refreshReactions(wrapper, messageId);
    }
});

connection.on("MessageDelivered", (messageId, userId) => {
    const msgEl = document.querySelector(`[data-message-id='${messageId}']`);
    const icon = msgEl?.querySelector(".status-icon");
    if (icon) icon.innerHTML = getStatusIcon("delivered");
});

connection.on("MessageRead", (messageId, userId) => {
    const msgEl = document.querySelector(`[data-message-id='${messageId}']`);
    const icon = msgEl?.querySelector(".status-icon");
    if (icon) icon.innerHTML = getStatusIcon("read");
});


async function refreshReactions(wrapper, messageId) {
    const oldContainer = wrapper.querySelector(".reaction-container");
    if (oldContainer) oldContainer.remove();

    try {
        const res = await fetch(`/api/reactions/${messageId}`);
        const reactionData = await res.json();

        const reactionContainer = document.createElement("div");
        reactionContainer.className = "reaction-container mt-1 d-flex flex-wrap gap-1";

        reactionData.forEach(r => {
            const btn = document.createElement("button");
            btn.className = "btn btn-sm btn-light border";
            btn.textContent = `${r.emoji} ${r.count}`;
            btn.title = r.users.map(u => u.userName).join(", ");
            btn.disabled = true;
            reactionContainer.appendChild(btn);
        });

        wrapper.appendChild(reactionContainer);
    } catch (err) {
        console.error("Nie udało się odświeżyć reakcji:", err);
    }
}
function getStatusIcon(status) {
    switch (status) {
        case "sent":
            return `<i class="bi bi-check"></i>`; // jedna fajka
        case "delivered":
            return `<i class="bi bi-check-all"></i>`; // dwie fajki
        case "read":
            return `<i class="bi bi-eye-fill"></i>`; // oko
        default:
            return `<i class="bi bi-check"></i>`;
    }
}
