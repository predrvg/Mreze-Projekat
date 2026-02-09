using System;
using System.Globalization;
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
    static List<string> reci = new List<string> {
      "avion",
      "harmonika",
      "radiolog",
      "programiranje",
      "matematika",
      "kompjuter",
      "internet",
      "biblioteka",
      "univerzitet",
      "telefon",
      "sladoled",
      "fotografija",
      "hemija",
      "biologija",
      "astronomija",
      "mikroskop",
      "televizija",
      "automobil", 
      "gradjevina",
      "muzika",
      "filozofija",
      "istorija",
      "geografija",
      "programer",
      "softver",
      "hardver",
      "robotika"
    };
    static Dictionary<string, int> bodoviIgraca = new();
    static Dictionary<Socket, int> bodoviPosmatraca = new();
    static List<Socket> soketiPosmatraca = new();
    static List<Socket> soketiAktivnihIgraca = new List<Socket>();
    static bool igraPokrenuta = false;
    static string poslednjeStanjeZaPosmatraca = "";

    static void Main(string[] args)
    {
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

                foreach (Socket? s in soketiIgraca.ToList())
                {
                    if (s == null)
                    {
                        soketiIgraca.Remove(s!);
                        continue;
                    }

                    try
                    {
                        if (s.Connected)
                        {
                            checkRead.Add(s);
                        }
                        else
                        {
                            soketiIgraca.Remove(s);
                        }
                    }
                    catch (SocketException) { soketiIgraca.Remove(s); }
                    catch (ObjectDisposedException) { soketiIgraca.Remove(s); }
                }

                if (checkRead.Count == 0) { Thread.Sleep(10); continue; }

                try
                {
                    Socket.Select(checkRead, null, null, 1000000);
                }
                catch (SocketException) { continue; }
                catch (ObjectDisposedException) { continue; }

                foreach (Socket socket in checkRead.ToList())
                {
                    try
                    {
                        if (socket == tcpServer)
                        {
                            Socket client = tcpServer.Accept();
                            client.Blocking = false;
                            soketiIgraca.Add(client);
                            Console.WriteLine("Novi klijent se povezao na TCP.");
                        }
                        else
                        {
                            if (soketiPosmatraca.Contains(socket))
                            {
                                if (Serijalizer.TryReceive<string>(socket, out string? porukaOdPosmatraca))
                                {
                                    if (int.TryParse(porukaOdPosmatraca, out int indexIgraca) && indexIgraca > 0 && indexIgraca <= prijavljeniIgraci.Count)
                                    {
                                        if (bodoviPosmatraca[socket] > 0)
                                        {
                                            Igrac ciljaniIgrac = prijavljeniIgraci[indexIgraca - 1];
                                            bodoviIgraca[ciljaniIgrac.KorisnickoIme]++;
                                            bodoviPosmatraca[socket]--;

                                            Serijalizer.Send(socket, $"Dodelili ste bod igraču {ciljaniIgrac.KorisnickoIme}. Preostalo: {bodoviPosmatraca[socket]}");
                                            Console.WriteLine($"Posmatrač dodelio bod igraču {ciljaniIgrac.KorisnickoIme}");

                                            foreach (var sIgrac in soketiAktivnihIgraca.ToList())
                                            {
                                                try
                                                {
                                                    if (sIgrac.Connected)
                                                        Serijalizer.Send(sIgrac, $"\n[PODRŠKA] Igrač {ciljaniIgrac.KorisnickoIme} dobio je bod podrške.");
                                                }
                                                catch { soketiAktivnihIgraca.Remove(sIgrac); }
                                            }
                                        }
                                        else
                                        {
                                            Serijalizer.Send(socket, "Nemate više bodova podrške!");
                                        }
                                    }
                                }
                            }
                            else if (!soketiAktivnihIgraca.Contains(socket))
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
                                        else if (noviIgrac.TipPrijave == TipIgraca.Posmatrac)
                                        {
                                            soketiPosmatraca.Add(socket);
                                            bodoviPosmatraca[socket] = MAX_BODOVI_PODRSKE;
                                            Console.WriteLine("Prijavljen POSMATRAČ");
                                            Serijalizer.Send(socket, $"Prijava uspešna (POSMATRAC). Preostali bodovi: {MAX_BODOVI_PODRSKE}");

                                            if (!string.IsNullOrWhiteSpace(poslednjeStanjeZaPosmatraca))
                                                Serijalizer.Send(socket, poslednjeStanjeZaPosmatraca);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (SocketException) { soketiIgraca.Remove(socket); }
                    catch (ObjectDisposedException) { soketiIgraca.Remove(socket); }
                }
            }
            
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Doslo je do greske {ex}");
        }
        catch (ObjectDisposedException)
        {
            Console.WriteLine("Server soket je zatvoren.");
        }
        finally
        {
            if (tcpServer != null)
            {
                tcpServer.Close();
                Console.WriteLine("Glavni TCP server zatvoren.");
            }
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

                    Dictionary<Igrac, int> pocetneGreske = new Dictionary<Igrac, int>();
                    foreach (var ig in prijavljeniIgraci)
                        pocetneGreske[ig] = brojDozvoljenihGresaka;

                    PosaljiStanjePosmatracima(pocetnoStanje, pocetneGreske);
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
                   // Console.WriteLine("Ceka se potez...");
                    continue;
                }

                foreach (var sPosmatrac in soketiPosmatraca.ToList())
                {
                    try
                    {
                        if (Serijalizer.TryReceive<string>(sPosmatrac, out string? porukaOdPosmatraca))
                        {
                            if (int.TryParse(porukaOdPosmatraca, out int indexIgraca)
                                && indexIgraca > 0 && indexIgraca <= prijavljeniIgraci.Count)
                            {
                                if (bodoviPosmatraca[sPosmatrac] > 0)
                                {
                                    Igrac ciljaniIgrac = prijavljeniIgraci[indexIgraca - 1];
                                    bodoviIgraca[ciljaniIgrac.KorisnickoIme]++;
                                    bodoviPosmatraca[sPosmatrac]--;
                                    PosaljiStanjePosmatracima(new string(maskiranaRec), greske);

                                    Serijalizer.Send(sPosmatrac,
                                        $"Dodelili ste bod igraču {ciljaniIgrac.KorisnickoIme}. Preostalo: {bodoviPosmatraca[sPosmatrac]}");

                                    foreach (var sIgrac in soketiAktivnihIgraca.ToList())
                                         if (sIgrac.Connected)
                                            Serijalizer.Send(sIgrac, $"\n[PODRŠKA] Igrač {ciljaniIgrac.KorisnickoIme} dobio je bod podrške.");
                                   
                                }
                                else
                                {
                                    Serijalizer.Send(sPosmatrac, "Nemate više bodova podrške!");
                                }
                            }
                        }
                    }
                    catch
                    {
                        soketiPosmatraca.Remove(sPosmatrac);
                    }
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

                if (greske[igracPoteza] <= 0)
                {
                   
                    byte[] info = Serijalizer.Serialize("Potrošili ste sve pokušaje. Čekamo ostale...");
                    udpServer.SendTo(info, new IPEndPoint(IPAddress.Parse(igracPoteza.IpAdresa), igracPoteza.Port));

                   
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
                        byte[] greskaMsg = Serijalizer.Serialize("Pogresna duzina reci. Rec ima " + tajnaRec.Length + " slova.");
                        udpServer.SendTo(greskaMsg, udpKlijentEP);
                        continue;
                    }
                    else if (string.Equals(pokusaj.Trim(), tajnaRec, StringComparison.OrdinalIgnoreCase))
                    {
                        maskiranaRec = tajnaRec.ToCharArray();
                        Console.WriteLine("Rec je pogodjena.");

                        pogodjenaRec[igracPoteza] = true;
                        tacnaSlova[igracPoteza] = tajnaRec.Length;
                    }
                    else
                    {
                        greske[igracPoteza]--;
                        Console.WriteLine($"{igracPoteza.KorisnickoIme} je promašio celu reč.");
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

                PosaljiStanjePosmatracima(new string(maskiranaRec),greske); 

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
            udpServer.Close();
            Console.WriteLine("UDP server ugašen.");
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

    static void PosaljiStanjePosmatracima(string stanjeReci, Dictionary<Igrac, int> greske)
    {
        var sb = new StringBuilder();

        sb.AppendLine("===== STANJE IGRE =====");
        sb.AppendLine("Reč: " + stanjeReci);
        sb.AppendLine();

        sb.AppendLine("Igrači:");
        for (int i = 0; i < prijavljeniIgraci.Count; i++)
        {
            Igrac ig = prijavljeniIgraci[i];

            int podrska = bodoviIgraca.ContainsKey(ig.KorisnickoIme)
                ? bodoviIgraca[ig.KorisnickoIme]
                : 0;

            sb.AppendLine(
                $"{i + 1}. {ig.KorisnickoIme} | Greške: {greske[ig]} | Bodovi podrške: {podrska}"
            );
        }

        poslednjeStanjeZaPosmatraca = sb.ToString();

        foreach (var s in soketiPosmatraca.ToList())
        {
            try
            {
                if (s.Connected)
                    Serijalizer.Send(s, poslednjeStanjeZaPosmatraca);
            }
            catch
            {
                soketiPosmatraca.Remove(s);
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

        // Tabela
        var sb = new StringBuilder();
        sb.AppendLine("\n\n\n===== KRAJ IGRE =====");
        sb.AppendLine($"Pobednik: {pobednik}");
        sb.AppendLine($"Originalna reč: {originalnaRec}");

        sb.AppendLine("\nTABELA BODOVA:");
        sb.AppendLine("-----------------------------------------------------------------------");
        sb.AppendLine(string.Format("{0,-15} | {1,-12} | {2,-13} | {3,-16} | {4,-15}",
      "Igrač", "Tačna slova", "Pogođena reč", "Preostale greške", "Bodovi podrške"));
        sb.AppendLine(new string('-', 85)); 

        foreach (var ig in prijavljeniIgraci)
        {
            int podrska = 0;
            if (bodoviIgraca.ContainsKey(ig.KorisnickoIme))
                podrska = bodoviIgraca[ig.KorisnickoIme];

            sb.AppendLine(string.Format("{0,-15} | {1,-12} | {2,-13} | {3,-16} | {4,-15}",
                ig.KorisnickoIme,
                tacnaSlova[ig],
                (pogodjenaRec[ig] ? "DA" : "NE"),
                greske[ig],
                podrska));
        }

        sb.AppendLine();

        // Rang lista
        var rang = new List<Igrac>(prijavljeniIgraci);
        rang.Sort((x, y) =>
        {
            int bx = bodoviIgraca.ContainsKey(x.KorisnickoIme) ? bodoviIgraca[x.KorisnickoIme] : 0;
            int by = bodoviIgraca.ContainsKey(y.KorisnickoIme) ? bodoviIgraca[y.KorisnickoIme] : 0;

            int cmp = by.CompareTo(bx);
            if (cmp != 0) return cmp;
            return tacnaSlova[y].CompareTo(tacnaSlova[x]);
        });

        sb.AppendLine("\nRang lista:");
        string formatRanga = "{0,-5} | {1,-15} | {2,-15} | {3,-12}";

        sb.AppendLine(string.Format(formatRanga, "Rank", "Igrač", "Bodovi podrške", "Tačna slova"));
        sb.AppendLine(new string('-', 55)); 

        for (int i = 0; i < rang.Count; i++)
        {
            var ig = rang[i];
            int podrska = bodoviIgraca.ContainsKey(ig.KorisnickoIme) ? bodoviIgraca[ig.KorisnickoIme] : 0;

            sb.AppendLine(string.Format(formatRanga,
                (i + 1) + ".",
                ig.KorisnickoIme,
                podrska,
                tacnaSlova[ig]));
        }

        sb.AppendLine("======================================================");

        return sb.ToString();

    }

    static void PosaljiZavrsnuPorukuSvima(string poruka)
    {
        foreach (Socket s in soketiAktivnihIgraca.ToList())
        {
            try
            {
                if (s.Connected)
                {
                    Serijalizer.Send(s, poruka);
                    
                }
            }
            catch { }
        }

        foreach (Socket s in soketiPosmatraca.ToList())
        {
            try
            {
                if (s.Connected)
                {
                    Serijalizer.Send(s, poruka);
                   
                }
            }
            catch { }
        }

        Thread.Sleep(2000);
        soketiAktivnihIgraca.Clear();
        soketiPosmatraca.Clear();
        soketiIgraca.Clear();
        prijavljeniIgraci.Clear(); 
        igraPokrenuta = false;
        Console.WriteLine("[SERVER]: Igra je završena. Liste su očišćene.");
    }
}
