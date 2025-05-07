import { addFriend } from "./friends.js";

export async function loadUsers() {
    const response = await fetch("/api/users");
    const users = await response.json();
    const userList = document.getElementById("users");

    userList.innerHTML = ""; // Clear old content first
    document.getElementById("searchResults").innerHTML = "";
    document.getElementById("searchInput").value = "";

    users.forEach(user => {
        const container = document.createElement("div");
        container.className = "d-flex justify-content-between align-items-center mb-2"; // Bootstrap flex

        const button = document.createElement("button");
        button.className = "btn btn-outline-primary flex-grow-1 me-2"; // Bootstrap button
        button.textContent = user.name;
        button.dataset.id = user.id;
        button.addEventListener("click", () => {
            import("./chat.js").then(module => {
                module.selectUser(user.id, user.name);
            });
        });

        const friendButton = document.createElement("button");
        friendButton.textContent = "➕";
        friendButton.className = "btn btn-success"; // Bootstrap button
        friendButton.title = "Dodaj do znajomych";
        friendButton.addEventListener("click", (e) => {
            e.stopPropagation(); // To prevent selecting user
            addFriend(user.id);
        });

        container.appendChild(button);
        container.appendChild(friendButton);
        userList.appendChild(container);
    });
}
