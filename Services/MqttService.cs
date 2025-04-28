using MQTTnet.Protocol;
using MQTTnet;
using System.Security.Authentication;
using System.Text;

namespace MauiApp2.Services;

public class HiveMqMqttClient
{
    private IMqttClient _mqttClient;
    public bool IsConnected => _mqttClient?.IsConnected == true;


    // Ini event buat lempar data ke luar
    public event Action<string, string> MessageReceived;

    public async Task ConnectAsync()
    {
        var mqttFactory = new MqttClientFactory();
        _mqttClient = mqttFactory.CreateMqttClient();

        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer("a3f850802ac34230b60106b86aaa6ae8.s1.eu.hivemq.cloud", 8883)
            .WithClientId("clientId-BtJuB7AAXo")
            .WithCredentials("hivemq.webclient.1745768022480", "K1zTM:0g!roXYdJ74w;>")
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
            .WithTlsOptions(options =>
            {
                options.WithSslProtocols(SslProtocols.Tls12);
                options.WithCertificateValidationHandler(_ => true);
            })
            .Build();

        _mqttClient.ConnectedAsync += async e =>
        {
            Console.WriteLine("✅ MQTT connected!");
            await Task.CompletedTask;
        };

        _mqttClient.DisconnectedAsync += async e =>
        {
            Console.WriteLine("⚠️ MQTT disconnected!");
            await Task.CompletedTask;
        };

        _mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            var topicReceived = e.ApplicationMessage.Topic;
            var payloadReceived = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            Console.WriteLine($"📥 Message received - Topic: {topicReceived}, Payload: {payloadReceived}");

            // Panggil event, kasih tau class lain
            MessageReceived?.Invoke(topicReceived, payloadReceived);

            await Task.CompletedTask;
        };

        try
        {
            var connectResult = await _mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);
            Console.WriteLine($"🚀 Connect Result: {connectResult.ResultCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Connect failed: {ex.Message}");
        }
    }

    public async Task SubscribeAsync(string topic)
    {
        if (_mqttClient is null || !_mqttClient.IsConnected)
        {
            Console.WriteLine("❌ Client not connected.");
            return;
        }

        await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder()
            .WithTopic(topic)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build());

        Console.WriteLine($"✅ Subscribed to topic: {topic}");
    }

    public async Task PublishAsync(string topic, string payload)
    {
        if (_mqttClient is null || !_mqttClient.IsConnected)
        {
            Console.WriteLine("❌ Client not connected.");
            return;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqttClient.PublishAsync(message, CancellationToken.None);
        Console.WriteLine("✅ Message published.");
    }

    public async Task DisconnectAsync()
    {
        if (_mqttClient != null && _mqttClient.IsConnected)
        {
            await _mqttClient.DisconnectAsync();
            Console.WriteLine("✅ Disconnected from MQTT broker.");
        }
    }
}
