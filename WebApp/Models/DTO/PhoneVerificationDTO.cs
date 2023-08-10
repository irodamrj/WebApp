using System.ComponentModel.DataAnnotations;

namespace WebApp.Models.DTO
{
    public class PhoneVerificationDTO
    {
        [Required]
        public long Phone { get; set; }
        [Required]
        public int VerificationCode { get; set; }
    }
}
