namespace TourApp.CMS.Models
{
    /// <summary>
    /// Lưu trữ thông tin giao dịch thanh toán (giả lập)
    /// </summary>
    public class Payment
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;  // QR, Momo, CreditCard...
        public string? TransactionId { get; set; }                  // SIM_QR_xxx
        public string Status { get; set; } = "Success";            // Success, Failed, Pending
        public DateTime? PaidAt { get; set; }
        public string? QrCodeData { get; set; }                     // JSON data từ QR
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
