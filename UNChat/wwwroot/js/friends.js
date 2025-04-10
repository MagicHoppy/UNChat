import { selectUser } from "./chat.js";

export async function loadFriends() {
    const userId = document.getElementById("userId").value;
    const response = await fetch(`/api/friends/${userId}`);
    const friends = await response.json();
    const friendsList = document.getElementById("friends");
    friendsList.innerHTML = "";

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

export async function addFriend(friendId) {
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
            alert("Błąd: " + await response.text());
        }
    } catch (error) {
        console.error("Błąd dodawania znajomego:", error);
    }
}

export async function removeFriend(friendId) {
    const currentUserId = document.getElementById("userId").value;

    try {
        const response = await fetch("/api/friends/remove", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ friend1Id: currentUserId, friend2Id: friendId })
        });

        if (response.ok) {
            alert("Usunięto z znajomych!");
            loadFriends();
        } else {
            alert("Błąd: " + await response.text());
        }
    } catch (error) {
        console.error("Błąd usuwania znajomego:", error);
    }
}
