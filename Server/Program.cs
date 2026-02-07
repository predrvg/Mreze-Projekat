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
        server.Listen(BROJ_IGRACA);

        Console.WriteLine("Server pokrenut. Čeka prijavu igrača.\n");

        while (true)
        {
            Socket client = server.Accept();
            Console.WriteLine("Novi klijent se povezao.");
            Igrac noviIgrac = Serijalizer.Receive<Igrac>(client);

            prijavljeniIgraci.Add(noviIgrac);
            soketiIgraca.Add(client);

            Console.WriteLine($"Prijavljen igrač: {noviIgrac.Ime} ({noviIgrac.KorisnickoIme})");

            string poruka = "Prijava uspešna!";
            Serijalizer.Send(client, poruka);

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

        Console.WriteLine("\n---IGRA POCINJE---");

        string pocetnoStanje = String.Empty;
        for (int i = 0; i < duzinaReci; i++)
        {
            pocetnoStanje += "_ ";
        }

        pocetnoStanje = pocetnoStanje.Trim(); 
        Console.WriteLine($"\nPočetno stanje riječi: {pocetnoStanje}");


        Igra igra = new Igra(
            prijavljeniIgraci[0].Ime,
            prijavljeniIgraci[1].Ime,
            trajanje: 300,
            duzinaReci: duzinaReci,
            brojDozvoljenihGresaka: brojDozvoljenihGresaka
            );

        for (int i = 0; i < soketiIgraca.Count; i++)
        {
            try
            {
                Serijalizer.Send(soketiIgraca[i], igra);
                Console.WriteLine($"\nStartni parametri poslati igraču {prijavljeniIgraci[i].KorisnickoIme}");
            }
            catch
            {
                Console.WriteLine($"\nNe mogu da pošaljem startne parametre igraču {prijavljeniIgraci[i].KorisnickoIme}");
            }
        }

    }

}