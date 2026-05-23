 public class Pagamento
    {
        public int Id { get; set; }

        public int AlunoId { get; set; }

        public decimal Valor { get; set; }

        public DateTime DataPagamento { get; set; }

        public DateTime Vencimento { get; set; }

        public bool Pago { get; set; }

        public string Metodo { get; set; }
    }