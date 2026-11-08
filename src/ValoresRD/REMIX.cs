namespace ValoresRD.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    

    [Table("REMIX")]
    public partial class REMIX : Bond
    {
        [Key]
        [StringLength(50)]
        public override string ISIN { get; set; }

        [Required]
        [StringLength(50)]
        public override string Alias { get; set; }

        [Required]
        [StringLength(50)]
        public override string Type { get; set; }

        [Column(TypeName = "date")]
        public override DateTime IssueDate { get; set; }

        [Column(TypeName = "date")]
        public override DateTime MaturityDate { get; set; }

        public override double CouponRate { get; set; }

        public override int Frequency { get; set; }

        public override int Denomination { get; set; }

        [Required]
        [StringLength(50)]
        public override string Currency { get; set; }

        public override int CalculationMethod { get; set; }

        public bool Amortized { get; set; }

        public int PrincipalRepayments { get; set; }

        [Column(TypeName = "date")]
        public DateTime FirstPayment { get; set; }
    }
}

