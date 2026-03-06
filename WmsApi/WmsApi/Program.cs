using Microsoft.EntityFrameworkCore;
using WmsApi.Data;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "WMS API", Version = "v1" });
});

// ── SQL Server ────────────────────────────────
builder.Services.AddDbContext<WmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// ── CORS (Flutter) ────────────────────────────
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p => p
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader()));

var app = builder.Build();

// ── Middleware ────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.Urls.Add("http://0.0.0.0:5000");

app.Run();