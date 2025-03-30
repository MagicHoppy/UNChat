using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UNChat.Context;
using UNChat.Models;

namespace UNChat.Hubs
{
    public class ChatHub : Hub
    {
        private readonly UNChatDbContext _context;

        // Słownik do przechowywania połączeń użytkowników
        private static readonly ConcurrentDictionary<string, string> _connections = new();

        public ChatHub(UNChatDbContext context)
        {
            _context = context;
        }

        // Obsługa podłączenia użytkownika
        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier; // Pobranie ID zalogowanego użytkownika
            if (!string.IsNullOrEmpty(userId))
            {
                _connections[userId] = Context.ConnectionId; // Przypisanie ConnectionId do użytkownika
            }

            await base.OnConnectedAsync();
        }

        // Obsługa rozłączenia użytkownika
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                _connections.TryRemove(userId, out _);
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Wysyłanie wiadomości do konkretnego użytkownika
        public async Task SendMessage(string senderId, string receiverId, string message)
        {
            var chatMessage = new ChatMessage
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();

            // Wysyłanie wiadomości tylko jeśli użytkownik jest podłączony
            if (_connections.TryGetValue(receiverId, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync("ReceiveMessage", senderId, message);
            }
        }
    }
}
