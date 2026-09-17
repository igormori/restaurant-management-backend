namespace RestaurantManagement.Modules.Menu.Models
{
    public class MenuCategoryResponse
    {
        public Guid Id { get; set; }
        public Guid MenuId { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid? LocationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
