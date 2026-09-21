using Microsoft.AspNetCore.SignalR;

namespace IncidentService.API.Realtime;

// Broadcast only: clients subscribe by connecting and never call anything on it. A hub with no
// callable methods has no surface to secure beyond the connection itself, which matters while
// there is still no authentication (Adım 16).
public sealed class IncidentHub : Hub;
