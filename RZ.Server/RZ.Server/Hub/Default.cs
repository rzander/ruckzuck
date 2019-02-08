using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace RZ.Server
{
    public class Default : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();

            await Clients.Client(Context.ConnectionId).SendAsync("Append", "<li class=\"list-group-item list-group-item-info\">%tt% waiting for RuckZuck messages</li>");
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
        }

        public async Task Append(string message)
        {
            await Clients.All.SendAsync("Append", message);
        }
    }


}
