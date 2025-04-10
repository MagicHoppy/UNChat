export function addMessage(type, message) {
    const messagesDiv = document.getElementById("messages");
    const messageElement = document.createElement("div");
    messageElement.className = `message ${type}`;

    if (message.startsWith("http") && message.includes(".gif")) {
        const img = document.createElement("img");
        img.src = message;
        img.style.maxWidth = "200px";
        messageElement.appendChild(img);
    } else if (message.startsWith("<img")) {
        // gdy wiadomość to HTML (np. po kliknięciu GIF-a)
        messageElement.innerHTML = message;
    } else {
        messageElement.textContent = message;
    }

    messagesDiv.appendChild(messageElement);
    messagesDiv.scrollTop = messagesDiv.scrollHeight;
}
