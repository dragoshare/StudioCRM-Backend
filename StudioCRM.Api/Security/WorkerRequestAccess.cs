using System.Security.Cryptography;
using System.Text;
namespace StudioCRM.Api.Security;
public static class WorkerRequestAccess
{
    public static bool CanExecute(HttpContext context, IConfiguration configuration)
    {
        if (context.User.IsInRole("Owner")) return true;
        var expected = configuration["Workers:ApiKey"];
        var provided = context.Request.Headers["X-Worker-Key"].ToString();
        return !string.IsNullOrEmpty(expected) && expected.Length >= 32 &&
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided));
    }
}
