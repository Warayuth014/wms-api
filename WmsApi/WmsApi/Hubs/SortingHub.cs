using Microsoft.AspNetCore.SignalR;

namespace WmsApi.Hubs;

/// <summary>
/// SignalR Hub สำหรับ Sorting — broadcast-only
/// Events:
///   StationCounterUpdated — { stationId, palletId, current, total }
///   StationFull           — { stationId, palletId }
///   StationCleared        — { stationId, palletId }
///   StationToggled        — { stationId, enabled }
///   BatchQueued           — { queueId, batchSize }
///   BatchAssigned         — { queueId, stationId, palletId }
/// </summary>
public class SortingHub : Hub { }
