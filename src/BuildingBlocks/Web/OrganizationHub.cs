using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BuildingBlocks.Web;

/// <summary>The group every connection of one organisation joins, and the only audience a broadcast has.</summary>
public static class OrganizationGroups
{
    public static string For(Guid organizationId) => $"org:{organizationId:N}";
}

/// <summary>
/// A hub whose connections hear only their own organisation.
/// </summary>
/// <remarks>
/// <para>
/// Until now every hub broadcast to <c>Clients.All</c>, and every connected console received every
/// organisation's incidents, signals and deliveries as they happened. Query filters closed the
/// reads; they could not close this, because a push is not a query — nothing is read, a message is
/// simply handed to every open socket. The group is what a query filter is for a socket.
/// </para>
/// <para>
/// Still broadcast-only: clients connect and never call anything. What changed is that a
/// connection now has to be authenticated to exist at all — the access cookie rides the WebSocket
/// handshake on one origin — and that it is placed in exactly one group, taken from its own claim.
/// A client cannot ask to join a group; there is no method to ask with.
/// </para>
/// </remarks>
[Authorize]
public abstract class OrganizationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var claim = Context.User?.FindFirst(PlatformClaims.Organization)?.Value;

        // Authorised but without an organisation is a malformed token, not an anonymous visitor.
        // Refusing the connection is the only safe answer: a socket in no group would hear nothing
        // and look healthy, and there is no group it could be put in that would not be a guess.
        if (!Guid.TryParse(claim, out var organizationId))
        {
            Context.Abort();

            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, OrganizationGroups.For(organizationId));

        await base.OnConnectedAsync();
    }
}
