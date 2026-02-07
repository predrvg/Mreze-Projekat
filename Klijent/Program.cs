using System;
using System.Net;
using System.Net.Sockets;

class Program
{
    const int TCP_PORT = 5000;
    const int UDP_PORT = 7000;
    static void Main(string[] args)
    {
        Console.WriteLine("---PRIJAVA IGRACA---\n");
        Console.Write("Unesite svoje ime:");
        string ime = Console.ReadLine() ?? "";

        Console.Write("Unesite korisnicko ime:");
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

        Igrac igrac = new Igrac
        {
            Ime = ime,
            KorisnickoIme = korisnickoIme,
            IpAdresa = "127.0.0.1", 
            Port = UDP_PORT,
            TipPrijave = tipPrijave
        };

        //TCP soket
        Socket soket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        soket.Connect(IPAddress.Loopback, TCP_PORT);
        Console.WriteLine("\nPovezano sa serverom.");

        Serijalizer.Send(soket, igrac);
        Console.WriteLine("\nPodaci poslati serveru.");

        string potvrda = Serijalizer.Receive<string>(soket);
        Console.WriteLine($"\nServer: {potvrda}");

        Igra igra = Serijalizer.Receive<Igra>(soket);
        Console.WriteLine($"\nPočetak igre: {igra.ImePrvogIgraca} vs {igra.ImeDrugogIgraca}");
        Console.WriteLine($"\nDužina reči: {igra.DuzinaReci}, Dozvoljene greške: {igra.BrojDozvoljenihGresaka}, UDP port servera: {igrac.Port}");

        //UDP soket
        Socket udpClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        EndPoint serverEP = new IPEndPoint(IPAddress.Loopback, UDP_PORT);

        Console.WriteLine("\nPritisnite ENTER za izlaz...");
        Console.ReadLine();
    }
}
