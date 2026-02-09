using System;
using System.Net;
using System.Net.Sockets;

class Program
{
    const int TCP_PORT = 33333;
    const int UDP_PORT = 44444;
    static void Main(string[] args)
    {
        Console.WriteLine("---PRIJAVA IGRACA---\n");
        Console.Write("Unesite svoje ime: ");
        string ime = Console.ReadLine() ?? "";

        Console.Write("Unesite korisnicko ime: ");
        string korisnickoIme = Console.ReadLine() ?? "";

        TipIgraca tipPrijave;
        while (true)
        {
            Console.WriteLine("\nIzaberite tip prijave:");
            Console.WriteLine("1. Igrac");
            Console.WriteLine("2. Posmatrac");
            Console.Write("Unesite 1 ili 2: ");

            string unos = Console.ReadLine() ?? "";

            if (unos == "1")
            {
                tipPrijave = TipIgraca.Igrac;
                break;
            }
            else if (unos == "2")
            {
                tipPrijave = TipIgraca.Posmatrac;
                break;
            }
            else
            {
                Console.WriteLine("Neispravan unos. Pokusajte ponovo.\n");
            }
        }

        // TCP 
        Socket tcpKlijent = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        tcpKlijent.Connect(IPAddress.Loopback, TCP_PORT);
        Console.WriteLine("\nPovezano sa serverom.");

        // UDP 
        Socket udpKlijent = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        udpKlijent.Bind(new IPEndPoint(IPAddress.Any, 0));
        EndPoint serverEP = new IPEndPoint(IPAddress.Loopback, UDP_PORT);

        Igrac igrac = new Igrac
        {
            Ime = ime,
            KorisnickoIme = korisnickoIme,
            IpAdresa = "127.0.0.1",
            Port = ((IPEndPoint)udpKlijent.LocalEndPoint!).Port,
            TipPrijave = tipPrijave
        };

        Serijalizer.Send(tcpKlijent, igrac);
        Console.WriteLine("\nPodaci poslati serveru.");

        string? potvrda;
        while (!Serijalizer.TryReceive<string>(tcpKlijent, out potvrda))
        {
            Thread.Sleep(50);
        }
        Console.WriteLine($"\nServer: {potvrda}");

        if (igrac.TipPrijave == TipIgraca.Posmatrac)
        {
            Console.WriteLine("\n--- POSMATRANJE IGRE ---");

            while (true)
            {
                if (tcpKlijent.Available > 0)
                {
                    if (Serijalizer.TryReceive<string>(tcpKlijent, out string? stanjeIgre))
                    {
                        Console.WriteLine("[IGRA]: " + stanjeIgre);

                        if (stanjeIgre != null && stanjeIgre.Contains("===== KRAJ IGRE ====="))
                        {
                            Console.WriteLine("\nIgra je završena. Pritisni ENTER za izlaz.");
                            Console.ReadLine();
                            return;
                        }
                    }
                }

                Thread.Sleep(50);
            }
        }

        Thread.Sleep(100);
        string? stanje;
        while (!Serijalizer.TryReceive<string>(tcpKlijent, out stanje))
        {
            Thread.Sleep(50);
        }
        Console.WriteLine("Početno stanje reči: " + stanje);

        Thread.Sleep(100);
        Igra igra;
        while (true)
        {
            if (Serijalizer.TryReceive<Igra>(tcpKlijent, out Igra? temp))
            {
                if (temp != null)
                {
                    igra = temp;
                    break;
                }
            }
            Thread.Sleep(50);
        }
        Console.WriteLine($"\nPočetak igre: {igra.ImePrvogIgraca} vs {igra.ImeDrugogIgraca}");
        Console.WriteLine($"\nDužina reči: {igra.DuzinaReci}, Dozvoljene greške: {igra.BrojDozvoljenihGresaka}, UDP port klijenta: {igrac.Port}");

        byte[] buffer = new byte[1024];

        while (true)
        {
            if (udpKlijent.Available > 0)
            {
                EndPoint serverEPtemp = new IPEndPoint(IPAddress.Any, 0);
                int primljeno = udpKlijent.ReceiveFrom(buffer, ref serverEPtemp);
                byte[] tacniPodaci = buffer.Take(primljeno).ToArray();
                string novoStanje = Serijalizer.Deserialize<string>(tacniPodaci);

                Console.WriteLine("\n--- NOVO STANJE ---");
                Console.WriteLine(novoStanje);
                Console.Write("Vaš potez: ");
            }

            if (tcpKlijent.Available > 0)
            {
                while (tcpKlijent.Available > 0)
                {
                    if (Serijalizer.TryReceive<string>(tcpKlijent, out string? poruka))
                    {
                        Console.WriteLine(poruka);
                        Console.WriteLine("\nIgra je završena. Pritisni ENTER za izlaz.");
                        Console.ReadLine();
                        return;
                    }
                    else break;
                }
            }

            if (igrac.TipPrijave == TipIgraca.Igrac && Console.KeyAvailable)
            {
                string? unos = Console.ReadLine();
                if (!string.IsNullOrEmpty(unos))
                {
                    byte[] data = Serijalizer.Serialize(unos);
                    udpKlijent.SendTo(data, serverEP);
                }
            }

            Thread.Sleep(10);
        }
    }

    static string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }
}
