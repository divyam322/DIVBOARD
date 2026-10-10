using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Storage.Streams;

internal static class Program
{
    private static readonly Guid ServiceId = Guid.Parse("9e7b1000-7c2d-4f8a-9a33-9b3a1d5e0001");
    private static readonly Guid CharacteristicId = Guid.Parse("9e7b1001-7c2d-4f8a-9a33-9b3a1d5e0001");
    private static readonly ConcurrentDictionary<Guid, WebSocket> Clients = new();
    private static GattServiceProvider? provider;
    private static long clientId;

    [STAThread]
    private static async Task Main()
    {
        Console.Title = "DIVBOARD Bluetooth Drawing Receiver";
        Console.WriteLine("DIVBOARD Bluetooth receiver");
        Console.WriteLine("Starting local WebSocket bridge at ws://127.0.0.1:8765/ ...");
        _ = RunWebSocketServer();
        try
        {
            var create = await GattServiceProvider.CreateAsync(ServiceId);
            if (create.Error != BluetoothError.Success || create.ServiceProvider is null)
                throw new InvalidOperationException("Windows could not create a BLE GATT service: " + create.Error +
                    ". This Bluetooth adapter/driver may not support the Windows peripheral role.");

            provider = create.ServiceProvider;
            var parameters = new GattLocalCharacteristicParameters
            {
                CharacteristicProperties = GattCharacteristicProperties.Write | GattCharacteristicProperties.WriteWithoutResponse,
                WriteProtectionLevel = GattProtectionLevel.Plain,
                UserDescription = "DIVBOARD drawing point"
            };
            var made = await provider.Service.CreateCharacteristicAsync(CharacteristicId, parameters);
            if (made.Error != BluetoothError.Success || made.Characteristic is null)
                throw new InvalidOperationException("Could not create drawing characteristic: " + made.Error);

            made.Characteristic.WriteRequested += OnWriteRequested;
            provider.StartAdvertising(new GattServiceProviderAdvertisingParameters
            {
                IsConnectable = true,
                IsDiscoverable = true
            });
            Console.WriteLine("BLE receiver advertising.");
            Console.WriteLine("On the phone, open https://divyam322.github.io/DIVBOARD/phone-pad.html");
            Console.WriteLine("Tap Connect to laptop and choose the DIVBOARD receiver.");
            Console.WriteLine("Keep this window open. Press Ctrl+C to stop.");
            await Task.Delay(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("Receiver could not start:");
            Console.WriteLine(ex.Message);
            Console.WriteLine();
            Console.WriteLine("If Windows reports that peripheral advertising is unsupported, this adapter/driver cannot host this BLE design.");
            Console.WriteLine("No phone settings are changed by this program.");
            Console.WriteLine("Press Enter to close.");
            Console.ReadLine();
        }
    }

    private static async void OnWriteRequested(GattLocalCharacteristic sender, GattWriteRequestedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var request = await args.GetRequestAsync();
            using var reader = DataReader.FromBuffer(request.Value);
            var bytes = new byte[request.Value.Length];
            reader.ReadBytes(bytes);
            request.Respond();
            if (bytes.Length < 5) return;
            int type = bytes[0];
            double x = (bytes[1] | bytes[2] << 8) / 65535.0;
            double y = (bytes[3] | bytes[4] << 8) / 65535.0;
            var message = JsonSerializer.Serialize(new { type = type == 1 ? "start" : type == 3 ? "end" : "point", x, y });
            await Broadcast(message);
        }
        catch (Exception ex) { Console.WriteLine("BLE packet error: " + ex.GetType().FullName + ": " + ex.ToString()); }
        finally { deferral.Complete(); }
    }

    private static async Task RunWebSocketServer()
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:8765/");
        try { listener.Start(); }
        catch (Exception ex) { Console.WriteLine("Local bridge failed to start: " + ex.Message); return; }
        while (true)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync(); } catch { return; }
            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 200;
                var bytes = Encoding.UTF8.GetBytes("DIVBOARD local drawing bridge is running.");
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
                continue;
            }
            _ = AcceptWebSocket(context);
        }
    }

    private static async Task AcceptWebSocket(HttpListenerContext context)
    {
        try
        {
            var accepted = await context.AcceptWebSocketAsync(null);
            var socket = accepted.WebSocket;
            var id = Guid.NewGuid();
            Clients[id] = socket;
            Console.WriteLine("Whiteboard connected to local bridge.");
            var buffer = new byte[1024];
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                }
            }
            finally
            {
                Clients.TryRemove(id, out _);
                socket.Dispose();
                Console.WriteLine("Whiteboard bridge disconnected.");
            }
        }
        catch (Exception ex) { Console.WriteLine("WebSocket client error: " + ex.Message); }
    }

    private static async Task Broadcast(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        foreach (var pair in Clients)
        {
            var socket = pair.Value;
            if (socket.State != WebSocketState.Open) { Clients.TryRemove(pair.Key, out _); continue; }
            try { await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None); }
            catch { Clients.TryRemove(pair.Key, out _); }
        }
    }
}
