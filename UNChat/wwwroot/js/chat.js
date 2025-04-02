const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .configureLogging(signalR.LogLevel.Information)
    .build();

let selectedReceiverId = null;

connection.start()
    .then(() => console.log("Połączono z czatem"))
    .catch(err => console.error("Błąd połączenia:", err));

async function loadUsers() {
    const response = await fetch("/api/users");
    const users = await response.json();
    const userList = document.getElementById("users");

    users.forEach(user => {
        const button = document.createElement("button");
        button.className = "user-btn";
        button.textContent = user.name;
        button.dataset.id = user.id;
        button.addEventListener("click", () => selectUser(user.id, user.name));
        userList.appendChild(button);
    });
}

async function selectUser(userId, userName) {
    selectedReceiverId = userId;
    document.getElementById("chatHeader").textContent = `Czat z ${userName}`;

    // Highlight selected user
    document.querySelectorAll(".user-btn").forEach(btn => btn.classList.remove("active"));
    document.querySelector(`[data-id='${userId}']`).classList.add("active");

    document.getElementById("messages").innerHTML = ""; // Clear previous messages

    // Fetch previous messages using ChatApiController
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

// Function to send a message
document.getElementById("sendButton").addEventListener("click", async () => {
    const messageInput = document.getElementById("messageInput");
    const senderId = document.getElementById("userId").value;
    const message = messageInput.value.trim();

    if (!selectedReceiverId || !message) {
        alert("Wybierz odbiorcę i wpisz wiadomość!");
        return;
    }

    // Send message using ChatApiController
    try {
        await fetch("/api/chat/send", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                senderId: senderId,
                receiverId: selectedReceiverId,
                message: message
            })
        });

        addMessage("sent", message);
        messageInput.value = "";
    } catch (error) {
        console.error("Błąd wysyłania wiadomości:", error);
    }
});

// Real-time message receiving via SignalR
connection.on("ReceiveMessage", (senderId, message) => {
    if (senderId === selectedReceiverId) {
        addMessage("received", message);
    }
});

function addMessage(type, message) {
    const messagesDiv = document.getElementById("messages");
    const messageElement = document.createElement("div");
    messageElement.className = `message ${type}`;
    messageElement.textContent = message;
    messagesDiv.appendChild(messageElement);
    messagesDiv.scrollTop = messagesDiv.scrollHeight;
}

loadUsers();
