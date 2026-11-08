using System.Text.Json;

namespace ValoresRD.Models;

// File-backed catalog replaces SQL, subscriber accounts, and visitor email logging.
public static class Utility
{
    public static List<Bond> AllBonds { get; private set; } = new();
    public static string ErrorMessage { get; set; }
    public static List<CatalogBond> Catalog { get; private set; } = new();

    public static void Load(string directory)
    {
        Catalog = JsonSerializer.Deserialize<List<CatalogBond>>(
            File.ReadAllText(Path.Combine(directory, "bonds.json")));
        AllBonds = Catalog.Select(c => c.Bond).ToList();
    }
}

public sealed class CatalogBond
{
    public string Id { get; set; }
    public string Issuer { get; set; }
    public Bond Bond { get; set; }
}
