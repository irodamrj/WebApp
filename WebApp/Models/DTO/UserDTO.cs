using System.ComponentModel.DataAnnotations;

namespace WebApp.Models.DTO
{
    public class UserDTO
    {
        [Required]
        public string Password { get; set; }
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
