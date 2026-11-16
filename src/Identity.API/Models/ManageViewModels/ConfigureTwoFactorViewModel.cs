namespace eShop.Identity.API.Models.ManageViewModels
{
    internal record ConfigureTwoFactorViewModel
    {
        public string SelectedProvider { get; init; }

        public ICollection<SelectListItem> Providers { get; init; }
    }
}
