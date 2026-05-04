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
    [AllowAnonymous]
    public async Task<ActionResult> VerifyQrPayment([FromBody] QrPaymentRequest request)
    {
        var authUserId = GetCurrentUserId();

        // 1. Tìm booking
        var booking = await _context.Bookings
            .Include(b => b.Tour)
            .FirstOrDefaultAsync(b => b.Id == request.BookingId);

        if (booking == null)
            return NotFound(new { message = "Không tìm thấy booking" });

        // Nếu user đã đăng nhập thì kiểm tra quyền; guest thì bỏ qua
        if (authUserId.HasValue && booking.UserId != authUserId.Value)
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
    /// POST /api/payment/create
    /// Tạo URL thanh toán cho VNPay/Momo/QR
    /// </summary>
    [HttpPost("create")]
    [Authorize]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        var authUserId = GetCurrentUserId();
        if (authUserId == null)
            return Unauthorized(new { message = "Vui lòng đăng nhập" });

        var booking = await _context.Bookings
            .Include(b => b.Tour)
            .FirstOrDefaultAsync(b => b.Id == request.BookingId);

        if (booking == null)
            return NotFound(new { message = "Không tìm thấy booking" });

        if (booking.UserId != authUserId)
            return Forbid();

        if (booking.Status != "Pending")
            return BadRequest(new { message = "Booking này không thể thanh toán" });

        var existingPayment = await _context.Payments
            .FirstOrDefaultAsync(p => p.BookingId == booking.Id && p.Status == "Success");
        if (existingPayment != null)
            return BadRequest(new { message = "Booking này đã được thanh toán" });

        // Tạo payment record với status Pending
        var payment = new Payment
        {
            BookingId = booking.Id,
            UserId = booking.UserId,
            Amount = booking.TotalPrice,
            PaymentMethod = request.Method, // VNPAY, MOMO, QR
            Status = "Pending",
            TransactionId = $"{request.Method}_{Guid.NewGuid():N}",
            CreatedAt = DateTime.Now
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        // Tạo URL thanh toán sandbox
        string paymentUrl;
        var returnUrl = $"{Request.Scheme}://{Request.Host}/api/payment/callback/{request.Method.ToLower()}";

        switch (request.Method?.ToUpper())
        {
            case "VNPAY":
                paymentUrl = CreateVnPaySandboxUrl(booking, payment.TransactionId, returnUrl);
                break;
            case "MOMO":
                paymentUrl = CreateMomoSandboxUrl(booking, payment.TransactionId, returnUrl);
                break;
            case "QR":
                paymentUrl = $"{Request.Scheme}://{Request.Host}/api/payment/callback/qr?bookingId={booking.Id}&txn={payment.TransactionId}";
                break;
            default:
                return BadRequest(new { message = "Phương thức thanh toán không hợp lệ" });
        }

        return Ok(new
        {
            paymentId = payment.Id,
            transactionId = payment.TransactionId,
            amount = payment.Amount,
            method = request.Method,
            paymentUrl,
            returnUrl
        });
    }

    /// <summary>
    /// GET /api/payment/callback/vnpay
    /// Callback giả lập VNPay Sandbox
    /// </summary>
    [HttpGet("callback/vnpay")]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayCallback(
        [FromQuery] string txn,
        [FromQuery] bool success = true)
    {
        return await ProcessCallbackAsync(txn, "VNPAY", success);
    }

    /// <summary>
    /// GET /api/payment/callback/momo
    /// Callback giả lập Momo Sandbox
    /// </summary>
    [HttpGet("callback/momo")]
    [AllowAnonymous]
    public async Task<IActionResult> MomoCallback(
        [FromQuery] string txn,
        [FromQuery] bool success = true)
    {
        return await ProcessCallbackAsync(txn, "MOMO", success);
    }

    /// <summary>
    /// GET /api/payment/callback/qr
    /// Callback giả lập QR
    /// </summary>
    [HttpGet("callback/qr")]
    [AllowAnonymous]
    public async Task<IActionResult> QrCallback(
        [FromQuery] int bookingId,
        [FromQuery] string txn)
    {
        return await ProcessCallbackAsync(txn, "QR", true);
    }

    private async Task<IActionResult> ProcessCallbackAsync(string txn, string method, bool success)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.TransactionId == txn);

        if (payment == null)
            return NotFound(new { message = "Không tìm thấy giao dịch" });

        var booking = await _context.Bookings.FindAsync(payment.BookingId);
        if (booking == null)
            return NotFound(new { message = "Không tìm thấy booking" });

        if (success)
        {
            payment.Status = "Success";
            payment.PaidAt = DateTime.Now;
            booking.Status = "Paid";
            booking.PaymentMethod = method;
            booking.TransactionId = payment.TransactionId;
            booking.PaidAt = DateTime.Now;
        }
        else
        {
            payment.Status = "Failed";
        }

        await _context.SaveChangesAsync();

        // Trả về JSON cho mobile xử lý inline, hoặc redirect nếu từ browser
        if (Request.Headers.ContainsKey("X-Requested-With") ||
            Request.Headers["Accept"].ToString().Contains("application/json"))
        {
            return Ok(new
            {
                success,
                transactionId = txn,
                amount = payment.Amount,
                message = success ? "Thanh toán thành công" : "Thanh toán thất bại"
            });
        }

        // Redirect về app deep link hoặc trang kết quả
        return Redirect($"/payment-result?success={success}&txn={txn}&amount={payment.Amount}");
    }

    private string CreateVnPaySandboxUrl(Booking booking, string txnId, string returnUrl)
    {
        // Sandbox URL giả lập - trong thực tế sẽ gọi VNPay API
        var url = $"{returnUrl}?txn={Uri.EscapeDataString(txnId)}&success=true";
        // Trả về trang giả lập VNPay (có thể mở trong WebView)
        return url;
    }

    private string CreateMomoSandboxUrl(Booking booking, string txnId, string returnUrl)
    {
        var url = $"{returnUrl}?txn={Uri.EscapeDataString(txnId)}&success=true";
        return url;
    }

    /// <summary>
    /// POST /api/payment/{bookingId}/cancel
    /// Hủy booking và hoàn tiền giả lập
    /// </summary>
    [HttpPost("{bookingId}/cancel")]
    [AllowAnonymous]
    public async Task<ActionResult> CancelBooking(int bookingId, [FromBody] CancelRequest request)
    {
        var authUserId = GetCurrentUserId();

        var booking = await _context.Bookings.FindAsync(bookingId);
        if (booking == null)
            return NotFound();

        // Nếu user đã đăng nhập thì kiểm tra quyền; guest thì bỏ qua
        if (authUserId.HasValue && booking.UserId != authUserId.Value)
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
/// Request model tạo thanh toán
/// </summary>
public class CreatePaymentRequest
{
    public int BookingId { get; set; }
    public string? Method { get; set; } // VNPAY, MOMO, QR
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
