
using System.ComponentModel.DataAnnotations;

namespace Outshot.Models
{
    public class Producto
    {
        public int ProductoId { get; set; }

        [Range(1, int.MaxValue,
            ErrorMessage = "Debe seleccionar una categoría.")]
        public int CategoriaId { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100, MinimumLength = 3,
            ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres.")]
        [RegularExpression(
            @"^(?=.*[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ])[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ0-9\s.,'’()/%&+\-]+$",
            ErrorMessage = "El nombre debe contener al menos una letra y no incluir caracteres especiales no permitidos.")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(500,
            ErrorMessage = "La descripción no puede superar los 500 caracteres.")]
        public string? Descripcion { get; set; }

        public decimal Precio { get; set; }

        public string? ImagenUrl { get; set; }

        public bool Activo { get; set; }

        public DateTime FechaRegistro { get; set; }

        public Categoria? Categoria { get; set; }

        public Inventario? Inventario { get; set; }

        public ICollection<DetallePedido> DetallePedidos { get; set; }
            = new List<DetallePedido>();
    }
}
