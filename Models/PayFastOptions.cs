namespace Vocentra.Models
{
    public class PayFastOptions
    {
        public string MerchantId { get; set; } = string.Empty;
        public string MerchantKey { get; set; } = string.Empty;
        public string PassPhrase { get; set; } = string.Empty;
        public bool UseSandbox { get; set; } = true;
        public string ReturnUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
        public string NotifyUrl { get; set; } = string.Empty;
    }
}