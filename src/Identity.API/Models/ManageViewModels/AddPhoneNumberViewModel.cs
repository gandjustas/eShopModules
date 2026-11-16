namespace eShop.Identity.API.Models.ManageViewModels
{
    internal record AddPhoneNumberViewModel
    {
        [Required]
        [Phone]
        [Display(Name = "Phone number")]
        public string PhoneNumber { get; init; }
    }
}
