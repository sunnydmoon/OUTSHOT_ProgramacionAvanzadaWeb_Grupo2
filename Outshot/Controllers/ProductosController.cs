
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Outshot.Data;
using Outshot.Models;

public class ProductosController : Controller
{
    private readonly OutshotContext _context;
    private readonly IWebHostEnvironment _environment;

    public ProductosController(
        OutshotContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<IActionResult> Index()
    {
        var productos = await _context.Productos
            .Include(p => p.Categoria)
            .ToListAsync();

        await CargarCategorias();

        return View(productos);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var producto = await _context.Productos
            .Include(p => p.Categoria)
            .FirstOrDefaultAsync(p => p.ProductoId == id);

        if (producto == null)
            return NotFound();

        return View(producto);
    }

    public async Task<IActionResult> Create()
    {
        await CargarCategorias();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("CategoriaId,Nombre,Descripcion,Precio,Activo")]
        Producto producto,
        IFormFile? Imagen)
    {
        await ValidarProducto(producto);
        ValidarImagen(Imagen);

        if (!ModelState.IsValid)
        {
            await CargarCategorias(producto.CategoriaId);

            var productos = await _context.Productos
                .Include(p => p.Categoria)
                .ToListAsync();

            ViewData["Nombre"] = producto.Nombre;
            ViewData["Descripcion"] = producto.Descripcion;
            ViewData["Precio"] = producto.Precio;
            ViewData["Activo"] = producto.Activo;

            return View("Index", productos);
        }

        if (Imagen != null && Imagen.Length > 0)
        {
            producto.ImagenUrl = await GuardarImagen(Imagen);
        }

        producto.FechaRegistro = DateTime.Now;

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();

        TempData["Mensaje"] = "Producto registrado correctamente.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var producto = await _context.Productos.FindAsync(id);

        if (producto == null)
            return NotFound();

        await CargarCategorias(producto.CategoriaId);

        return View(producto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("ProductoId,CategoriaId,Nombre,Descripcion,Precio,Activo")]
        Producto producto,
        IFormFile? Imagen)
    {
        if (id != producto.ProductoId)
            return NotFound();

        var productoActual = await _context.Productos.FindAsync(id);

        if (productoActual == null)
            return NotFound();

        await ValidarProducto(producto);
        ValidarImagen(Imagen);

        if (!ModelState.IsValid)
        {
            producto.FechaRegistro = productoActual.FechaRegistro;
            producto.ImagenUrl = productoActual.ImagenUrl;

            await CargarCategorias(producto.CategoriaId);

            return View(producto);
        }

        string? imagenAnterior = productoActual.ImagenUrl;

        if (Imagen != null && Imagen.Length > 0)
        {
            productoActual.ImagenUrl = await GuardarImagen(Imagen);
        }

        productoActual.Nombre = producto.Nombre;
        productoActual.Descripcion = producto.Descripcion;
        productoActual.CategoriaId = producto.CategoriaId;
        productoActual.Precio = producto.Precio;
        productoActual.Activo = producto.Activo;

        await _context.SaveChangesAsync();

        if (Imagen != null && Imagen.Length > 0)
        {
            EliminarImagen(imagenAnterior);
        }

        TempData["Mensaje"] = "Producto actualizado correctamente.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var producto = await _context.Productos
            .Include(p => p.Categoria)
            .FirstOrDefaultAsync(p => p.ProductoId == id);

        if (producto == null)
            return NotFound();

        return View(producto);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var producto = await _context.Productos.FindAsync(id);

        if (producto == null)
            return NotFound();

        bool tienePedidos = await _context.DetallesPedidos
            .AnyAsync(d => d.ProductoId == id);

        if (tienePedidos)
        {
            TempData["Error"] =
                "No se puede eliminar el producto porque tiene pedidos asociados.";

            return RedirectToAction(nameof(Index));
        }

        bool tieneInventario = await _context.Inventarios
            .AnyAsync(i => i.ProductoId == id);

        if (tieneInventario)
        {
            TempData["Error"] =
                "No se puede eliminar el producto porque tiene inventario asociado.";

            return RedirectToAction(nameof(Index));
        }

        string? imagenProducto = producto.ImagenUrl;

        try
        {
            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();

            EliminarImagen(imagenProducto);

            TempData["Mensaje"] = "Producto eliminado correctamente.";
        }
        catch (DbUpdateException)
        {
            _context.Entry(producto).State = EntityState.Detached;

            TempData["Error"] =
                "No se puede eliminar el producto porque tiene registros relacionados.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task ValidarProducto(Producto producto)
    {
        producto.Nombre = producto.Nombre?.Trim() ?? string.Empty;
        producto.Descripcion = producto.Descripcion?.Trim() ?? string.Empty;

        ModelState.Remove(nameof(Producto.Nombre));

        if (string.IsNullOrWhiteSpace(producto.Nombre))
        {
            ModelState.AddModelError(
                nameof(Producto.Nombre),
                "El nombre es obligatorio.");
        }
        else if (producto.Nombre.Length < 3 ||
                 producto.Nombre.Length > 100)
        {
            ModelState.AddModelError(
                nameof(Producto.Nombre),
                "El nombre debe tener entre 3 y 100 caracteres.");
        }
        else if (!producto.Nombre.Any(char.IsLetter))
        {
            ModelState.AddModelError(
                nameof(Producto.Nombre),
                "El nombre debe contener al menos una letra.");
        }

        if (producto.Descripcion.Length > 500)
        {
            ModelState.AddModelError(
                nameof(Producto.Descripcion),
                "La descripción no puede superar los 500 caracteres.");
        }

        if (producto.Precio <= 0)
        {
            ModelState.AddModelError(
                nameof(Producto.Precio),
                "El precio debe ser mayor que cero.");
        }
        else if (producto.Precio > 9999999999999999.99m)
        {
            ModelState.AddModelError(
                nameof(Producto.Precio),
                "El precio supera el límite permitido.");
        }
        else if (decimal.Round(producto.Precio, 2) != producto.Precio)
        {
            ModelState.AddModelError(
                nameof(Producto.Precio),
                "El precio no puede tener más de dos decimales.");
        }

        bool categoriaValida = await _context.Categorias
            .AnyAsync(c =>
                c.CategoriaId == producto.CategoriaId &&
                c.Activo);

        if (!categoriaValida)
        {
            ModelState.AddModelError(
                nameof(Producto.CategoriaId),
                "Debe seleccionar una categoría activa y válida.");
        }
    }

    private void ValidarImagen(IFormFile? imagen)
    {
        if (imagen == null || imagen.Length == 0)
            return;

        string extension = Path.GetExtension(imagen.FileName).ToLowerInvariant();

        if (extension != ".jpg" &&
            extension != ".jpeg" &&
            extension != ".png" &&
            extension != ".webp")
        {
            ModelState.AddModelError(
                "Imagen",
                "La imagen debe estar en formato JPG, PNG o WEBP.");
        }

        if (imagen.Length > 5 * 1024 * 1024)
        {
            ModelState.AddModelError(
                "Imagen",
                "La imagen no puede superar los 5 MB.");
        }

        if (imagen.ContentType != "image/jpeg" &&
            imagen.ContentType != "image/png" &&
            imagen.ContentType != "image/webp")
        {
            ModelState.AddModelError(
                "Imagen",
                "El archivo seleccionado no es una imagen válida.");
        }
    }

    private async Task<string> GuardarImagen(IFormFile imagen)
    {
        string carpeta = Path.Combine(
            _environment.WebRootPath,
            "images",
            "productos");

        Directory.CreateDirectory(carpeta);

        string extension = Path.GetExtension(imagen.FileName).ToLowerInvariant();
        string nombreArchivo = Guid.NewGuid().ToString("N") + extension;
        string rutaArchivo = Path.Combine(carpeta, nombreArchivo);

        using (var stream = new FileStream(rutaArchivo, FileMode.CreateNew))
        {
            await imagen.CopyToAsync(stream);
        }

        return nombreArchivo;
    }

    private void EliminarImagen(string? nombreArchivo)
    {
        if (string.IsNullOrWhiteSpace(nombreArchivo))
            return;

        if (Path.GetFileName(nombreArchivo) != nombreArchivo)
            return;

        string rutaArchivo = Path.Combine(
            _environment.WebRootPath,
            "images",
            "productos",
            nombreArchivo);

        if (System.IO.File.Exists(rutaArchivo))
        {
            System.IO.File.Delete(rutaArchivo);
        }
    }

    private async Task CargarCategorias(int? seleccionada = null)
    {
        var categorias = await _context.Categorias
            .Where(c => c.Activo || c.CategoriaId == seleccionada)
            .OrderBy(c => c.Nombre)
            .ToListAsync();

        ViewData["CategoriaId"] = new SelectList(
            categorias,
            "CategoriaId",
            "Nombre",
            seleccionada);
    }
}
