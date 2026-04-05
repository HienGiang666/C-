using Microsoft.EntityFrameworkCore;
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

// Bật tính năng OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Cấu hình Database của bạn
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Hiển thị trang web Swagger khi đang code (Development)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- 2. KÍCH HOẠT CỔNG CORS (Bắt buộc phải đặt trước Authorization) ---
app.UseCors("AllowAll");
// ----------------------------------------------------------------------

// app.UseHttpsRedirection(); // [DISABLED] Điện thoại kết nối HTTP, redirect HTTPS sẽ fail
app.UseAuthorization();
app.MapControllers();
app.Run();