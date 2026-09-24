using BuildingBlocks.Web;

namespace NotificationService.API.Realtime;

// Broadcast only, and only to the connection's own organisation. OrganizationHub requires an
// authenticated connection and puts it in exactly one group, taken from its own claim.
public sealed class NotificationHub : OrganizationHub;
