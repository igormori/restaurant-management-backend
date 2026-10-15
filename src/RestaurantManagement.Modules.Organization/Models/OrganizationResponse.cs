using System;

namespace RestaurantManagement.Modules.Organization.Models
{
    public class OrganizationResponse
    {
        private string _name = string.Empty;
        private string? _description;
        private string? _logoUrl;
        private string? _primaryColor;
        private string? _secondaryColor;
        private string? _accentColor;
        private string _planType = "TRIAL";

        public Guid Id { get; set; }

        public string Name
        {
            get => _name;
            set => _name = value?.Trim() ?? string.Empty;
        }

        public string? Description
        {
            get => _description;
            set => _description = value?.Trim();
        }

        public string? LogoUrl
        {
            get => _logoUrl;
            set => _logoUrl = value?.Trim();
        }

        public string? PrimaryColor
        {
            get => _primaryColor;
            set => _primaryColor = value?.Trim();
        }

        public string? SecondaryColor
        {
            get => _secondaryColor;
            set => _secondaryColor = value?.Trim();
        }

        public string? AccentColor
        {
            get => _accentColor;
            set => _accentColor = value?.Trim();
        }

        public string PlanType
        {
            get => _planType;
            set => _planType = string.IsNullOrWhiteSpace(value)
                ? "TRIAL"
                : value.Trim().ToUpperInvariant();
        }

        public int MaxLocations { get; set; } = 1;
        public DateTime? TrialEndDate { get; set; }
        public bool IsTrialActive { get; set; }
    }
}