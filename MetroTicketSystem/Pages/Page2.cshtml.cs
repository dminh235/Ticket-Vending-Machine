
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MetroTicketSystem.Services;
using MetroTicketSystem.Data;
using MetroTicketSystem.Models;

namespace MetroTicketSystem.Pages
{
    public class Page2Model : PageModel
    {
        private readonly VnpayService _vnpayService;
        private readonly TicketOrderRepository _orderRepository;

        public Page2Model(
            VnpayService vnpayService,
            TicketOrderRepository orderRepository)
        {
            _vnpayService = vnpayService;
            _orderRepository = orderRepository;
        }

        public string Destination { get; set; } = "";
        public string TripType { get; set; } = "oneway";
        public string Language { get; set; } = "vi";

        public string DestinationName { get; set; } = "";
        public int TicketPrice { get; set; }

        public bool IsVietnamese => Language == "vi";

        private static readonly Dictionary<string, (string Name, int Price)> Stations =
            new()
            {
                { "Tan My", ("Tân Mỹ", 10000) },
                { "Can Gio", ("Cần Giờ", 60000) },
                { "Le Thi Rieng", ("Lê Thị Riêng", 10000) },
                { "Tan Son Nhat", ("Tân Sơn Nhất", 12000) },
                { "Suoi Tien", ("Suối Tiên", 20000) },
                { "Long Thanh Airport", ("Sân bay Long Thành", 65000) }
            };

        public IActionResult OnGet(string destination, string tripType, string language)
        {
            Destination = destination ?? "";
            TripType = tripType == "roundtrip" ? "roundtrip" : "oneway";
            Language = language == "en" ? "en" : "vi";

            Response.Cookies.Append("lang", Language, new CookieOptions
            {
                Path = "/",
                MaxAge = TimeSpan.FromHours(1),
                SameSite = SameSiteMode.Lax
            });

            if (!Stations.TryGetValue(Destination, out var station))
            {
                return RedirectToPage("/Index");
            }

            DestinationName = station.Name;
            TicketPrice = station.Price;

            if (TripType == "roundtrip")
            {
                TicketPrice *= 2;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostPayAsync(
            string destination,
            string tripType,
            string language,
            int quantity,
            string paymentMethod)
        {
            if (quantity < 1 || quantity > 5)
            {
                return BadRequest("Số lượng vé không hợp lệ.");
            }

            if (paymentMethod != "card" && paymentMethod != "qr")
            {
                return BadRequest("Hình thức thanh toán không hợp lệ.");
            }

            if (!Stations.TryGetValue(destination ?? "", out var station))
            {
                return BadRequest("Ga đến không hợp lệ.");
            }

            tripType = tripType == "roundtrip" ? "roundtrip" : "oneway";
            language = language == "en" ? "en" : "vi";

            int pricePerTicket = station.Price;

            if (tripType == "roundtrip")
            {
                pricePerTicket *= 2;
            }

            int totalAmount = checked(pricePerTicket * quantity);

            string orderId =
                DateTime.Now.ToString("yyyyMMddHHmmss")
                + Guid.NewGuid().ToString("N")[..8];

            var order = new TicketOrder
            {
                OrderId = orderId,
                Destination = destination,
                DestinationName = station.Name,
                TripType = tripType,
                Quantity = quantity,
                PricePerTicket = pricePerTicket,
                TotalAmount = totalAmount,
                PaymentMethod = paymentMethod,
                PaymentStatus = "Pending",
                CreatedAt = DateTime.Now
            };

            await _orderRepository.CreateOrderAsync(order);

            if (paymentMethod == "qr")
            {
                return RedirectToPage("/QrSimulation", new
                {
                    destination,
                    tripType,
                    language,
                    quantity,
                    amount = totalAmount,
                    orderId
                });
            }

            string orderInfo =
                $"Metro ticket {station.Name}, {tripType}, quantity {quantity}";

            string returnUrl = $"{Request.Scheme}://{Request.Host}/PaymentReturn";

            string paymentUrl = _vnpayService.CreatePaymentUrl(
                HttpContext,
                orderId,
                totalAmount,
                orderInfo,
                returnUrl,
                language == "en" ? "en" : "vn",
                "VNBANK");

            return Redirect(paymentUrl);
        }
    }
}
