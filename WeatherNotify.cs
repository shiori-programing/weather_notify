using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MailKit.Net.Smtp;
using MimeKit;

namespace weather_notify;

public class WeatherNotify
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    public WeatherNotify(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
    {
        _logger = loggerFactory.CreateLogger<WeatherNotify>();
        _httpClient = httpClientFactory.CreateClient();
    }

    [Function("WeatherNotify")]
    public async Task Run([TimerTrigger("0 15 * * * *")] TimerInfo myTimer)
    {
        // 天気データ取得（東京）
        var url = "https://api.open-meteo.com/v1/forecast?latitude=35.68&longitude=139.69&current_weather=true";
        var response = await _httpClient.GetStringAsync(url);
        var json = JsonDocument.Parse(response);
        var current = json.RootElement.GetProperty("current_weather");
        var temperature = current.GetProperty("temperature").GetDouble();
        var weatherCode = current.GetProperty("weathercode").GetInt32();
        var weatherDesc = GetWeatherDescription(weatherCode);

        _logger.LogInformation("今日の東京の天気: {weather}, 気温: {temp}°C", weatherDesc, temperature);

        // メール送信
        await SendEmailAsync(weatherDesc, temperature);
    }

    private async Task SendEmailAsync(string weather, double temperature)
    {
        var message = new MimeMessage();
        var gmailAddress = Environment.GetEnvironmentVariable("GMAIL_ADDRESS")!;
        var gmailPassword = Environment.GetEnvironmentVariable("GMAIL_PASSWORD")!;
        message.From.Add(new MailboxAddress("天気通知", gmailAddress));
        message.To.Add(new MailboxAddress("", gmailAddress));
        message.Subject = "今日の東京の天気";
        message.Body = new TextPart("plain")
        {
            Text = $"天気: {weather}\n気温: {temperature}°C"
        };

        using var client = new SmtpClient();
        await client.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(gmailAddress, gmailPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("メール送信完了！");
    }

    private static string GetWeatherDescription(int code) => code switch
    {
        0 => "快晴",
        1 or 2 or 3 => "晴れ〜曇り",
        45 or 48 => "霧",
        51 or 53 or 55 => "霧雨",
        61 or 63 or 65 => "雨",
        71 or 73 or 75 => "雪",
        80 or 81 or 82 => "にわか雨",
        95 => "雷雨",
        _ => "不明"
    };
}