using Microsoft.AspNetCore.SignalR;

namespace namHub_FastFood.HUBs
{
    public class OrderHub : Hub
    {
        // Hàm để gửi thông báo tới client
        public async Task NotifyOrderUpdated( int orderId, string status )
        {
            await Clients.All.SendAsync( "OrderUpdated", orderId, status );
        }
    }
}