using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TourApp.API.Data;

var builder = WebApplication.CreateBuilder(args);

// --- 1. MỞ CỔNG CORS (Cho phép các App/Web khác gọi vào) ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
// -----------------------------------------------------------

builder.Services.AddControllers();

// Bật tính năng Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
    try
    {
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Migrate] {ex.Message}");
    }

    DbSeeder.ApplySchemaPatches(context);
    DbSeeder.EnsurePublicCatalogNumbers(context);
    DbSeeder.Seed(context);
    DbSeeder.EnsurePublicCatalogNumbers(context); // tour/booking seed có PublicCatalogNumber = 0 → gán TR-/BK-
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
app.UseSwaggerUI(c =>
// --- 2. KÍCH HOẠT CỔNG CORS (Bắt buộc phải đặt trước Authorization) ---
app.UseCors("AllowAll"));
// ----------------------------------------------------------------------

// [DISABLED] Phone kết nối qua HTTP → nếu redirect sang HTTPS sẽ fail
// app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();