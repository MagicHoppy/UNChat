import { selectUser } from "./chat.js";
import { connection } from "./connection.js";
import { loadUsers } from "./user.js";

let groupSelectionMode = false;
let selectedUserIds = [];

export function enableGroupSelectionMode() {
    groupSelectionMode = true;
    selectedUserIds = [];
    loadFriends(); // reload to show checkboxes
}

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
        userButton.dataset.id = friend.chatId;
        console.log(friend.chatId);

        if (groupSelectionMode) {
            const checkbox = document.createElement("input");
            checkbox.type = "checkbox";
            checkbox.className = "form-check-input me-2";
            checkbox.addEventListener("change", () => {
                if (checkbox.checked) {
                    selectedUserIds.push(friend.id);
                } else {
                    selectedUserIds = selectedUserIds.filter(id => id !== friend.id);
                }
            });

            listItem.prepend(checkbox);
        } else {
            userButton.addEventListener("click", () => selectUser(friend.chatId, friend.name));
        }


        const statusSpan = document.createElement("span");

        if (friend.isOnline) {
            statusSpan.innerHTML = `<span class="badge bg-success">Online</span>`;
        } else if (friend.lastOnline) {
            const lastSeen = new Date(friend.lastOnline);
            const diffMs = Date.now() - lastSeen.getTime();
            const minutes = Math.floor(diffMs / (1000 * 60));
            const hours = Math.floor(diffMs / (1000 * 60 * 60));
            const days = Math.floor(diffMs / (1000 * 60 * 60 * 24));

            let label = "";

            if (diffMs < 60000) {
                label = "przed chwilą";
            } else if (minutes < 60) {
                label = `${minutes} m`;
            } else if (hours < 24) {
                label = `${hours} h`;
            } else {
                label = `${days} d`;
            }

            statusSpan.innerHTML = `<span class="badge bg-secondary">${label}</span>`;
        } else {
            statusSpan.innerHTML = `<span class="badge bg-secondary">Offline</span>`;
        }

        const badgeContainer = document.createElement("div");
        badgeContainer.appendChild(statusSpan);

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

document.getElementById("startGroupChatBtn").addEventListener("click", () => {
    enableGroupSelectionMode();
    document.getElementById("groupChatForm").classList.remove("d-none");
});

document.getElementById("cancelGroupBtn").addEventListener("click", () => {
    groupSelectionMode = false;
    selectedUserIds = [];
    document.getElementById("groupChatForm").classList.add("d-none");
    loadFriends();
});

document.getElementById("createGroupBtn").addEventListener("click", async () => {
    const groupName = document.getElementById("groupChatName").value.trim();
    const creatorId = document.getElementById("userId").value;

    if (!groupName || selectedUserIds.length < 2) {
        showToast("Błąd", "Wybierz co najmniej 2 znajomych i wpisz nazwę grupy.", "warning");
        return;
    }

    try {
        const res = await fetch("/api/chat/create-group", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                name: groupName,
                userIds: selectedUserIds,
                creatorId: creatorId
            })
        });

        if (res.ok) {
            showToast("Sukces", "Utworzono czat grupowy!", "success");
            groupSelectionMode = false;
            selectedUserIds = [];
            document.getElementById("groupChatForm").classList.add("d-none");
            document.getElementById("groupChatName").value = "";
            loadFriends();
            loadGroupChats();
        } else {
            showToast("Błąd", await res.text(), "danger");
        }
    } catch (err) {
        console.error("Błąd tworzenia grupy:", err);
        showToast("Błąd", "Nie udało się utworzyć grupy", "danger");
    }
});


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

export async function loadGroupChats() {
    const userId = document.getElementById("userId").value;
    const response = await fetch("/api/chat/groups");
    const groupChats = await response.json();
    const groupChatsList = document.getElementById("groupChats");

    groupChatsList.innerHTML = "";

    if (groupChats.length === 0) {
        groupChatsList.innerHTML = `<div class="alert alert-info">Brak czatów grupowych.</div>`;
        return;
    }

    groupChats.forEach(chat => {
        const listItem = document.createElement("li");
        listItem.className = "list-group-item d-flex justify-content-between align-items-center flex-wrap";

        const button = document.createElement("button");
        button.className = "btn btn-link text-start flex-grow-1";
        button.textContent = chat.chatName || "Grupa bez nazwy";
        button.addEventListener("click", () => selectUser(chat.chatId, chat.chatName));

        const buttonGroup = document.createElement("div");
        buttonGroup.className = "d-flex gap-2";

        const leaveBtn = document.createElement("button");
        leaveBtn.className = "btn btn-outline-warning btn-sm";
        leaveBtn.innerHTML = '<i class="bi bi-box-arrow-right"></i> Opuść';
        leaveBtn.addEventListener("click", async (e) => {
            e.stopPropagation();
            if (confirm(`Czy na pewno chcesz opuścić grupę "${chat.chatName}"?`)) {
                const res = await fetch(`/api/chat/leave-group/${chat.chatId}`, { method: "POST" });
                if (res.ok) {
                    showToast("Grupa", "Opuściłeś czat grupowy.", "success");
                    loadGroupChats();
                } else {
                    showToast("Błąd", await res.text(), "danger");
                }
            }
        });
        buttonGroup.appendChild(leaveBtn);

        if (chat.isAdmin) {
            const deleteBtn = document.createElement("button");
            deleteBtn.className = "btn btn-outline-danger btn-sm";
            deleteBtn.innerHTML = '<i class="bi bi-trash"></i> Usuń';
            deleteBtn.addEventListener("click", async (e) => {
                e.stopPropagation();
                if (confirm(`Czy na pewno chcesz usunąć grupę "${chat.chatName}"?`)) {
                    const res = await fetch(`/api/chat/delete-group/${chat.chatId}`, { method: "DELETE" });
                    if (res.ok) {
                        showToast("Grupa", "Czat grupowy został usunięty.", "success");
                        loadGroupChats();
                    } else {
                        showToast("Błąd", await res.text(), "danger");
                    }
                }
            });
            buttonGroup.appendChild(deleteBtn);
        }

        listItem.appendChild(button);
        listItem.appendChild(buttonGroup);
        groupChatsList.appendChild(listItem);
    });
}
