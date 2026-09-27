using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Hangfire;
using NotificationService.Application.Jobs;
using NotificationService.Application.Validators;
using NotificationService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<NotificationRequestValidator>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

RecurringJob.AddOrUpdate<RetryFailedNotificationsJob>(
    "retry-failed-notifications",
    job => job.Execute(),
    Cron.Hourly);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHangfireDashboard();
app.UseCors("AllowAll");
app.MapControllers();

app.Run();
