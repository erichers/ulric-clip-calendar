using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Ulric.ClipCalendar.Api.Data;
using Ulric.ClipCalendar.Api.Media;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 80_000_000;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 80_000_000;
});

builder.Services.AddAppDatabase(builder.Configuration);

var ffmpeg = FfmpegLocator.Resolve(builder.Configuration["Ffmpeg:Path"], builder.Configuration["Ffmpeg:ProbePath"]);
builder.Services.AddSingleton(ffmpeg);
builder.Services.AddSingleton<StorageLayout>();
builder.Services.AddSingleton<MediaQueue>();
builder.Services.AddScoped<MediaProcessor>();
builder.Services.AddHostedService<MediaWorker>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("studio", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
            ?? new[] { "http://localhost:4200" };
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

var pathBase = app.Configuration["PathBase"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

Directory.CreateDirectory(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"));
Directory.CreateDirectory(app.Services.GetRequiredService<StorageLayout>().Root);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    if (app.Configuration.GetValue("Seed:Enabled", true))
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");
        await DbSeeder.SeedAsync(
            db,
            scope.ServiceProvider.GetRequiredService<StorageLayout>(),
            app.Environment.ContentRootPath,
            logger);
    }

    await scope.ServiceProvider.GetRequiredService<MediaQueue>().RequeueOutstandingAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("studio");
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/swagger"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var index = Path.Combine(app.Environment.WebRootPath ?? "", "index.html");
    if (!File.Exists(index))
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync("<!doctype html><title>Ulric Clip Calendar</title><p>Ulric studio API is running. Start the Angular app with ng serve, or build it into wwwroot.</p>");
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(index);
});

app.Run();

public partial class Program;
