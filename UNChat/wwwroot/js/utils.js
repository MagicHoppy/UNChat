export function addMessage(type, message, time = null) {
    const messagesDiv = document.getElementById("messages");
    const messageElement = document.createElement("div");
    messageElement.className = `message ${type}`;

    if (time) {
        const date = new Date(time);
        const formattedTime = formatMessageTime(date);
        messageElement.title = `Wysłano ${formattedTime}`;
    }

    if ((message.startsWith("/uploads/") || message.startsWith("http")) && (message.endsWith(".gif") || message.endsWith(".jpg") || message.endsWith(".png"))) {
        const img = document.createElement("img");
        img.src = message;
        img.style.maxWidth = "200px";
        messageElement.appendChild(img);
    }
    else if (message.startsWith("/uploads/") && message.endsWith(".mp4")) {
        const vid = document.createElement("video");
        vid.controls = true;
        const source = document.createElement("source");
        source.src = message;
        source.type = "video/mp4";
        vid.style.maxWidth = "300px";
        vid.appendChild(source);
        messageElement.appendChild(vid);
    }
    else if (message.startsWith("/uploads/") && message.endsWith(".mp3")) {
        const audio = document.createElement("audio");
        audio.controls = true;
        const source = document.createElement("source");
        source.src = message;
        source.type = "audio/mp3";
        audio.style.minWidth = "100px";
        audio.style.maxWidth = "300px";
        audio.appendChild(source);
        messageElement.appendChild(audio);
    }
    else if (message.startsWith("/uploads/") && /\.(pdf|txt|zip|docx?|xlsx?)$/i.test(message)) {
        const link = document.createElement("a");
        link.href = message;

        // Wyciągnij nazwę pliku z końcówki URL-a
        const fullFileName = message.split("/").pop();

        // Usuń GUID — zostaw tylko część po "_"
        const cleanFileName = fullFileName.split("_").slice(1).join("_");

        link.textContent = `Zalacznik ${cleanFileName}`;
        link.download = cleanFileName;
        link.target = "_blank";

        messageElement.appendChild(link);
    }
 else {
        messageElement.textContent = message;
    }

    messagesDiv.appendChild(messageElement);
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
