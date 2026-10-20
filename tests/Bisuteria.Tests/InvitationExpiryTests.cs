using System.Net;
using System.Text.RegularExpressions;
using Bisuteria.Domain.Entities;
using Bisuteria.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class AuditTests
{
    [Fact]
    public async Task ExpiredInvitationCannotActivateAndResendRevokesPreviousLink()
    {
        using var developer = await f.Login("Developer");
        var email = $"expired-invite-{Guid.NewGuid():N}@example.test";
        var fields = new Dictionary<string, string> { ["email"] = email, ["roleName"] = "Owner" };
        Assert.Equal(HttpStatusCode.Redirect, (await AuditFactory.Post(developer, "/StaffInvitation/Create", fields)).StatusCode);
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invitation = await db.StaffInvitations.SingleAsync(row => row.Email == email);
        var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("BisuteriasDanns.StaffInvitation.Email.v1");
        var body = await db.Set<EmailOutbox>().Where(row => row.Recipient == email && row.Kind == "StaffInvitation").Select(row => row.TextBody).SingleAsync();
        var token = Regex.Match(protector.Unprotect(body), @"token=([^\s]+)").Groups[1].Value;
        invitation.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        using var guest = f.Client();
        var expired = await guest.GetStringAsync("/StaffInvitation/Accept?token=" + Uri.EscapeDataString(token));
        Assert.Contains("Invitación no disponible", WebUtility.HtmlDecode(expired));
        Assert.Equal(HttpStatusCode.Redirect, (await AuditFactory.Post(developer, "/StaffInvitation/Create", fields)).StatusCode);
        await db.Entry(invitation).ReloadAsync();
        Assert.NotNull(invitation.RevokedAtUtc);
        Assert.Equal(1, await db.StaffInvitations.CountAsync(row => row.Email == email && row.RevokedAtUtc == null && row.ExpiresAtUtc > DateTime.UtcNow));
        var rejected = await guest.GetStringAsync("/StaffInvitation/Accept?token=" + Uri.EscapeDataString(token));
        Assert.Contains("Invitación no disponible", WebUtility.HtmlDecode(rejected));
    }
}
