using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CalcBond;

namespace ValoresRD.Models
{
    public class CurrentBond
    {
        private int ValorNominal { get; set; }
        private string Emisor { get; set; }
        private string ISIN { get; set; }
        private int CalcularPor { get; set; }
        private DateTime FechaLiquidacion { get; set; }
        private double CalcVariable { get; set; } // precio limpio, valorTransado, rendimiento
        public Bono Resultados { get; private set; }        
        private const string BANCO_CENTRAL = "Banco Central";
        private const string HACIENDA = "Ministerio de Hacienda";
        private const string HACIENDAUSD = "Ministerio de Hacienda USD";

        public CurrentBond(string emisor, string ISIN, DateTime fechaLiquidacion, int valorNominal, double calcVariable, int calculateBy)
        {
            this.Emisor = emisor;
            this.ISIN = ISIN;
            this.FechaLiquidacion = fechaLiquidacion;
            this.ValorNominal = valorNominal;
            this.CalcVariable = calcVariable;
            this.CalcularPor = calculateBy;
            Calculate();
            if (CalcularPor == 1)
            {
                Bono.SetBCPrecioLimpio(Resultados);
            }
            Resultados.GenerateDataTable();
            Resultados.SetMacaulayDuration();
        }

        public CurrentBond(string ISIN, DateTime fechaLiquidacion, double calcVariable, int calculateBy)
        {
            this.ISIN = ISIN;
            this.FechaLiquidacion = fechaLiquidacion;
            this.ValorNominal = 1000000000;
            this.CalcVariable = calcVariable;
            this.CalcularPor = calculateBy;
            Calculate();
        }

        public void Calculate()
        {            
            Resultados = new Bono();
            Bond bond = new Bond();

            bond = Utility.AllBonds.Where(t => t.ISIN == ISIN).FirstOrDefault();

            if (bond == null)
            {
                Utility.ErrorMessage = "ISIN invalido";
                throw new Exception();
            }

            if (string.IsNullOrEmpty(Emisor))
            {
                if (bond is BancoCentral)
                {
                    Resultados.Emisor = BANCO_CENTRAL;
                }
                
                if (bond is MinisterioDeHacienda)
                {
                    Resultados.Emisor = HACIENDA;
                }

                if (bond is MHUSD)
                {
                    Resultados.Emisor = HACIENDAUSD;
                    Resultados.Amortized = ((MHUSD)bond).Amortized;
                    Resultados.FirstPayment = ((MHUSD)bond).FirstPayment;
                    Resultados.PrincipalRepayments = ((MHUSD)bond).PrincipalRepayments;
                }

                if (bond is DOMINICANPOWER)
                {
                    Resultados.Emisor = Bondtype.DOMINICANPOWER.ToString();
                    Resultados.Amortized = ((DOMINICANPOWER)bond).Amortized;
                    Resultados.FirstPayment = ((DOMINICANPOWER)bond).FirstPayment;
                    Resultados.PrincipalRepayments = ((DOMINICANPOWER)bond).PrincipalRepayments;
                }

                if (bond is HAINA)
                {
                    Resultados.Emisor = Bondtype.HAINA.ToString();
                    Resultados.Amortized = ((HAINA)bond).Amortized;
                    Resultados.FirstPayment = ((HAINA)bond).FirstPayment;
                    Resultados.PrincipalRepayments = ((HAINA)bond).PrincipalRepayments;
                }

                if (bond is REMIX)
                {
                    Resultados.Emisor = Bondtype.REMIX.ToString();
                    Resultados.Amortized = ((REMIX)bond).Amortized;
                    Resultados.FirstPayment = ((REMIX)bond).FirstPayment;
                    Resultados.PrincipalRepayments = ((REMIX)bond).PrincipalRepayments;
                }
            }

            Resultados.FechaInicio = bond.IssueDate.Date;
            Resultados.FechaVencimiento = bond.MaturityDate.Date;
            Resultados.TasaCupon = bond.CouponRate / 100;
            Resultados.Periocidad = bond.Frequency;
            Resultados.Denominacion = bond.Denomination;
            Resultados.Calctype = bond.CalculationMethod;
            Resultados.ISIN = ISIN;
            Resultados.Alias = bond.Alias;
            Resultados.Moneda = bond.Currency;
            Resultados.Tipo = bond.Type;
            Resultados.FechaLiquidacion = FechaLiquidacion.Date;
            Resultados.ValorNominal = ValorNominal;

            // calculation method 1 (Yield)
            if (CalcularPor == 1)
            {
                Resultados.CalcularPorYield(CalcVariable / 100);
            }
            
            // calculation method 2 (Precio Limpio)
            if (CalcularPor == 2)
            {
                Resultados.CalcularPorPrecioLimpio(CalcVariable / 100);
            }

            // calculation method 3 (Valor Transado)
            if (CalcularPor == 3)
            {
                Resultados.CalcularPorValorTransado(CalcVariable);
            }
        }

        public string CreatHtmlTable()
        {
            StringBuilder strHTMLBuilder = new StringBuilder();

            strHTMLBuilder.Append("<table>");

            strHTMLBuilder.Append("<tr>");
            foreach (DataColumn myColumn in Resultados.GetPaymentsTable().Columns)
            {
                strHTMLBuilder.Append("<td>");
                strHTMLBuilder.Append(myColumn.ColumnName);
                strHTMLBuilder.Append("</td>");

            }
            strHTMLBuilder.Append("</tr>");


            foreach (DataRow myRow in Resultados.GetPaymentsTable().Rows)
            {

                strHTMLBuilder.Append("<tr>");
                foreach (DataColumn myColumn in Resultados.GetPaymentsTable().Columns)
                {
                    strHTMLBuilder.Append("<td>");
                    strHTMLBuilder.Append(myRow[myColumn.ColumnName].ToString());
                    strHTMLBuilder.Append("</td>");

                }
                strHTMLBuilder.Append("</tr>");
            }

            //Close tags.  
            strHTMLBuilder.Append("</table>");

            return strHTMLBuilder.ToString();
        }
    }
}