export function addMessage(type, message, time = null) {
    const messagesDiv = document.getElementById("messages");

    // Create wrapper
    const wrapper = document.createElement("div");
    wrapper.className = `d-flex flex-column ${type === "sent" ? "align-items-end" : "align-items-start"} mb-2`;

    // Create inner row
    const row = document.createElement("div");
    row.className = "d-flex align-items-center"; // Flexbox: [button][message]

    // Create delete button
    const deleteButton = document.createElement("button");
    deleteButton.innerHTML = "&times;";
    deleteButton.className = "delete-button btn btn-sm btn-danger me-2"; // Margin right
    deleteButton.style.padding = "0.2rem 0.5rem";
    deleteButton.style.visibility = "hidden"; // instead of display = "none"
    wrapper.addEventListener("mouseenter", () => {
        deleteButton.style.visibility = "visible"; // show it
    });
    wrapper.addEventListener("mouseleave", () => {
        deleteButton.style.visibility = "hidden"; // hide it but keep space
    });

    // Handle delete button click
    deleteButton.addEventListener("click", () => {
        wrapper.remove();
    });

    // Create message bubble
    const messageElement = document.createElement("div");
    messageElement.className = `message ${type} p-2 rounded position-relative`;

    // Detect content type
    if ((message.startsWith("/uploads/") || message.startsWith("http")) && (message.endsWith(".gif") || message.endsWith(".jpg") || message.endsWith(".png"))) {
        const img = document.createElement("img");
        img.src = message;
        img.className = "img-fluid rounded";
        img.style.maxWidth = "200px";
        messageElement.appendChild(img);
    }
    else if (message.startsWith("/uploads/") && message.endsWith(".mp4")) {
        const vid = document.createElement("video");
        vid.controls = true;
        vid.className = "w-100 rounded";
        const source = document.createElement("source");
        source.src = message;
        source.type = "video/mp4";
        vid.appendChild(source);
        messageElement.appendChild(vid);
    }
    else if (message.startsWith("/uploads/") && message.endsWith(".mp3")) {
        const audio = document.createElement("audio");
        audio.controls = true;
        const source = document.createElement("source");
        source.src = message;
        source.type = "audio/mp3";
        audio.appendChild(source);
        messageElement.appendChild(audio);
    }
    else if (message.startsWith("/uploads/") && /\.(pdf|txt|zip|docx?|xlsx?)$/i.test(message)) {
        const link = document.createElement("a");
        link.href = message;
        const fullFileName = message.split("/").pop();
        const cleanFileName = fullFileName.split("_").slice(1).join("_");
        link.textContent = `Załącznik: ${cleanFileName}`;
        link.download = cleanFileName;
        link.target = "_blank";
        link.className = "btn btn-sm btn-outline-primary";
        messageElement.appendChild(link);
    }
    else {
        messageElement.textContent = message;
    }

    if (time) {
        const timeSpan = document.createElement("small");
        timeSpan.className = "text-muted mb-1 d-block";
        const date = new Date(time);
        timeSpan.textContent = formatMessageTime(date);
        wrapper.appendChild(timeSpan);
    }
    if (type === "sent") {
        row.appendChild(deleteButton);
    }
    row.appendChild(messageElement);
    // Add row to wrapper
    wrapper.appendChild(row);
    // Append wrapper
    messagesDiv.appendChild(wrapper);

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
