using DailyPilot.Data;
using DailyPilot.Models;
using DailyPilot.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers;

[Authorize]
public class AttachmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly UserManager<ApplicationUser> _userManager;

    public AttachmentsController(ApplicationDbContext db, IFileStorageService storage, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _storage = storage;
        _userManager = userManager;
    }

    private string UserId => _userManager.GetUserId(User)!;

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Upload(int taskId, IFormFile file)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == UserId);
        if (task is null) return NotFound();

        if (file is null || file.Length == 0)
        {
            TempData["Error"] = "Please choose a file to upload.";
            return RedirectToEdit(taskId);
        }

        if (file.Length > _storage.MaxFileSizeBytes)
        {
            TempData["Error"] = "File is too large (max 10 MB).";
            return RedirectToEdit(taskId);
        }

        if (!_storage.IsAllowed(file.FileName, file.ContentType))
        {
            TempData["Error"] = "That file type is not allowed.";
            return RedirectToEdit(taskId);
        }

        var storedName = await _storage.SaveAsync(UserId, file);

        _db.TaskAttachments.Add(new TaskAttachment
        {
            TaskItemId = taskId,
            UserId = UserId,
            FileName = Path.GetFileName(file.FileName),
            StoredFileName = storedName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = "Attachment uploaded.";
        return RedirectToEdit(taskId);
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var attachment = await _db.TaskAttachments
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == UserId);
        if (attachment is null) return NotFound();

        var stream = _storage.OpenRead(UserId, attachment.StoredFileName);
        if (stream is null) return NotFound();

        return File(stream, attachment.ContentType, attachment.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var attachment = await _db.TaskAttachments
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == UserId);
        if (attachment is null) return NotFound();

        _storage.Delete(UserId, attachment.StoredFileName);
        var taskId = attachment.TaskItemId;
        _db.TaskAttachments.Remove(attachment);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Attachment removed.";
        return RedirectToEdit(taskId);
    }

    private IActionResult RedirectToEdit(int taskId) =>
        RedirectToAction("Edit", "Tasks", new { id = taskId });
}
