# ValoresRD

A restored C# bond calculator originally built around 2018 with ASP.NET Web API on .NET Framework 4.6.1, SQL Server, and a separately hosted PHP frontend. This edition runs as a single ASP.NET Core application with a JSON catalog. The frontend reuses the original HTML layout, Bootstrap/Lux theme, custom CSS, calculation radio buttons, and results tables, connected to the new API through plain JavaScript. Original analytics and obsolete remote API calls are omitted.

## Run locally

Install the .NET 8 SDK, then run from the repository root:

```sh
dotnet run --project src/ValoresRD --urls http://localhost:8080
```

Open http://localhost:8080. No database, credentials, PHP, or external NuGet packages are required.

## Containers

With Docker running in Linux container mode:

```sh
docker compose up --build
```

Open http://localhost:8080. The runtime runs as the image's unprivileged `app` user. Alternatively, open this folder in VS Code with Dev Containers, choose **Reopen in Container**, and run the local command above. The dev container forwards port 8080.

## What was preserved

The original bond models and `CurrentBond.cs` remain for reference. Only unused framework-specific `using` directives were removed from the model files. The new API adapter constructs `Bono` directly to avoid ambiguous lookups in the historical catalog; it follows the original calculation sequence, including clean-price normalization, payment table generation, and duration calculation.

The active application replaces the old IIS host, Windows Forms administration tool, and PHP frontend. There are no subscriber accounts, database writes, visitor tracking, or outgoing email in the restored application. The obsolete backend and frontend folders, deployment profiles, embedded credentials, vendored packages, and generated artifacts have been removed. The original calculation engine, reference models, and frontend theme are retained in `src/ValoresRD/`.

## Historical data and compatibility

The JSON catalog contains 70 historical bonds: 49 Banco Central bonds from `Central.xml` and 21 Hacienda bonds from `Hacienda.xml`, supplied from the original BondImporter project. These replace the initial eight-bond VLP sample. All ISINs in this catalog are unique; catalog IDs combine issuer and the source series ID. Source series IDs (used as display titles), ISINs, dates, coupon rates, frequencies, denominations, currencies, and calculation-method values are retained. The importer also normalizes `Pesos` to `DOP` for older exports.

The Hacienda XML uses calculation method **3**, described in the old engine as the ten-decimal convention. This engine version implements Hacienda method **4** (four-decimal convention) and has no method-3 cash-flow branch. The adapter maps 3 to 4 without changing the source JSON or engine. Results therefore use that later rounding convention; they are not a reproduction of the older ten-decimal implementation.

The frontend starts with a historical settlement date and adjusts it for the selected bond. API inputs must fall strictly between issue and maturity; nominal amounts must be positive denomination multiples. Yield input is expressed as a percentage and currently limited to `(0, 100]`. Price input is a percentage; transaction value is a currency amount.

These are historical demonstration data, not current market prices or financial advice. Calculations have not been independently certified. Extreme price or transaction-value inputs can exceed the legacy solver's numerical capabilities.

To regenerate the catalog with PowerShell 7:

```powershell
./tools/Import-Bonds.ps1 -SourceDirectory '/path/to/BondImporter'
```

The source XML files remain outside this repository; the generated JSON is included. The importer expects `Central.xml` and `Hacienda.xml` in the supplied directory. Older VLP inputs can be selected with `-CentralFile` and `-HaciendaFile`. Restart the application after importing to reload the catalog.

## Calculation formulas

These equations describe the implementation in [`CalcBond.cs`](src/ValoresRD/CalcBond.cs), including its historical conventions and limitations. They are not a certification of market-standard valuation. GitHub renders the mathematical notation below.

### Notation and method selection

| Symbol | Meaning |
| --- | --- |
| $N$ | Nominal amount passed to the engine |
| $c$ | Annual coupon rate as a decimal, e.g. $0.0575$ |
| $y$ | Nominal annual yield as a decimal |
| $m$ | Coupon payments per year (`Periocidad`) |
| $t_0,t_1,\ldots,t_L$ | Issue date and scheduled payment dates |
| $s$ | Settlement date |
| $k$ | Index of the next payment; the previous date is $t_{k-1}$ |
| $n=L-k+1$ | Number of remaining payments |
| $d_i$ | Day count from $t_{i-1}$ to $t_i$ |
| $B_i$ | Annual day-count denominator for period $i$ |
| $a$ | Day count from $t_{k-1}$ to settlement |
| $C_i,R_i,F_i$ | Coupon, principal repayment, and total cash flow $F_i=C_i+R_i$ |
| $A$ | Accrued interest (`CuponCorrido`) |
| $V,Q$ | Transaction value (`ValorTransado`) and clean purchase amount (`ValorCompra`) |
| $p_c,p_d$ | Clean and dirty prices as ratios of nominal; multiply by 100 to display percentages |
| $\mathrm{round}_q(x)$ | .NET `Math.Round(x, q)`, using midpoint-to-even rounding |

The issuer's name does not select a unique formula: `CalculationMethod` does. The current catalog has 14 Banco Central bonds using method 1, 28 using method 2, seven using method 5, and 21 Hacienda bonds stored as method 3 and adapted to method 4. The retained MHUSD, HAINA, DOMINICANPOWER, and REMIX models are not entries in this catalog. Their methods and repayment terms must come from their issue records; they cannot be inferred from the issuer alone.

| Method | Engine convention | Coupon $C_i$ | Annual denominator $B_i$ |
| --- | --- | --- | --- |
| 1 | Banco Central Actual/Actual, paired periods | $Nc\,d_i/B_i$ | Sum of actual days in the paired coupon periods |
| 2 | Banco Central Actual/Actual, calendar rule | $Nc\,d_i/B_i$ | 365 or 366 under the legacy leap-year rule below |
| 3 | Historical Hacienda convention | Not independently implemented in this engine | API maps to method 4 |
| 4 | Hacienda Actual/Actual | $Nc\,d_i/B_i$ | 365 or 366 under the Hacienda rule below |
| 5 | Banco Central ICMA branch | $Nc/m$ | Paired-period denominator, although coupons and accrual use $m$ directly |
| 6 | Actual/365 branch | $Nc\,d_i/365$ | 365 |
| 7 | USD 30/360 branch | $Nc\,d_i/360$ | 360 |
| 8 | Special Hacienda branch | Same as method 4 | Same as method 4, plus a final-payment discount adjustment |
| 9 | Additional legacy Actual/Actual branch | $Nc\,d_i/B_i$ | 366 if payment year is leap and payment is after February 28; otherwise 365 |

Method 9 exists in the engine but is absent from the `CalculationMethods` enum. The UI's `Bono`, `Nota`, and `Certificado` labels are instrument descriptions; they do not introduce separate pricing equations.

### Dates, day counts, and accrued interest

For methods other than 7, the engine derives the number of payments from whole months between issue and maturity, divided by $12/m$. It generates dates forward from issue in $12/m$-month increments and substitutes maturity for the final date. Method 7 instead generates dates from the explicit `FirstPayment`, then appends maturity. These rules do not constitute general support for irregular coupon schedules.

Methods other than 7 use actual calendar days for $d_i$ and $a$. Method 7 uses the following implemented 30/360 function:

$$
D_{360}(u,v)=360(Y_v-Y_u)+30(M_v-M_u)+(D'_v-D'_u).
$$

Here $D'_u=30$ if the start date is the 31st or the last day of February; otherwise it is the original start day. The end day becomes 30 only if the adjusted start day is 30 and the original end day is 31. Other end days are unchanged. This is the code's specific rule, not a claim that all 30/360 variants are interchangeable.

For methods 1 and 5, periods are paired from issue:

$$
B_{2j-1}=B_{2j}=d_{2j-1}+d_{2j}.
$$

This implementation assumes complete pairs and is principally suited to the semiannual schedules for which it was written.

For methods 2, 4, and 8, define $H_i$ as February 28 in the payment year, and the predicate

$$
E_i=\bigl[\mathrm{leap}(\mathrm{year}(t_i))\land t_i>H_i\bigr]
\lor\bigl[\mathrm{leap}(\mathrm{year}(t_i-6\text{ months}))\land t_i<H_i\bigr].
$$

Method 2 sets $B_i=366$ when $E_i$ is true and $i>1$; otherwise $B_i=365$. Methods 4 and 8 use February 28 of the **issue year** for $H_1$, and the payment-year date for subsequent periods. They set $B_i=366$ when $E_i$ is true, except that the first period uses 365 if issue is after $H_1$; otherwise they use 365. The fixed six-month lookback and these exceptions are historical code behavior, not a general Actual/Actual definition.

The remaining fraction of the current coupon period and the periodic discount base are

$$
\alpha=\frac{d_k-a}{d_k},\qquad z=1+\frac{y}{m}.
$$

Accrued interest is rounded to cents before pricing:

$$
A=\begin{cases}
\mathrm{round}_2\!\left(\dfrac{Nc\,a}{m\,d_k}\right),&\text{method 5},\\
\mathrm{round}_2\!\left(\dfrac{Nc\,a}{B_k}\right),&\text{other methods}.
\end{cases}
$$

### Price from yield

For methods 1, 2, 5, 6, 7, and 9, the engine discounts remaining cash flows in coupon-period units:

$$
V(y)=\sum_{j=0}^{n-1}\frac{F_{k+j}}{z^{j+\alpha}},\qquad Q=V-A.
$$

For method 4, the first discounted flow has accrued interest subtracted, so the summed present value is a **clean amount**:

$$
S_4(y)=\sum_{j=0}^{n-1}\frac{F_{k+j}}{z^{j+\alpha}}-A,
\qquad
V=N\mathrm{round}_6\!\left(\frac{S_4(y)}{N}\right)+A.
$$

Method 8 uses the same clean-amount treatment, but adds a hard-coded $1/184$ of a coupon period to the final flow's exponent:

$$
S_8(y)=\sum_{j=0}^{n-1}\frac{F_{k+j}}{z^{e_j}}-A,
\qquad
e_j=j+\alpha+\frac{\mathbf{1}_{\{j=n-1,\ j\ge1\}}}{184}.
$$

The $j\ge1$ condition reflects the actual control flow: with just one remaining payment, the first-payment branch takes precedence and the extra offset is not applied. Method 8 uses the same transaction-value rounding as method 4. The offset is a preserved special case whose contractual justification has not been verified.

Prices are then

$$
p_c=\mathrm{round}_q\!\left(\frac{V-A}{N}\right),\qquad
p_d=\mathrm{round}_q\!\left(\frac{V}{N}\right).
$$

Use $q=6$ for methods 4 and 8, and $q=10$ for all other methods.

Six decimal places in a **price ratio** correspond to four decimal places in the displayed percentage price.

For calculation by yield, the API also preserves `SetBCPrecioLimpio`: it recalculates a shallow clone with $N=10^9$ and copies back only $p_c$. Despite its name, the original call applies to every method. Because accrued interest is rounded to cents, this normalization can make the returned clean price differ slightly from $(V-A)/N$ at the user's nominal amount.

### Yield from clean price or transaction value

For input clean-price ratio $p_c$, the engine first sets

$$
V=Np_c+A,\qquad Q=V-A.
$$

For an input transaction amount $V$, it derives clean and dirty prices using the rounding rules above. It then seeks a root in $z=1+y/m$:

$$
f(z)=\begin{cases}
S_4(z)-Q,&\text{method 4},\\
S_8(z)-Q,&\text{method 8},\\
\displaystyle\sum_{j=0}^{n-1}F_{k+j}z^{-(j+\alpha)}-V,&\text{other methods}.
\end{cases}
$$

The iteration has the Newton–Raphson form

$$
z_{r+1}=z_r-\frac{f(z_r)}{D_{\mathrm{legacy}}(z_r)},\qquad y=m(z-1),
$$

but its implemented derivative is

$$
D_{\mathrm{legacy}}(z)=
-\sum_{j=0}^{n-1}(j+\alpha)F_{k+j}z^{-(j+\alpha+1)}
-N(n-1+\alpha)z^{-(n+\alpha)}.
$$

The final term is extra: principal is already present in the payment array. The derivative also omits method 8's exponent adjustment. These are known inconsistencies, not recommended numerical formulas. The usual initial estimate is $z_0=1+c/(mp_c)$; a special single-payment, negative-total-return case uses a 365-day estimate and a hard-coded divisor of 2 instead.

The loop stops after at most 1,000 iterations, on a `NaN` next estimate, or when the target equals the present value rounded to 12 decimal places. For methods 4 and 8, its stopping target is $V$ with multiple payments and $V-A$ with one payment, even though the root equation uses the clean amount $Q$. There is no reliable residual-tolerance or convergence-status check. Round-trip tests demonstrate internal consistency for tested inputs, not independent accuracy.

### USD and corporate amortizing bonds

For a bullet bond, the engine uses $R_L=N$ and $R_i=0$ before maturity. For an amortizing bond with $r$ principal repayments, it instead adds $N/r$ at zero-based array indices

$$
\ell_p=L-pm+1,\qquad p=r,r-1,\ldots,1.
$$

This index rule is not a general repayment schedule: it aligns annual installments with the last coupon of each year for semiannual payments, but not for arbitrary frequencies. Coupons still use the original $N$, without reducing it after repayments. The retained MHUSD, HAINA, DOMINICANPOWER, and REMIX paths share this behavior when their `Amortized` flag is enabled.

For comparison, a bond whose terms specify interest on outstanding principal would require the contractual schedule

$$
O_i=O_{i-1}-R_i,\qquad
C_i=O_{i-1}c\,\delta_i,\qquad F_i=C_i+R_i,
$$

where $O_{i-1}$ is principal outstanding before payment $i$ and $\delta_i$ is the contractual accrual fraction. This is a **description of the required correction**, not the current implementation. Settlement price must also be interpreted on the correct original-principal or outstanding-principal basis. In particular, the historical Haina 5.75% May 27, 2020 case should not be considered validated by the restored catalog's tests.

### Displayed duration

The field named `MacaulayDuration` also preserves a legacy formula. With $T$ the day count from settlement to maturity, define $h_j=d_{k+j}/B_{k+j}$ and $b_j=T/d_{k+j}$. The code computes

$$
D_{\mathrm{legacy}}=\frac{1}{V}\sum_{j=0}^{n-1}
\frac{\left[N(y/m)h_j+F_{k+j}\right]b_j}
{(1+yh_j)^{b_j+j}}.
$$

This is not the standard present-value-weighted time-to-payment definition of Macaulay duration, and its displayed value should not be treated as a verified duration in years. It does not feed back into price or yield calculations.

## Verify

With the application running on port 5080:

```powershell
dotnet build src/ValoresRD -c Release
# In another terminal: dotnet run --project src/ValoresRD --urls http://localhost:5080
./tools/Test-Smoke.ps1
```

The smoke checks cover all 70 bonds, all three calculation modes, price/yield and transaction-value/yield round trips, payment schedules, invalid IDs, matured settlement rejection, frontend HTTP availability, and the original engine hash. These checks validate integration and consistency, not independent financial correctness.

Local build and smoke checks passed during restoration. Docker execution and visual browser QA have not been verified in the restoration environment. Nothing has been deployed or committed.

## Layout

```text
src/ValoresRD/           ASP.NET Core host, original C# engine and models
src/ValoresRD/Data/      Historical JSON catalog
src/ValoresRD/wwwroot/   Static single-page frontend
tools/                  XML import and smoke checks
.devcontainer/          Linux development environment
```

Before publishing, review the source and data, and choose an appropriate license. Any credentials formerly embedded in the old project should be rotated if still active.

