using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MetroTicketSystem.Data;
using MetroTicketSystem.Models;

namespace MetroTicketSystem.Pages
{
    public class QrSuccessModel : PageModel
    {
        private readonly TicketOrderRepository _orderRepository;

        public QrSuccessModel(TicketOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }

        public TicketOrder Order { get; set; } = new();
        public List<Ticket> Tickets { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return BadRequest();
            }

            var order = await _orderRepository.GetOrderAsync(orderId);

            // Chỉ hiện thành công khi DB xác nhận đơn QR đã Paid
            if (order == null ||
                order.PaymentMethod != "qr" ||
                order.PaymentStatus != "Paid")
            {
                return BadRequest("Đơn hàng không hợp lệ hoặc chưa thanh toán.");
            }

            Order = order;

            await _orderRepository.IssueTicketsAsync(order);
            Tickets = await _orderRepository.GetTicketsAsync(order.OrderId);

            return Page();
        }
    }
}
