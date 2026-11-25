
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Outshot.Data;
using Outshot.Models;

public class ClientesController : Controller
{
    private readonly OutshotContext _context;

    public ClientesController(OutshotContext context)
    {
        _context = context;
    }

    // GET: Clientes
    public async Task<IActionResult> Index()
    {
        var clientes = await _context.Clientes.ToListAsync();

        return View(clientes);
    }

    // GET: Clientes/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.ClienteId == id);

        if (cliente == null)
        {
            return NotFound();
        }

        return View(cliente);
    }

    // GET: Clientes/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Clientes/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Nombre,Correo,Telefono,Direccion")] Cliente cliente)
    {
        if (ModelState.IsValid)
        {
            cliente.FechaRegistro = DateTime.Now;

            _context.Add(cliente);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        return View(cliente);
    }

    // GET: Clientes/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var cliente = await _context.Clientes.FindAsync(id);

        if (cliente == null)
        {
            return NotFound();
        }

        return View(cliente);
    }

    // POST: Clientes/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("ClienteId,Nombre,Correo,Telefono,Direccion,FechaRegistro")] Cliente cliente)
    {
        if (id != cliente.ClienteId)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(cliente);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ClienteExists(cliente.ClienteId))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        return View(cliente);
    }

    // GET: Clientes/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.ClienteId == id);

        if (cliente == null)
        {
            return NotFound();
        }

        return View(cliente);
    }

    // POST: Clientes/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var cliente = await _context.Clientes
            .Include(c => c.Pedidos)
            .FirstOrDefaultAsync(c => c.ClienteId == id);

        if (cliente == null)
        {
            return NotFound();
        }

        // No eliminar clientes que tengan pedidos registrados.
        if (cliente.Pedidos.Any())
        {
            ModelState.AddModelError(
                string.Empty,
                "No se puede eliminar el cliente porque tiene pedidos registrados."
            );

            return View("Delete", cliente);
        }

        _context.Clientes.Remove(cliente);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private bool ClienteExists(int id)
    {
        return _context.Clientes.Any(c => c.ClienteId == id);
    }
}
