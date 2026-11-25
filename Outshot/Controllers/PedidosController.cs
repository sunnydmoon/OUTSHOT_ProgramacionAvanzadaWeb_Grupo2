
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Outshot.Data;
using Outshot.Models;

public class PedidosController : Controller
{
    private readonly OutshotContext _context;

    public PedidosController(OutshotContext context)
    {
        _context = context;
    }

    // GET: Pedidos
    public async Task<IActionResult> Index()
    {
        var pedidos = _context.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Usuario);

        return View(await pedidos.ToListAsync());
    }

    // GET: Pedidos/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var pedido = await _context.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Usuario)
            .Include(p => p.DetallePedidos)
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(p => p.PedidoId == id);

        if (pedido == null)
            return NotFound();

        return View(pedido);
    }

    // GET: Pedidos/Create
    public IActionResult Create()
    {
        CargarListas();
        return View();
    }

    // POST: Pedidos/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("ClienteId,UsuarioId,Estado")] Pedido pedido)
    {
        if (ModelState.IsValid)
        {
            pedido.Fecha = DateTime.Now;
            pedido.Total = 0;

            _context.Add(pedido);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        CargarListas(pedido.ClienteId, pedido.UsuarioId);
        return View(pedido);
    }

    // GET: Pedidos/AgregarProducto/5
    public async Task<IActionResult> AgregarProducto(int? id)
    {
        if (id == null)
            return NotFound();

        var pedido = await _context.Pedidos.FindAsync(id);

        if (pedido == null)
            return NotFound();

        CargarProductos();
        ViewBag.PedidoId = pedido.PedidoId;

        return View();
    }

    // POST: Pedidos/AgregarProducto/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarProducto(
        int id, int productoId, int cantidad)
    {
        var pedido = await _context.Pedidos.FindAsync(id);

        if (pedido == null)
            return NotFound();

        if (cantidad <= 0)
        {
            ModelState.AddModelError(
                "cantidad", "La cantidad debe ser mayor que cero.");
        }

        var producto = await _context.Productos
            .Include(p => p.Inventario)
            .FirstOrDefaultAsync(p => p.ProductoId == productoId);

        if (producto == null || !producto.Activo)
        {
            ModelState.AddModelError(
                "productoId", "El producto no existe o está inactivo.");
        }
        else if (producto.Inventario == null)
        {
            ModelState.AddModelError(
                "productoId", "El producto no tiene inventario registrado.");
        }
        else if (cantidad > producto.Inventario.Existencias)
        {
            ModelState.AddModelError(
                "cantidad", "No hay suficientes existencias.");
        }

        if (!ModelState.IsValid)
        {
            CargarProductos();
            ViewBag.PedidoId = pedido.PedidoId;
            return View();
        }

        await using var transaccion =
            await _context.Database.BeginTransactionAsync();

        try
        {
            decimal subtotal = producto!.Precio * cantidad;

            var detalle = new DetallePedido
            {
                PedidoId = pedido.PedidoId,
                ProductoId = producto.ProductoId,
                Cantidad = cantidad,
                PrecioUnitario = producto.Precio,
                Subtotal = subtotal
            };

            _context.DetallesPedidos.Add(detalle);

            producto.Inventario!.Existencias -= cantidad;
            producto.Inventario.UltimaActualizacion = DateTime.Now;

            pedido.Total += subtotal;

            RegistrarMovimiento(
                producto.Inventario,
                pedido.UsuarioId,
                "Salida",
                cantidad,
                "Salida por pedido #" + pedido.PedidoId
            );

            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();

            return RedirectToAction(
                nameof(Details), new { id = pedido.PedidoId });
        }
        catch
        {
            await transaccion.RollbackAsync();

            ModelState.AddModelError(
                string.Empty, "No fue posible agregar el producto.");

            CargarProductos();
            ViewBag.PedidoId = pedido.PedidoId;
            return View();
        }
    }

    // GET: Pedidos/EditarDetalle/5
    public async Task<IActionResult> EditarDetalle(int? id)
    {
        if (id == null)
            return NotFound();

        var detalle = await _context.DetallesPedidos
            .Include(d => d.Pedido)
            .Include(d => d.Producto)
            .FirstOrDefaultAsync(d => d.DetallePedidoId == id);

        if (detalle == null)
            return NotFound();

        return View(detalle);
    }

    // POST: Pedidos/EditarDetalle/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarDetalle(
        int id, int cantidad)
    {
        if (cantidad <= 0)
        {
            ModelState.AddModelError(
                "cantidad", "La cantidad debe ser mayor que cero.");

            var detalleInvalido = await _context.DetallesPedidos
                .Include(d => d.Pedido)
                .Include(d => d.Producto)
                .FirstOrDefaultAsync(d => d.DetallePedidoId == id);

            if (detalleInvalido == null)
                return NotFound();

            return View(detalleInvalido);
        }

        await using var transaccion =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var detalle = await _context.DetallesPedidos
                .Include(d => d.Pedido)
                .Include(d => d.Producto)
                    .ThenInclude(p => p.Inventario)
                .FirstOrDefaultAsync(d => d.DetallePedidoId == id);

            if (detalle == null || detalle.Producto.Inventario == null)
                return NotFound();

            var inventario = detalle.Producto.Inventario;
            int diferencia = cantidad - detalle.Cantidad;

            if (diferencia > inventario.Existencias)
            {
                await transaccion.RollbackAsync();

                ModelState.AddModelError(
                    "cantidad", "No hay suficientes existencias.");

                return View(detalle);
            }

            if (diferencia > 0)
            {
                inventario.Existencias -= diferencia;

                RegistrarMovimiento(
                    inventario,
                    detalle.Pedido.UsuarioId,
                    "Salida",
                    diferencia,
                    "Aumento de cantidad en pedido #" +
                    detalle.PedidoId
                );
            }
            else if (diferencia < 0)
            {
                inventario.Existencias += Math.Abs(diferencia);

                RegistrarMovimiento(
                    inventario,
                    detalle.Pedido.UsuarioId,
                    "Entrada",
                    Math.Abs(diferencia),
                    "Devolución por modificación del pedido #" +
                    detalle.PedidoId
                );
            }

            decimal subtotalAnterior = detalle.Subtotal;

            detalle.Cantidad = cantidad;
            detalle.Subtotal = detalle.PrecioUnitario * cantidad;

            detalle.Pedido.Total +=
                detalle.Subtotal - subtotalAnterior;

            inventario.UltimaActualizacion = DateTime.Now;

            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();

            return RedirectToAction(
                nameof(Details), new { id = detalle.PedidoId });
        }
        catch
        {
            await transaccion.RollbackAsync();
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: Pedidos/EliminarDetalle/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarDetalle(int id)
    {
        await using var transaccion =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var detalle = await _context.DetallesPedidos
                .Include(d => d.Pedido)
                .Include(d => d.Producto)
                    .ThenInclude(p => p.Inventario)
                .FirstOrDefaultAsync(d => d.DetallePedidoId == id);

            if (detalle == null)
                return NotFound();

            if (detalle.Producto.Inventario == null)
                return BadRequest();

            var inventario = detalle.Producto.Inventario;

            inventario.Existencias += detalle.Cantidad;
            inventario.UltimaActualizacion = DateTime.Now;

            detalle.Pedido.Total -= detalle.Subtotal;

            RegistrarMovimiento(
                inventario,
                detalle.Pedido.UsuarioId,
                "Entrada",
                detalle.Cantidad,
                "Devolución por eliminación del detalle del pedido #" +
                detalle.PedidoId
            );

            int pedidoId = detalle.PedidoId;

            _context.DetallesPedidos.Remove(detalle);

            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();

            return RedirectToAction(
                nameof(Details), new { id = pedidoId });
        }
        catch
        {
            await transaccion.RollbackAsync();
            return RedirectToAction(nameof(Index));
        }
    }

    // GET: Pedidos/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var pedido = await _context.Pedidos.FindAsync(id);

        if (pedido == null)
            return NotFound();

        CargarListas(pedido.ClienteId, pedido.UsuarioId);
        return View(pedido);
    }

    // POST: Pedidos/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("PedidoId,ClienteId,UsuarioId,Estado")] Pedido pedido)
    {
        if (id != pedido.PedidoId)
            return NotFound();

        if (ModelState.IsValid)
        {
            var pedidoExistente = await _context.Pedidos.FindAsync(id);

            if (pedidoExistente == null)
                return NotFound();

            pedidoExistente.ClienteId = pedido.ClienteId;
            pedidoExistente.UsuarioId = pedido.UsuarioId;
            pedidoExistente.Estado = pedido.Estado;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        CargarListas(pedido.ClienteId, pedido.UsuarioId);
        return View(pedido);
    }

    // GET: Pedidos/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var pedido = await _context.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.PedidoId == id);

        if (pedido == null)
            return NotFound();

        return View(pedido);
    }

    // POST: Pedidos/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.DetallePedidos)
            .FirstOrDefaultAsync(p => p.PedidoId == id);

        if (pedido == null)
            return NotFound();

        if (pedido.DetallePedidos.Any())
        {
            return RedirectToAction(
                nameof(Details), new { id = pedido.PedidoId });
        }

        _context.Pedidos.Remove(pedido);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private void RegistrarMovimiento(
        Inventario inventario,
        int usuarioId,
        string tipo,
        int cantidad,
        string observacion)
    {
        var movimiento = new MovimientoInventario
        {
            InventarioId = inventario.InventarioId,
            UsuarioId = usuarioId,
            Tipo = tipo,
            Cantidad = cantidad,
            Fecha = DateTime.Now,
            Observacion = observacion
        };

        _context.MovimientosInventario.Add(movimiento);
    }

    private void CargarListas(
        int? clienteId = null,
        int? usuarioId = null)
    {
        ViewData["ClienteId"] = new SelectList(
            _context.Clientes,
            "ClienteId",
            "Nombre",
            clienteId
        );

        ViewData["UsuarioId"] = new SelectList(
            _context.Usuarios,
            "UsuarioId",
            "Nombre",
            usuarioId
        );
    }

    private void CargarProductos()
    {
        ViewData["ProductoId"] = new SelectList(
            _context.Productos.Where(p => p.Activo),
            "ProductoId",
            "Nombre"
        );
    }
}


