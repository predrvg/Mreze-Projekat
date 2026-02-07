using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

class Program
{
    const int TCP_PORT = 5000;
    const int BROJ_IGRACA = 2;

    static List<Igrac> prijavljeniIgraci = new List<Igrac>();
    static List<Socket> soketiIgraca = new List<Socket>();

    static List<string> reci = new List<string> {"avion", "harmonika", "radiolog", "programiranje"};
    static void Main(string[] args)
    {
        //TCP soket
        Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
       
        IPEndPoint serverEP = new IPEndPoint(IPAddress.Any, TCP_PORT);
        
        server.Bind(serverEP);
        server.Listen(2);

        Console.WriteLine("$Server pokrenut. Čeka prijavu igrača.");

        while (true)
        {
            Socket client = server.Accept();

            byte[] buffer = new byte[1024];
            int bytesRead = client.Receive(buffer);

            Igrac noviIgrac = Serijalizer.Deserialize<Igrac>(buffer[..bytesRead]);
            prijavljeniIgraci.Add(noviIgrac);
            soketiIgraca.Add(client);

            Console.WriteLine($"Prijavljen igrač: {noviIgrac.Ime} ({noviIgrac.KorisnickoIme})");

            string potvrda = "Prijava uspesna!";
            client.Send(Encoding.UTF8.GetBytes(potvrda));

            if (prijavljeniIgraci.Count == BROJ_IGRACA)
            {
                PokreniIgru();
            }

        }
    }

    static void PokreniIgru()
    {
        Random rnd = new Random();
        string izabranaRec = reci[rnd.Next(reci.Count)];
        int duzinaReci = izabranaRec.Length;
        int brojDozvoljenihGresaka = 5;

        Console.WriteLine("Igra pocinje!");

        string pocetnoStanje = new string('_', duzinaReci);

        Igra igra = new Igra(
            prijavljeniIgraci[0].Ime,
            prijavljeniIgraci[1].Ime,
            trajanje: 300,
            duzinaReci: duzinaReci,
            brojDozvoljenihGresaka: brojDozvoljenihGresaka
            );

        byte[] data = Serijalizer.Serialize(igra);

        for (int i = 0; i < soketiIgraca.Count; i++)
        {
            Socket s = soketiIgraca[i];
            try
            {
                s.Send(data);
            }
            catch
            {
                Console.WriteLine($"Ne mogu da pošaljem startne parametre igraču {prijavljeniIgraci[i].KorisnickoIme}");
            }
            finally
            {
                s.Close();
            }
        }

        prijavljeniIgraci.Clear();
        soketiIgraca.Clear();
    }

}