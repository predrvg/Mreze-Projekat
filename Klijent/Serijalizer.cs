using System.Text;
using System.Text.Json;

static class Serijalizer
{
    public static byte[] Serialize<T>(T obj)
    {
        string json = JsonSerializer.Serialize(obj);
        return Encoding.UTF8.GetBytes(json);
    }

    public static T Deserialize<T>(byte[] data)
    {
        string json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<T>(json)!;
    }
}
