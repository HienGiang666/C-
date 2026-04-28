using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TourApp.API.Data;
using TourApp.API.Services;
using TourApp.API.Hubs;

var builder = WebApplication.CreateBuilder(args);

// === CẤU HÌNH JWT AUTHENTICATION ===
var jwtKey = builder.Configuration["Jwt:Key"] ?? "TourApp-Secret-Key-For-Development-Only-Change-In-Production";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TourApp";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TourApp.Mobile";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
        
        // Hỗ trợ cả query string cho SignalR
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// --- 1. MỞ CỔNG CORS (Cho phép các App/Web khác gọi vào) ---
// Lưu ý: SignalR yêu cầu AllowCredentials nên không dùng AllowAnyOrigin()
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)  // Cho phép tất cả origins
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();  // Bắt buộc cho SignalR
    });
});
// -----------------------------------------------------------

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// === SIGNALR - Real-time tracking ===
builder.Services.AddSignalR();

// Đăng ký IHttpContextAccessor (dùng trong Controllers)
builder.Services.AddHttpContextAccessor();

// Bật tính năng Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Đăng ký BusinessKeyService (Scoped để dùng DbContext)
builder.Services.AddScoped<BusinessKeyService>();

// Cấu hình Database — hỗ trợ cả SQL Server và PostgreSQL
// Tự động chọn provider dựa vào connection string
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    // Kiểm tra nếu connection string là PostgreSQL (chứa các từ khóa PostgreSQL)
    if (connectionString != null && 
        (connectionString.Contains("Host=") || connectionString.Contains("Server=postgres") || 
         connectionString.Contains("Database=postgres") || connectionString.Contains("postgresql://")))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        // Mặc định SQL Server cho local development
        options.UseSqlServer(connectionString);
    }
    
    options.ConfigureWarnings(w =>
        w.Ignore(RelationalEventId.PendingModelChangesWarning));
});

var app = builder.Build();

// Tự động Seed dữ liệu — BỌC TRY/CATCH để không crash khi DB chưa sẵn sàng
try
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    // 1. CHẠY MIGRATION TRƯỚC ĐỂ ĐẢM BẢO CẤU TRÚC DB SẴN SÀNG
    try
    {
        Console.WriteLine("[System] Applying migrations...");
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Migrate] {ex.Message}");
    }

    // 2. SAU ĐÓ MỚI SEED DỮ LIỆU
    Console.WriteLine("[System] Auto-resetting Admin credentials...");
    DbSeeder.Seed(context); 

    DbSeeder.ApplySchemaPatches(context);
    DbSeeder.EnsureBusinessKeyCodes(context);
    DbSeeder.EnsureBusinessKeyCodes(context); // tour/booking seed có Code 02= null → gán TR-/BK-
    DbSeeder.AssignPoiOwnersCuongHien(context);
}
catch (Exception ex)
{
    // Log lỗi nhưng KHÔNG crash — API vẫn chạy, endpoint vẫn hoạt động
    Console.WriteLine($"[DbSeeder] WARNING: {ex.Message}");
}

// Hiển thị trang web Swagger khi đang code (Development)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- 2. KÍCH HOẠT CỔNG CORS (Bắt buộc phải đặt trước Authorization) ---
app.UseCors("AllowAll");
// ----------------------------------------------------------------------

// [DISABLED] Phone kết nối qua HTTP → nếu redirect sang HTTPS sẽ fail
// app.UseHttpsRedirection();

// Serve ảnh từ CMS wwwroot/uploads (cho mobile app tải ảnh qua API URL)
var cmsUploadsPath = Path.Combine(app.Environment.ContentRootPath, "..", "TourApp.CMS", "wwwroot", "uploads");
if (Directory.Exists(cmsUploadsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.GetFullPath(cmsUploadsPath)),
        RequestPath = "/uploads"
    });
    Console.WriteLine($"[Static Files] Serving uploads from: {Path.GetFullPath(cmsUploadsPath)}");
}
else
{
    Console.WriteLine($"[Static Files] WARNING: CMS uploads not found at {cmsUploadsPath}");
}

// === THỨ TỰ QUAN TRỌNG: Authentication TRƯỚC Authorization ===
app.UseAuthentication();
app.UseAuthorization();
// =========================================================

// === SIGNALR Hub endpoint ===
app.MapHub<UserLocationHub>("/hubs/userlocation");

app.MapControllers();
app.Run();