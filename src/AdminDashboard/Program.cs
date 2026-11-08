namespace AdminDashboard;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

public class Program
{
    public static void Main(string[] args)
    {
        // Automatically load .env environment variables
        NigerianNewsGrid.Client.EnvLoader.Load();

        var builder = WebApplication.CreateBuilder(args);

        // Persist ASP.NET Core Data Protection & Antiforgery keys to a dedicated persistent path
        var dataProtectionDir = new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "data", "keys"));
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(dataProtectionDir)
            .SetApplicationName("NigerianNewsGrid.AdminDashboard");

        // Fix #7: Configure Cookie Authentication for Admin Portal
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Login";
                options.LogoutPath = "/Logout";
                options.AccessDeniedPath = "/Login";
                options.Cookie.Name = ".NigerianNewsGrid.AdminAuth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            });

        builder.Services.AddRazorPages(options =>
        {
            options.Conventions.AuthorizeFolder("/");
            options.Conventions.AllowAnonymousToPage("/Login");
            // Restrict sensitive operation and settings pages to Administrator role only
            options.Conventions.AuthorizePage("/Settings", "AdminOnly");
            options.Conventions.AuthorizePage("/SystemHealth", "AdminOnly");
            options.Conventions.AuthorizePage("/Sponsors", "AdminOnly");
            options.Conventions.AuthorizePage("/Categorizer", "AdminOnly");
            options.Conventions.AuthorizePage("/SearchConsole", "AdminOnly");
            options.Conventions.AuthorizePage("/Analytics", "AdminOnly");
        })
        .AddRazorRuntimeCompilation();

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Administrator"));
        });

        builder.Services.AddMemoryCache();
        builder.Services.AddControllers();
        builder.Services.AddHttpClient();
        builder.Services.AddHttpClient("NewsApiClient", (sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var apiKey = config["ApiKey"] ?? config["NewsApi:ApiKey"] ?? Environment.GetEnvironmentVariable("API_KEY");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            }
        });

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseStaticFiles();
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapRazorPages();
        app.MapControllers();

        app.Run();
    }
}



