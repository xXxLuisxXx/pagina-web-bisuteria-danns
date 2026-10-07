using Bisuteria.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Areas.Admin.Controllers;

public class MessagesController : AdminBaseController
{
    private readonly IContactService _contact;

    public MessagesController(IContactService contact)
    {
        _contact = contact;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool unreadOnly = false, CancellationToken ct = default)
    {
        ViewData["Title"] = "Mensajes";
        ViewBag.UnreadOnly = unreadOnly;
        ViewBag.UnreadCount = await _contact.CountUnreadAsync(ct);
        var messages = await _contact.GetMessagesAsync(unreadOnly, ct);
        return View(messages);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken ct)
    {
        await _contact.MarkAsReadAsync(id, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _contact.DeleteAsync(id, ct);
        TempData["Success"] = "Mensaje eliminado.";
        return RedirectToAction(nameof(Index));
    }
}
