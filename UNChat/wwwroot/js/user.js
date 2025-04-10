import { addFriend } from "./friends.js";

export async function loadUsers() {
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
