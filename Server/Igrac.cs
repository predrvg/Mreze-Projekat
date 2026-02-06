class Igrac
{
    public string Ime {get; set;} = "";
    public string KorisnickoIme {get; set;} = "";
    public string IpAdresa {get; set;} = "";
    public int Port {get; set;} = 0;
    public TipIgraca TipPrijave {get; set;}

    public Igrac()
    {
    }
    public Igrac(string ime, string korisnickoIme, string ipAdresa, int port, TipIgraca tipPrijave)
    {
        Ime = ime;
        KorisnickoIme = korisnickoIme;
        IpAdresa = ipAdresa;
        Port = port;
        TipPrijave = tipPrijave;
    }
}