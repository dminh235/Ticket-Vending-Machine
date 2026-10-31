
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MetroTicketSystem.Data;

namespace MetroTicketSystem.Pages
{
    public class QrSimulationModel : PageModel
    {
        private readonly TicketOrderRepository _orderRepository;

        public QrSimulationModel(TicketOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }

        public string OrderId { get; set; } = "";
        public string Destination { get; set; } = "";
        public string TripType { get; set; } = "";
        public int Quantity { get; set; }
        public int Amount { get; set; }
        public string QrImage { get; set; } = "";
        public string Language { get; set; } = "vi";

        public async Task<IActionResult> OnGetAsync(
            string destination,
            string tripType,
            string language,
            int quantity,
            int amount,
            string orderId)
        {
            if (quantity < 1 || quantity > 5 || amount <= 0 ||
                string.IsNullOrWhiteSpace(orderId) ||
                string.IsNullOrWhiteSpace(destination))
            {
                return BadRequest();
            }

            var order = await _orderRepository.GetOrderAsync(orderId);

            if (order == null ||
                order.PaymentMethod != "qr" ||
                order.PaymentStatus != "Pending" ||
                order.Destination != destination ||
                order.Quantity != quantity ||
                order.TotalAmount != amount)
            {
                return BadRequest("Thông tin đơn hàng không hợp lệ.");
            }

            Destination = destination;
            TripType = tripType;
            Language = language == "en" ? "en" : "vi";
            Quantity = quantity;
            Amount = amount;
            OrderId = orderId;

            return Page();
        }

        public async Task<IActionResult> OnPostConfirmAsync(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return BadRequest("Mã đơn hàng không hợp lệ.");
            }

            var order = await _orderRepository.GetOrderAsync(orderId);

            if (order == null ||
                order.PaymentMethod != "qr" ||
                order.PaymentStatus != "Pending")
            {
                return BadRequest("Đơn hàng không hợp lệ hoặc đã được xử lý.");
            }

            if (DateTime.Now - order.CreatedAt > TimeSpan.FromMinutes(5))
            {
                await _orderRepository.UpdatePaymentAsync(orderId, "Failed");
                return BadRequest("Mã QR đã hết hạn.");
            }

            await _orderRepository.UpdatePaymentAsync(orderId, "Paid");
            await _orderRepository.IssueTicketsAsync(order);

            return RedirectToPage("/QrSuccess", new { orderId = order.OrderId });
        }
    }
}
