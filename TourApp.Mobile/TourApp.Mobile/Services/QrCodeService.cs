using System.Security.Cryptography;
using System.Text;

namespace TourApp.Mobile.Services;

/// <summary>
/// Service tạo và xử lý mã QR
/// </summary>
public static class QrCodeService
{
    private const string SecretKey = "TourApp-QR-Secret-2026";

    /// <summary>
    /// Tạo ImageSource cho QR code từ chuỗi data
    /// </summary>
    public static ImageSource? GenerateQrCodeImageSource(string data, int size = 200)
    {
        try
        {
            // Create QR-like BMP image
            var bmpBytes = CreatePlaceholderPng(size);
            
            // Convert to ImageSource
            return ImageSource.FromStream(() => new MemoryStream(bmpBytes));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QrCodeService] Error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Tạo dữ liệu QR cho thanh toán
    /// </summary>
    public static string CreatePaymentQrData(int bookingId, decimal amount, string tourName)
    {
        var payload = new
        {
            type = "payment",
            bookingId,
            amount,
            tourName,
            timestamp = DateTime.UtcNow.Ticks,
            expiry = DateTime.UtcNow.AddMinutes(15).Ticks
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var checksum = GenerateChecksum(json);
        
        return $"{json}|{checksum}";
    }

    /// <summary>
    /// Verify checksum của QR data
    /// </summary>
    public static bool VerifyQrData(string qrData)
    {
        try
        {
            var parts = qrData.Split('|');
            if (parts.Length != 2) return false;

            var json = parts[0];
            var checksum = parts[1];
            var expectedChecksum = GenerateChecksum(json);

            return checksum == expectedChecksum;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Extract booking ID từ QR data
    /// </summary>
    public static int? ExtractBookingId(string qrData)
    {
        try
        {
            if (!VerifyQrData(qrData)) return null;

            var json = qrData.Split('|')[0];
            var payload = System.Text.Json.JsonSerializer.Deserialize<PaymentQrPayload>(json);
            
            if (payload?.type != "payment") return null;
            if (payload.expiry < DateTime.UtcNow.Ticks) return null; // Expired
            
            return payload.bookingId;
        }
        catch
        {
            return null;
        }
    }

    #region Private Methods

    private static string GenerateChecksum(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input + SecretKey);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash)[..16]; // Lấy 16 ký tự đầu
    }

    private static bool[,] GenerateSimpleQrMatrix(string data, int size)
    {
        // Tạo pattern dựa trên hash của data
        var matrix = new bool[size, size];
        var hash = GenerateChecksum(data);
        var random = new Random(hash.GetHashCode());

        // Tạo pattern 3 ô vuông ở 3 góc (giống QR code thật)
        DrawPositionPattern(matrix, 0, 0, size);
        DrawPositionPattern(matrix, size - 7, 0, size);
        DrawPositionPattern(matrix, 0, size - 7, size);

        // Fill data pattern
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (matrix[x, y]) continue; // Skip position patterns
                
                // Tạo pattern ngẫu nhiên dựa trên data
                matrix[x, y] = random.NextDouble() > 0.5;
            }
        }

        return matrix;
    }

    private static void DrawPositionPattern(bool[,] matrix, int startX, int startY, int size)
    {
        int patternSize = 7;
        
        for (int y = 0; y < patternSize; y++)
        {
            for (int x = 0; x < patternSize; x++)
            {
                int px = startX + x;
                int py = startY + y;
                
                if (px >= size || py >= size) continue;

                // Outer square (black)
                if (x == 0 || x == patternSize - 1 || y == 0 || y == patternSize - 1)
                {
                    matrix[px, py] = true;
                }
                // Middle white square
                else if (x == 1 || x == patternSize - 2 || y == 1 || y == patternSize - 2)
                {
                    matrix[px, py] = false;
                }
                // Inner black square
                else
                {
                    matrix[px, py] = true;
                }
            }
        }
    }

    private static byte[] CreatePlaceholderPng(int size)
    {
        // Return a simple gray square as placeholder
        // In production, use a proper QR library
        var color = new byte[] { 0xCC, 0xCC, 0xCC, 0xFF }; // Gray
        var pixelData = new byte[size * size * 4];
        
        for (int i = 0; i < pixelData.Length; i += 4)
        {
            pixelData[i] = color[0];     // R
            pixelData[i + 1] = color[1]; // G
            pixelData[i + 2] = color[2]; // B
            pixelData[i + 3] = color[3]; // A
        }

        // Add a border pattern to simulate QR code
        var random = new Random(12345);
        for (int y = 10; y < size - 10; y++)
        {
            for (int x = 10; x < size - 10; x++)
            {
                if (random.NextDouble() > 0.5)
                {
                    int idx = (y * size + x) * 4;
                    if (idx + 3 < pixelData.Length)
                    {
                        pixelData[idx] = 0;     // R
                        pixelData[idx + 1] = 0; // G
                        pixelData[idx + 2] = 0; // B
                    }
                }
            }
        }

        // Draw position patterns (3 corners like real QR)
        DrawSquare(pixelData, size, 10, 10, 20, new byte[] { 0, 0, 0, 255 });
        DrawSquare(pixelData, size, size - 30, 10, 20, new byte[] { 0, 0, 0, 255 });
        DrawSquare(pixelData, size, 10, size - 30, 20, new byte[] { 0, 0, 0, 255 });

        // Create simple BMP header + data (easier than PNG for now)
        return CreateBmpImage(pixelData, size, size);
    }

    private static void DrawSquare(byte[] pixelData, int imgSize, int x, int y, int size, byte[] color)
    {
        for (int dy = 0; dy < size; dy++)
        {
            for (int dx = 0; dx < size; dx++)
            {
                int px = x + dx;
                int py = y + dy;
                if (px < imgSize && py < imgSize)
                {
                    int idx = (py * imgSize + px) * 4;
                    if (idx + 3 < pixelData.Length)
                    {
                        pixelData[idx] = color[0];
                        pixelData[idx + 1] = color[1];
                        pixelData[idx + 2] = color[2];
                        pixelData[idx + 3] = color[3];
                    }
                }
            }
        }
    }

    /// <summary>
    /// Create a simple BMP image from RGBA pixel data
    /// </summary>
    private static byte[] CreateBmpImage(byte[] rgbaData, int width, int height)
    {
        int rowSize = ((width * 3 + 3) / 4) * 4; // 24-bit BMP, padded to 4 bytes
        int imageSize = rowSize * height;
        int fileSize = 54 + imageSize;

        var bmp = new byte[fileSize];
        int offset = 0;

        // BMP Header (14 bytes)
        bmp[offset++] = (byte)'B';
        bmp[offset++] = (byte)'M';
        WriteInt32(bmp, ref offset, fileSize);
        WriteInt32(bmp, ref offset, 0); // Reserved
        WriteInt32(bmp, ref offset, 54); // Offset to pixel data

        // DIB Header (BITMAPINFOHEADER - 40 bytes)
        WriteInt32(bmp, ref offset, 40); // Header size
        WriteInt32(bmp, ref offset, width);
        WriteInt32(bmp, ref offset, height);
        WriteInt16(bmp, ref offset, 1); // Planes
        WriteInt16(bmp, ref offset, 24); // Bits per pixel
        WriteInt32(bmp, ref offset, 0); // Compression (none)
        WriteInt32(bmp, ref offset, imageSize);
        WriteInt32(bmp, ref offset, 2835); // X pixels per meter
        WriteInt32(bmp, ref offset, 2835); // Y pixels per meter
        WriteInt32(bmp, ref offset, 0); // Colors in palette
        WriteInt32(bmp, ref offset, 0); // Important colors

        // Pixel data (BGR format, bottom-up)
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                int srcIdx = (y * width + x) * 4;
                if (srcIdx + 2 < rgbaData.Length)
                {
                    bmp[offset++] = rgbaData[srcIdx + 2]; // B
                    bmp[offset++] = rgbaData[srcIdx + 1]; // G
                    bmp[offset++] = rgbaData[srcIdx];     // R
                }
                else
                {
                    bmp[offset++] = 255;
                    bmp[offset++] = 255;
                    bmp[offset++] = 255;
                }
            }
            // Row padding
            while ((offset - 54) % 4 != 0)
            {
                bmp[offset++] = 0;
            }
        }

        return bmp;
    }

    private static void WriteInt16(byte[] buffer, ref int offset, short value)
    {
        buffer[offset++] = (byte)(value & 0xFF);
        buffer[offset++] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteInt32(byte[] buffer, ref int offset, int value)
    {
        buffer[offset++] = (byte)(value & 0xFF);
        buffer[offset++] = (byte)((value >> 8) & 0xFF);
        buffer[offset++] = (byte)((value >> 16) & 0xFF);
        buffer[offset++] = (byte)((value >> 24) & 0xFF);
    }

    private class PaymentQrPayload
    {
        public string type { get; set; } = "";
        public int bookingId { get; set; }
        public decimal amount { get; set; }
        public string tourName { get; set; } = "";
        public long timestamp { get; set; }
        public long expiry { get; set; }
    }

    #endregion
}
