using System.ComponentModel.DataAnnotations;

namespace UNChat.Models
{
    public class EditProfileViewModel
    {
        [Required(ErrorMessage = "Imię jest wymagane.")]
        [StringLength(100, ErrorMessage = "Imię nie może mieć więcej niż 100 znaków.")]
        [Display(Name = "Imię")]
        public string Name { get; set; }
        public string? ApiKey { get; set; } 

    }
}