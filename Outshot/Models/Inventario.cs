namespace Outshot.Models
{
    public class Inventario
    {
        public int InventarioId { get; set; }

        public int ProductoId { get; set; }

        public int Existencias { get; set; }

        public int StockMinimo { get; set; }

        public DateTime UltimaActualizacion { get; set; }

        public Producto Producto { get; set; } = null!;

        public ICollection<MovimientoInventario> MovimientosInventario { get; set; } = new List<MovimientoInventario>();
    }
}