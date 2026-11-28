using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Outshot.Data;
using Outshot.Models;
using Outshot.ViewModels;
using System.Security.Claims;

[Authorize(Roles = "Administrador")]
public class InventariosController : Controller
{
    private const string TipoEntrada = "Entrada";
    private const string TipoSalida = "Salida";

    private readonly OutshotContext _context;

    public InventariosController(OutshotContext context)
    {
        _context = context;
    }

    // GET: Inventarios
    public async Task<IActionResult> Index(string? buscar, string? estado)
    {
        var consulta = _context.Inventarios
            .Include(i => i.Producto)
                .ThenInclude(p => p.Categoria)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            string texto = buscar.Trim();

            consulta = consulta.Where(i =>
                i.Producto.Nombre.Contains(texto));
        }

        switch (estado)
        {
            case "agotado":
                consulta = consulta.Where(i => i.Existencias == 0);
                break;

            case "bajo":
                consulta = consulta.Where(i =>
                    i.Existencias > 0 &&
                    i.Existencias <= i.StockMinimo);
                break;

            case "disponible":
                consulta = consulta.Where(i =>
                    i.Existencias > i.StockMinimo);
                break;
        }

        ViewData["Buscar"] = buscar;
        ViewData["Estado"] = estado;

        ViewData["ProductosSinInventario"] = await _context.Productos
            .CountAsync(p => p.Inventario == null);

        var inventarios = await consulta
            .OrderBy(i => i.Producto.Nombre)
            .ToListAsync();

        return View(inventarios);
    }

    // GET: Inventarios/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var inventario = await _context.Inventarios
            .Include(i => i.Producto)
                .ThenInclude(p => p.Categoria)
            .Include(i => i.MovimientosInventario)
                .ThenInclude(m => m.Usuario)
            .FirstOrDefaultAsync(i => i.InventarioId == id);

        if (inventario == null)
            return NotFound();

        return View(inventario);
    }

    // GET: Inventarios/Create
    public async Task<IActionResult> Create(int? productoId)
    {
        await CargarProductosSinInventario(productoId);

        var modelo = new InventarioFormViewModel
        {
            ProductoId = productoId ?? 0
        };

        return View(modelo);
    }

    // POST: Inventarios/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InventarioFormViewModel modelo)
    {
        int? usuarioId = ObtenerUsuarioId();

        if (usuarioId == null)
            return Forbid();

        var producto = await _context.Productos
            .Include(p => p.Inventario)
            .FirstOrDefaultAsync(p => p.ProductoId == modelo.ProductoId);

        if (producto == null)
        {
            ModelState.AddModelError(
                nameof(modelo.ProductoId),
                "Debe seleccionar un producto válido.");
        }
        else if (producto.Inventario != null)
        {
            ModelState.AddModelError(
                nameof(modelo.ProductoId),
                "Este producto ya tiene un inventario registrado.");
        }

        if (!ModelState.IsValid)
        {
            await CargarProductosSinInventario(modelo.ProductoId);
            return View(modelo);
        }

        var inventario = new Inventario
        {
            ProductoId = modelo.ProductoId,
            Existencias = modelo.Existencias,
            StockMinimo = modelo.StockMinimo,
            UltimaActualizacion = DateTime.Now
        };

        _context.Inventarios.Add(inventario);

        // Las existencias iniciales quedan registradas como una entrada.
        if (modelo.Existencias > 0)
        {
            RegistrarMovimiento(
                inventario,
                usuarioId.Value,
                TipoEntrada,
                modelo.Existencias,
                "Inventario inicial");
        }

        await _context.SaveChangesAsync();

        TempData["Mensaje"] = "Inventario registrado correctamente.";

        return RedirectToAction(nameof(Index));
    }

    // GET: Inventarios/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var inventario = await _context.Inventarios
            .Include(i => i.Producto)
            .FirstOrDefaultAsync(i => i.InventarioId == id);

        if (inventario == null)
            return NotFound();

        var modelo = new InventarioFormViewModel
        {
            InventarioId = inventario.InventarioId,
            ProductoId = inventario.ProductoId,
            NombreProducto = inventario.Producto.Nombre,
            Existencias = inventario.Existencias,
            StockMinimo = inventario.StockMinimo
        };

        return View(modelo);
    }

    // POST: Inventarios/Edit/5
    // Si cambian las existencias, la diferencia se registra como un movimiento de ajuste.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, InventarioFormViewModel modelo)
    {
        if (id != modelo.InventarioId)
            return NotFound();

        int? usuarioId = ObtenerUsuarioId();

        if (usuarioId == null)
            return Forbid();

        var inventario = await _context.Inventarios
            .Include(i => i.Producto)
            .FirstOrDefaultAsync(i => i.InventarioId == id);

        if (inventario == null)
            return NotFound();

        ModelState.Remove(nameof(modelo.ProductoId));

        if (!ModelState.IsValid)
        {
            modelo.ProductoId = inventario.ProductoId;
            modelo.NombreProducto = inventario.Producto.Nombre;

            return View(modelo);
        }

        int diferencia = modelo.Existencias - inventario.Existencias;

        if (diferencia != 0)
        {
            RegistrarMovimiento(
                inventario,
                usuarioId.Value,
                diferencia > 0 ? TipoEntrada : TipoSalida,
                Math.Abs(diferencia),
                $"Ajuste manual de existencias ({inventario.Existencias} → {modelo.Existencias})");

            inventario.Existencias = modelo.Existencias;
        }

        inventario.StockMinimo = modelo.StockMinimo;
        inventario.UltimaActualizacion = DateTime.Now;

        await _context.SaveChangesAsync();

        TempData["Mensaje"] = "Inventario actualizado correctamente.";

        return RedirectToAction(nameof(Index));
    }

    // GET: Inventarios/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var inventario = await _context.Inventarios
            .Include(i => i.Producto)
            .Include(i => i.MovimientosInventario)
            .FirstOrDefaultAsync(i => i.InventarioId == id);

        if (inventario == null)
            return NotFound();

        ViewData["MotivoBloqueo"] = await ObtenerMotivoBloqueoEliminacion(inventario);

        return View(inventario);
    }

    // POST: Inventarios/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var inventario = await _context.Inventarios
            .Include(i => i.MovimientosInventario)
            .FirstOrDefaultAsync(i => i.InventarioId == id);

        if (inventario == null)
            return NotFound();

        string? motivoBloqueo = await ObtenerMotivoBloqueoEliminacion(inventario);

        if (motivoBloqueo != null)
        {
            TempData["Error"] = motivoBloqueo;
            return RedirectToAction(nameof(Index));
        }

        _context.MovimientosInventario.RemoveRange(inventario.MovimientosInventario);
        _context.Inventarios.Remove(inventario);

        await _context.SaveChangesAsync();

        TempData["Mensaje"] = "Inventario eliminado correctamente.";

        return RedirectToAction(nameof(Index));
    }

    // GET: Inventarios/Movimiento/5?tipo=Entrada
    public async Task<IActionResult> Movimiento(int? id, string? tipo)
    {
        if (id == null)
            return NotFound();

        var inventario = await _context.Inventarios
            .Include(i => i.Producto)
            .FirstOrDefaultAsync(i => i.InventarioId == id);

        if (inventario == null)
            return NotFound();

        var modelo = new MovimientoInventarioViewModel
        {
            InventarioId = inventario.InventarioId,
            Tipo = tipo == TipoSalida ? TipoSalida : TipoEntrada,
            Cantidad = 1
        };

        CompletarDatosMovimiento(modelo, inventario);

        return View(modelo);
    }

    // POST: Inventarios/Movimiento
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Movimiento(MovimientoInventarioViewModel modelo)
    {
        int? usuarioId = ObtenerUsuarioId();

        if (usuarioId == null)
            return Forbid();

        var inventario = await _context.Inventarios
            .Include(i => i.Producto)
            .FirstOrDefaultAsync(i => i.InventarioId == modelo.InventarioId);

        if (inventario == null)
            return NotFound();

        modelo.Observacion = modelo.Observacion?.Trim() ?? string.Empty;

        if (modelo.Tipo != TipoEntrada && modelo.Tipo != TipoSalida)
        {
            ModelState.AddModelError(
                nameof(modelo.Tipo),
                "El tipo de movimiento no es válido.");
        }

        if (modelo.Tipo == TipoSalida &&
            modelo.Cantidad > inventario.Existencias)
        {
            ModelState.AddModelError(
                nameof(modelo.Cantidad),
                $"No hay suficientes existencias. Disponibles: {inventario.Existencias}.");
        }

        if (!ModelState.IsValid)
        {
            CompletarDatosMovimiento(modelo, inventario);
            return View(modelo);
        }

        if (modelo.Tipo == TipoEntrada)
            inventario.Existencias += modelo.Cantidad;
        else
            inventario.Existencias -= modelo.Cantidad;

        inventario.UltimaActualizacion = DateTime.Now;

        RegistrarMovimiento(
            inventario,
            usuarioId.Value,
            modelo.Tipo,
            modelo.Cantidad,
            modelo.Observacion);

        await _context.SaveChangesAsync();

        TempData["Mensaje"] =
            $"{modelo.Tipo} de {modelo.Cantidad} unidad(es) registrada correctamente.";

        return RedirectToAction(nameof(Details), new { id = inventario.InventarioId });
    }

    // GET: Inventarios/Movimientos
    // Historial general de movimientos con filtros.
    public async Task<IActionResult> Movimientos(
        string? tipo,
        int? productoId,
        DateTime? desde,
        DateTime? hasta)
    {
        var consulta = _context.MovimientosInventario
            .Include(m => m.Inventario)
                .ThenInclude(i => i.Producto)
            .Include(m => m.Usuario)
            .AsQueryable();

        if (tipo == TipoEntrada || tipo == TipoSalida)
        {
            consulta = consulta.Where(m => m.Tipo == tipo);
        }

        if (productoId.HasValue)
        {
            int idProducto = productoId.Value;
            consulta = consulta.Where(m => m.Inventario.ProductoId == idProducto);
        }

        if (desde.HasValue)
        {
            DateTime fechaInicio = desde.Value.Date;
            consulta = consulta.Where(m => m.Fecha >= fechaInicio);
        }

        if (hasta.HasValue)
        {
            DateTime fechaLimite = hasta.Value.Date.AddDays(1);
            consulta = consulta.Where(m => m.Fecha < fechaLimite);
        }

        var productos = await _context.Productos
            .Where(p => p.Inventario != null)
            .OrderBy(p => p.Nombre)
            .ToListAsync();

        ViewData["ProductoId"] = new SelectList(
            productos,
            "ProductoId",
            "Nombre",
            productoId);

        ViewData["Tipo"] = tipo;
        ViewData["Desde"] = desde?.ToString("yyyy-MM-dd");
        ViewData["Hasta"] = hasta?.ToString("yyyy-MM-dd");

        var movimientos = await consulta
            .OrderByDescending(m => m.Fecha)
            .Take(500)
            .ToListAsync();

        return View(movimientos);
    }

    // Validar si un inventario se puede eliminar.
    private async Task<string?> ObtenerMotivoBloqueoEliminacion(Inventario inventario)
    {
        if (inventario.Existencias > 0)
        {
            return "No se puede eliminar el inventario porque todavía tiene existencias. " +
                   "Registrá una salida hasta dejarlo en 0.";
        }

        bool tienePedidosPendientes = await _context.DetallesPedidos
            .AnyAsync(d =>
                d.ProductoId == inventario.ProductoId &&
                d.Pedido.Estado == "Pendiente");

        if (tienePedidosPendientes)
        {
            return "No se puede eliminar el inventario porque el producto " +
                   "está incluido en pedidos pendientes.";
        }

        return null;
    }

    // Registrar movimientos de inventario.
    private void RegistrarMovimiento(
        Inventario inventario,
        int usuarioId,
        string tipo,
        int cantidad,
        string observacion)
    {
        var movimiento = new MovimientoInventario
        {
            Inventario = inventario,
            UsuarioId = usuarioId,
            Tipo = tipo,
            Cantidad = cantidad,
            Fecha = DateTime.Now,
            Observacion = observacion
        };

        _context.MovimientosInventario.Add(movimiento);
    }

    // Completar los datos de solo lectura del formulario de movimiento.
    private static void CompletarDatosMovimiento(
        MovimientoInventarioViewModel modelo,
        Inventario inventario)
    {
        modelo.NombreProducto = inventario.Producto.Nombre;
        modelo.ExistenciasActuales = inventario.Existencias;
        modelo.StockMinimo = inventario.StockMinimo;
    }

    // Obtener el identificador del usuario autenticado.
    private int? ObtenerUsuarioId()
    {
        var identificador = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (int.TryParse(identificador, out int usuarioId))
            return usuarioId;

        return null;
    }

    // Cargar productos que todavía no tienen inventario.
    private async Task CargarProductosSinInventario(int? seleccionado = null)
    {
        var productos = await _context.Productos
            .Where(p => p.Inventario == null)
            .OrderBy(p => p.Nombre)
            .ToListAsync();

        ViewData["ProductoId"] = new SelectList(
            productos,
            "ProductoId",
            "Nombre",
            seleccionado);
    }
}