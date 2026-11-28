
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Outshot.Data;
using Outshot.Models;
using System.Security.Claims;

[Authorize]
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
            .Include(p => p.Usuario)
            .AsQueryable();

        // El cliente únicamente consulta sus propios pedidos.
        if (!User.IsInRole("Administrador"))
        {
            int? usuarioId = ObtenerUsuarioId();

            if (usuarioId == null)
                return Forbid();

            int idUsuario = usuarioId.Value;

            // Pedidos que hizo el cliente o que se registraron a su nombre.
            pedidos = pedidos.Where(p =>
                p.UsuarioId == idUsuario ||
                p.Cliente.UsuarioId == idUsuario);
        }

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

        // Verificar que el pedido pertenezca al cliente autenticado.
        if (!User.IsInRole("Administrador") &&
            !EsPedidoPropio(pedido))
        {
            return Forbid();
        }

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
        if (User.IsInRole("Administrador"))
        {
            // Las propiedades de navegación no vienen en el formulario,
            // solo los Id. Se quitan para que no invaliden el modelo.
            ModelState.Remove(nameof(Pedido.Cliente));
            ModelState.Remove(nameof(Pedido.Usuario));
            ModelState.Remove(nameof(Pedido.Estado));
        }
        else
        {
            // Para clientes, los datos se obtienen de la cuenta
            // autenticada y no desde el formulario.
            int? usuarioId = ObtenerUsuarioId();

            if (usuarioId == null)
                return Forbid();

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId.Value);

            if (cliente == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Tu cuenta no tiene un registro de cliente asociado.");

                return View(pedido);
            }

            pedido.UsuarioId = usuarioId.Value;
            pedido.ClienteId = cliente.ClienteId;

            // Se descartan las validaciones del formulario porque
            // ningún dato del cliente viene de ahí.
            ModelState.Clear();
        }

        if (ModelState.IsValid)
        {
            // Todo pedido nuevo inicia en estado Pendiente.
            pedido.Estado = "Pendiente";
            pedido.Fecha = DateTime.Now;
            pedido.Total = 0;

            _context.Add(pedido);
            await _context.SaveChangesAsync();

            // Después de crear el pedido, se dirige directamente
            // a agregar el primer producto para evitar dejarlo vacío.
            return RedirectToAction(
                nameof(AgregarProducto), new { id = pedido.PedidoId });
        }

        CargarListas(pedido.ClienteId, pedido.UsuarioId);
        return View(pedido);
    }

    // GET: Pedidos/AgregarProducto/5
    public async Task<IActionResult> AgregarProducto(int? id)
    {
        if (id == null)
            return NotFound();

        var pedido = await _context.Pedidos
            .FirstOrDefaultAsync(p => p.PedidoId == id);

        if (pedido == null)
            return NotFound();

        // El cliente solo puede modificar sus propios pedidos.
        if (!User.IsInRole("Administrador") && !EsPedidoPropio(pedido))
            return Forbid();

        // No se permiten modificaciones cuando el pedido ya fue procesado.
        if (!string.Equals(pedido.Estado, "Pendiente", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Solo se pueden agregar productos a pedidos pendientes.");

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
        var pedido = await _context.Pedidos
            .FirstOrDefaultAsync(p => p.PedidoId == id);

        if (pedido == null)
            return NotFound();

        // El cliente solo puede modificar sus propios pedidos.
        if (!User.IsInRole("Administrador") && !EsPedidoPropio(pedido))
            return Forbid();

        if (!string.Equals(pedido.Estado, "Pendiente", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Solo se pueden agregar productos a pedidos pendientes.");

        if (productoId <= 0)
        {
            ModelState.AddModelError(
                "productoId", "Debe seleccionar un producto.");
        }

        if (cantidad <= 0)
        {
            ModelState.AddModelError(
                "cantidad", "La cantidad debe ser mayor que cero.");
        }

        if (!ModelState.IsValid)
        {
            CargarProductos();
            ViewBag.PedidoId = pedido.PedidoId;
            return View();
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
        else if (producto.Inventario.Existencias < cantidad)
        {
            ModelState.AddModelError(
                "cantidad", "No hay suficientes existencias para esa cantidad.");
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
            // Se vuelve a comprobar la existencia dentro de la transacción.
            // Esto reduce el riesgo de registrar una salida con inventario insuficiente.
            var inventario = producto!.Inventario!;

            if (inventario.Existencias < cantidad)
            {
                await transaccion.RollbackAsync();
                ModelState.AddModelError(
                    "cantidad", "Las existencias cambiaron. No hay suficiente inventario.");
                CargarProductos();
                ViewBag.PedidoId = pedido.PedidoId;
                return View();
            }

            decimal subtotal = producto.Precio * cantidad;

            var detalle = new DetallePedido
            {
                PedidoId = pedido.PedidoId,
                ProductoId = producto.ProductoId,
                Cantidad = cantidad,
                PrecioUnitario = producto.Precio,
                Subtotal = subtotal
            };

            _context.DetallesPedidos.Add(detalle);

            inventario.Existencias -= cantidad;
            inventario.UltimaActualizacion = DateTime.Now;

            pedido.Total += subtotal;

            RegistrarMovimiento(
                inventario,
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
                string.Empty, "No fue posible agregar el producto. Intente nuevamente.");

            CargarProductos();
            ViewBag.PedidoId = pedido.PedidoId;
            return View();
        }
    }

    // GET: Pedidos/EditarDetalle/5
    [Authorize(Roles = "Administrador")]
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
    [Authorize(Roles = "Administrador")]
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
    [Authorize(Roles = "Administrador")]
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
    [Authorize(Roles = "Administrador")]
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
    [Authorize(Roles = "Administrador")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("PedidoId,ClienteId,UsuarioId,Estado")] Pedido pedido)
    {
        if (id != pedido.PedidoId)
            return NotFound();

        // Las propiedades de navegación no vienen en el formulario,
        // solo los Id. Se quitan para que no invaliden el modelo.
        ModelState.Remove(nameof(Pedido.Cliente));
        ModelState.Remove(nameof(Pedido.Usuario));

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
    [Authorize(Roles = "Administrador")]
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
    [Authorize(Roles = "Administrador")]
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


    // POST: Pedidos/Confirmar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirmar(int id)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.DetallePedidos)
            .FirstOrDefaultAsync(p => p.PedidoId == id);

        if (pedido == null)
            return NotFound();

        if (!User.IsInRole("Administrador") && !EsPedidoPropio(pedido))
            return Forbid();

        if (!string.Equals(
            pedido.Estado,
            "Pendiente",
            StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorPedido"] = "El pedido ya fue procesado.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!pedido.DetallePedidos.Any())
        {
            TempData["ErrorPedido"] =
                "No podés confirmar un pedido sin productos.";

            return RedirectToAction(nameof(Details), new { id });
        }

        pedido.Estado = "Confirmado";

        await _context.SaveChangesAsync();

        TempData["MensajePedido"] =
            "El pedido fue confirmado correctamente.";

        return RedirectToAction(nameof(Details), new { id });
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
            InventarioId = inventario.InventarioId,
            UsuarioId = usuarioId,
            Tipo = tipo,
            Cantidad = cantidad,
            Fecha = DateTime.Now,
            Observacion = observacion
        };

        _context.MovimientosInventario.Add(movimiento);
    }

    // Obtener el identificador del usuario autenticado.
    private int? ObtenerUsuarioId()
    {
        var identificador = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (int.TryParse(identificador, out int usuarioId))
            return usuarioId;

        return null;
    }

    // Comprobar si el pedido pertenece al usuario autenticado.
    private bool EsPedidoPropio(Pedido pedido)
    {
        int? usuarioId = ObtenerUsuarioId();

        if (!usuarioId.HasValue)
            return false;

        if (pedido.UsuarioId == usuarioId.Value)
            return true;

        // También es propio si el pedido se registró a nombre de su cliente.
        return _context.Clientes.Any(c =>
            c.ClienteId == pedido.ClienteId &&
            c.UsuarioId == usuarioId.Value);
    }

    // Cargar listas para los formularios administrativos.
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

    // Cargar productos activos con existencias, agrupados por categoría.
    private void CargarProductos()
    {
        var productos = _context.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Inventario)
            .Where(p =>
                p.Activo &&
                p.Inventario != null &&
                p.Inventario.Existencias > 0)
            .OrderBy(p => p.Categoria!.Nombre)
            .ThenBy(p => p.Nombre)
            .ToList()
            .Select(p => new
            {
                p.ProductoId,
                Texto = $"{p.Nombre} — ₡{p.Precio:N2} (disponibles: {p.Inventario!.Existencias})",
                Categoria = p.Categoria?.Nombre ?? "Sin categoría"
            })
            .ToList();

        ViewData["ProductoId"] = new SelectList(
            productos,
            "ProductoId",
            "Texto",
            null,
            "Categoria"
        );
    }
}
