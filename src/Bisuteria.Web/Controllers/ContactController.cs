using Bisuteria.Application.Interfaces;
using Bisuteria.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Controllers;

public class ContactController : Controller
{
    private readonly IContactService _contact;

    public ContactController(IContactService contact)
    {
        _contact = contact;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Contacto · Bisutería y Accesorios Personalizados";
        return View(new ContactViewModel());
    }

    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("public-forms")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ContactViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Contacto · Bisutería y Accesorios Personalizados";
        if (!ModelState.IsValid)
            return View(model);

        await _contact.SendMessageAsync(model.Name, model.Email, model.Phone, model.Subject, model.Message, ct);
        TempData["Success"] = "¡Gracias por escribirnos! Te responderemos lo antes posible.";
        return RedirectToAction(nameof(Index));
    }
}
