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
        try
        {
            if (soket.Available < 4) return false;

            byte[] duzinaBuffer = new byte[4];
            int primljeno = soket.Receive(duzinaBuffer);

            if (primljeno < 4) return false;

            int duzina = BitConverter.ToInt32(duzinaBuffer, 0);

            if (duzina <= 0 || duzina > 10 * 1024 * 1024)
            {
                return false;
            }

            if (soket.Available < duzina)
            {

                return false;
            }

            byte[] data = new byte[duzina];
            soket.Receive(data);
            obj = Deserialize<T>(data);
            return true;
        }
        catch
        {
            return false;
        }
    }

}
