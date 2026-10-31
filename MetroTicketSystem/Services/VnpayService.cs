using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MetroTicketSystem.Services
{
    public class VnpayService
    {
        private readonly IConfiguration _configuration;

        public VnpayService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string CreatePaymentUrl(
            HttpContext context,
            string orderId,
            int amount,
            string orderInfo,
            string returnUrl,
            string language = "vn",
            string? bankCode = null)
        {
            string tmnCode = _configuration["Vnpay:TmnCode"]
                ?? throw new InvalidOperationException("Missing VNPAY TmnCode.");

            string hashSecret = _configuration["Vnpay:HashSecret"]
                ?? throw new InvalidOperationException("Missing VNPAY HashSecret.");

            var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_Version"] = "2.1.0",
                ["vnp_Command"] = "pay",
                ["vnp_TmnCode"] = tmnCode,
                ["vnp_Amount"] = (amount * 100L).ToString(CultureInfo.InvariantCulture),
                ["vnp_CurrCode"] = "VND",
                ["vnp_TxnRef"] = orderId,
                ["vnp_OrderInfo"] = orderInfo,
                ["vnp_OrderType"] = "other",
                ["vnp_Locale"] = language,
                ["vnp_ReturnUrl"] = returnUrl,
                ["vnp_IpAddr"] = GetIpAddress(context),
                ["vnp_CreateDate"] = DateTime.Now.ToString(
        "yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                ["vnp_ExpireDate"] = DateTime.Now.AddMinutes(5).ToString(
        "yyyyMMddHHmmss", CultureInfo.InvariantCulture)
            };

            if (!string.IsNullOrWhiteSpace(bankCode))
            {
                parameters["vnp_BankCode"] = bankCode;
            }

            var hashData = new StringBuilder();
            var query = new StringBuilder();

            foreach (var parameter in parameters)
            {
                if (hashData.Length > 0)
                {
                    hashData.Append('&');
                    query.Append('&');
                }

                string encodedKey = Uri.EscapeDataString(parameter.Key);
                string encodedValue = Uri.EscapeDataString(parameter.Value);

                encodedKey = encodedKey.Replace("%20", "+");
                encodedValue = encodedValue.Replace("%20", "+");

                hashData.Append(encodedKey)
                        .Append('=')
                        .Append(encodedValue);

                query.Append(encodedKey)
                     .Append('=')
                     .Append(encodedValue);
            }

            string secureHash = HmacSHA512(hashSecret, hashData.ToString());

            return "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?"
                   + query
                   + "&vnp_SecureHash="
                   + secureHash;
        }

        private static string HmacSHA512(string key, string data)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);

            using var hmac = new HMACSHA512(keyBytes);
            byte[] hash = hmac.ComputeHash(dataBytes);

            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string GetIpAddress(HttpContext context)
        {
            return context.Connection.RemoteIpAddress?.ToString()
                   ?? "127.0.0.1";
        }
    }
}