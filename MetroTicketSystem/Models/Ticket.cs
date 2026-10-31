namespace MetroTicketSystem.Models
{
    public class Ticket
    {
        public int Id { get; set; }

        public string TicketId { get; set; } = "";

        public string OrderId { get; set; } = "";

        public int Seq { get; set; }

        public string Direction { get; set; } = "outbound";

        public string FromStation { get; set; } = "";

        public string ToStation { get; set; } = "";

        public int Fare { get; set; }

        public DateTime IssuedAt { get; set; }

        public DateTime ValidDate { get; set; }

        public string Status { get; set; } = "Unused";
    }
}
