using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TourApp.API.Data;
using TourApp.API.Models;

namespace TourApp.API.Controllers;

/// <summary>
/// Controller xử lý thanh toán QR giả lập
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class PaymentController : ControllerBase
{
    private readonly AppDbContext _context;

    public PaymentController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET /api/payment/user/{userId}
    /// Lấy lịch sử thanh toán của user
    /// </summary>
    [HttpGet("user/{userId}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<Payment>>> GetUserPayments(int userId)
    {
        // Kiểm tra quyền
        var authUserId = GetCurrentUserId();
        if (authUserId == null || authUserId != userId)
            return Forbid();

        var payments = await _context.Payments
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return Ok(payments);
    }

    /// <summary>
    /// GET /api/payment/admin/all
    /// Admin xem tất cả lịch sử thanh toán
    /// </summary>
    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<Payment>>> GetAllPayments()
    {
        var payments = await _context.Payments
            .Include(p => p.Booking)
            .ThenInclude(b => b.Tour)
            .Include(p => p.User)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return Ok(payments);
    }

    /// <summary>
    /// GET /api/payment/booking/{bookingId}
    /// Lấy thông tin thanh toán của booking
    /// </summary>
    [HttpGet("booking/{bookingId}")]
    [Authorize]
    public async Task<ActionResult<Payment>> GetPaymentByBooking(int bookingId)
    {
        var booking = await _context.Bookings.FindAsync(bookingId);
        if (booking == null)
            return NotFound(new { message = "Không tìm thấy booking" });

        var authUserId = GetCurrentUserId();
        if (authUserId == null || authUserId != booking.UserId)
            return Forbid();

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.BookingId == bookingId);

        if (payment == null)
            return NotFound(new { message = "Chưa có thanh toán cho booking này" });

        return Ok(payment);
    }

    /// <summary>
    /// POST /api/payment/verify-qr
    /// Xử lý thanh toán QR giả lập
    /// </summary>
    [HttpPost("verify-qr")]
    [Authorize]
    public async Task<ActionResult> VerifyQrPayment([FromBody] QrPaymentRequest request)
    {
        var authUserId = GetCurrentUserId();
        if (authUserId == null)
            return Unauthorized(new { message = "Vui lòng đăng nhập" });

        // 1. Tìm booking
        var booking = await _context.Bookings
            .Include(b => b.Tour)
            .FirstOrDefaultAsync(b => b.Id == request.BookingId);

        if (booking == null)
            return NotFound(new { message = "Không tìm thấy booking" });

        if (booking.UserId != authUserId)
            return Forbid();

        if (booking.Status != "Pending")
            return BadRequest(new { message = "Booking này không thể thanh toán" });

        // 2. Kiểm tra xem đã có payment chưa
        var existingPayment = await _context.Payments
            .FirstOrDefaultAsync(p => p.BookingId == booking.Id);

        if (existingPayment != null && existingPayment.Status == "Success")
            return BadRequest(new { message = "Booking này đã được thanh toán" });

        // 3. Tạo payment giả lập
        var payment = new Payment
        {
            BookingId = booking.Id,
            UserId = booking.UserId,
            Amount = booking.TotalPrice,
            PaymentMethod = "QR",
            TransactionId = $"SIM_QR_{Guid.NewGuid():N}",
            Status = "Success",
            PaidAt = DateTime.Now,
            QrCodeData = request.QrData,
            CreatedAt = DateTime.Now
        };

        _context.Payments.Add(payment);

        // 4. Update booking
        booking.Status = "Paid";
        booking.PaymentMethod = "QR";
        booking.TransactionId = payment.TransactionId;
        booking.PaidAt = DateTime.Now;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Thanh toán thành công!",
            transactionId = payment.TransactionId,
            amount = payment.Amount,
            paidAt = payment.PaidAt
        });
    }

    /// <summary>
    /// POST /api/payment/{bookingId}/cancel
    /// Hủy booking và hoàn tiền giả lập
    /// </summary>
    [HttpPost("{bookingId}/cancel")]
    [Authorize]
    public async Task<ActionResult> CancelBooking(int bookingId, [FromBody] CancelRequest request)
    {
        var authUserId = GetCurrentUserId();
        if (authUserId == null)
            return Unauthorized();

        var booking = await _context.Bookings.FindAsync(bookingId);
        if (booking == null)
            return NotFound();

        if (booking.UserId != authUserId)
            return Forbid();

        // Chỉ cho hủy nếu chưa hoàn thành tour
        if (booking.Status == "Completed")
            return BadRequest(new { message = "Tour đã hoàn thành, không thể hủy" });

        // Update booking
        booking.Status = "Cancelled";
        booking.CancelledAt = DateTime.Now;
        booking.CancelReason = request.Reason;

        // Update payment nếu có
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.BookingId == bookingId);
        if (payment != null)
        {
            payment.Status = "Refunded";
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã hủy booking thành công" });
    }

    #region Helper Methods

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return null;
        return userId;
    }

    #endregion
}

/// <summary>
/// Request model cho thanh toán QR
/// </summary>
public class QrPaymentRequest
{
    public int BookingId { get; set; }
    public string? QrData { get; set; }
}

/// <summary>
/// Request model cho hủy booking
/// </summary>
public class CancelRequest
{
    public string? Reason { get; set; }
}
