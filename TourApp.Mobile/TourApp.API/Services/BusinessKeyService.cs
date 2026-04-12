using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TourApp.API.Data;

namespace TourApp.API.Services;

/// <summary>
/// Service để sinh mã Business Key (Code) tự động.
/// Mã được sinh dựa trên prefix + số thứ tự tăng dần.
/// </summary>
public class BusinessKeyService
{
    private readonly AppDbContext _context;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public BusinessKeyService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>Sinh mã Code mới cho POI (format: #P{n})</summary>
    public async Task<string> GeneratePOICodeAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            // Lấy số lớn nhất từ các Code hiện có
            var maxNumber = await _context.POIs
                .Select(p => p.Code)
                .ToListAsync();
            
            var nextNumber = ExtractMaxNumber(maxNumber, "#P") + 1;
            return $"#P{nextNumber}";
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>Sinh mã Code mới cho Tour (format: TR-{n})</summary>
    public async Task<string> GenerateTourCodeAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var maxNumber = await _context.Tours
                .Select(t => t.Code)
                .ToListAsync();
            
            var nextNumber = ExtractMaxNumber(maxNumber, "TR-") + 1;
            return $"TR-{nextNumber}";
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>Sinh mã Code mới cho Booking (format: BK-{n})</summary>
    public async Task<string> GenerateBookingCodeAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var maxNumber = await _context.Bookings
                .Select(b => b.Code)
                .ToListAsync();
            
            var nextNumber = ExtractMaxNumber(maxNumber, "BK-") + 1;
            return $"BK-{nextNumber}";
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>Sinh mã Code mới cho User (format: #U{n})</summary>
    public async Task<string> GenerateUserCodeAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var maxNumber = await _context.Users
                .Select(u => u.Code)
                .ToListAsync();
            
            var nextNumber = ExtractMaxNumber(maxNumber, "#U") + 1;
            return $"#U{nextNumber}";
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>Sinh mã Code bằng SQL SEQUENCE (tối ưu hiệu năng, tránh race condition)</summary>
    public async Task<string> GenerateCodeWithSequenceAsync(string entityType, string prefix)
    {
        var sequenceName = $"SQ_{entityType}_Code";
        
        // Kiểm tra sequence tồn tại
        var checkSql = $@"
            IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = '{sequenceName}')
            BEGIN
                CREATE SEQUENCE {sequenceName} START WITH 1 INCREMENT BY 1;
            END";
        
        await _context.Database.ExecuteSqlRawAsync(checkSql);
        
        // Lấy giá trị tiếp theo
        var nextValueSql = $"SELECT NEXT VALUE FOR {sequenceName}";
        var result = await _context.Database
            .SqlQueryRaw<int>(nextValueSql)
            .FirstAsync();
        
        return $"{prefix}{result}";
    }

    /// <summary>Trích xuất số lớn nhất từ danh sách Code</summary>
    private static int ExtractMaxNumber(List<string> codes, string prefix)
    {
        var max = 0;
        foreach (var code in codes)
        {
            if (string.IsNullOrEmpty(code)) continue;
            
            if (code.StartsWith(prefix) && int.TryParse(code[prefix.Length..], out var num))
            {
                if (num > max) max = num;
            }
        }
        return max;
    }
}
