export const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .configureLogging(signalR.LogLevel.Information)
    .build();

connection.start()
    .then(() => console.log("Połączono z czatem"))
    .catch(err => console.error("Błąd połączenia:", err));
