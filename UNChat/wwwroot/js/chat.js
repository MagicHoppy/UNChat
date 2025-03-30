const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .configureLogging(signalR.LogLevel.Information)
    .build();

connection.start()
    .then(() => console.log("Connected to chatHub"))
    .catch(err => console.error("Connection error:", err));

connection.on("ReceiveMessage", (senderId, message) => {
    console.log(`New message from ${senderId}: ${message}`);
});
