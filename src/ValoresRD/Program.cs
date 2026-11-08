using System.Data;
using ValoresRD.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
var app = builder.Build();
Utility.Load(Path.Combine(app.Environment.ContentRootPath, "Data"));
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/bonds", () => Utility.Catalog);
app.MapGet("/health", () => Results.Ok(new { status = "ok", bonds = Utility.Catalog.Count }));
app.MapPost("/api/calculate", (CalculationRequest request) =>
{
    var entry = Utility.Catalog.SingleOrDefault(b => b.Id == request.Id);
    if (entry == null) return Results.BadRequest(new { error = "Choose a bond from the catalog." });
    var bond = entry.Bond;
    if (request.Settlement.Date <= bond.IssueDate || request.Settlement.Date >= bond.MaturityDate)
        return Results.BadRequest(new { error = "Settlement must be after issue and before maturity." });
    if (request.Nominal <= 0 || request.Nominal % bond.Denomination != 0 || request.Mode is < 1 or > 3 ||
        !double.IsFinite(request.Value) || request.Value <= 0 || (request.Mode == 1 && request.Value > 100))
        return Results.BadRequest(new { error = "Use a positive value, yield up to 100%, and a nominal amount in denomination multiples." });
    // Use the selected catalog entry directly: historic XML ISINs are placeholders and repeat.
    var result = new CalcBond.Bono
    {
        Emisor = entry.Issuer, ISIN = bond.ISIN, Alias = bond.Alias, Moneda = bond.Currency,
        Tipo = bond.Type, FechaInicio = bond.IssueDate, FechaVencimiento = bond.MaturityDate,
        TasaCupon = bond.CouponRate / 100, Periocidad = bond.Frequency, Denominacion = bond.Denomination,
        // XML method 3 predates this engine; its Hacienda implementation is method 4.
        Calctype = bond.CalculationMethod == 3 ? 4 : bond.CalculationMethod,
        FechaLiquidacion = request.Settlement.Date, ValorNominal = request.Nominal
    };
    try
    {
        if (request.Mode == 1) { result.CalcularPorYield(request.Value / 100); CalcBond.Bono.SetBCPrecioLimpio(result); }
        if (request.Mode == 2) result.CalcularPorPrecioLimpio(request.Value / 100);
        if (request.Mode == 3) result.CalcularPorValorTransado(request.Value);
        if (result.ErrorExists()) return Results.BadRequest(new { error = result.GetErrorMessage() });
        result.GenerateDataTable();
        result.SetMacaulayDuration();
        var table = result.GetPaymentsTable();
        var payments = table.Rows.Cast<DataRow>().Select(r => r.ItemArray.Select(v => v?.ToString()).ToArray());
        return Results.Ok(new { result, columns = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName), payments });
    }
    catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or ArithmeticException)
    {
        return Results.BadRequest(new { error = "The historical engine could not calculate this combination." });
    }
});
app.Run();

public sealed record CalculationRequest(string Id, DateTime Settlement, int Nominal, int Mode, double Value);
