import { selectUser } from "./chat.js";
import { connection } from "./connection.js";
import { loadUsers } from "./user.js";
export async function loadFriends() {
    const userId = document.getElementById("userId").value;
    const response = await fetch(`/api/friends/${userId}`);
    const friends = await response.json();
    const friendsList = document.getElementById("friends");
    friendsList.innerHTML = "";

    if (friends.length === 0) {
        friendsList.innerHTML = `
            <div class="alert alert-info">
                Obecnie nie masz znajomych.
            </div>
        `;
        return;
    }

    friends.forEach(friend => {
        const listItem = document.createElement("li");
        listItem.className = "list-group-item d-flex justify-content-between align-items-center";

        const userButton = document.createElement("button");
        userButton.className = "btn btn-link text-start flex-grow-1";
        userButton.textContent = friend.name;
        userButton.dataset.id = friend.id;
        userButton.addEventListener("click", () => selectUser(friend.id, friend.name));

        const badgeContainer = document.createElement("div");

        const removeButton = document.createElement("button");
        removeButton.className = "btn btn-outline-danger btn-sm ms-2";
        removeButton.innerHTML = '<i class="bi bi-person-dash"></i>';
        removeButton.title = "Remove friend";
        removeButton.addEventListener("click", (e) => {
            e.stopPropagation();
            if (confirm(`Czy na pewno chcesz usunąć ${friend.name} z znajomych?`)) {
                removeFriend(friend.id);
            }
        });

        badgeContainer.appendChild(removeButton);
        listItem.appendChild(userButton);
        listItem.appendChild(badgeContainer);
        friendsList.appendChild(listItem);
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
            // Using Toast notification instead of alert
            showToast("Success", "Wysłano zaproszenie!", "success");
            loadFriends();
        } else {
            showToast("Error", await response.text(), "danger");
        }
    } catch (error) {
        console.error("Error dodawania znajomego:", error);
        showToast("Error", "Błąd dodawania znajomego", "danger");
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
            showToast("Success", "Poprawnie usunięto ze znajomych!", "success");
            loadFriends();
        } else {
            showToast("Error", await response.text(), "danger");
        }
    } catch (error) {
        console.error("Error removing friend:", error);
        showToast("Error", "Błąd usuwania znajomego", "danger");
    }
}

// Helper function for Bootstrap toast notifications
function showToast(title, message, type = "info") {
    const toastContainer = document.getElementById("toastContainer") || createToastContainer();
    const toastId = `toast-${Date.now()}`;

    const toast = document.createElement("div");
    toast.className = `toast align-items-center text-white bg-${type} border-0`;
    toast.id = toastId;
    toast.setAttribute("role", "alert");
    toast.setAttribute("aria-live", "assertive");
    toast.setAttribute("aria-atomic", "true");

    toast.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">
                <strong>${title}</strong><br>
                ${message}
            </div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
        </div>
    `;

    toastContainer.appendChild(toast);
    const bsToast = new bootstrap.Toast(toast);
    bsToast.show();

    // Remove toast after it's hidden
    toast.addEventListener("hidden.bs.toast", () => {
        toast.remove();
    });
}

function createToastContainer() {
    const container = document.createElement("div");
    container.id = "toastContainer";
    container.className = "position-fixed bottom-0 end-0 p-3";
    container.style.zIndex = "11";
    document.body.appendChild(container);
    return container;
}
connection.on("FriendRemoved", (removedFriendId) => {
    const currentUserId = document.getElementById("userId").value;
    if (removedFriendId) {
        loadFriends(); // refresh the friend list
        loadUsers();
    }
});
connection.on("FriendAdded", (addedFriendId) => {
    const currentUserId = document.getElementById("userId").value;
    if (addedFriendId) {
        loadFriends(); // refresh the friend list
        loadUsers();

    }
});
