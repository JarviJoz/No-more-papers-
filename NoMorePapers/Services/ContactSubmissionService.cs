using NoMorePapers.Models;
using System.Net.Http;
using System.Net.Http.Json;

namespace NoMorePapers.Services
{
    public sealed class ContactSubmissionService
    {
        private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
        private const string EndpointEnvironmentVariable = "NMP_CONTACT_API_URL";

        public async Task SubmitAsync(ContactMessage message, CancellationToken cancellationToken = default)
        {
            var endpointText = Environment.GetEnvironmentVariable(EndpointEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(endpointText))
            {
                throw new InvalidOperationException("El envío de mensajes aún no está configurado. Configura NMP_CONTACT_API_URL con la URL HTTPS de un servicio de contacto seguro.");
            }

            if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint) ||
                (endpoint.Scheme != Uri.UriSchemeHttps && !(endpoint.IsLoopback && endpoint.Scheme == Uri.UriSchemeHttp)))
            {
                throw new InvalidOperationException("La dirección del servicio de contacto debe usar HTTPS (se permite HTTP solo para pruebas locales).");
            }

            using var response = await Client.PostAsJsonAsync(endpoint, message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"El servicio de contacto respondió con el estado {(int)response.StatusCode}.");
            }
        }
    }
}