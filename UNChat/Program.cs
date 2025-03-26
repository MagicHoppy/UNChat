using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using UNChat.Models;

var builder = WebApplication.CreateBuilder(args);

// Konfiguracja bazy danych
builder.Services.AddDbContext<UNChatDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("UNChatDB")));

builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UNChatDBContext>();

    if (!dbContext.Users.Any()) // Sprawdzamy, czy tabela Users jest pusta
    {
        dbContext.Users.AddRange(new List<User>
        {
            new User { Username = "Alice", PasswordHash = "hashed_password1" },
            new User { Username = "Bob", PasswordHash = "hashed_password2" }
        });

        dbContext.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();


