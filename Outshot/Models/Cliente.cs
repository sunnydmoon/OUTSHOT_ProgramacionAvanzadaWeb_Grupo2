
namespace Outshot.Models
{
    public class Cliente
    {
        public int ClienteId { get; set; }

        public int? UsuarioId { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Correo { get; set; } = string.Empty;

        public string Telefono { get; set; } = string.Empty;

        public string Direccion { get; set; } = string.Empty;

        public string Pais { get; set; } = string.Empty;

        public DateTime FechaRegistro { get; set; }

        public Usuario? Usuario { get; set; }

        public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    }
}
