using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UNChat.Context;
using UNChat.Models;

namespace UNChat.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly UNChatDbContext _context;
        private static readonly ConcurrentDictionary<string, string> _connections = new();

        public ChatHub(UNChatDbContext context)
        {
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                _connections[userId] = Context.ConnectionId;

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user != null)
                {
                    user.IsOnline = true;
                    await _context.SaveChangesAsync();
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                _connections.TryRemove(userId, out _);

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user != null)
                {
                    user.IsOnline = false;
                    user.LastOnline = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
        public async Task Heartbeat()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.LastOnline = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
        }

    }
}
