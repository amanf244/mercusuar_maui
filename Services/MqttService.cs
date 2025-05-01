using MQTTnet;
using MQTTnet.Protocol;
using System.Security.Authentication;
using System.Text;
using System.Timers;

namespace MauiApp2.Services;

public class HiveMqMqttClient
{
    private IMqttClient _mqttClient;
    public IMqttClient MqttClient => _mqttClient;

    private System.Timers.Timer _reconnectTimer;

    public event Action<string, string> MessageReceived;
    public event Action<string> ConnectionStateChanged;

    private bool _isTryingToReconnect = false;
    private bool _userInitiatedDisconnect = false;

    public bool IsConnected => _mqttClient?.IsConnected ?? false;

    public HiveMqMqttClient()
    {
        var mqttFactory = new MqttClientFactory();
        _mqttClient = mqttFactory.CreateMqttClient();

        _mqttClient.ConnectedAsync += OnConnected;
        _mqttClient.DisconnectedAsync += OnDisconnected;
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;

        _reconnectTimer = new System.Timers.Timer(5000);
        _reconnectTimer.Elapsed += async (s, e) => await TryReconnectAsync();
        _reconnectTimer.AutoReset = true;
    }

    private async Task OnConnected(MqttClientConnectedEventArgs arg)
    {
        Console.WriteLine("✅ MQTT connected!");
        _reconnectTimer.Stop();
        _isTryingToReconnect = false;
        ConnectionStateChanged?.Invoke("Connected");
        await Task.CompletedTask;
    }

    private async Task OnDisconnected(MqttClientDisconnectedEventArgs arg)
    {
        Console.WriteLine("⚠️ MQTT disconnected!");

        if (!_userInitiatedDisconnect && !_isTryingToReconnect)
        {
            _isTryingToReconnect = true;
            _reconnectTimer.Start();
        }

        ConnectionStateChanged?.Invoke("Disconnected");
        await Task.CompletedTask;
    }

    private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs arg)
    {
        var topic = arg.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);
        MessageReceived?.Invoke(topic, payload);
        await Task.CompletedTask;
    }

    public async Task ConnectAsync()
    {
        if (_mqttClient.IsConnected)
        {
            Console.WriteLine("🔵 Already connected. Skipping connect.");
            return;
        }

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("a3f850802ac34230b60106b86aaa6ae8.s1.eu.hivemq.cloud", 8883)
            .WithClientId("maui")
            .WithCredentials("hivemq.webclient.1745768022480", "K1zTM:0g!roXYdJ74w;>")
            .WithTlsOptions(tls =>
            {
                tls.WithSslProtocols(SslProtocols.Tls12);
                tls.WithCertificateValidationHandler(_ => true);
            })
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
            .Build();

        try
        {
            ConnectionStateChanged?.Invoke("Connecting");
            await _mqttClient.ConnectAsync(options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Connect failed: {ex.Message}");
            ConnectionStateChanged?.Invoke("ConnectFailed");
            _reconnectTimer.Start();
        }
    }

    private async Task TryReconnectAsync()
    {
        if (!_mqttClient.IsConnected)
        {
            Console.WriteLine("🔄 Attempting reconnect...");
            ConnectionStateChanged?.Invoke("Reconnecting");

            try
            {
                await ConnectAsync();
            }
            catch
            {
                // Gagal reconnect, biarkan timer jalan terus
            }
        }
    }

    public async Task SubscribeAsync(string topic)
    {
        if (_mqttClient.IsConnected)
        {
            await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic(topic)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build());
            Console.WriteLine($"✅ Subscribed to {topic}");
        }
    }

    public async Task PublishAsync(string topic, string payload)
    {
        if (_mqttClient.IsConnected)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _mqttClient.PublishAsync(message);
        }
    }

    public async Task DisconnectAsync()
    {
        if (_mqttClient.IsConnected)
        {
            _userInitiatedDisconnect = true;
            await _mqttClient.DisconnectAsync();
            _reconnectTimer.Stop();
            Console.WriteLine("✅ User requested disconnect.");
        }
    }
}
