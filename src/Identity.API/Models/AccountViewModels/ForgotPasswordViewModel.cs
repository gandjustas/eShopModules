namespace eShop.Identity.API.Models.AccountViewModels
{
    internal record ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; init; }
    }
}
