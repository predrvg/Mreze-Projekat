class Igra
{
    public string ImePrvogIgraca {get; set;} = "";
    public string ImeDrugogIgraca {get; set;} = "";
    public int Trajanje {get; set;} = 0;
    public int DuzinaReci{get; set;} = 0;
    public int BrojDozvoljenihGresaka {get; set;} = 0;

    public Igra(string imePrvogIgraca, string imeDrugogIgraca, int trajanje, int duzinaReci, int brojDozvoljenihGresaka)
    {
        ImePrvogIgraca = imePrvogIgraca;
        ImeDrugogIgraca = imeDrugogIgraca;
        Trajanje = trajanje;
        DuzinaReci = duzinaReci;
        BrojDozvoljenihGresaka = brojDozvoljenihGresaka;
    }

    public Igra()
    {
    }
}