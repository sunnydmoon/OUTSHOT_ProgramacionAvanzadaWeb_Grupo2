namespace Outshot.Models
{
    public class Categoria
    {
        public int CategoriaId { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Descripcion { get; set; } = string.Empty;

        public bool Activo { get; set; }

        public ICollection<Producto> Productos { get; set; } = new List<Producto>();
    }
}