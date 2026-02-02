
[Serializable]
class Igrac
{
    public string ime {get; set;} = "";
    public string korisnickoIme {get; set;} = "";
    public string ipAdresa {get; set;} = "";
    public int port {get; set;} = 0;
    public TipIgraca tipPrijave {get; set;}
}