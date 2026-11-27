using Outshot.Models;

namespace Outshot.ViewModels
{
    // Indicadores del dashboard de reportes.
    public class DashboardViewModel
    {
        public int ProductosActivos { get; set; }

        public int ProductosConInventario { get; set; }

        public int ProductosSinInventario { get; set; }

        public int UnidadesEnInventario { get; set; }

        public decimal ValorInventario { get; set; }

        public int ProductosBajoStock { get; set; }

        public int ProductosAgotados { get; set; }

        public int ClientesRegistrados { get; set; }

        public int PedidosPendientes { get; set; }

        public int PedidosConfirmados { get; set; }

        public decimal VentasConfirmadas { get; set; }

        public int EntradasUltimos30Dias { get; set; }

        public int SalidasUltimos30Dias { get; set; }

        public List<Inventario> AlertasStock { get; set; } = new();

        public List<ProductoVendidoItem> ProductosMasVendidos { get; set; } = new();

        public List<VentaMensualItem> VentasPorMes { get; set; } = new();

        public List<MovimientoInventario> UltimosMovimientos { get; set; } = new();
    }

    public class ProductoVendidoItem
    {
        public string Nombre { get; set; } = string.Empty;

        public int Cantidad { get; set; }

        public decimal Total { get; set; }
    }

    public class VentaMensualItem
    {
        public string Mes { get; set; } = string.Empty;

        public int Pedidos { get; set; }

        public decimal Total { get; set; }
    }
}
