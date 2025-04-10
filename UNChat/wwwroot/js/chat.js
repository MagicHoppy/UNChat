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
        const container = document.createElement("div");
        container.style.display = "flex";
        container.style.justifyContent = "space-between";
        container.style.alignItems = "center";
        container.style.marginBottom = "5px";

        const button = document.createElement("button");
        button.className = "user-btn";
        button.textContent = user.name;
        button.dataset.id = user.id;

        const friendButton = document.createElement("button");
        friendButton.textContent = "➕";
        friendButton.className = "friend-btn";
        friendButton.title = "Dodaj do znajomych";
        friendButton.style.marginLeft = "5px";
        friendButton.addEventListener("click", (e) => {
            e.stopPropagation(); // Żeby nie wywołać selectUser
            addFriend(user.id);
        });

        container.appendChild(button);
        container.appendChild(friendButton);
        userList.appendChild(container);
    });
}
async function loadFriends() {
    const userId = document.getElementById("userId").value;
    const response = await fetch(`/api/friends/${userId}`);
    const friends = await response.json();
    const friendsList = document.getElementById("friends");
    friendsList.innerHTML = ""; // wyczyść przed załadowaniem

    friends.forEach(friend => {
        const container = document.createElement("div");
        container.style.display = "flex";
        container.style.justifyContent = "space-between";
        container.style.alignItems = "center";
        container.style.marginBottom = "5px";

        const button = document.createElement("button");
        button.className = "user-btn";
        button.textContent = friend.name;
        button.dataset.id = friend.id;
        button.addEventListener("click", () => selectUser(friend.id, friend.name));
       // console.log(friend.name);
        const removeButton = document.createElement("button");
        removeButton.textContent = "-";
        removeButton.className = "friend-remove-btn";
        removeButton.title = "usun z znajomych";
        removeButton.style.marginLeft = "5px";
        removeButton.addEventListener("click", (e) => {
            e.stopPropagation(); // Żeby nie wywołać selectUser
            removeFriend(friend.id);
        });
        container.appendChild(button);
        container.appendChild(removeButton);
        friendsList.appendChild(container);

    });
}

async function addFriend(friendId) {
    const currentUserId = document.getElementById("userId").value;

    try {
        const response = await fetch("/api/friends/add", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ friend1Id: currentUserId, friend2Id: friendId })
        });

        if (response.ok) {
            alert("Dodano do znajomych!");
            loadFriends();
        } else {
            const err = await response.text();
            alert("Błąd: " + err);
        }
    } catch (error) {
        console.error("Błąd dodawania znajomego:", error);
    }
}

async function removeFriend(friendId) {
    const currentUserId = document.getElementById("userId").value;

    try {
        const response = await fetch("/api/friends/remove", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ friend1Id: currentUserId, friend2Id: friendId })
        });
        
        if (response.ok) {
            alert("usunieto z znajomych!");
            loadFriends();
        } else {
            const err = await response.text();
            alert("Błąd: " + err);
        }
    } catch (error) {
        console.error("Błąd usuwania znajomego:", error);
    }
}


async function selectUser(userId, userName) {
    selectedReceiverId = userId;
    document.getElementById("chatHeader").textContent = `Czat z ${userName}`;

    // Highlight selected user
    // Usuwamy 'active' tylko z przycisków w sekcji znajomych
    document.querySelectorAll("#friends .user-btn").forEach(btn => btn.classList.remove("active"));

    // Dodajemy 'active' tylko jeśli kliknięto znajomego
    const selectedButton = document.querySelector(`#friends .user-btn[data-id='${userId}']`);
    if (selectedButton) {
        selectedButton.classList.add("active");
    }

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

document.addEventListener("DOMContentLoaded", function () {
    const emojiButton = document.getElementById("emojiButton");
    const emojiPicker = document.getElementById("emojiPicker");
    const messageInput = document.getElementById("messageInput");

    // Toggle emoji picker
    emojiButton.addEventListener("click", () => {
        emojiPicker.style.display = emojiPicker.style.display === "none" ? "block" : "none";
    });

    // Insert emoji into input field
    document.querySelectorAll(".emoji").forEach(emoji => {
        emoji.addEventListener("click", function () {
            messageInput.value += this.innerText;
            emojiPicker.style.display = "none"; // Hide after selection
        });
    });

    // Hide emoji picker when clicking outside
    document.addEventListener("click", (event) => {
        if (!emojiPicker.contains(event.target) && event.target !== emojiButton) {
            emojiPicker.style.display = "none";
        }
    });
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
async function loadFriendRequests() {
    const userId = document.getElementById("userId").value;
    const response = await fetch(`/api/friends/requests/${userId}`);
    const requests = await response.json();
    const requestList = document.getElementById("requests");
    requestList.innerHTML = "";

    requests.forEach(request => {
        const container = document.createElement("div");
        container.style.display = "flex";
        container.style.justifyContent = "space-between";
        container.style.alignItems = "center";
        container.style.marginBottom = "5px";

        const name = document.createElement("span");
        name.textContent = request.name;

        const acceptButton = document.createElement("button");
        acceptButton.textContent = "✅";
        acceptButton.title = "Akceptuj";
        acceptButton.addEventListener("click", async () => {
            await fetch("/api/friends/accept", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ friend1Id: request.id, friend2Id: userId })
            });
            alert("Zaakceptowano zaproszenie");
            loadFriends();
            loadFriendRequests();
        });

        container.appendChild(name);
        container.appendChild(acceptButton);
        requestList.appendChild(container);
    });
}


loadUsers();
loadFriends();
loadFriendRequests();
