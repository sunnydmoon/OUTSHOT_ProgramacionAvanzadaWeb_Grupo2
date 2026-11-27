using System.ComponentModel.DataAnnotations;

namespace Outshot.ViewModels
{
    // Datos del formulario para registrar una entrada o salida manual.
    public class MovimientoInventarioViewModel
    {
        public int InventarioId { get; set; }

        public string? NombreProducto { get; set; }

        public int ExistenciasActuales { get; set; }

        public int StockMinimo { get; set; }

        [Required(ErrorMessage = "Debe seleccionar el tipo de movimiento.")]
        [Display(Name = "Tipo de movimiento")]
        public string Tipo { get; set; } = string.Empty;

        [Range(1, 1000000,
            ErrorMessage = "La cantidad debe estar entre 1 y 1.000.000.")]
        public int Cantidad { get; set; }

        [Required(ErrorMessage = "La observación es obligatoria.")]
        [StringLength(250, MinimumLength = 5,
            ErrorMessage = "La observación debe tener entre 5 y 250 caracteres.")]
        [Display(Name = "Observación")]
        public string Observacion { get; set; } = string.Empty;
    }
}