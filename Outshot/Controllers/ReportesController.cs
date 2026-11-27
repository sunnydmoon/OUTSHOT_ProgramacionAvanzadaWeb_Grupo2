using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Outshot.Data;
using Outshot.ViewModels;
using System.Globalization;

[Authorize(Roles = "Administrador")]
public class ReportesController : Controller
{
    private readonly OutshotContext _context;

    public ReportesController(OutshotContext context)
    {
        _context = context;
    }

    // GET: Reportes
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Dashboard));
    }

    // GET: Reportes/Dashboard
    public async Task<IActionResult> Dashboard()
    {
        DateTime hoy = DateTime.Now;
        DateTime hace30Dias = hoy.AddDays(-30);
        DateTime inicioSeisMeses = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-5);

        var modelo = new DashboardViewModel();

        // Inventario
        modelo.ProductosActivos = await _context.Productos
            .CountAsync(p => p.Activo);

        modelo.ProductosConInventario = await _context.Inventarios
            .CountAsync();

        modelo.ProductosSinInventario = await _context.Productos
            .CountAsync(p => p.Inventario == null);

        modelo.UnidadesEnInventario = await _context.Inventarios
            .SumAsync(i => (int?)i.Existencias) ?? 0;

        modelo.ValorInventario = await _context.Inventarios
            .SumAsync(i => (decimal?)(i.Existencias * i.Producto.Precio)) ?? 0;

        modelo.ProductosAgotados = await _context.Inventarios
            .CountAsync(i => i.Existencias == 0);

        modelo.ProductosBajoStock = await _context.Inventarios
            .CountAsync(i =>
                i.Existencias > 0 &&
                i.Existencias <= i.StockMinimo);

        // Clientes y pedidos
        modelo.ClientesRegistrados = await _context.Clientes
            .CountAsync();

        modelo.PedidosPendientes = await _context.Pedidos
            .CountAsync(p => p.Estado == "Pendiente");

        modelo.PedidosConfirmados = await _context.Pedidos
            .CountAsync(p => p.Estado == "Confirmado");

        modelo.VentasConfirmadas = await _context.Pedidos
            .Where(p => p.Estado == "Confirmado")
            .SumAsync(p => (decimal?)p.Total) ?? 0;

        // Movimientos de los últimos 30 días
        modelo.EntradasUltimos30Dias = await _context.MovimientosInventario
            .Where(m => m.Tipo == "Entrada" && m.Fecha >= hace30Dias)
            .SumAsync(m => (int?)m.Cantidad) ?? 0;

        modelo.SalidasUltimos30Dias = await _context.MovimientosInventario
            .Where(m => m.Tipo == "Salida" && m.Fecha >= hace30Dias)
            .SumAsync(m => (int?)m.Cantidad) ?? 0;

        // Productos agotados o en stock mínimo
        modelo.AlertasStock = await _context.Inventarios
            .Include(i => i.Producto)
            .Where(i => i.Existencias <= i.StockMinimo)
            .OrderBy(i => i.Existencias)
            .Take(8)
            .ToListAsync();

        // Productos más vendidos en pedidos confirmados
        modelo.ProductosMasVendidos = await _context.DetallesPedidos
            .Where(d => d.Pedido.Estado == "Confirmado")
            .GroupBy(d => new { d.ProductoId, d.Producto.Nombre })
            .Select(g => new ProductoVendidoItem
            {
                Nombre = g.Key.Nombre,
                Cantidad = g.Sum(d => d.Cantidad),
                Total = g.Sum(d => d.Subtotal)
            })
            .OrderByDescending(p => p.Cantidad)
            .Take(5)
            .ToListAsync();

        // Ventas confirmadas de los últimos 6 meses
        var ventasAgrupadas = await _context.Pedidos
            .Where(p => p.Estado == "Confirmado" && p.Fecha >= inicioSeisMeses)
            .GroupBy(p => new { p.Fecha.Year, p.Fecha.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Pedidos = g.Count(),
                Total = g.Sum(p => p.Total)
            })
            .ToListAsync();

        var cultura = new CultureInfo("es-CR");

        for (int i = 0; i < 6; i++)
        {
            DateTime mes = inicioSeisMeses.AddMonths(i);

            var venta = ventasAgrupadas.FirstOrDefault(v =>
                v.Year == mes.Year && v.Month == mes.Month);

            modelo.VentasPorMes.Add(new VentaMensualItem
            {
                Mes = cultura.TextInfo.ToTitleCase(mes.ToString("MMM yyyy", cultura)),
                Pedidos = venta?.Pedidos ?? 0,
                Total = venta?.Total ?? 0
            });
        }

        // Últimos movimientos registrados
        modelo.UltimosMovimientos = await _context.MovimientosInventario
            .Include(m => m.Inventario)
                .ThenInclude(i => i.Producto)
            .Include(m => m.Usuario)
            .OrderByDescending(m => m.Fecha)
            .Take(8)
            .ToListAsync();

        return View(modelo);
    }
}