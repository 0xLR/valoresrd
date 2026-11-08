param([string]$BaseUrl = 'http://127.0.0.1:5080')
$ErrorActionPreference = 'Stop'
function Assert($condition, $message) { if (!$condition) { throw $message } }
$catalog = Invoke-RestMethod "$BaseUrl/api/bonds"
$expectedCatalog = Get-Content (Join-Path $PSScriptRoot '../src/ValoresRD/Data/bonds.json') -Raw | ConvertFrom-Json
Assert ($catalog.Count -eq $expectedCatalog.Count) 'API catalog count differs from JSON; restart the application after importing'
Assert ((@($catalog.id | Sort-Object) -join ',') -eq (@($expectedCatalog.id | Sort-Object) -join ',')) 'API catalog IDs differ from JSON'
foreach ($entry in $catalog) {
    $request = @{ id=$entry.id; settlement=([datetime]$entry.bond.IssueDate).AddDays(30).ToString('yyyy-MM-dd'); nominal=1000000; mode=1; value=10 }
    $result = Invoke-RestMethod "$BaseUrl/api/calculate" -Method Post -ContentType application/json -Body ($request | ConvertTo-Json)
    Assert ($result.result.precioLimpio -gt 0) "Invalid price: $($entry.id)"
    Assert ($result.payments.Count -gt 0) "Missing cash flows: $($entry.id)"
    $request.mode=2; $request.value=$result.result.precioLimpio*100
    $reverse = Invoke-RestMethod "$BaseUrl/api/calculate" -Method Post -ContentType application/json -Body ($request | ConvertTo-Json)
    Assert ([math]::Abs($reverse.result.yield - 0.1) -lt 0.0001) "Price/yield round trip failed: $($entry.id)"
    $request.mode=3; $request.value=$result.result.valorTransado
    $reverse = Invoke-RestMethod "$BaseUrl/api/calculate" -Method Post -ContentType application/json -Body ($request | ConvertTo-Json)
    Assert ([math]::Abs($reverse.result.yield - 0.1) -lt 0.0001) "Amount/yield round trip failed: $($entry.id)"
    Write-Output "PASS $($entry.id): yield, price, amount, cash flows"
}
$request.id='missing'
try { Invoke-RestMethod "$BaseUrl/api/calculate" -Method Post -ContentType application/json -Body ($request|ConvertTo-Json); throw 'Unknown ID accepted' }
catch { Assert ($_.Exception.Response.StatusCode.value__ -eq 400) 'Unknown ID must return 400' }
$request.id=$catalog[0].id; $request.settlement=$catalog[0].bond.maturityDate
try { Invoke-RestMethod "$BaseUrl/api/calculate" -Method Post -ContentType application/json -Body ($request|ConvertTo-Json); throw 'Matured bond accepted' }
catch { Assert ($_.Exception.Response.StatusCode.value__ -eq 400) 'Maturity must return 400' }
Assert ((Invoke-WebRequest $BaseUrl).StatusCode -eq 200) 'Frontend unavailable'
$expected='16D7EEE71C930EE230825CBD8414CE5656E40155AF54F0ECC7C8297868C541C5'
Assert ((Get-FileHash (Join-Path $PSScriptRoot '../src/ValoresRD/CalcBond.cs')).Hash -eq $expected) 'Original algorithm file changed'
Write-Output 'PASS input validation, frontend HTTP, original engine SHA-256'
