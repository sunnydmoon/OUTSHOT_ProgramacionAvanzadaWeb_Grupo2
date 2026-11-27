using System.ComponentModel.DataAnnotations;

namespace Outshot.ViewModels
{
    // Datos del formulario para registrar o editar un inventario.
    public class InventarioFormViewModel
    {
        public int InventarioId { get; set; }

        [Range(1, int.MaxValue,
            ErrorMessage = "Debe seleccionar un producto.")]
        [Display(Name = "Producto")]
        public int ProductoId { get; set; }

        public string? NombreProducto { get; set; }

        [Range(0, 1000000,
            ErrorMessage = "Las existencias deben estar entre 0 y 1.000.000.")]
        [Display(Name = "Existencias iniciales")]
        public int Existencias { get; set; }

        [Range(0, 1000000,
            ErrorMessage = "El stock mínimo debe estar entre 0 y 1.000.000.")]
        [Display(Name = "Stock mínimo")]
        public int StockMinimo { get; set; }
    }
}