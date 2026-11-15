namespace Outshot.Models
{
    public class MovimientoInventario
    {
        public int MovimientoId { get; set; }

        public int InventarioId { get; set; }

        public int UsuarioId { get; set; }

        public string Tipo { get; set; } = string.Empty;

        public int Cantidad { get; set; }

        public DateTime Fecha { get; set; }

        public string Observacion { get; set; } = string.Empty;

        public Inventario Inventario { get; set; } = null!;

        public Usuario Usuario { get; set; } = null!;
    }
}