using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Config;
using MoneyBack.Api.Data;
using MoneyBack.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<SubsidiosOptions>(builder.Configuration.GetSection(SubsidiosOptions.SectionName));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapUsuariosEndpoints();
app.MapHogaresEndpoints();
app.MapMetasEndpoints();
app.MapSubsidiosEndpoints();

app.Run();
