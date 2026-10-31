using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MetroTicketSystem.Data;
using MetroTicketSystem.Models;

namespace MetroTicketSystem.Pages
{
    public class PaymentReturnModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly TicketOrderRepository _orderRepository;

        public PaymentReturnModel(
            IConfiguration configuration,
            TicketOrderRepository orderRepository)
        {
            _configuration = configuration;
            _orderRepository = orderRepository;
        }

        public bool IsValidSignature { get; set; }
        public bool IsSuccess { get; set; }

        public string Message { get; set; } = "";
        public string OrderId { get; set; } = "";
        public string TransactionNo { get; set; } = "";
        public string BankCode { get; set; } = "";
        public string OrderInfo { get; set; } = "";
        public string PayDate { get; set; } = "";
        public decimal Amount { get; set; }
        public List<Ticket> Tickets { get; set; } = new();

        private bool Vi => Request.Cookies["lang"] != "en";
        private string T(string vi, string en) => Vi ? vi : en;

        public async Task<IActionResult> OnGetAsync()
        {
            string receivedHash = Request.Query["vnp_SecureHash"].ToString();

            if (string.IsNullOrEmpty(receivedHash))
            {
                Message = T("Không tìm thấy chữ ký giao dịch.", "Transaction signature not found.");
                return Page();
            }

            string hashSecret = _configuration["Vnpay:HashSecret"] ?? "";

            if (string.IsNullOrEmpty(hashSecret))
            {
                Message = T("Không tìm thấy cấu hình VNPAY.", "VNPAY configuration not found.");
                return Page();
            }

            var parameters = new SortedDictionary<string, string>(
                StringComparer.Ordinal
            );

            foreach (var item in Request.Query)
            {
                if (item.Key.StartsWith("vnp_", StringComparison.Ordinal)
                    && item.Key != "vnp_SecureHash"
                    && item.Key != "vnp_SecureHashType")
                {
                    parameters[item.Key] = item.Value.ToString();
                }
            }

            var hashData = new StringBuilder();

            foreach (var parameter in parameters)
            {
                if (hashData.Length > 0)
                {
                    hashData.Append('&');
                }

                string key = Uri.EscapeDataString(parameter.Key).Replace("%20", "+");
                string value = Uri.EscapeDataString(parameter.Value).Replace("%20", "+");

                hashData.Append(key).Append('=').Append(value);
            }

            string calculatedHash;

            using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(hashSecret)))
            {
                byte[] hashBytes = hmac.ComputeHash(
                    Encoding.UTF8.GetBytes(hashData.ToString())
                );

                calculatedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            try
            {
                byte[] receivedBytes = Convert.FromHexString(receivedHash);
                byte[] calculatedBytes = Convert.FromHexString(calculatedHash);

                IsValidSignature =
                    receivedBytes.Length == calculatedBytes.Length &&
                    CryptographicOperations.FixedTimeEquals(
                        receivedBytes,
                        calculatedBytes
                    );
            }
            catch
            {
                IsValidSignature = false;
            }

            if (!IsValidSignature)
            {
                Message = T("Chữ ký không hợp lệ. Không thể xác nhận giao dịch.", "Invalid signature. The transaction cannot be confirmed.");
                return Page();
            }

            OrderId = Request.Query["vnp_TxnRef"].ToString();
            TransactionNo = Request.Query["vnp_TransactionNo"].ToString();
            BankCode = Request.Query["vnp_BankCode"].ToString();
            OrderInfo = Request.Query["vnp_OrderInfo"].ToString();
            PayDate = Request.Query["vnp_PayDate"].ToString();

            if (long.TryParse(
                Request.Query["vnp_Amount"],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long amount))
            {
                Amount = amount / 100m;
            }

            string responseCode = Request.Query["vnp_ResponseCode"].ToString();
            string transactionStatus = Request.Query["vnp_TransactionStatus"].ToString();

            var order = await _orderRepository.GetOrderAsync(OrderId);

            if (order == null)
            {
                Message = T("Không tìm thấy đơn hàng.", "Order not found.");
                return Page();
            }

            if (order.PaymentMethod != "card")
            {
                Message = T("Phương thức thanh toán không hợp lệ.", "Invalid payment method.");
                return Page();
            }

            if (amount != order.TotalAmount * 100L)
            {
                Message = T("Số tiền giao dịch không khớp với đơn hàng.", "The transaction amount does not match the order.");
                return Page();
            }

            if (order.PaymentStatus != "Pending")
            {
                IsSuccess = order.PaymentStatus == "Paid";

                if (IsSuccess)
                {
                    await _orderRepository.IssueTicketsAsync(order);
                    Tickets = await _orderRepository.GetTicketsAsync(order.OrderId);
                }
                Message = IsSuccess
                    ? T("Đơn hàng đã được thanh toán trước đó.", "This order has already been paid.")
                    : T("Đơn hàng đã được xử lý trước đó.", "This order has already been processed.");

                return Page();
            }

            IsSuccess = responseCode == "00" && transactionStatus == "00";

            if (IsSuccess)
            {
                await _orderRepository.UpdatePaymentAsync(
                    OrderId,
                    "Paid",
                    TransactionNo
                );

                await _orderRepository.IssueTicketsAsync(order);
                Tickets = await _orderRepository.GetTicketsAsync(OrderId);

                Message = T("Thanh toán thành công!", "Payment successful!");
            }
            else
            {
                await _orderRepository.UpdatePaymentAsync(
                    OrderId,
                    "Failed"
                );

                Message = T("Thanh toán không thành công.", "Payment was not successful.");
            }

            return Page();
        }
    }
}
