namespace Outshot.Models
{
    public class Producto
    {
        public int ProductoId { get; set; }

        public int CategoriaId { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Descripcion { get; set; } = string.Empty;

        public decimal Precio { get; set; }

        public string ImagenUrl { get; set; } = string.Empty;

        public bool Activo { get; set; }

        public DateTime FechaRegistro { get; set; }

        public Categoria? Categoria { get; set; }

        public Inventario? Inventario { get; set; }

        public ICollection<DetallePedido> DetallePedidos { get; set; } = new List<DetallePedido>();
    }
}