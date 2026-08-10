using Microsoft.AspNetCore.Mvc;

namespace WmsApi.Controllers;

// เอาไว้ให้ mobile app ping เช็คว่าเจอ WmsApi จริงไหม (ไม่ใช่แค่ port เปิด) — ไม่ต้อง auth ไม่แตะ DB
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { service = "WmsApi", ok = true });
}
