using Microsoft.AspNetCore.SignalR;

namespace WmsApi.Hubs;

/// <summary>
/// SignalR Hub สำหรับ Putaway — broadcast-only
/// ไม่มี server-side method, server push events จาก controllers
/// </summary>
public class PutawayHub : Hub { }
