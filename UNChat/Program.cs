using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UNChat.Context;
using UNChat.Models;



var builder = WebApplication.CreateBuilder(args);

// Pobranie konfiguracji
var configuration = builder.Configuration;

// Rejestracja us³ug
builder.Services.AddDbContext<UNChatDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("UNChatDb")));

builder.Services.AddIdentity<User, IdentityRole>()
    .AddEntityFrameworkStores<UNChatDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Konfiguracja potoku przetwarzania ¿¹dañ
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

app.Run();
