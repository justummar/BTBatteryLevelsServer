using BTBatteryLevelsService;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<BluetoothSensorWorker>(); // Register the worker as a singleton
builder.Services.AddHostedService<BluetoothSensorWorker>(); // Add the worker as a hosted service
builder.Services.AddSignalR();
var app = builder.Build();

app.MapHub<SensorHub>("/SensorHub");

app.Run();