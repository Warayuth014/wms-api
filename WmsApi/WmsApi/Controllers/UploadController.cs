using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WmsApi.Data;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/upload")]
public class UploadController(IWebHostEnvironment env, WmsDbContext db) : ControllerBase
{
    /// <summary>
    /// อัปโหลดรูป Part + บันทึก URL ลง DB
    /// POST /api/upload/part-image
    /// Body: multipart/form-data { file, partId }
    /// </summary>
    [HttpPost("part-image")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadPartImage(
        IFormFile file,
        [FromForm] string partId)
    {
        if (string.IsNullOrWhiteSpace(partId))
            return BadRequest(new { Error = "กรุณาระบุ partId" });

        var part = await db.Parts.FindAsync(partId);
        if (part is null)
            return NotFound(new { Error = $"ไม่พบ Part '{partId}'" });

        if (file.Length == 0)
            return BadRequest(new { Error = "ไม่มีไฟล์" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowed.Contains(ext))
            return BadRequest(new { Error = $"รองรับเฉพาะ {string.Join(", ", allowed)}" });

        var uploadDir = Path.Combine(env.WebRootPath, "uploads", "parts");
        Directory.CreateDirectory(uploadDir);

        // ลบรูปเก่าถ้ามี
        if (!string.IsNullOrEmpty(part.ImageUrl))
        {
            var oldPath = Path.Combine(env.WebRootPath, part.ImageUrl.TrimStart('/'));
            if (System.IO.File.Exists(oldPath))
                System.IO.File.Delete(oldPath);
        }

        var fileName = $"{partId}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        var filePath = Path.Combine(uploadDir, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var url = $"/uploads/parts/{fileName}";
        part.ImageUrl = url;
        await db.SaveChangesAsync();

        return Ok(new { PartId = partId, ImageUrl = url, Message = $"อัปโหลดรูป {partId} สำเร็จ" });
    }

    /// <summary>
    /// ดึงรายการ Parts ทั้งหมด (สำหรับหน้าจัดการรูป)
    /// GET /api/upload/parts
    /// </summary>
    [HttpGet("parts")]
    public async Task<IActionResult> GetAllParts()
    {
        var parts = await db.Parts
            .OrderBy(p => p.PartId)
            .Select(p => new
            {
                p.PartId,
                p.Owner,
                p.Brand,
                p.ItemDesc,
                p.ImageUrl,
            })
            .ToListAsync();

        return Ok(parts);
    }
}
