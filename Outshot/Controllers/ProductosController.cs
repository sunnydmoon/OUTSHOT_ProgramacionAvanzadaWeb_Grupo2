using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Outshot.Data;
using Outshot.Models;

public class ProductosController : Controller
{
    private readonly OutshotContext _context;

    public ProductosController(OutshotContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var productos = _context.Productos
            .Include(p => p.Categoria);

        ViewData["CategoriaId"] = new SelectList(
            _context.Categorias,
            "CategoriaId",
            "Nombre"
        );

        return View(await productos.ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var producto = await _context.Productos
            .Include(p => p.Categoria)
            .FirstOrDefaultAsync(m => m.ProductoId == id);

        if (producto == null)
        {
            return NotFound();
        }

        return View(producto);
    }

    public IActionResult Create()
    {
        ViewData["CategoriaId"] = new SelectList(
            _context.Categorias,
            "CategoriaId",
            "Nombre"
        );

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("ProductoId,CategoriaId,Nombre,Descripcion,Precio,ImagenUrl,Activo")] Producto producto)
    {
        producto.FechaRegistro = DateTime.Now;

        if (ModelState.IsValid)
        {
            _context.Add(producto);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        ViewData["CategoriaId"] = new SelectList(
            _context.Categorias,
            "CategoriaId",
            "Nombre",
            producto.CategoriaId
        );

        return View(producto);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var producto = await _context.Productos.FindAsync(id);

        if (producto == null)
        {
            return NotFound();
        }

        ViewData["CategoriaId"] = new SelectList(
            _context.Categorias,
            "CategoriaId",
            "Nombre",
            producto.CategoriaId
        );

        return View(producto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("ProductoId,CategoriaId,Nombre,Descripcion,Precio,ImagenUrl,Activo,FechaRegistro")] Producto producto)
    {
        if (id != producto.ProductoId)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(producto);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductoExists(producto.ProductoId))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        ViewData["CategoriaId"] = new SelectList(
            _context.Categorias,
            "CategoriaId",
            "Nombre",
            producto.CategoriaId
        );

        return View(producto);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var producto = await _context.Productos
            .Include(p => p.Categoria)
            .FirstOrDefaultAsync(m => m.ProductoId == id);

        if (producto == null)
        {
            return NotFound();
        }

        return View(producto);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var producto = await _context.Productos.FindAsync(id);

        if (producto != null)
        {
            _context.Productos.Remove(producto);
        }

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private bool ProductoExists(int id)
    {
        return _context.Productos.Any(e => e.ProductoId == id);
    }
}