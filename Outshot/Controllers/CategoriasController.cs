
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Outshot.Data;
using Outshot.Models;

public class CategoriasController : Controller
{
    private readonly OutshotContext _context;

    public CategoriasController(OutshotContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var categorias = await _context.Categorias
            .OrderBy(c => c.Nombre)
            .ToListAsync();

        return View(categorias);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c => c.CategoriaId == id);

        if (categoria == null)
            return NotFound();

        return View(categoria);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Nombre,Descripcion,Activo")] Categoria categoria)
    {
        await ValidarCategoria(categoria);

        if (!ModelState.IsValid)
        {
            var categorias = await _context.Categorias
                .OrderBy(c => c.Nombre)
                .ToListAsync();

            ViewData["Nombre"] = categoria.Nombre;
            ViewData["Descripcion"] = categoria.Descripcion;
            ViewData["Activo"] = categoria.Activo;

            return View("Index", categorias);
        }

        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();

        TempData["Mensaje"] = "Categoría registrada correctamente.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var categoria = await _context.Categorias.FindAsync(id);

        if (categoria == null)
            return NotFound();

        return View(categoria);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("CategoriaId,Nombre,Descripcion,Activo")] Categoria categoria)
    {
        if (id != categoria.CategoriaId)
            return NotFound();

        var categoriaActual = await _context.Categorias.FindAsync(id);

        if (categoriaActual == null)
            return NotFound();

        await ValidarCategoria(categoria, id);

        if (!ModelState.IsValid)
            return View(categoria);

        categoriaActual.Nombre = categoria.Nombre;
        categoriaActual.Descripcion = categoria.Descripcion;
        categoriaActual.Activo = categoria.Activo;

        await _context.SaveChangesAsync();

        TempData["Mensaje"] = "Categoría actualizada correctamente.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c => c.CategoriaId == id);

        if (categoria == null)
            return NotFound();

        return View(categoria);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var categoria = await _context.Categorias.FindAsync(id);

        if (categoria == null)
            return NotFound();

        bool tieneProductos = await _context.Productos
            .AnyAsync(p => p.CategoriaId == id);

        if (tieneProductos)
        {
            TempData["Error"] =
                "No se puede eliminar la categoría porque tiene productos asociados.";

            return RedirectToAction(nameof(Index));
        }

        try
        {
            _context.Categorias.Remove(categoria);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "Categoría eliminada correctamente.";
        }
        catch (DbUpdateException)
        {
            _context.Entry(categoria).State = EntityState.Detached;

            TempData["Error"] =
                "No se puede eliminar la categoría porque tiene registros relacionados.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task ValidarCategoria(Categoria categoria, int? id = null)
    {
        categoria.Nombre = categoria.Nombre?.Trim() ?? string.Empty;
        categoria.Descripcion = categoria.Descripcion?.Trim() ?? string.Empty;

        ModelState.Remove(nameof(Categoria.Nombre));
        ModelState.Remove(nameof(Categoria.Descripcion));

        if (string.IsNullOrWhiteSpace(categoria.Nombre))
        {
            ModelState.AddModelError(
                nameof(Categoria.Nombre),
                "El nombre de la categoría es obligatorio.");
        }
        else if (categoria.Nombre.Length < 3 ||
                 categoria.Nombre.Length > 100)
        {
            ModelState.AddModelError(
                nameof(Categoria.Nombre),
                "El nombre debe tener entre 3 y 100 caracteres.");
        }
        else if (!categoria.Nombre.Any(char.IsLetter))
        {
            ModelState.AddModelError(
                nameof(Categoria.Nombre),
                "El nombre debe contener al menos una letra.");
        }

        if (categoria.Descripcion.Length > 500)
        {
            ModelState.AddModelError(
                nameof(Categoria.Descripcion),
                "La descripción no puede superar los 500 caracteres.");
        }

        if (!string.IsNullOrWhiteSpace(categoria.Nombre))
        {
            bool existe = await _context.Categorias
                .AnyAsync(c =>
                    c.Nombre.ToLower() == categoria.Nombre.ToLower() &&
                    (!id.HasValue || c.CategoriaId != id.Value));

            if (existe)
            {
                ModelState.AddModelError(
                    nameof(Categoria.Nombre),
                    "Ya existe una categoría con ese nombre.");
            }
        }
    }
}
