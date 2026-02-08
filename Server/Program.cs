using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

class Program
{

    const int TCP_PORT = 21005;
    const int UDP_PORT = 21106;
    const int BROJ_IGRACA = 2;

    static Socket udpServer = null!;

    static List<Igrac> prijavljeniIgraci = new List<Igrac>();
    static List<Socket> soketiIgraca = new List<Socket>();

    static List<string> reci = new List<string> { "avion", "harmonika", "radiolog", "programiranje" };
    static void Main(string[] args)
    {
        //TCP soket
        Socket tcpServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        IPEndPoint serverEP = new IPEndPoint(IPAddress.Any, TCP_PORT);

        tcpServer.Bind(serverEP);
        tcpServer.Listen(BROJ_IGRACA);
        tcpServer.Blocking = false;

        Console.WriteLine("Server pokrenut. Čeka prijavu igrača.\n");

        try
        {
            while (true)
            {
                List<Socket> checkRead = new List<Socket>();
                List<Socket> checkError = new List<Socket>();

                if (prijavljeniIgraci.Count < BROJ_IGRACA)
                {
                    checkRead.Add(tcpServer);
                }
                checkError.Add(tcpServer);

                Socket.Select(checkRead, null, checkError, 1000000);


                if (checkError.Count > 0)
                {
                    Console.WriteLine("Greška na serverskoj utičnici!");
                    break;
                }
                //nema dogadjaja server nastavlja sa radom
                if (checkRead.Count == 0)
                {
                    continue;
                }

                Console.WriteLine($"Broj događaja: {checkRead.Count}");

                foreach (Socket socket in checkRead)
                {
                    Socket client = tcpServer.Accept();
                    client.Blocking = false;

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
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Doslo je do greske {ex}");
        }
    }

    

    static void PokreniIgru()
    {
       try
        {
            udpServer = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Any, UDP_PORT);
            udpServer.Bind(serverEP);
            udpServer.Blocking = false;

            Console.WriteLine($"\nUDP tcpServer pokrenut na portu {UDP_PORT}");

            Random rnd = new Random();
            string izabranaRec = reci[rnd.Next(reci.Count)];
            int duzinaReci = izabranaRec.Length;
            int brojDozvoljenihGresaka = 5;

            Console.WriteLine("\nIgra je pocela.");

            string pocetnoStanje = String.Empty;
            for (int i = 0; i < duzinaReci; i++)
            {
                pocetnoStanje += "_ ";
            }

            pocetnoStanje = pocetnoStanje.Trim();

            HashSet<char> pokusanaSlova = new HashSet<char>();

            Igra igra = new Igra(
                prijavljeniIgraci[0].Ime,
                prijavljeniIgraci[1].Ime,
                trajanje: 300,
                duzinaReci: duzinaReci,
                brojDozvoljenihGresaka: brojDozvoljenihGresaka,
                pokusanaSlova: pokusanaSlova
                );

            for (int i = 0; i < soketiIgraca.Count; i++)
            {
                try
                {
                    Serijalizer.Send(soketiIgraca[i], pocetnoStanje);
                    Serijalizer.Send(soketiIgraca[i], igra);
                    Console.WriteLine($"\nStartni parametri poslati igraču {prijavljeniIgraci[i].KorisnickoIme}");
                }
                catch
                {
                    Console.WriteLine($"\nNe mogu da pošaljem startne parametre igraču {prijavljeniIgraci[i].KorisnickoIme}");
                }
            }

            ObradiPoteze(izabranaRec, brojDozvoljenihGresaka, igra);
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Greška pri pokretanju UDP servera: {ex.Message}");
        }
    }

    static void ObradiPoteze(string tajnaRec, int brojGresaka, Igra igra)
    { 

        char[] maskiranaRec = new char[tajnaRec.Length];
        for (int i = 0; i < maskiranaRec.Length; i++)
            maskiranaRec[i] = '_';

        tajnaRec = tajnaRec.ToLower();

        Dictionary<string, int> greske = new Dictionary<string, int>();

        foreach (Igrac igrac in prijavljeniIgraci)
            greske[igrac.KorisnickoIme] = brojGresaka;

        byte[] buffer = new byte[1024];

        try
        {
            while (true)
            {
                List<Socket> checkRead = new List<Socket> { udpServer };
                List<Socket> checkError = new List<Socket> { udpServer };

                Socket.Select(checkRead, null, checkError, 1000000);

                if (checkError.Count > 0)
                {
                    Console.WriteLine("Greška na UDP utičnici!");
                    break;
                }

                if (checkRead.Count == 0)
                {
                    // nema poteza – igra se ne blokira
                    continue;
                }

                EndPoint udpKlijentEP = new IPEndPoint(IPAddress.Any, 0);
                int primljeno = udpServer.ReceiveFrom(buffer, ref udpKlijentEP);

                byte[] tacniPodaci = buffer.Take(primljeno).ToArray();
                string pokusaj = Serijalizer.Deserialize<string>(tacniPodaci);

                if (pokusaj.Length == 1)
                {
                    Igrac igrac = NadjiIgracaPoEP(udpKlijentEP);
                    char slovo = char.ToLower(pokusaj[0]);

                    if (igra.PokusanaSlova.Contains(slovo))
                    {
                        Console.WriteLine($"Slovo '{slovo}' je već pokušano.");
                    }
                    else
                    {
                        bool pogodak = false;

                        for (int i = 0; i < tajnaRec.Length; i++)
                        {
                            if (tajnaRec[i] == slovo)
                            {
                                maskiranaRec[i] = slovo;
                                pogodak = true;
                            }
                        }

                        if (!pogodak)
                        {
                            igra.PokusanaSlova.Add(slovo);
                            string korisnik = igrac.KorisnickoIme;
                            greske[korisnik]--;
                            Console.WriteLine($"{igrac.KorisnickoIme} je promašio slovo '{slovo}'");
                        }
                        else
                        {
                            Console.WriteLine($"{igrac.KorisnickoIme} je pogodio slovo '{slovo}'");
                            igra.PokusanaSlova.Add(slovo);
                        }
                    }

                }
                else
                {
                    if (pokusaj.Length != tajnaRec.Length)
                    {
                        Console.WriteLine("Pogresna duzina reci.");
                    }
                    else if (pokusaj == tajnaRec)
                    {
                        maskiranaRec = tajnaRec.ToCharArray();
                        Console.WriteLine("Rec je pogodjena.");
                    }
                    else
                    {
                        Igrac igrac = NadjiIgracaPoEP(udpKlijentEP);

                        string korisnik = igrac.KorisnickoIme;
                        greske[korisnik]--;
                    }
                }

                foreach (Igrac i in prijavljeniIgraci)
                {
                    string stanjeZaIgraca = new string(maskiranaRec) + $" | Preostale greške: {greske[i.KorisnickoIme]}";

                    byte[] odgovor = Serijalizer.Serialize(stanjeZaIgraca);
                    EndPoint ep = new IPEndPoint(IPAddress.Parse(i.IpAdresa), i.Port);
                    udpServer.SendTo(odgovor, ep);
                }

            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Doslo je do greske {ex}");
        }
}
        static Igrac NadjiIgracaPoEP(EndPoint ep)
        {
            IPEndPoint ip = (IPEndPoint)ep;

            foreach(Igrac igrac in prijavljeniIgraci)
            {
                if (igrac.Port == ip.Port && igrac.IpAdresa == ip.Address.ToString())
                {
                    return igrac;
                }
            }

            return new Igrac();
        }
}
