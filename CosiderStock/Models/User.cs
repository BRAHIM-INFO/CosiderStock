using System.ComponentModel.DataAnnotations;

namespace CosiderStock.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Le nom d'utilisateur est requis")]
        [Display(Name = "Nom d'utilisateur")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Entre 3 et 50 caractères")]
        public string Username { get; set; } = string.Empty;

        [Display(Name = "Mot de passe")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Nom complet")]
        public string? FullName { get; set; }

        [Display(Name = "Email")]
        [EmailAddress(ErrorMessage = "Email invalide")]
        public string? Email { get; set; }

        [Display(Name = "Téléphone")]
        public string? Phone { get; set; }

        [Display(Name = "Rôle")]
        public string? Role { get; set; }

        [Display(Name = "Actif")]
        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime? LastLogin { get; set; }
        public string? ProfileImage { get; set; }
    }
}