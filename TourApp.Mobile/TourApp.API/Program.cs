using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TourApp.API.Data;
using TourApp.API.Services;
using TourApp.API.Hubs;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddControllers();

// === SIGNALR - Real-time tracking ===
builder.Services.AddSignalR();

// Bật tính năng Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Đăng ký BusinessKeyService (Scoped để dùng DbContext)
builder.Services.AddScoped<BusinessKeyService>();

// Cấu hình Database — bỏ qua PendingModelChangesWarning khi model đã có cột (ApplySchemaPatches)
// nhưng chưa có file migration tương ứng, tránh crash tại Migrate().
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
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
    DbSeeder.EnsureBusinessKeyCodes(context); // tour/booking seed có Code = null → gán TR-/BK-
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
app.UseAuthorization();

// === SIGNALR Hub endpoint ===
app.MapHub<UserLocationHub>("/hubs/userlocation");

app.MapControllers();
app.Run();