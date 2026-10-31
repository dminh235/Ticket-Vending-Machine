namespace MetroTicketSystem.Models
{
    public class TicketOrder
    {
        public int Id { get; set; }

        public string OrderId { get; set; } = "";

        public string Destination { get; set; } = "";

        public string DestinationName { get; set; } = "";

        public string TripType { get; set; } = "";

        public int Quantity { get; set; }

        public int PricePerTicket { get; set; }

        public int TotalAmount { get; set; }

        public string PaymentMethod { get; set; } = "";

        public string PaymentStatus { get; set; } = "Pending";

        public string? TransactionNo { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? PaidAt { get; set; }
    }
}
