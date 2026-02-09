using System.Net.Sockets;
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

    public static void Send<T>(Socket soket, T obj)
    {
        byte[] data = Serialize(obj);             
        byte[] lenBytes = BitConverter.GetBytes(data.Length); 
        soket.Send(lenBytes);                       
        soket.Send(data);                           
    }

    public static bool TryReceive<T>(Socket soket, out T? obj)
    {
        obj = default;

        if (soket.Available < 4)
            return false;

        byte[] lenBytes = new byte[4];
        int readLen = soket.Receive(lenBytes, 0, 4, SocketFlags.None);
        if (readLen < 4)
            return false;

        int length = BitConverter.ToInt32(lenBytes, 0);

        if (soket.Available < length)
            return false;

        byte[] data = new byte[length];
        int total = 0;
        while (total < length)
        {
            int received = soket.Receive(data, total, length - total, SocketFlags.None);
            total += received;
        }

        obj = Deserialize<T>(data)!;
        return true;
    }

}
