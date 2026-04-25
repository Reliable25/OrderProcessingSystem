using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OrderProcessing.Application.Utils
{
    public static class IdempotencyHelper
    {
        public static string ComputeHash<T>(T request)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            var json = JsonSerializer.Serialize(request, options);
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(json);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }
    }
}
