using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.Hubs;
using WmsApi.Services.Receiving;
using WmsApi.Services.Unload;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "WMS API", Version = "v1" });
});

// ── SQL Server ────────────────────────────────
builder.Services.AddDbContext<WmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<IReceivingService, ReceivingService>();
builder.Services.AddScoped<IUnloadService, UnloadService>();

// ── SignalR ───────────────────────────────────
builder.Services.AddSignalR();

// ── CORS (Flutter + SignalR) ──────────────────
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p => p
        .SetIsOriginAllowed(_ => true)
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()));

var app = builder.Build();

// ── Middleware ────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseStaticFiles();   // serve wwwroot/ (รูปภาพ, ไฟล์ static)
app.UseAuthorization();
app.MapControllers();
app.MapHub<PutawayHub>("/hubs/putaway");
app.Urls.Add("http://0.0.0.0:5000");

app.Run();
