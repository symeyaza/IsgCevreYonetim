using System.Text.Json;

namespace IsgCevreYonetim.Web.Extensions
{
    public static class SessionExtensions
    {
        public static void Set<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        public static T? Get<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default : JsonSerializer.Deserialize<T>(value);
        }

        public static string GetStringValue(this ISession session, string key)
        {
            return session.GetString(key) ?? string.Empty;
        }
    }
}