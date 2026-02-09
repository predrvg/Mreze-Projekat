using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Program
{
    const int TCP_PORT = 33333;
    const int UDP_PORT = 44444;
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
    static string poslednjeStanjeZaPosmatraca = "";

    static void Main(string[] args)
    {
        // TCP
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
                                if (noviIgrac.TipPrijave == TipIgraca.Igrac)
                                {
                                    prijavljeniIgraci.Add(noviIgrac);
                                    bodoviIgraca[noviIgrac.KorisnickoIme] = 0;

                                    soketiAktivnihIgraca.Add(socket);

                                    Console.WriteLine($"Prijavljen IGRAČ: {noviIgrac.KorisnickoIme}");
                                    Serijalizer.Send(socket, "Prijava uspešna (IGRAC)!");

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
                                        $"Prijava uspešna (POSMATRAC). Preostali bodovi podrške: {MAX_BODOVI_PODRSKE}");

                                    if (!string.IsNullOrWhiteSpace(poslednjeStanjeZaPosmatraca))
                                    {   // saljemo odmah trenutno stanje ako je igra vec zapocela
                                        Serijalizer.Send(socket, poslednjeStanjeZaPosmatraca);
                                    }
                                }
                            }
                        }
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
            // UDP
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

        // za statistiku na kraju meca
        Dictionary<Igrac, int> tacnaSlova = new Dictionary<Igrac, int>();
        Dictionary<Igrac, bool> pogodjenaRec = new Dictionary<Igrac, bool>();
        foreach (var ig in prijavljeniIgraci)
        {
            tacnaSlova[ig] = 0;
            pogodjenaRec[ig] = false;
        }

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
                    Console.WriteLine("Ceka se potez...");
                    continue;
                }

                EndPoint udpKlijentEP = new IPEndPoint(IPAddress.Any, 0);
                int primljeno = udpServer.ReceiveFrom(buffer, ref udpKlijentEP);

                if (primljeno <= 0) continue;

                Console.WriteLine($"[UDP] Primljen paket od {udpKlijentEP} ({primljeno} bytes)");
                byte[] tacniPodaci = buffer.Take(primljeno).ToArray();
                string pokusaj = Serijalizer.Deserialize<string>(tacniPodaci);

                Igrac igracPoteza = NadjiIgracaPoEP(udpKlijentEP);
                if (igracPoteza == null || string.IsNullOrEmpty(igracPoteza.KorisnickoIme))
                {
                    Console.WriteLine("Ignorišem paket: Nepoznat igrač.");
                    continue;
                }

                if (pokusaj.Length == 1)
                {
                    char slovo = char.ToLower(pokusaj[0]);

                    if (igra.PokusanaSlova.Contains(slovo))
                    {
                        Console.WriteLine($"Slovo '{slovo}' je već pokušano.");
                    }
                    else
                    {
                        bool pogodak = false;

                        int preOtkriveno = 0;
                        for (int i = 0; i < maskiranaRec.Length; i++)
                            if (maskiranaRec[i] != '_') preOtkriveno++;

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
                            greske[igracPoteza]--;
                            Console.WriteLine($"{igracPoteza.KorisnickoIme} je promašio slovo '{slovo}'");
                        }
                        else
                        {
                            Console.WriteLine($"{igracPoteza.KorisnickoIme} je pogodio slovo '{slovo}'");
                            igra.PokusanaSlova.Add(slovo);

                            int posleOtkriveno = 0;
                            for (int i = 0; i < maskiranaRec.Length; i++)
                                if (maskiranaRec[i] != '_') posleOtkriveno++;

                            int novo = posleOtkriveno - preOtkriveno;
                            if (novo > 0) tacnaSlova[igracPoteza] += novo;
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

                        pogodjenaRec[igracPoteza] = true;
                        tacnaSlova[igracPoteza] = tajnaRec.Length;
                    }
                    else
                    {
                        greske[igracPoteza]--;
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

                bool recOtkrivena = !new string(maskiranaRec).Contains('_');
                bool nekoPogodioRec = prijavljeniIgraci.Exists(ig => pogodjenaRec[ig]);
                bool sviPotrosiliGreske = prijavljeniIgraci.TrueForAll(ig => greske[ig] <= 0);

                if (nekoPogodioRec || recOtkrivena || sviPotrosiliGreske)
                {
                    string zavrsnaPoruka = KreirajZavrsnuPoruku(tajnaRec, greske, tacnaSlova, pogodjenaRec);
                    PosaljiZavrsnuPorukuSvima(zavrsnaPoruka);
                    break;
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

        foreach (Igrac igrac in prijavljeniIgraci)
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
        poslednjeStanjeZaPosmatraca = stanje;

        foreach (var s in soketiPosmatraca)
        {
            if (s.Connected)
            {
                Serijalizer.Send(s, stanje);
            }
        }
    }

    static string KreirajZavrsnuPoruku(
        string originalnaRec,
        Dictionary<Igrac, int> greske,
        Dictionary<Igrac, int> tacnaSlova,
        Dictionary<Igrac, bool> pogodjenaRec)
    {

        string pobednik;

        var pogodiliRec = new List<Igrac>();
        foreach (var ig in prijavljeniIgraci)
            if (pogodjenaRec[ig]) pogodiliRec.Add(ig);

        if (pogodiliRec.Count == 1)
        {
            pobednik = pogodiliRec[0].KorisnickoIme;
        }
        else
        {
            bool sviPotrosili = true;
            foreach (var ig in prijavljeniIgraci)
                if (greske[ig] > 0) { sviPotrosili = false; break; }

            if (sviPotrosili)
            {
                int a = tacnaSlova[prijavljeniIgraci[0]];
                int b = tacnaSlova[prijavljeniIgraci[1]];
                if (a > b) pobednik = prijavljeniIgraci[0].KorisnickoIme;
                else if (b > a) pobednik = prijavljeniIgraci[1].KorisnickoIme;
                else pobednik = "NEREŠENO (isti broj tačnih slova)";
            }
            else
            {
                int a = tacnaSlova[prijavljeniIgraci[0]];
                int b = tacnaSlova[prijavljeniIgraci[1]];
                if (a > b) pobednik = prijavljeniIgraci[0].KorisnickoIme;
                else if (b > a) pobednik = prijavljeniIgraci[1].KorisnickoIme;
                else pobednik = "NEREŠENO (isti broj tačnih slova)";
            }
        }

        // tabela
        var sb = new StringBuilder();
        sb.AppendLine("\n\n\n===== KRAJ IGRE =====");
        sb.AppendLine($"Pobednik: {pobednik}");
        sb.AppendLine($"Originalna reč: {originalnaRec}");

        sb.AppendLine("\nTABELA BODOVA:");
        sb.AppendLine("-----------------------------------------------------------------------");
        sb.AppendLine("Igrac | Tacna slova | Pogodjena rec | Preostale greske | Bodovi podrske");
        sb.AppendLine("-----------------------------------------------------------------------");

        foreach (var ig in prijavljeniIgraci)
        {
            int podrska = 0;
            if (bodoviIgraca.ContainsKey(ig.KorisnickoIme)) podrska = bodoviIgraca[ig.KorisnickoIme];

            sb.AppendLine($"{ig.KorisnickoIme} | {tacnaSlova[ig]} | {(pogodjenaRec[ig] ? "DA" : "NE")} | {greske[ig]} | {podrska}");
        }

        sb.AppendLine();

        // rang lista
        var rang = new List<Igrac>(prijavljeniIgraci);
        rang.Sort((x, y) =>
        {
            int bx = bodoviIgraca.ContainsKey(x.KorisnickoIme) ? bodoviIgraca[x.KorisnickoIme] : 0;
            int by = bodoviIgraca.ContainsKey(y.KorisnickoIme) ? bodoviIgraca[y.KorisnickoIme] : 0;

            int cmp = by.CompareTo(bx);
            if (cmp != 0) return cmp;
            return tacnaSlova[y].CompareTo(tacnaSlova[x]);
        });

        sb.AppendLine("Rang lista:");
        for (int i = 0; i < rang.Count; i++)
        {
            var ig = rang[i];
            int podrska = bodoviIgraca.ContainsKey(ig.KorisnickoIme) ? bodoviIgraca[ig.KorisnickoIme] : 0;
            sb.AppendLine($"{i + 1}. {ig.KorisnickoIme} (Podrska: {podrska}, Pogodjenih slova: {tacnaSlova[ig]})");
        }

        sb.AppendLine("======================");
        return sb.ToString();
    }

    static void PosaljiZavrsnuPorukuSvima(string poruka)
    {
        foreach (var s in soketiAktivnihIgraca)
        {
            try
            {
                if (s.Connected) Serijalizer.Send(s, poruka);
            }
            catch { }
        }
        PosaljiStanjePosmatracima(poruka);
    }
}
