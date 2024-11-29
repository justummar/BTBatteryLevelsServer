using Microsoft.AspNetCore.SignalR;

public interface ISensorClient
{
    Task ReceiveMessage(string user, string message);
    Task SendMessage(string user, string message);
    Task SendMessageToCaller(string user, string message);
    Task SendMessageToGroup(string user, string message);
    Task SendBatteryLevel(string recipientMethod,  Dictionary<string, float> deviceBatteryLevel);

}
public class SensorHub : Hub<ISensorClient>
{
    private const string recipient = "streamdecklistener";
    private const string sender = "SensorHub";
    private const string recipientMethod = "ReceiveBatteryLevels";

    public async Task SendMessage(string user, string message)
        => await Clients.All.ReceiveMessage(user, message);

    public async Task SendMessageToCaller(string user, string message)
        => await Clients.Caller.ReceiveMessage(user, message);

    public async Task SendMessageToGroup(string user, string message)
        => await Clients.Group("SignalR Users").ReceiveMessage(user, message);

    public async Task SendBatteryLevel(Dictionary<string, float> deviceBatteryLevel)
        => await Clients.All.SendBatteryLevel(recipientMethod ,deviceBatteryLevel);

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "streamdecklistener");
        await Clients.All.SendMessageToGroup(recipient, "the streamdeck user has joined");

        //start the bluetooth service
        var _deviceBatteryLevel = new Dictionary<string, float>();
        _deviceBatteryLevel.Add("device 1", 0.0f);

        await Clients.All.SendBatteryLevel(recipientMethod, _deviceBatteryLevel);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    public Task ReceiveMessage(string user, string message)
    {
        Console.WriteLine($"Received message from {user}: {message}");
        return Task.CompletedTask;
    }
}



