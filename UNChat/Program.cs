using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UNChat.Context;
using UNChat.Hubs;
using UNChat.Models;
using Microsoft.AspNetCore.Authentication.Google;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();

builder.Services.AddDbContext<UNChatDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("UNChatDb")));

builder.Services.AddIdentity<User, IdentityRole>()
    .AddEntityFrameworkStores<UNChatDbContext>()
    .AddDefaultTokenProviders();
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5); // czas blokady
    options.Lockout.MaxFailedAccessAttempts = 3; // próby do zablokowania
    options.Lockout.AllowedForNewUsers = true; // blokowanie nowych kont
});

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login"; // Œcie¿ka do logowania
    options.AccessDeniedPath = "/Account/Login"; // Œcie¿ka, gdy brak dostêpu
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60); // Czas trwania sesji
    options.SlidingExpiration = true; // Odnawianie ciasteczka przy aktywnoœci
});

builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
        options.CallbackPath = "/signin-google";
    });


builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.Utc;
    });

//builder.Services.AddAuthorization(options =>
//{
//    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin")); // Przyk³adowa polityka
//});


var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<UNChatDbContext>();
    DbInitializer.Initialize(context);
}


if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<ChatHub>("/chatHub");

app.MapControllerRoute(
    name: "account",
    pattern: "{controller=Account}/{action=Login}/{id?}");


app.Run();
