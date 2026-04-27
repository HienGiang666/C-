using System.Text.Json.Serialization;

namespace TourApp.Mobile.Models
{
    /// <summary>
    /// Thông tin giao dịch thanh toán (giả lập)
    /// </summary>
    public class Payment
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("bookingId")]
        public int BookingId { get; set; }
        
        [JsonPropertyName("userId")]
        public int UserId { get; set; }
        
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }
        
        [JsonPropertyName("paymentMethod")]
        public string PaymentMethod { get; set; } = string.Empty;
        
        [JsonPropertyName("transactionId")]
        public string? TransactionId { get; set; }
        
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        
        [JsonPropertyName("paidAt")]
        public DateTime? PaidAt { get; set; }
        
        [JsonPropertyName("qrCodeData")]
        public string? QrCodeData { get; set; }
        
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }
}
