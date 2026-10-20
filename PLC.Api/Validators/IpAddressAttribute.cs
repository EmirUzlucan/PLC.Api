using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;

namespace PLC.Api.Validators
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public class IpAddressAttribute : ValidationAttribute
    {
        public bool AllowIpv6 { get; set; } = false;

        public IpAddressAttribute()
        {
            ErrorMessage = "Geçerli bir IP adresi giriniz (örn: 192.168.1.1).";
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null)
                return ValidationResult.Success; // Null handled by [Required] if needed

            if (value is not string s)
                return new ValidationResult(ErrorMessage);

            if (string.IsNullOrWhiteSpace(s))
                return ValidationResult.Success; // Let [Required] handle empty strings

            if (!IPAddress.TryParse(s, out var addr))
                return new ValidationResult(ErrorMessage);

            if (!AllowIpv6 && addr.AddressFamily == AddressFamily.InterNetworkV6)
                return new ValidationResult(ErrorMessage);

            return ValidationResult.Success;
        }
    }
}
