using TourApp.CMS.Filters;
using TourApp.CMS.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<AuthFilter>();
    options.Filters.Add<CmsAccessFilter>();
});

builder.Services.AddHttpClient("TourApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5254/");
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();

// Register application services
builder.Services.AddSingleton<IActivityLogger, ActivityLogger>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddSingleton<ILanguageSettingsService, LanguageSettingsService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();       // Phải trước UseAuthorization
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
