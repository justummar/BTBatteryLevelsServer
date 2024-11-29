using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using InTheHand.Bluetooth;
using Microsoft.AspNetCore.SignalR;

namespace BTBatteryLevelsService;

public class BluetoothSensorWorker : Hub, IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly List<string> _listOfDevicesBeingSearched;
    private static Dictionary<string, float>? _deviceBatteryLevel;
    public IHubContext<SensorHub, ISensorClient> _SensorHubContext { get; }

    public BluetoothSensorWorker(IConfiguration configuration, IHubContext<SensorHub, ISensorClient> sensorHubContext)
    {
        _configuration = configuration;
        _SensorHubContext = sensorHubContext;
        _listOfDevicesBeingSearched = InitializeDeviceList(_configuration["Devices"]);
        _deviceBatteryLevel = new Dictionary<string, float>();
    }

    public async Task<Task> StartAsync(CancellationToken cancellationToken)
    {
        // Start the Bluetooth service
        StartBluetoothService();
        System.Threading.Thread.Sleep(2000);
        await SendBatteryLevelToClients(_deviceBatteryLevel);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Implementation for stopping the service
        return Task.CompletedTask;
    }

    private List<string> InitializeDeviceList(string? devicesString)
    {
        if (string.IsNullOrEmpty(devicesString))
        {
            throw new ArgumentException("Devices configuration is missing or empty.");
        }

        return devicesString.Split(',')
            .Select(device => device.Trim())
            .ToList();
    }

    public void StartBluetoothService()
    {
        foreach (var deviceName in _listOfDevicesBeingSearched)
        {
            var gattServer = ListBluetoothDevicesAsync(deviceName).ConfigureAwait(false).GetAwaiter().GetResult();

            if (gattServer == null)
            {
                Debug.WriteLine($"Could not get info for {deviceName}");
                continue;
            }

            var batteryLevelString = GetBatteryLevel(gattServer).ConfigureAwait(false).GetAwaiter().GetResult();
            Debug.WriteLine($"Initial battery level of {deviceName}: {batteryLevelString}");
        }
    }

    public static async Task<RemoteGattServer?> ListBluetoothDevicesAsync(string deviceName)
    {
        try
        {
            Debug.WriteLine("Requesting Bluetooth Device...");

            // Log the DBus address
            var dbusAddress = GetDbusAddress();
            Debug.WriteLine($"DBus Address: {dbusAddress}");

            var devices = await Bluetooth.GetPairedDevicesAsync().ConfigureAwait(false);
            var device = devices.FirstOrDefault(d => d.Name.Trim() == deviceName.Trim());

            if (device == null) return null;

            var gatt = device.Gatt;

            Debug.WriteLine("Connecting to GATT Server...");
            await gatt.ConnectAsync().ConfigureAwait(false);

            return gatt;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error listing Bluetooth devices: {ex.Message}");
            return null;
        }
    }

    private static string GetDbusAddress()
    {
        // Retrieve the DBus address from the environment or configuration
        var dbusAddress = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS");
        return dbusAddress ?? "DBus address not found";
    }

    public async Task<string> GetBatteryLevel(RemoteGattServer gatt)
    {
        try
        {
            Debug.WriteLine("Getting Battery Service...");
            var service = await gatt.GetPrimaryServiceAsync(GattServiceUuids.Battery).ConfigureAwait(false);

            Debug.WriteLine("Getting Battery Level Characteristic...");
            var characteristic = await service.GetCharacteristicAsync(BluetoothUuid.GetCharacteristic("battery_level")).ConfigureAwait(false);

            Debug.WriteLine("Reading Battery Level...");
            var value = await characteristic.ReadValueAsync().ConfigureAwait(false);

            _deviceBatteryLevel[gatt.Device.Name] = value[0];

            characteristic.CharacteristicValueChanged += Characteristic_CharacteristicValueChanged;
            await characteristic.StartNotificationsAsync().ConfigureAwait(false);

            await SendBatteryLevelToClients(_deviceBatteryLevel);

            return value[0].ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting battery level: {ex.Message}");
            return "Error";
        }
    }

    private async void Characteristic_CharacteristicValueChanged(object? sender, GattCharacteristicValueChangedEventArgs e)
    {
        // Send the battery level to the client
        await SendBatteryLevelToClients(_deviceBatteryLevel);
    }

    public async Task SendBatteryLevelToClients(Dictionary<string, float> deviceBatteryLevel)
    {
        Debug.WriteLine($"About to send battery level to clients: {deviceBatteryLevel}");
        var user = "test user";
        await _SensorHubContext.Clients.All.SendBatteryLevel(user, deviceBatteryLevel);
        Debug.WriteLine($"Sent battery level to clients");
    }
}
