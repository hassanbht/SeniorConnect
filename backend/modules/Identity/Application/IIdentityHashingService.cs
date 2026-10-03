namespace SeniorConnect.Modules.Identity.Application;

public interface IIdentityHashingService
{
    string HashDestination(string destination);
    string HashCode(string code);
    string? HashIp(string? ipAddress);
    string HashToken(string token);
}
