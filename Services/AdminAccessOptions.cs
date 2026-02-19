namespace Vocentra.Services
{
    public class AdminAccessOptions
    {
        // Default fallback reads from environment variable AdminAccess__Code when available,
        // otherwise uses a safe default for local development. This value is overridden by
        // configuration binding when AdminAccess:Code is present.
        public string? Code { get; set; } = System.Environment.GetEnvironmentVariable("AdminAccess__Code") ?? "ashzoelefa";
    }
}
