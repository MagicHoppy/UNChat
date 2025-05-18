using System.ComponentModel.DataAnnotations;

namespace UNChat.Models
{
    public class RegisterViewModel
    {
        [Required]
        [StringLength(25, MinimumLength = 2, ErrorMessage = "Imię musi mieć od 2 do 25 znaków")]
        public string Name { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
