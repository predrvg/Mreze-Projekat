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

    const int TCP_PORT = 22222;
    const int UDP_PORT = 33333;
    const int BROJ_IGRACA = 2;

    const int MAX_BODOVI_PODRSKE = 5;

    static Socket udpServer = null!;

    static List<Igrac> prijavljeniIgraci = new List<Igrac>();
    static List<Socket> soketiIgraca = new List<Socket>();

    static List<string> reci = new List<string> { "avion", "harmonika", "radiolog", "programiranje" };

    static Dictionary<string, int> bodoviIgraca = new();
    static Dictionary<Socket, int> bodoviPosmatraca = new();
    static List<Socket> soketiPosmatraca = new();

    static List<Socket> soketiAktivnihIgraca = new List<Socket>();

    static bool igraPokrenuta = false;

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
                checkRead.Add(tcpServer);
                foreach (Socket s in soketiIgraca)
                {
                    checkRead.Add(s);
                }

                if (checkRead.Count == 0) { Thread.Sleep(10); continue; }
                Socket.Select(checkRead, null, null, 1000000);

                foreach (Socket socket in checkRead.ToList())
                {
                    if (socket == tcpServer)
                    {
                        Socket client = tcpServer.Accept();
                        client.Blocking = false;
                        soketiIgraca.Add(client);
                        Console.WriteLine("Novi klijent se povezao.");
                    }
                    else
                    {
                        if (Serijalizer.TryReceive<Igrac>(socket, out Igrac? noviIgrac))
                        {
                            if (noviIgrac != null)
                            {
                               /* prijavljeniIgraci.Add(noviIgrac);
                                Console.WriteLine($"Prijavljen igrač: {noviIgrac.KorisnickoIme}");
                                Serijalizer.Send(socket, "Prijava uspešna!");

                                if (prijavljeniIgraci.Count == BROJ_IGRACA)
                                {
                                    sviPrijavljeni = true;
                                   
                                }*/
                                if (noviIgrac.TipPrijave == TipIgraca.Igrac)
                                    {
                                        prijavljeniIgraci.Add(noviIgrac);
                                        bodoviIgraca[noviIgrac.KorisnickoIme] = 0;

                                        soketiAktivnihIgraca.Add(socket);

                                        Console.WriteLine($"Prijavljen IGRAČ: {noviIgrac.KorisnickoIme}");
                                        Serijalizer.Send(socket, "Prijava uspešna (IGRAČ)!");

                                        if (prijavljeniIgraci.Count == BROJ_IGRACA && !igraPokrenuta)
                                        {
                                            igraPokrenuta = true;
                                            Console.WriteLine("Svi igrači su tu. Pokrećem igru...");
                                            ThreadPool.QueueUserWorkItem(_ => PokreniIgru());
                                        }
                                    }
                                    else // POSMATRAČ
                                    {
                                        soketiPosmatraca.Add(socket);
                                        bodoviPosmatraca[socket] = MAX_BODOVI_PODRSKE;

                                        Console.WriteLine("Prijavljen POSMATRAČ");
                                        Serijalizer.Send(socket,
                                            $"Prijava uspešna (POSMATRAČ). Preostali bodovi podrške: {MAX_BODOVI_PODRSKE}");
                                    }
                            }
                        }
                    }
                }
            }
            Console.WriteLine("Svi igrači su tu. Pokrećem igru...");
            Thread.Sleep(500);
            PokreniIgru();
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

            Console.WriteLine($"\nUDP server pokrenut na portu {UDP_PORT}");

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


            for (int i = 0; i < soketiAktivnihIgraca.Count; i++)
            {
                try
                {
                    if (soketiAktivnihIgraca[i].Connected)
                    {
                        Serijalizer.Send(soketiAktivnihIgraca[i], pocetnoStanje);
                        Serijalizer.Send(soketiAktivnihIgraca[i], igra);

                        Console.WriteLine($"\nStartni parametri poslati igraču {prijavljeniIgraci[i].KorisnickoIme}");
                    }
                    PosaljiStanjePosmatracima(pocetnoStanje);
                    Console.WriteLine("Početno stanje poslato posmatračima.");
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

        Dictionary<Igrac, int> greske = new Dictionary<Igrac, int>();

        foreach (Igrac igrac in prijavljeniIgraci)
            greske[igrac] = brojGresaka;

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
                    Console.WriteLine("Cekam potez...");
                    continue;
                }

                EndPoint udpKlijentEP = new IPEndPoint(IPAddress.Any, 0);
                int primljeno = udpServer.ReceiveFrom(buffer, ref udpKlijentEP);
                
                if (primljeno <= 0) continue;

                Console.WriteLine($"[UDP] Primljen paket od {udpKlijentEP} ({primljeno} bytes)");
                byte[] tacniPodaci = buffer.Take(primljeno).ToArray();
                string pokusaj = Serijalizer.Deserialize<string>(tacniPodaci);

                if (pokusaj.Length == 1)
                {
                    Igrac igrac = NadjiIgracaPoEP(udpKlijentEP);

                    if (igrac == null || string.IsNullOrEmpty(igrac.KorisnickoIme))
                    {
                        Console.WriteLine("Ignorišem paket: Nepoznat igrač.");
                        continue; 
                    }

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
                            greske[igrac]--;
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
                        greske[igrac]--;
                    }
                }

                foreach (Igrac i in prijavljeniIgraci)
                {
                    string stanjeZaIgraca = new string(maskiranaRec) + $" | Preostale greške: {greske[i]}";

                    byte[] odgovor = Serijalizer.Serialize(stanjeZaIgraca);
                    EndPoint ep = new IPEndPoint(IPAddress.Parse(i.IpAdresa), i.Port);
                    udpServer.SendTo(odgovor, ep);
                    Console.WriteLine($"[UDP] Stanje poslato igraču {i.KorisnickoIme} na port {i.Port}");

                }
                string stanjeZaPosmatraca = new string(maskiranaRec) +
                $" | Greške: {greske[prijavljeniIgraci[0]]} / {greske[prijavljeniIgraci[1]]}";

                PosaljiStanjePosmatracima(stanjeZaPosmatraca);
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

            return null!;
        }
        
        static void PosaljiStanjePosmatracima(string stanje)
        {
            foreach (var s in soketiPosmatraca)
            {
                if (s.Connected)
                {
                    Serijalizer.Send(s, stanje);
                }
            }
        }

}
