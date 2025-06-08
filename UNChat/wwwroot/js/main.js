import { loadUsers } from "./user.js";
import { loadFriends } from "./friends.js";
import { loadGroupChats } from "./friends.js";
import { loadFriendRequests } from "./requests.js";
import { setupChat } from "./chat.js";
import { setupEmojiPicker } from "./emoji.js";


document.addEventListener("DOMContentLoaded", () => {
    loadUsers();
    loadFriends();
    loadGroupChats();

    loadFriendRequests();
    setupChat();
    setupEmojiPicker();
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('/sw.js')
            .then(reg => {
                console.log('Service Worker registered!', reg);
            })
            .catch(err => {
                console.error('Service Worker registration failed:', err);
            });
    }

});
setInterval(loadFriends, 30000); // 30 sekund
