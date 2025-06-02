import { loadFriends } from "./friends.js";
import { connection } from "./connection.js";
import { loadUsers } from "./user.js";
export async function loadFriendRequests() {
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
        acceptButton.innerHTML = '<i class="bi bi-check-lg"></i>';
        acceptButton.className = "btn btn-outline-success btn-sm p-1 me-1";
        acceptButton.title = "Akceptuj";
        acceptButton.style.width = "30px";
        acceptButton.style.height = "30px";
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

        const denyButton = document.createElement("button");
        denyButton.innerHTML = '<i class="bi bi-x-lg"></i>';
        denyButton.className = "btn btn-outline-danger btn-sm p-1";
        denyButton.title = "Odrzuć";
        denyButton.style.width = "30px";
        denyButton.style.height = "30px";
        denyButton.addEventListener("click", async () => {
            await fetch("/api/friends/deny", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ friend1Id: request.id, friend2Id: userId })
            });
            alert("Odrzucono zaproszenie");
            loadFriends();
            loadFriendRequests();
        });



        container.appendChild(name);
        container.appendChild(acceptButton);
        container.appendChild(denyButton);
        requestList.appendChild(container);
    });
}

connection.on("FriendRequestReceived", (fromUserId) => {
    console.log("New friend request received from:", fromUserId);
    loadFriendRequests();
});

connection.on("FriendRequestAccepted", (otherUserId) => {
    console.log("Friend request accepted by:", otherUserId);
    loadFriends();
    loadFriendRequests();
    loadUsers();
});

connection.on("FriendRequestDenied", (otherUserId) => {
    console.log("Friend request denied by:", otherUserId);
    loadFriendRequests();
});
