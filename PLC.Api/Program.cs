using Microsoft.EntityFrameworkCore;
using PLC.Api.Data;
using PLC.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IPlcApiService, PlcApiService>();
builder.Services.AddSingleton<IPlcService, PlcService>();
builder.Services.AddSingleton<IDataBlockService, DataBlockService>();
builder.Services.AddScoped<IDataBlockApiService, DataBlockApiService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
