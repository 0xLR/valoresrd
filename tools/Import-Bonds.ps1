param(
    [Parameter(Mandatory)][string]$SourceDirectory,
    [string]$CentralFile = 'Central.xml',
    [string]$HaciendaFile = 'Hacienda.xml'
)
$ErrorActionPreference = 'Stop'
$specs = @(
    @{ File = $CentralFile; Issuer = 'Banco Central'; Prefix = 'bc' },
    @{ File = $HaciendaFile; Issuer = 'Ministerio de Hacienda'; Prefix = 'mh' }
)
$catalog = foreach ($spec in $specs) {
    [xml]$xml = Get-Content -LiteralPath (Join-Path $SourceDirectory $spec.File) -Raw
    if ($xml.SelectNodes('//Serie').Count -eq 0) { throw "No bond series in $($spec.File)" }
    foreach ($s in $xml.SelectNodes('//Serie')) {
        $issue = [datetime]::new([int]$s.Inicio_Ano, [int]$s.Inicio_Mes, [int]$s.Inicio_Dia)
        $maturity = [datetime]::new([int]$s.Venc_Ano, [int]$s.Venc_Mes, [int]$s.Venc_Dia)
        if ($maturity -le $issue -or [int]$s.Denominacion -le 0 -or [int]$s.Periocidad -le 0 -or
            [string]::IsNullOrWhiteSpace($s.id) -or [string]::IsNullOrWhiteSpace($s.ISIN)) {
            throw "Invalid bond data: $($s.id)"
        }
        [ordered]@{
            Id = "$($spec.Prefix)-$($s.id)"; Issuer = $spec.Issuer
            Bond = [ordered]@{
                ISIN = [string]$s.ISIN; Alias = [string]$s.id; Type = [string]$s.Tipo
                IssueDate = $issue.ToString('yyyy-MM-dd'); MaturityDate = $maturity.ToString('yyyy-MM-dd')
                CouponRate = [double]::Parse($s.Tasa, [cultureinfo]::InvariantCulture)
                Frequency = [int]$s.Periocidad; Denomination = [int]$s.Denominacion
                Currency = $(if ($s.Moneda -eq 'Pesos') { 'DOP' } else { [string]$s.Moneda })
                CalculationMethod = [int]$s.CalcType
            }
        }
    }
}
if (($catalog.Id | Select-Object -Unique).Count -ne $catalog.Count) { throw 'Duplicate catalog IDs' }
$catalog | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 (Join-Path $PSScriptRoot '../src/ValoresRD/Data/bonds.json')
Write-Output "Imported $($catalog.Count) historical bonds."
