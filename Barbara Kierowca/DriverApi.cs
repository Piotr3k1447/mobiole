using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Barbara_Kierowca;

public sealed class DriverApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed class Company
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Timezone { get; set; } = "Europe/Warsaw";
}

public sealed class Trip
{
    public string Id { get; set; } = "";
    public string Code { get; set; } = "";
    [JsonPropertyName("route_name")] public string RouteName { get; set; } = "";
    public string Status { get; set; } = "";
    [JsonPropertyName("departure_at")] public DateTimeOffset? DepartureAt { get; set; }
}

public sealed class Passenger
{
    [JsonPropertyName("first_name")] public string FirstName { get; set; } = "";
    [JsonPropertyName("last_name")] public string LastName { get; set; } = "";
    [JsonPropertyName("from_label")] public string FromLabel { get; set; } = "";
    [JsonPropertyName("to_label")] public string ToLabel { get; set; } = "";
    [JsonPropertyName("payment_status")] public string PaymentStatus { get; set; } = "";
    public string PaymentLabel => PaymentStatus switch
    {
        "paid" => "Zapłacono", "pending" => "Nieopłacona", "expired" => "Wygasła",
        "cancelled" => "Anulowana", "paid_review" => "Do weryfikacji",
        "partially_refunded" => "Częściowy zwrot", "refunded" => "Zwrot",
        _ => "Do weryfikacji"
    };
}

public sealed class LoginReply
{
    public string Token { get; set; } = "";
    public List<Company> Companies { get; set; } = [];
}
public sealed class TripsReply { public List<Trip> Trips { get; set; } = []; }
public sealed class PassengersReply { public List<Passenger> Passengers { get; set; } = []; }
public sealed class StartReply { [JsonPropertyName("tracking_key")] public string TrackingKey { get; set; } = ""; }
public sealed class OkReply { public bool Ok { get; set; } }

public static class DriverApi
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new StringIdentifierConverter() }
    };

    public static async Task<T> CallAsync<T>(string action, object? body = null, Dictionary<string, string>? query = null)
    {
        string server = Preferences.Default.Get("server", "").Trim().TrimEnd('/');
        if (!Uri.TryCreate(server, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment))
            throw new InvalidOperationException("Podaj prawidłowy adres serwera HTTPS.");

        var parameters = new Dictionary<string, string> { ["action"] = action };
        if (query != null)
            foreach (var item in query) parameters[item.Key] = item.Value;
        string suffix = string.Join("&", parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        using var request = new HttpRequestMessage(body == null ? HttpMethod.Get : HttpMethod.Post, $"{server}/driver-api.php?{suffix}");
        var token = await SecureStorage.Default.GetAsync("token");
        if (!string.IsNullOrEmpty(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await Client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            int status = (int)response.StatusCode;
            string message = status switch
            {
                404 => "Nie znaleziono API kierowcy (HTTP 404). Sprawdź adres serwera i wdrożenie pliku driver-api.php.",
                >= 300 and < 400 => $"Serwer przekierowuje żądanie (HTTP {status}). Sprawdź adres API.",
                _ => $"Błąd serwera (HTTP {status})."
            };
            try
            {
                using var error = JsonDocument.Parse(text);
                if (error.RootElement.ValueKind == JsonValueKind.Object &&
                    error.RootElement.TryGetProperty("error", out var value) &&
                    value.ValueKind == JsonValueKind.String)
                    message = value.GetString() ?? message;
            }
            catch (JsonException) { }
            throw new DriverApiException(status, message);
        }
        return JsonSerializer.Deserialize<T>(text, JsonOptions) ?? throw new InvalidOperationException("Nieprawidłowa odpowiedź serwera.");
    }

    private sealed class StringIdentifierConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Number ? reader.GetInt64().ToString() : reader.GetString();
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
    }
}
