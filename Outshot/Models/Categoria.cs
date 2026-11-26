
using System.ComponentModel.DataAnnotations;

namespace Outshot.Models
{
    public class Categoria
    {
        public int CategoriaId { get; set; }

        [Required(ErrorMessage = "El nombre de la categoría es obligatorio.")]
        [StringLength(100, MinimumLength = 3,
            ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres.")]
        [RegularExpression(
            @"^(?=.*[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ])[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ0-9\s.,'’()/%&+\-]+$",
            ErrorMessage = "El nombre debe contener al menos una letra y no incluir caracteres especiales no permitidos.")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(500,
            ErrorMessage = "La descripción no puede superar los 500 caracteres.")]
        public string? Descripcion { get; set; }

        public bool Activo { get; set; }

        public ICollection<Producto> Productos { get; set; } = new List<Producto>();
    }
}
