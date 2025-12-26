using System.Net.Http.Json;

public static class TelegramNotifier
{
    private static readonly HttpClient Http = new();

    public static async Task SendHtmlAsync(string html)
    {
        var token  = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        var chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
            throw new InvalidOperationException("TELEGRAM_BOT_TOKEN or TELEGRAM_CHAT_ID missing.");

        var url = $"https://api.telegram.org/bot{token}/sendMessage";
        var payload = new { chat_id = chatId, text = html, parse_mode = "HTML" };

        var resp = await Http.PostAsJsonAsync(url, payload);
        if (!resp.IsSuccessStatusCode)
            throw new Exception(await resp.Content.ReadAsStringAsync());
    }
}