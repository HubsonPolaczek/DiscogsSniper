using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DiscogsSniper.Services
{
    public class NotificationService
    {
        // TUTAJ WKLEJ SWÓJ ACCESS TOKEN Z PUSHBULLET!
        private const string AccessToken = "TWOJ_TOKEN_Z_PUSHBULLET";

        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task SendPushNotification(string title, string body)
        {
            if (string.IsNullOrEmpty(AccessToken) || AccessToken == "token")
                return;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.pushbullet.com/v2/pushes");
                request.Headers.Add("Access-Token", AccessToken);

                // Tworzymy paczkę danych dla telefonu
                var payload = new
                {
                    type = "note",
                    title = title,
                    body = body
                };

                string jsonPayload = JsonSerializer.Serialize(payload);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Błąd Pushbullet API: {error}");
                }
            }
            catch (Exception ex)
            {
                // Cichy błąd, żeby bot nie przestał działać, gdy np. padnie internet
                System.Diagnostics.Debug.WriteLine("Błąd wysyłania powiadomienia na telefon: " + ex.Message);
            }
        }
    }
}