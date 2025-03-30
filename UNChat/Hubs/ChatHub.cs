using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using UNChat.Context;
using UNChat.Models;

namespace UNChat.Hubs
{
    public class ChatHub : Hub
    {
        private readonly UNChatDbContext _context;

        public ChatHub(UNChatDbContext context)
        {
            _context = context;
        }

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

            await Clients.User(receiverId).SendAsync("ReceiveMessage", senderId, message);
        }
    }
}
