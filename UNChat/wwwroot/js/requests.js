import { loadFriends } from "./friends.js";

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

        const denyButton = document.createElement("button");
        denyButton.textContent = "X";
        denyButton.title = "Odrzuc";
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

