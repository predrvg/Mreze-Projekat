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

        //TCP 
        Socket tcpKlijent = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        tcpKlijent.Connect(IPAddress.Loopback, TCP_PORT);
        Console.WriteLine("\nPovezano sa serverom.");

        //UDP 
        Socket udpKlijent = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        udpKlijent.Bind(new IPEndPoint(IPAddress.Any, 0));
        EndPoint klijentEP = new IPEndPoint(IPAddress.Loopback, UDP_PORT);

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

        string potvrda = Serijalizer.Receive<string>(tcpKlijent);
        Console.WriteLine($"\nServer: {potvrda}");

        string stanje = Serijalizer.Receive<string>(tcpKlijent);
        Console.WriteLine("Početno stanje reči: " + stanje);


        Igra igra = Serijalizer.Receive<Igra>(tcpKlijent);
        Console.WriteLine($"\nPočetak igre: {igra.ImePrvogIgraca} vs {igra.ImeDrugogIgraca}");
        Console.WriteLine($"\nDužina reči: {igra.DuzinaReci}, Dozvoljene greške: {igra.BrojDozvoljenihGresaka}, UDP port servera: {igrac.Port}");

        byte[] buffer = new byte[1024];

        while (true)
        {
            if (Console.KeyAvailable)
            {
                Console.WriteLine("Unesite slovo ili rijec: ");
                string unos = Console.ReadLine() ?? "";

                byte[] data = Serijalizer.Serialize(unos);
                udpKlijent.SendTo(data, klijentEP);
            }

            if (udpKlijent.Available > 0)
            {
                EndPoint serverEPtemp = new IPEndPoint(IPAddress.Any, 0);
                int primljeno = udpKlijent.ReceiveFrom(buffer, ref serverEPtemp);
                byte[] tacniPodaci = buffer.Take(primljeno).ToArray();
                string novoStanje = Serijalizer.Deserialize<string>(tacniPodaci);

                Console.WriteLine("\n---NOVO STANJE---");
                Console.WriteLine(novoStanje);
            }
        }

    }
}
