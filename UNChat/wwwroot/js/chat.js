const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .configureLogging(signalR.LogLevel.Information)
    .build();

connection.start()
    .then(() => console.log("Połączono z czatem"))
    .catch(err => console.error("Błąd połączenia:", err));

async function loadUsers() {
    const response = await fetch("/api/users");
    const users = await response.json();
    const select = document.getElementById("receiverId");

    users.forEach(user => {
        const option = document.createElement("option");
        option.value = user.id;
        option.textContent = user.name;
        select.appendChild(option);
    });
}

loadUsers();


document.getElementById("sendButton").addEventListener("click", async () => {
    const messageInput = document.getElementById("messageInput");
    const receiverInput = document.getElementById("receiverId"); // ID odbiorcy
    const senderId = document.getElementById("userId").value; // ID nadawcy
    const receiverId = receiverInput.value.trim();
    const message = messageInput.value.trim();

    if (!receiverId || !message) {
        alert("Wybierz odbiorcę i wpisz wiadomość!");
        return;
    }

    await connection.invoke("SendMessage", senderId, receiverId, message);
    messageInput.value = "";
});

// Odbiór wiadomości
connection.on("ReceiveMessage", (senderId, message) => {
    const messagesDiv = document.getElementById("messages");
    const messageElement = document.createElement("div");
    messageElement.innerHTML = `<strong>${senderId}:</strong> ${message}`;
    messagesDiv.appendChild(messageElement);
});
