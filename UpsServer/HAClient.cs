using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UpsServer;

public sealed class HAClient(ILogger<HAClient> _logger) : IDisposable
{
    private readonly string _haUrl = "ws://home-assistant.home.arpa:8123/api/websocket";
    private readonly string _haToken = Environment.GetEnvironmentVariable("ha_token");
    private readonly string[] _entities = [
        "sensor.ups_battery",
        "binary_sensor.ups_ac_plugged_in",
        "sensor.ups_input_power",
        "sensor.ups_output_power" ,
        "number.ups_charge_limit",
        "number.ups_discharge_limit",
    ];
    private ClientWebSocket _client;
    private CancellationTokenSource _cts;
    private Task _webSocketTask;
    private int _messageId = 1;

    public HAStates States { get; } = new();
    public delegate void StatesChangedHandler(HAStates states);
    public event StatesChangedHandler StatesChanged;

    public Task StartAsync(CancellationToken token)
    {
        _logger.LogInformation("Запуск HomeAssistant WebSocket клиента");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _webSocketTask = ConnectToWebSocket(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken token)
    {
        _logger.LogInformation("Остановка HomeAssistant WebSocket клиента");

        if (_client?.State == WebSocketState.Open)
        {
            await _client.CloseAsync(
                closeStatus: WebSocketCloseStatus.NormalClosure,
                statusDescription: "Service stopping",
                cancellationToken: CancellationToken.None);
        }

        await _cts?.CancelAsync();

        try
        {
            if (_webSocketTask != null)
            {
                await _webSocketTask;
            }
        }
        catch
        {
        }
    }

    private async Task ConnectToWebSocket(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                _client = new ClientWebSocket();
                await _client.ConnectAsync(new Uri(_haUrl), token);
                _logger.LogInformation("Подключено к Home Assistant!");

                await ProcessAuthAsync(token);
                await InitStatesAsync(token);
                await SubscribeStatesUpdatesAsync(token);
                await ListenStatesUpdatesAsync(token);

            }
            catch (TaskCanceledException) when (token.IsCancellationRequested)
            {
                _logger.LogInformation($"Задача отменена");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка WebSocket-соединения: {ex.Message}");
                _client?.Dispose();
                _client = null;
                await Task.Delay(5000, token);
            }
        }
    }

    private async Task SendMessageAsync(object message, CancellationToken token)
    {
        string json = JsonSerializer.Serialize(message);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        await _client!.SendAsync(buffer, WebSocketMessageType.Text, true, token);
    }

    private async Task<JsonNode> ReceiveMessageAsync(CancellationToken token)
    {
        const int bufferSize = 1024;
        using MemoryStream stream = new(bufferSize);
        using StreamReader reader = new(stream, Encoding.UTF8);
        byte[] buffer = new byte[bufferSize];
        WebSocketReceiveResult message;

        do
        {
            message = await _client!.ReceiveAsync(buffer, token);
            stream.Write(buffer, 0, message.Count);
        } while (message.EndOfMessage != true);

        if (stream.Position == 0)
        {
            return null;
        }

        stream.Position = 0;
        JsonNode result = JsonNode.Parse(stream);
        return result;
    }

    private async Task ProcessAuthAsync(CancellationToken token)
    {
        _logger.LogInformation("Ожидание аутентификации");
        JsonNode authRequired = await ReceiveMessageAsync(token);
        if (authRequired?["type"]?.GetValue<string>() == "auth_required")
        {
            _logger.LogInformation("Получен запрос на аутентификацию");
        }
        else
        {
            throw new Exception(
                $"Ожидался запрос на аутентификацию, но пришло это: {authRequired?.ToJsonString()}");
        }

        await SendMessageAsync(new { type = "auth", access_token = _haToken }, token);

        JsonNode authResult = await ReceiveMessageAsync(token);
        if (authResult?["type"]?.GetValue<string>() == "auth_ok")
        {
            _logger.LogInformation("Успешная аутентификация");
        }
        else
        {
            throw new Exception($"Ошибка аутентификации: {authResult?.ToJsonString()}");
        }
    }

    private void EnsureCorrectMessageId(JsonNode message)
    {
        if (message?["id"]?.GetValue<int>() != _messageId)
        {
            throw new Exception(
                $"Ожидалось id сообщения {_messageId}: {message?.ToJsonString()}");
        }
    }

    private void EnsureSuccessfulResult(JsonNode message)
    {
        EnsureCorrectMessageId(message);
        if (message?["type"]?.GetValue<string>() != "result")
        {
            throw new Exception($"Ожидалось сообщение с типом result: {message?.ToJsonString()}");
        }
        if (message?["success"]?.GetValue<bool>() != true)
        {
            throw new Exception($"Ожидалось сообщении c success=true: {message?.ToJsonString()}");
        }
    }

    private void UpdateStates(JsonNode entity)
    {
        string entity_id = entity["entity_id"].GetValue<string>();
        string value = entity["state"].GetValue<string>();

        if (entity_id == "sensor.ups_battery")
        {
            States.Battery = ToNumber(value);
        }
        else if (entity_id == "binary_sensor.ups_ac_plugged_in")
        {
            States.AcPluggedIn = ToBool(value);
        }
        else if (entity_id == "sensor.ups_input_power")
        {
            States.InputPower = ToNumber(value);
        }
        else if (entity_id == "sensor.ups_output_power")
        {
            States.OutputPower = ToNumber(value);
        }
        else if (entity_id == "number.ups_charge_limit")
        {
            States.ChargeLimit = ToNumber(value);
        }
        else if (entity_id == "number.ups_discharge_limit")
        {
            States.DischargeLimit = ToNumber(value);
        }
        else
        {
            return;
        }

        _logger.LogInformation($"{entity_id} => {value}");
        StatesChanged?.Invoke(States);

        static double? ToNumber(string source) =>
            double.TryParse(source, CultureInfo.InvariantCulture, out double value) ? value : null;
        static bool? ToBool(string source) => source?.ToLower() switch
        {
            "on" => true,
            "off" => false,
            _ => null,
        };
    }

    private async Task InitStatesAsync(CancellationToken token)
    {
        _logger.LogInformation("Получение состояний");
        await SendMessageAsync(new { id = ++_messageId, type = "get_states" }, token);
        JsonNode statesResult = await ReceiveMessageAsync(token);
        EnsureSuccessfulResult(statesResult);
        foreach (JsonNode state in statesResult["result"].AsArray())
        {
            UpdateStates(state);
        }
    }

    private async Task SubscribeStatesUpdatesAsync(CancellationToken token)
    {
        _logger.LogInformation("Подписка на изменения состояний");
        await SendMessageAsync(new
        {
            id = ++_messageId,
            type = "subscribe_trigger",
            trigger = _entities.Select(entity_id => new { platform = "state", entity_id })
        }, token);
        JsonNode subscribeResult = await ReceiveMessageAsync(token);
        EnsureSuccessfulResult(subscribeResult);
    }

    private async Task ListenStatesUpdatesAsync(CancellationToken token)
    {
        while (_client.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            JsonNode message = await ReceiveMessageAsync(token);

            if (message == null)
            {
                continue;
            }

            EnsureCorrectMessageId(message);
            if (message["type"].GetValue<string>() != "event")
            {
                throw new Exception($"Ожидалось сообщение с типом event: {message?.ToJsonString()}");
            }
            JsonNode state = message["event"]["variables"]["trigger"]["to_state"];
            UpdateStates(state);
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
