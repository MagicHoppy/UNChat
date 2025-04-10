export function setupEmojiPicker() {
    const emojiButton = document.getElementById("emojiButton");
    const emojiPicker = document.getElementById("emojiPicker");
    const messageInput = document.getElementById("messageInput");

    emojiButton.addEventListener("click", () => {
        emojiPicker.style.display = emojiPicker.style.display === "none" ? "block" : "none";
    });

    document.querySelectorAll(".emoji").forEach(emoji => {
        emoji.addEventListener("click", function () {
            messageInput.value += this.innerText;
            emojiPicker.style.display = "none";
        });
    });

    document.addEventListener("click", (event) => {
        if (!emojiPicker.contains(event.target) && event.target !== emojiButton) {
            emojiPicker.style.display = "none";
        }
    });
}
