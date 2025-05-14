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
});
setInterval(loadFriends, 30000); // refresh every 30 seconds
