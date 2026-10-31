
using Microsoft.Data.SqlClient;
using MetroTicketSystem.Models;

namespace MetroTicketSystem.Data
{
    public class TicketOrderRepository
    {
        private readonly string _connectionString;

        public TicketOrderRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Database connection string not found.");
        }

        public async Task CreateOrderAsync(TicketOrder order)
        {
            const string sql = @"
                INSERT INTO TicketOrders
                (
                    OrderId, Destination, DestinationName,
                    TripType, Quantity, PricePerTicket,
                    TotalAmount, PaymentMethod, PaymentStatus,
                    CreatedAt
                )
                VALUES
                (
                    @OrderId, @Destination, @DestinationName,
                    @TripType, @Quantity, @PricePerTicket,
                    @TotalAmount, @PaymentMethod, @PaymentStatus,
                    @CreatedAt
                )";

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@OrderId", order.OrderId);
            command.Parameters.AddWithValue("@Destination", order.Destination);
            command.Parameters.AddWithValue("@DestinationName", order.DestinationName);
            command.Parameters.AddWithValue("@TripType", order.TripType);
            command.Parameters.AddWithValue("@Quantity", order.Quantity);
            command.Parameters.AddWithValue("@PricePerTicket", order.PricePerTicket);
            command.Parameters.AddWithValue("@TotalAmount", order.TotalAmount);
            command.Parameters.AddWithValue("@PaymentMethod", order.PaymentMethod);
            command.Parameters.AddWithValue("@PaymentStatus", order.PaymentStatus);
            command.Parameters.AddWithValue("@CreatedAt", order.CreatedAt);

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdatePaymentAsync(
            string orderId,
            string status,
            string? transactionNo = null)
        {
            const string sql = @"
                UPDATE TicketOrders
                SET PaymentStatus = @PaymentStatus,
                    TransactionNo = @TransactionNo,
                    PaidAt = CASE
                        WHEN @PaymentStatus = 'Paid' THEN GETDATE()
                        ELSE PaidAt
                    END
                WHERE OrderId = @OrderId
                  AND PaymentStatus = 'Pending'";

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@OrderId", orderId);
            command.Parameters.AddWithValue("@PaymentStatus", status);
            command.Parameters.AddWithValue(
                "@TransactionNo",
                (object?)transactionNo ?? DBNull.Value);

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task<TicketOrder?> GetOrderAsync(string orderId)
        {
            const string sql = @"
        SELECT *
        FROM TicketOrders
        WHERE OrderId = @OrderId";

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@OrderId", orderId);

            await connection.OpenAsync();

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            return new TicketOrder
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                OrderId = reader.GetString(reader.GetOrdinal("OrderId")),
                Destination = reader.GetString(reader.GetOrdinal("Destination")),
                DestinationName = reader.GetString(reader.GetOrdinal("DestinationName")),
                TripType = reader.GetString(reader.GetOrdinal("TripType")),
                Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                PricePerTicket = reader.GetInt32(reader.GetOrdinal("PricePerTicket")),
                TotalAmount = reader.GetInt32(reader.GetOrdinal("TotalAmount")),
                PaymentMethod = reader.GetString(reader.GetOrdinal("PaymentMethod")),
                PaymentStatus = reader.GetString(reader.GetOrdinal("PaymentStatus")),
                TransactionNo = reader.IsDBNull(reader.GetOrdinal("TransactionNo"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("TransactionNo")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                PaidAt = reader.IsDBNull(reader.GetOrdinal("PaidAt"))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal("PaidAt"))
            };
        }


        private const string DepartureStation = "Bến Thành";

        // Phát hành vé sau khi đơn đã thanh toán thành công.
        // Gọi nhiều lần vẫn an toàn: đã có vé của đơn này thì không tạo thêm.
        public async Task IssueTicketsAsync(TicketOrder order)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            await using (var check = new SqlCommand(
                "SELECT COUNT(*) FROM Tickets WHERE OrderId = @OrderId",
                connection, transaction))
            {
                check.Parameters.AddWithValue("@OrderId", order.OrderId);
                int existing = (int)(await check.ExecuteScalarAsync())!;

                if (existing > 0)
                {
                    await transaction.CommitAsync();
                    return;
                }
            }

            bool isRoundTrip = order.TripType == "roundtrip";

            // Vé khứ hồi = 2 vé riêng, mỗi vé tính giá một chiều
            int fare = isRoundTrip ? order.PricePerTicket / 2 : order.PricePerTicket;
            string[] directions = isRoundTrip
                ? new[] { "outbound", "return" }
                : new[] { "outbound" };

            const string sql = @"
                INSERT INTO Tickets
                (
                    TicketId, OrderId, Seq, Direction,
                    FromStation, ToStation, Fare,
                    IssuedAt, ValidDate, Status
                )
                VALUES
                (
                    @TicketId, @OrderId, @Seq, @Direction,
                    @FromStation, @ToStation, @Fare,
                    @IssuedAt, @ValidDate, 'Unused'
                )";

            for (int seq = 1; seq <= order.Quantity; seq++)
            {
                foreach (string direction in directions)
                {
                    bool outbound = direction == "outbound";

                    await using var command = new SqlCommand(sql, connection, transaction);

                    command.Parameters.AddWithValue("@TicketId",
                        "TK" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant());
                    command.Parameters.AddWithValue("@OrderId", order.OrderId);
                    command.Parameters.AddWithValue("@Seq", seq);
                    command.Parameters.AddWithValue("@Direction", direction);
                    command.Parameters.AddWithValue("@FromStation",
                        outbound ? DepartureStation : order.DestinationName);
                    command.Parameters.AddWithValue("@ToStation",
                        outbound ? order.DestinationName : DepartureStation);
                    command.Parameters.AddWithValue("@Fare", fare);
                    command.Parameters.AddWithValue("@IssuedAt", DateTime.Now);
                    command.Parameters.AddWithValue("@ValidDate", DateTime.Today);

                    await command.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();
        }

        public async Task<List<Ticket>> GetTicketsAsync(string orderId)
        {
            const string sql = @"
                SELECT Id, TicketId, OrderId, Seq, Direction,
                       FromStation, ToStation, Fare,
                       IssuedAt, ValidDate, Status
                FROM Tickets
                WHERE OrderId = @OrderId
                ORDER BY Seq, Direction";

            var tickets = new List<Ticket>();

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@OrderId", orderId);

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tickets.Add(new Ticket
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    TicketId = reader.GetString(reader.GetOrdinal("TicketId")),
                    OrderId = reader.GetString(reader.GetOrdinal("OrderId")),
                    Seq = reader.GetInt32(reader.GetOrdinal("Seq")),
                    Direction = reader.GetString(reader.GetOrdinal("Direction")),
                    FromStation = reader.GetString(reader.GetOrdinal("FromStation")),
                    ToStation = reader.GetString(reader.GetOrdinal("ToStation")),
                    Fare = reader.GetInt32(reader.GetOrdinal("Fare")),
                    IssuedAt = reader.GetDateTime(reader.GetOrdinal("IssuedAt")),
                    ValidDate = reader.GetDateTime(reader.GetOrdinal("ValidDate")),
                    Status = reader.GetString(reader.GetOrdinal("Status"))
                });
            }

            return tickets;
        }

    }
}
