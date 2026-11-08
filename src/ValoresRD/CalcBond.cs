using System;
using System.Data;
using System.Linq;

namespace CalcBond
{
    public class Bono
    {
        public string Emisor { get; set; }
        public string ISIN { get; set; }
        public string Alias { get; set; }
        public string Moneda { get; set; }
        public string Tipo { get; set; }
        public string Metodo { get; private set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public double TasaCupon { get; set; }
        public int Periocidad { get; set; }
        public int Denominacion { get; set; }
        public double Yield { get; private set; }
        public DateTime FechaLiquidacion { get; set; }
        public long ValorNominal { get; set; }
        public double ValorTransado { get; private set; }
        public double PrecioLimpio { get; private set; }
        //Calctype integer: Method of calculation
        //Banco Central Certificados and old Notas = 1; Banco Central modern Notas = 2; Banco Central ICMA = 5
        //Ministerio de Hacienda 10 decimals = 3; Ministerio de Hacienda 4 decimals = 4
        public int Calctype { private get; set; }
        private int Flujos;
        private double rendimientoPeriodo;
        private DateTime[] FechasdePagos;
        private double[] numDiasEntrePagos;
        private double[] anoBase;
        private int NumeroPago;
        private int UltimoPago;
        private int pmtsRemaining;
        private double[] MontoPago { get; set; }
        private DateTime fechaUltimoPago;
        public int DiasAcumulados { get; private set; }
        public double CuponCorrido { get; private set; }
        public double RatioCupon { get; private set; }
        private double[] ValorPresenteFlujos { get; set; }
        private double ValorPresenteTotal { get; set; }
        private double EquationValorPresente;
        private double[] DerivativeValorPresente;
        private double SumDerivativeValorPresente;
        private double y; // y = (1+Rendimiento)        
        public double ValorCompra { get; private set; }
        public double PrecioSucio { get; private set; }
        public int DiasAVencimiento { get; private set; }
        public double MacaulayDuration { get; private set; }
        //private int i = 0;
        private DataTable PaymentsTable { get; set; }
        private bool ContainsError { get; set; }
        private string ErrorMessage { get; set; }
        public DateTime FirstPayment { get; set; }
        public bool Amortized { get; set; }
        public int PrincipalRepayments { get; set; }

        public Bono()
        {
            Yield = 0;
            PrecioLimpio = 0;
            ValorTransado = 0;
            ContainsError = false;
            ErrorMessage = "Error: ";
        }

        public static void SetBCPrecioLimpio(Bono original)
        {
            Bono newBond = (Bono)original.MemberwiseClone();
            newBond.ValorNominal = 1000000000;
            newBond.CalcularPorYield(original.Yield);
            original.PrecioLimpio = newBond.PrecioLimpio;
        }

        public bool ErrorExists()
        {
            return ContainsError;
        }

        public string GetErrorMessage()
        {
            return ErrorMessage;
        }

        public void SetErrorMessage(string error)
        {
            ErrorMessage = error;
            ContainsError = true;
        }

        public DataTable GetPaymentsTable()
        {
            return PaymentsTable;
        }

        public void CalcularPorYield(double tasaRend)
        {
            Yield = tasaRend;
            SetData();
            SetDiasAVencimiento();
            SetRendimientoPeriodo();
            SetValorPresenteFlujos();
            SetValorTransado();
            SetPreciosLimpioYSucio();
            //GenerateDataTable();
            //SetMacaulayDuration();
        }

        public void CalcularPorValorTransado(double valorTrans)
        {
            ValorTransado = valorTrans;
            SetData();
            SetDiasAVencimiento();
            SetPreciosLimpioYSucio();
            CalculateRendimiento();
            //GenerateDataTable();
            //SetMacaulayDuration();
        }

        public void CalcularPorPrecioLimpio(double precLimpio)
        {
            PrecioLimpio = precLimpio;
            SetData();
            SetDiasAVencimiento();
            ValorTransado = ((ValorNominal * PrecioLimpio) + CuponCorrido);
            ValorCompra = ValorTransado - CuponCorrido;
            SetPrecioSucio();
            CalculateRendimiento();
            //GenerateDataTable();
            //SetMacaulayDuration();
        }

        private string GetCalcType()
        {
            switch (Calctype)
            {
                case 1:
                    return "Actual/Actual";
                case 2:
                    return "Actual/Actual";
                case 4:
                    return "Actual/Actual";
                case 5:
                    return "ICMA";
                case 6:
                    return "Actual/365";
                case 7:
                    return "30/360";
                default:
                    return "Actual/Actual";
            }
        }

        public string UpdateDiasaVencimiento(DateTime fecha)
        {
            FechaLiquidacion = fecha.Date;
            SetDiasAVencimiento();
            return GetDiasAVencimiento().ToString();
        }

        public void GenerateDataTable()
        {
            int p = 0;
            PaymentsTable = new DataTable();
            PaymentsTable.Columns.Add("Flujo");
            PaymentsTable.Columns.Add("Fecha de Pago");
            PaymentsTable.Columns.Add("Dias Cupón");
            PaymentsTable.Columns.Add("Dias Acumulados");
            PaymentsTable.Columns.Add("Monto");
            PaymentsTable.Columns.Add("Valor Presente");
            while (p < pmtsRemaining)
            {
                PaymentsTable.Rows.Add();
                PaymentsTable.Rows[p][0] = (p + 1).ToString();
                PaymentsTable.Rows[p][1] = (FechasdePagos[p + NumeroPago]).ToString("dd-MMM-yyyy");
                PaymentsTable.Rows[p][2] = (numDiasEntrePagos[p + NumeroPago]).ToString();
                if ((p + NumeroPago == NumeroPago))
                {
                    PaymentsTable.Rows[p][3] = (numDiasEntrePagos[p + NumeroPago]).ToString();
                }
                else
                {
                    PaymentsTable.Rows[p][3] = ((numDiasEntrePagos[p + NumeroPago]) + Convert.ToDouble(PaymentsTable.Rows[p - 1][3])).ToString();
                }
                PaymentsTable.Rows[p][4] = Math.Round((MontoPago[p + (NumeroPago)]), 2).ToString("0,0.00");
                PaymentsTable.Rows[p][5] = Math.Round((ValorPresenteFlujos[p]), 2).ToString("0,0.00");
                p++;
            }
            
            //round to 4 decimals
            if (Calctype == 4 || Calctype == 8)
            {
                p = (pmtsRemaining - 1);
                PaymentsTable.Rows[p][4] = (/*(ValorNominal) +*/ Math.Round((MontoPago[p + (NumeroPago)]), 2)).ToString("0,0.00");
                PaymentsTable.Rows[p][5] = ((ValorPresenteFlujos[pmtsRemaining-1]) /*+ Math.Round((ValorPresenteFlujos[p]), 2)*/).ToString("0,0.00");
                p = (pmtsRemaining);
                PaymentsTable.Rows.Add();
            }
            //if (Calctype == 1 || Calctype == 2 || Calctype == 5 || Calctype == 7 || Calctype == 6)
            else
            {
                //PaymentsTable.Rows.Add();
                //PaymentsTable.Rows[pmtsRemaining][0] = (pmtsRemaining).ToString();
                //PaymentsTable.Rows[pmtsRemaining][1] = PaymentsTable.Rows[pmtsRemaining - 1][1];
                //PaymentsTable.Rows[pmtsRemaining][3] = PaymentsTable.Rows[pmtsRemaining - 1][3];
                //PaymentsTable.Rows[pmtsRemaining][4] = (ValorNominal).ToString("0,0.00");
                //PaymentsTable.Rows[pmtsRemaining][5] = (ValorPresenteFlujos[pmtsRemaining-1]).ToString("0,0.00");
                p = pmtsRemaining /*+ 1*/;
                // sum of the present values of the payments
                PaymentsTable.Rows.Add();
                PaymentsTable.Rows[p][5] = (ValorPresenteTotal).ToString("0,0.00");
            }
            PaymentsTable.Rows[p][1] = "Total";
            // sum of all the payments to collect            
            PaymentsTable.Rows[p][4] = PaymentsTable.AsEnumerable().Sum(t => Convert.ToDouble(t.Field<string>("Monto"))).ToString("0,0.00");
        }

        private void SetData()
        {
            SetNumofPayments();
            SetFechasdePagos();
            SetnumDiasEntrePagos();
            SetAnoBase();
            SetNumeroPago();
            SetMontoPago();
            Metodo = GetCalcType();
            ContainsError = !DataValid();
        }

        private bool DataValid()
        {
            if (FechaLiquidacion < FechaInicio || FechaLiquidacion > FechaVencimiento)
            {
                ErrorMessage += "La fecha de liquidación no puede ser menos que la fecha de emisión o mayor a la fecha de vencimiento.";
                return false;
            }

            if (ValorNominal % Denominacion != 0)
            {
                ErrorMessage += "El valor nominal no es un multiple de la denominacion.";
                return false;
            }

            if (Yield < 0)
            {
                ErrorMessage += "El yield no puede ser menos de 0.";
                return false;
            }

            if (PrecioLimpio < 0)
            {
                ErrorMessage += "El precio limpio no puede ser menos de 0.";
                return false;
            }

            if (ValorTransado < 0)
            {
                ErrorMessage += "El valor transado no puede ser menos de 0.";
                return false;
            }

            if (PrecioLimpio >= 2)
            {
                ErrorMessage += "El precio limpio no puede ser mayor de 200%.";
                return false;
            }

            if (((ValorTransado - CuponCorrido) / ValorNominal) >= 2)
            {
                ErrorMessage += "El precio limpio no puede ser mayor de 200%. Verifique el valor transado.";
                return false;
            }

            return true;
        }

        private void SetDiasAVencimiento()
        {
            if (Calctype == 7)
            {
                DiasAVencimiento = Numdays360(FechaLiquidacion, FechaVencimiento);
            }
            else
            {
                DiasAVencimiento = (FechaVencimiento - FechaLiquidacion).Days;
            }
        }

        private int GetDiasAVencimiento()
        {
            return DiasAVencimiento;
        }

        private void SetNumofPayments()
        {
            if (Calctype == 7)
            {
                DateTime tempDate = FirstPayment;
                Flujos = 0;
                while (tempDate < FechaVencimiento)
                {
                    Flujos++;
                    tempDate = tempDate.AddMonths(12 / Periocidad);
                }
                Flujos++;
            }
            else
            {
                Flujos = (((FechaVencimiento.Year - FechaInicio.Year) * 12) + (FechaVencimiento.Month - FechaInicio.Month)) / (12 / Periocidad);
            }
        }
        private void SetRendimientoPeriodo()
        {
            rendimientoPeriodo = Yield / Periocidad;
        }
        private void SetFechasdePagos()
        {
            FechasdePagos = new DateTime[Flujos];
            int i = 0;
            if (Calctype == 7)
            {
                while (i < Flujos - 1)
                {
                    FechasdePagos[i] = FirstPayment.AddMonths(i * (12 / Periocidad));
                    i++;
                }
                FechasdePagos[Flujos - 1] = FechaVencimiento;
            }
            else
            {
                while (i < Flujos)
                {
                    var tempDate = FechaInicio.AddMonths((i + 1) * (12 / Periocidad));
                    if (tempDate.AddMonths(12 / Periocidad) > FechaVencimiento)
                    {
                        FechasdePagos[i] = FechaVencimiento;
                    }
                    else
                    {
                        FechasdePagos[i] = tempDate;
                    }
                    i++;
                }
            }
        }

        private int Numdays360(DateTime startDate, DateTime endDate)
        {
            int StartDay = startDate.Day;
            int StartMonth = startDate.Month;
            int StartYear = startDate.Year;
            int EndDay = endDate.Day;
            int EndMonth = endDate.Month;
            int EndYear = endDate.Year;

            if (StartDay == 31 || IsLastDayOfFebruary(startDate))
            {
                StartDay = 30;
            }

            if (StartDay == 30 && EndDay == 31)
            {
                EndDay = 30;
            }

            return ((EndYear - StartYear) * 360) + ((EndMonth - StartMonth) * 30) + (EndDay - StartDay);
        }

        private bool IsLastDayOfFebruary(DateTime date)
        {
            return date.Month == 2 && date.Day == DateTime.DaysInMonth(date.Year, date.Month);
        }

        private void SetnumDiasEntrePagos()
        {
            numDiasEntrePagos = new double[Flujos];
            int i = 1;
            if (Calctype == 7)
            {
                numDiasEntrePagos[0] = Numdays360(FechaInicio, FirstPayment);
                while (i < Flujos)
                {
                    numDiasEntrePagos[i] = Numdays360(FechasdePagos[i - 1], FechasdePagos[i]);
                    i++;
                }
            }
            else
            {
                numDiasEntrePagos[0] = (FechasdePagos[0] - FechaInicio).TotalDays;

                while (i < (Flujos))
                {
                    numDiasEntrePagos[i] = (FechasdePagos[i] - FechasdePagos[i - 1]).TotalDays;
                    i++;
                }
            }
        }
        private void SetAnoBase()
        {
            anoBase = new double[Flujos];
            int i = 0;
            if (Calctype == 1 || Calctype == 5)
            {
                while (i < Flujos)
                {
                    if ((i + 1) % 2 == 0)
                    {
                        anoBase[i] = (numDiasEntrePagos[i] + numDiasEntrePagos[i - 1]);
                    }
                    else
                    {
                        anoBase[i] = (numDiasEntrePagos[i] + numDiasEntrePagos[i + 1]);
                    }
                    i++;
                }
            }
            if (Calctype == 2)
            {
                while (i < Flujos)
                {
                    // 01/2016 is less than 02/2016
                    // 03/2016 is greater than 02/2016
                    DateTime leapmonth = new DateTime(FechasdePagos[i].Year, 02, 28);
                    if ((DateTime.IsLeapYear(FechasdePagos[i].Year).Equals(true) && FechasdePagos[i] > leapmonth)
                        || (DateTime.IsLeapYear(FechasdePagos[i].AddMonths(-6).Year).Equals(true) && FechasdePagos[i] < leapmonth))
                    {
                        if (i == 0)
                        {
                            anoBase[i] = 365;
                        }
                        else
                        {
                            anoBase[i] = 366;
                        }

                    }
                    else
                    {
                        anoBase[i] = 365;
                    }
                    i++;
                }
            }

            if (Calctype == 4 || Calctype == 8)
            {
                while (i < Flujos)
                {
                    // 01/2016 is less than 02/2016
                    // 03/2016 is greater than 02/2016
                    DateTime leapmonth;
                    if (i == 0)
                    {
                        leapmonth = new DateTime(FechaInicio.Year, 02, 28);
                    }
                    else
                    {
                        leapmonth = new DateTime(FechasdePagos[i].Year, 02, 28);
                    }

                    if ((DateTime.IsLeapYear((FechasdePagos[i]).Year)) && (FechasdePagos[i] > leapmonth)
                        || (DateTime.IsLeapYear(FechasdePagos[i].AddMonths(-6).Year).Equals(true) && FechasdePagos[i] < leapmonth))
                    {
                        if ((i == 0) && (FechaInicio > leapmonth))
                        {
                            anoBase[i] = 365;
                        }
                        else
                        {
                            anoBase[i] = 366;
                        }
                    }
                    else
                    {
                        anoBase[i] = 365;
                    }
                    i++;
                }
            }

            if (Calctype == 7)
            {
                while (i < Flujos)
                {
                    anoBase[i] = 360;
                    i++;
                }
            }

            if (Calctype == 6)
            {
                while (i < Flujos)
                {
                    anoBase[i] = 365;                    
                    i++;
                }
            }

            if (Calctype == 9)
            {
                while (i < Flujos)
                {
                    anoBase[i] = 365;
                                            
                    DateTime leapmonth = new DateTime(FechasdePagos[i].Year, 02, 28);
                    if ((DateTime.IsLeapYear((FechasdePagos[i]).Year)) && (FechasdePagos[i] > leapmonth))
                    {
                        anoBase[i] = 366;
                    }                    
                    i++;
                }
            }
        }

        private void SetNumeroPago()
        {

            int i = 0;
            while (i < Flujos)
            {
                if (!((FechaLiquidacion >= FechasdePagos[Flujos - 2])))
                {
                    if ((FechaLiquidacion < FechasdePagos[0]) && (FechaLiquidacion >= FechaInicio))
                    {
                        NumeroPago = 0;
                        UltimoPago = -1;
                        break;
                    }
                    else if ((FechaLiquidacion >= FechasdePagos[i]) && (FechaLiquidacion < (FechasdePagos[i + 1])))
                    {
                        NumeroPago = i + 1;
                        UltimoPago = i;
                        break;
                    }
                }
                else
                {
                    NumeroPago = Flujos - 1;
                    UltimoPago = Flujos - 2;
                    break;
                }
                i++;
            }

            pmtsRemaining = Flujos - NumeroPago;
            SetFechaUltimoPagoYCuponCorrido();
        }

        private void SetMontoPago()
        {
            MontoPago = new double[Flujos];
            int i = 0;
            while (i < Flujos)
            {
                if (Calctype == 5)
                {
                    MontoPago[i] = (ValorNominal * TasaCupon * (1.00 / Convert.ToDouble(Periocidad)));
                }                
                else
                {
                    MontoPago[i] = (ValorNominal * TasaCupon * numDiasEntrePagos[i]) / (anoBase[i]);
                }
                
                i++;
            }
            if (Amortized)
            {
                int p = PrincipalRepayments;
                while (p > 0)
                {
                    MontoPago[(Flujos - (p * Periocidad)) + 1] += (ValorNominal / PrincipalRepayments);
                    p--;
                }
            }
            else
            {
                MontoPago[Flujos - 1] += ValorNominal;
            }
        }

        private void SetValorPresenteFlujos()
        {
            int i;            
            //ValorPresenteFlujos = new double[pmtsRemaining + 1];
            ValorPresenteFlujos = new double[pmtsRemaining];
            
            if (Calctype == 4 || Calctype == 8)
            {
                ValorPresenteFlujos[0] = ((MontoPago[NumeroPago] / (Math.Pow((1 + rendimientoPeriodo), (RatioCupon))))) - CuponCorrido;
                i = 1;
            }
            else
            {
                i = 0;
            }

            while (i < pmtsRemaining)
            {
                if ((i == (pmtsRemaining - 1)) && Calctype == 8)
                {
                    ValorPresenteFlujos[i] = (MontoPago[(NumeroPago) + i]) / (Math.Pow((1 + rendimientoPeriodo), (i + RatioCupon + (1.00/184.00))));
                }                
                else
                {
                    ValorPresenteFlujos[i] = (MontoPago[(NumeroPago) + i]) / (Math.Pow((1 + rendimientoPeriodo), (i + RatioCupon)));
                }                
                i++;
            }
            //ValorPresenteFlujos[pmtsRemaining] = ValorNominal / (Math.Pow((1 + rendimientoPeriodo), ((pmtsRemaining - 1) + RatioCupon)));
            ValorPresenteTotal = ValorPresenteFlujos.Sum();
            SetEquationValorPresenteFlujos();
        }

        public void SetMacaulayDuration()
        {
            double[] CashFlowsPV = new double[pmtsRemaining];
            for (int i = 0; i < pmtsRemaining; i++)
            {
                int CurrentPeriod = NumeroPago + i;
                double FactorDiasCupon = numDiasEntrePagos[CurrentPeriod] / anoBase[CurrentPeriod];
                double FactorDiasAlVencimiento = DiasAVencimiento / numDiasEntrePagos[CurrentPeriod];
                CashFlowsPV[i] = ((ValorNominal * (rendimientoPeriodo * FactorDiasCupon) + MontoPago[CurrentPeriod]) * FactorDiasAlVencimiento) / (Math.Pow((1 + (Yield * FactorDiasCupon)), (FactorDiasAlVencimiento + i)));
            }
            MacaulayDuration = CashFlowsPV.Sum() / ValorTransado;
        }

        private void SetEquationValorPresenteFlujos()
        {            
            if (Calctype == 4 || Calctype == 8)
            {
                EquationValorPresente = ValorPresenteTotal - ValorCompra;
            }
            else
            {
                EquationValorPresente = ValorPresenteTotal - ValorTransado;
            }

        }

        private void SetDerivativeEquationValorPresente()
        {
            DerivativeValorPresente = new double[pmtsRemaining + 1];
            double exponent;
            double exponentPlusOne;
            
            for (int i = 0; i < pmtsRemaining; i++)
            {                
                exponent = (i + RatioCupon) * (-1);
                exponentPlusOne = (i + RatioCupon + 1);
                DerivativeValorPresente[i] = ((MontoPago[(NumeroPago) + i]) * exponent) / (Math.Pow(y, exponentPlusOne));
            }

            DerivativeValorPresente[pmtsRemaining] = ((ValorNominal * ((pmtsRemaining - 1) + RatioCupon) * (-1)) / (Math.Pow(y, (pmtsRemaining + RatioCupon))));

            SumDerivativeValorPresente = DerivativeValorPresente.Sum();            
        }

        private void SetFechaUltimoPagoYCuponCorrido()
        {
            if (UltimoPago == -1)
            {
                fechaUltimoPago = FechaInicio;
            }
            else
            {
                fechaUltimoPago = FechasdePagos[UltimoPago];
            }
            if (Calctype == 7)
            {
                DiasAcumulados = Numdays360(fechaUltimoPago, FechaLiquidacion);
            }
            else
            {
                DiasAcumulados = (FechaLiquidacion - fechaUltimoPago).Days;
            }
            RatioCupon = (numDiasEntrePagos[NumeroPago] - DiasAcumulados) / (numDiasEntrePagos[NumeroPago]);
            
            SetCuponCorrido();
        }

        private void SetCuponCorrido()
        {
            if (Calctype == 5)
            {
                CuponCorrido = (ValorNominal * TasaCupon * DiasAcumulados * (1.00 / Convert.ToDouble(Periocidad))) / (numDiasEntrePagos[NumeroPago]);
            }
            else
            {
                CuponCorrido = (ValorNominal * TasaCupon * DiasAcumulados) / (anoBase[NumeroPago]);
            }

            CuponCorrido = Math.Round(CuponCorrido, 2);            
        }

        private void CalculateRendimiento()
        {
            int n = NumeroPago;
            double total = 0.00;

            while (n < (MontoPago.Length))
            {
                total += MontoPago[n];
                n++;
            }

            //if (Calctype == 1 || Calctype == 2 || Calctype == 5 || Calctype == 4 || Calctype == 8)
            //{
            total += ValorNominal;
            //}

            double diff = total - ValorTransado;

            if ((diff < 0) && (pmtsRemaining < 2))
            {
                double estimateReturn = (diff / ValorTransado);
                y = ((((estimateReturn / DiasAVencimiento) * 365) / 2) + 1);
            }
            else
            {
                double multiple = (1.00 / PrecioLimpio);
                //y = (TasaCupon / Periocidad) + 1;
                y = ((TasaCupon*multiple) / Periocidad) + 1;
            }
            // Determine if security is Ministerio de Hacienda with one payment remaining
            double valorTransadoType;
            if ((Calctype == 4 || Calctype == 8) && pmtsRemaining == 1)
            {
                valorTransadoType = ValorTransado - CuponCorrido;
            }
            else
            {
                valorTransadoType = ValorTransado;
            }

            int j = 0;
            while (j < 1000)
            {
                rendimientoPeriodo = y - 1;
                Yield = rendimientoPeriodo * Periocidad;
                SetValorPresenteFlujos();
                SetEquationValorPresenteFlujos();
                SetDerivativeEquationValorPresente();
                double NewtonRaphson = y - (EquationValorPresente / SumDerivativeValorPresente);
                if (valorTransadoType == Math.Round(ValorPresenteTotal, 12) || Double.IsNaN(NewtonRaphson))
                {
                    break;
                }
                else
                {
                    y = NewtonRaphson;
                    j++;
                }
            }
        }

        private void SetValorTransado()
        {            
            if (Calctype == 4 || Calctype == 8)
            {
                ValorTransado = Math.Round((ValorPresenteTotal / ValorNominal), 6) * ValorNominal + CuponCorrido;
            }
            else
            {
                ValorTransado = ValorPresenteTotal;
            }
        }

        private void SetPrecioLimpio()
        {
            ValorCompra = ValorTransado - CuponCorrido;

            if (Calctype == 4 || Calctype == 8)
            {
                PrecioLimpio = Math.Round((ValorCompra / ValorNominal), 6);
            }
            else
            {
                PrecioLimpio = Math.Round(((ValorCompra) / ValorNominal), 10);
            }
        }

        private void SetPreciosLimpioYSucio()
        {
            SetPrecioLimpio();
            SetPrecioSucio();
        }

        private void SetPrecioSucio()
        {
            if (Calctype == 4 || Calctype == 8)
            {
                PrecioSucio = Math.Round((ValorTransado / ValorNominal), 6);
            }
            else
            {
                PrecioSucio = Math.Round((ValorTransado / ValorNominal), 10);
            }
        }
    }
}
