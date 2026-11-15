namespace Outshot.Models
{
    public class Pedido
    {
        public int PedidoId { get; set; }

        public int ClienteId { get; set; }

        public int UsuarioId { get; set; }

        public DateTime Fecha { get; set; }

        public string Estado { get; set; } = string.Empty;

        public decimal Total { get; set; }

        public Cliente Cliente { get; set; } = null!;

        public Usuario Usuario { get; set; } = null!;

        public ICollection<DetallePedido> DetallePedidos { get; set; } = new List<DetallePedido>();
    }
}