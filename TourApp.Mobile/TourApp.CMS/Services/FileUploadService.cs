namespace TourApp.CMS.Services
{
    public interface IFileUploadService
    {
        Task<string> UploadImageAsync(IFormFile file, string folder);
        Task<string> UploadAudioAsync(IFormFile file, string folder);
        bool DeleteImage(string imagePath);
    }

    public class FileUploadService : IFileUploadService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileUploadService> _logger;
        private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private readonly string[] _allowedAudioExtensions = { ".mp3", ".wav", ".m4a", ".aac" };
        private const long MaxImageSize = 5 * 1024 * 1024; // 5MB
        private const long MaxAudioSize = 20 * 1024 * 1024; // 20MB
        private const string CmsBaseUrl = "https://localhost:7031";

        public FileUploadService(IWebHostEnvironment environment, ILogger<FileUploadService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<string> UploadImageAsync(IFormFile file, string folder)
        {
            try
            {
                // Validate file
                if (file == null || file.Length == 0)
                    throw new ArgumentException("File không hợp lệ");

                if (file.Length > MaxImageSize)
                    throw new ArgumentException("Kích thước ảnh vượt quá 5MB");

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!_allowedImageExtensions.Contains(extension))
                    throw new ArgumentException("Định dạng file không được hỗ trợ");

                // Create directory if not exists
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", folder);
                Directory.CreateDirectory(uploadsFolder);

                // Generate unique filename
                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Upload file
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                _logger.LogInformation($"File uploaded: {filePath}");
                return CmsBaseUrl + Path.Combine("/uploads", folder, uniqueFileName).Replace("\\", "/");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading file: {ex.Message}");
                throw;
            }
        }

        public async Task<string> UploadAudioAsync(IFormFile file, string folder)
        {
            try
            {
                if (file == null || file.Length == 0)
                    throw new ArgumentException("File âm thanh không hợp lệ");

                if (file.Length > MaxAudioSize)
                    throw new ArgumentException("Kích thước file vượt quá 20MB");

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!_allowedAudioExtensions.Contains(extension))
                    throw new ArgumentException("Định dạng file không được hỗ trợ (chỉ nhận .mp3, .wav, .m4a, .aac)");

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", folder);
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                _logger.LogInformation($"Audio uploaded: {filePath}");
                return CmsBaseUrl + Path.Combine("/uploads", folder, uniqueFileName).Replace("\\", "/");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading audio: {ex.Message}");
                throw;
            }
        }

        public bool DeleteImage(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath))
                    return false;
                
                // Remove the base url when searching local path
                string relativePath = imagePath;
                if (imagePath.StartsWith(CmsBaseUrl))
                {
                    relativePath = imagePath.Substring(CmsBaseUrl.Length);
                }

                var fullPath = Path.Combine(_environment.WebRootPath, relativePath.TrimStart('/'));
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation($"File deleted: {fullPath}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting file: {ex.Message}");
                return false;
            }
        }
    }
}
