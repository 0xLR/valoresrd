using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;


namespace ValoresRD.Models
{
    public enum Bondtype
    {
        BancoCentral,
        MinisterioDeHacienda,
        MHUSD,
        HAINA,
        DOMINICANPOWER,
        REMIX
    }

    public enum CalculationMethods
    {
        Actual_Actual_BC_1 = 1,
        Actual_Actual_BC_2 = 2,
        Actual_Actual_MH_4 = 4,
        ICMA_BC_5 = 5,
        Actual_365_6 = 6,
        USD_30_360_7 = 7,
        MH_Special_8 = 8
    }

    public enum InstrumentType
    {
        Bono,
        CDEEE,
        Certificado,
        Nota,
        Soberano,
        Corporativo
    }

    public enum Currency
    {
        DOP,
        USD
    }

     public class Bond
    {
        public virtual string ISIN { get; set; }

        public virtual string Alias { get; set; }

        public virtual string Type { get; set; }

        public virtual DateTime IssueDate { get; set; }

        public virtual DateTime MaturityDate { get; set; }

        public virtual double CouponRate { get; set; }

        public virtual int Frequency { get; set; }

        public virtual int Denomination { get; set; }

        public virtual string Currency { get; set; }

        public virtual int CalculationMethod { get; set; }

        public override string ToString()
        {
            return this.Alias;
        }
    }
}
